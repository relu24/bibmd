using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using Bibui.Core.Draw;
using Bibui.Core.Runtime;
using Bibui.Platform.Browser;
using Bibui.Raster.Managed.Backend;

namespace BibMd;

/// <summary>
/// The reader in the page: one window, and the document the page gives it. A document is
/// written into the runtime's in-memory file system under <see cref="Root"/>, at its path
/// in the site, so the reader reads it as on the desktop, and an image beside it resolves
/// to the path the page fetches it from.
/// </summary>
public static partial class Viewer
{
    private const string Root = "/bibmd/";
    private static BibuiRuntime _runtime = null!;
    private static ManagedRenderer _managed = null!;
    private static App? _app;
    private static string? _seen;   // the text as the page last heard it
    private static (int, int, bool)? _range;   // the open block as the page last heard it
    private static string? _typed;
    private static string? _state;   // the chrome's state as the page last heard it

    public static void Start(BibuiRuntime runtime, BrowserWindow window, ManagedRenderer managed)
    {
        (_runtime, _managed) = (runtime, managed);
        App.LinkOpener = OpenLink;
        Bib.Saver = (name, text) => { Download(name, text); return $"Downloaded {name}."; };
        // A finger across a code block, a wide table or the section strip scrolls it; elsewhere it drags.
        window.ScrollsAcross = runtime.ScrollsAcrossAt;
        // The status line's messages fade, and typing autosaves (into memory), with no input to wake a frame.
        _ = new Timer(_ => runtime.RequestFrame(), null, 500, 500);
        BrowserWindow.Run(window, () => runtime.FramesRequested > 0, () =>
        {
            long start = Stopwatch.GetTimestamp();
            runtime.ConsumeFrame();
            runtime.BeginBuild(window.Frame, window.Events);
            if (_app is not null) _app.Build();
            runtime.EndBuild();
            // Each change to the text (a block put back, typing saved), for the page to keep.
            if (_app is not null && !ReferenceEquals(_app.DocumentText, _seen)) Changed(_seen = _app.DocumentText);
            if (_app is { Live: true }) Report(_app);
            // What the page's chrome shows: the toolbar's toggles, the menu, a flash.
            if (_app is not null && _app.HostState() is var state && state != _state) State(_state = state);
            // A phone's keyboard opens only while a block (or a bib field) edits, its IME at the caret.
            window.SetTextInput(runtime.TextInput);
            long built = Stopwatch.GetTimestamp();
            BrowserWindow.BeginDraw(window);
            managed.Draw(runtime.DrawList);
            long drawn = Stopwatch.GetTimestamp();
            BrowserWindow.EndDraw(window);
            _perf.Add(start, built, drawn, Stopwatch.GetTimestamp(), window.FramebufferWidth, window.FramebufferHeight);
        });
        Ready();
    }

    /// <summary>
    /// The frames since the last ask, for the page's ?perf readout: how many, and on average the
    /// milliseconds each took to build and lay out, to rasterize, and to hand to the canvas.
    /// </summary>
    [JSExport]
    public static string Perf() => _perf.Take();

    private static readonly FrameTimes _perf = new();

    private sealed class FrameTimes
    {
        private int _frames, _width, _height;
        private long _build, _raster, _present;

        public void Add(long start, long built, long drawn, long presented, int width, int height)
        {
            (_build, _raster, _present) = (_build + built - start, _raster + drawn - built, _present + presented - drawn);
            (_frames, _width, _height) = (_frames + 1, width, height);
        }

        public string Take()
        {
            double Ms(long ticks) => _frames == 0 ? 0 : ticks * 1000.0 / Stopwatch.Frequency / _frames;
            string s = FormattableString.Invariant(
                $"{{\"frames\":{_frames},\"build\":{Ms(_build):F1},\"raster\":{Ms(_raster):F1},\"present\":{Ms(_present):F1},\"width\":{_width},\"height\":{_height}}}");
            (_frames, _build, _raster, _present) = (0, 0, 0, 0);
            return s;
        }
    }

    /// <summary>Shows <paramref name="text"/> as the file at <paramref name="path"/> (a path in the site, or a dropped file's name).</summary>
    [JSExport]
    public static void Open(string path, string text, bool dark, bool locked)
    {
        string file = Root + path.TrimStart('/');
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, text);
        bool keepDark = _app?.Dark ?? dark;   // what t chose stays for the next document
        _app?.Close();
        var images = new Images((w, h, px) => _managed.CreateTexture(TextureFormat.RGBA8, TextureKind.Static, w, h, px),
            _runtime.RequestFrame, Read);
        _app = new App(_runtime, new Document(file), images) { Bare = true };
        _state = null;
        _seen = _app.DocumentText;
        (_range, _typed) = (null, null);
        _app.Dark = keepDark;
        if (locked) _app.Lock();
        _runtime.RequestFrame();
    }

    /// <summary>
    /// The colour emoji face, as the font's bytes: the browser has no system fonts, so the page
    /// fetches one and hands it over before the first document opens.
    /// </summary>
    [JSExport]
    public static void UseEmoji(byte[] font)
    {
        _runtime.Fonts.UseClusterFont(new Bibui.Text.FreeType.FontData(font, "Twemoji"));
        _runtime.RequestFrame();
    }

    /// <summary>
    /// A font for the characters Inter and the emoji face lack (an arrow, a math operator), as its
    /// bytes: what fontconfig's fallback is on the desktop. Those registered first are asked first.
    /// </summary>
    [JSExport]
    public static void UseFallback(byte[] font)
    {
        Bibui.Text.FontRegistry.RegisterFallback(font);
        _runtime.RequestFrame();
    }

    // In a shared document: which block is open (held for this reader), and what is being typed in it.
    private static void Report(App app)
    {
        var range = app.EditRange;
        if (range != _range)
        {
            _range = range;
            _typed = null;
            if (range is var (start, end, isNew)) Editing(start, end, isNew); else Editing(-1, -1, false);
        }
        if (range is not null && app.Buffer != _typed) Typing(_typed = app.Buffer, app.Cursor);
    }

    /// <summary>A shared document: the whole-document undo is off, typing reaches the text after a pause.</summary>
    [JSExport]
    public static void SetLive(bool live)
    {
        if (_app is not null) _app.Live = live;
        _range = null;
    }

    /// <summary>Someone else's change to the text, in this reader's own positions.</summary>
    [JSExport]
    public static void ApplyRemote(int start, int end, string text)
    {
        if (_app is null) return;
        _app.ApplyRemote(start, end, text);
        _seen = _app.DocumentText;
        _runtime.RequestFrame();
    }

    /// <summary>The others: [{name, color, start, end, isNew, typing, caret}], positions in this reader's text.</summary>
    [JSExport]
    public static void SetRemotes(string json)
    {
        if (_app is null) return;
        using var d = System.Text.Json.JsonDocument.Parse(json);
        var list = new List<Remote>();
        foreach (var r in d.RootElement.EnumerateArray())
            list.Add(new Remote(r.GetProperty("name").GetString() ?? "", r.GetProperty("color").GetString() ?? "#888888",
                r.GetProperty("start").GetInt32(), r.GetProperty("end").GetInt32(), r.GetProperty("isNew").GetBoolean(),
                r.TryGetProperty("typing", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.String ? t.GetString() : null,
                r.TryGetProperty("caret", out var c) && c.ValueKind == System.Text.Json.JsonValueKind.Number ? c.GetInt32() : 0));
        _app.SetRemotes(list);
        _runtime.RequestFrame();
    }

    /// <summary>The block this reader opened was someone else's first.</summary>
    [JSExport]
    public static void DropEdit(string who)
    {
        _app?.DropEdit(who);
        _runtime.RequestFrame();
    }

    // ── The page's chrome: what its toolbar, menu and insert menu do ────────────

    /// <summary>A toolbar button or a menu item, by name (<see cref="App.Command"/>).</summary>
    [JSExport]
    public static void Command(string name) => Do(app => app.Command(name));

    /// <summary>A heading picked in the contents, or a section in the strip: its block.</summary>
    [JSExport]
    public static void GoTo(int block) => Do(app => app.GoToBlock(block));

    /// <summary>A version picked in the history.</summary>
    [JSExport]
    public static void GoToVersion(int index) => Do(app => app.GoToVersionAt(index));

    [JSExport] public static string Outline() => _app?.OutlineJson() ?? "[]";
    [JSExport] public static string History() => _app?.HistoryJson() ?? "[]";
    [JSExport] public static string Keys() => _app?.KeysJson() ?? "[]";

    /// <summary>The insert menu's choices for what its field holds.</summary>
    [JSExport]
    public static string MenuItems(string query) => _app?.MenuJson(query) ?? "[]";

    [JSExport]
    public static void ChooseMenu(string query, int index) => Do(app => app.ChooseMenu(query, index));

    [JSExport]
    public static void CloseMenu(string query, bool back) => Do(app => app.CloseMenu(query, back));

    private static void Do(Action<App> act)
    {
        if (_app is null) return;
        act(_app);
        _runtime.RequestFrame();
    }

    /// <summary>The document as it is now, edits and all, for the page's download.</summary>
    [JSExport]
    public static string Text() => _app?.DocumentText ?? "";

    // An image as PNG: the page fetches it (from the site, a URL, or the files dropped with the
    // document) and lets the browser decode whatever format it is in.
    private static async Task<byte[]> Read(string source) =>
        Convert.FromBase64String(await Png(source.StartsWith(Root) ? source[Root.Length..] : source));

    private static void OpenLink(string link)
    {
        if (link.StartsWith('#')) return;
        Navigate(link);
    }

    [JSImport("ready", "bibmd")] private static partial void Ready();
    [JSImport("png", "bibmd")] private static partial Task<string> Png(string source);
    [JSImport("navigate", "bibmd")] private static partial void Navigate(string link);
    [JSImport("download", "bibmd")] private static partial void Download(string name, string text);
    [JSImport("changed", "bibmd")] private static partial void Changed(string text);
    [JSImport("editing", "bibmd")] private static partial void Editing(int start, int end, bool isNew);
    [JSImport("typing", "bibmd")] private static partial void Typing(string text, int caret);
    [JSImport("state", "bibmd")] private static partial void State(string json);
}

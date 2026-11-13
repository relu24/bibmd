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

    public static void Start(BibuiRuntime runtime, BrowserWindow window, ManagedRenderer managed)
    {
        (_runtime, _managed) = (runtime, managed);
        App.LinkOpener = OpenLink;
        // The status line's messages fade, and typing autosaves (into memory), with no input to wake a frame.
        _ = new Timer(_ => runtime.RequestFrame(), null, 500, 500);
        BrowserWindow.Run(window, () => runtime.FramesRequested > 0, () =>
        {
            runtime.ConsumeFrame();
            runtime.BeginBuild(window.Frame, window.Events);
            if (_app is not null) _app.Build();
            runtime.EndBuild();
            // Each change to the text (a block put back, typing saved), for the page to keep.
            if (_app is not null && !ReferenceEquals(_app.DocumentText, _seen)) Changed(_seen = _app.DocumentText);
            // A phone's keyboard opens only while a block (or a bib field) edits, its IME at the caret.
            window.SetTextInput(runtime.TextInput);
            BrowserWindow.BeginDraw(window);
            managed.Draw(runtime.DrawList);
            BrowserWindow.EndDraw(window);
        });
        Ready();
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
        _app = new App(_runtime, new Document(file), images);
        _seen = _app.DocumentText;
        _app.Dark = keepDark;
        if (locked) _app.Lock();
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
    [JSImport("changed", "bibmd")] private static partial void Changed(string text);
}

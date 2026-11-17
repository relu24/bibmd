using Bibui.Core;
using Bibui.Core.Draw;
using Bibui.Core.Runtime;
using Bibui.Platform.iOS;
using Bibui.Raster.Managed.Backend;
using Foundation;
using UIKit;

namespace BibMd;

/// <summary>
/// The reader in its window on iOS: one document at a time, read and edited where it is.
/// A document from the Files app is held with its security-scoped access for as long as it
/// is open. The page keeps clear of the notch, the home indicator and the keyboard
/// (<c>runtime.Insets</c>); the theme follows the system's light or dark mode; going to the
/// background puts the open block back, so what was typed is in the file.
/// </summary>
public sealed class Host
{
    private const string Sample = "sourdough.bib.md";
    private const string LastKey = "bibmd.last";

    private readonly BibuiRuntime _runtime = BibuiRuntime.Create();
    private readonly ManagedRenderer _managed;
    private readonly Timer _tick;
    private App? _app;
    private string? _path;           // the open document
    private NSUrl? _scoped;          // the document's security-scoped access, while it is open
    private string? _openNext;       // a link to another document, opened after the frame that followed it

    public IosWindow Window { get; }

    public Host(UIWindowScene scene)
    {
        App.RegisterFonts();
        Window = IosWindow.Open(scene);
        _runtime.SetTheme(App.ThemeFor(Window.Dark));
        _runtime.WakeUp = Window.Wake;
        _runtime.WindowClipboard = (Window.GetClipboardText, Window.SetClipboardText);
        // A finger across a code block, a wide table or the section strip scrolls it; elsewhere it drags.
        Window.ScrollsAcross = _runtime.ScrollsAcrossAt;
        _managed = new ManagedRenderer(Window.Cpu) { DamageTracking = true };
        App.LinkOpener = OpenLink;
        Window.OnAppearanceChanged = () => { if (_app is not null) _app.Dark = Window.Dark; };
        // iOS may end an app in the background without a word: the open block goes back into the file.
        Window.OnBackground = () => _app?.Close();
        // The status line's messages fade, and typing autosaves, with no input to wake a frame.
        _tick = new Timer(_ => _runtime.RequestFrame(), null, 500, 500);
        IosWindow.Run(Window, () => _runtime.FramesRequested > 0, Frame);
    }

    private void Frame()
    {
        _runtime.ConsumeFrame();
        _runtime.BeginBuild(Window.Frame, Window.Events);
        // The page inside what the system leaves it: below the notch, above the home indicator and the keyboard.
        var safe = _runtime.BeginParent(_runtime.Column().Sized(Size.Fill, Size.Fill));
        safe.Style.Padding = _runtime.Insets;
        _app?.Build();
        _runtime.EndParent(safe);
        _runtime.EndBuild();
        Window.SetTextInput(_runtime.TextInput);
        IosWindow.BeginDraw(Window);
        _managed.Draw(_runtime.DrawList);
        IosWindow.EndDraw(Window);
        if (_openNext is { } next) { _openNext = null; Open(next); }
    }

    // ── Documents ───────────────────────────────────────────────────────────

    /// <summary>The document read last, if it is still there; else the sourdough notebook, copied into Documents the first time.</summary>
    public void OpenDefault()
    {
        if (NSUserDefaults.StandardUserDefaults.StringForKey(LastKey) is { } last && File.Exists(last)) { Open(last); return; }
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string sample = Path.Combine(documents, Sample);
        if (!File.Exists(sample) && NSBundle.MainBundle.PathForResource(Path.GetFileNameWithoutExtension(Sample), Path.GetExtension(Sample).TrimStart('.')) is { } bundled)
        {
            Directory.CreateDirectory(documents);
            File.Copy(bundled, sample);
        }
        Open(sample);
    }

    /// <summary>A document the system hands over (the Files app, a share): opened in place, with its access held while it is open.</summary>
    public void OpenUrl(NSUrl url)
    {
        if (url.Path is not { } path) return;
        bool scoped = url.StartAccessingSecurityScopedResource();
        Open(path, scoped ? url : null);
    }

    private void Open(string path, NSUrl? scoped = null)
    {
        _app?.Close();
        _scoped?.StopAccessingSecurityScopedResource();
        (_scoped, _path) = (scoped, path);
        var images = new Images((w, h, px) => _managed.CreateTexture(TextureFormat.RGBA8, TextureKind.Static, w, h, px),
            _runtime.RequestFrame, ReadImage);
        _app = new App(_runtime, new Document(path), images) { Dark = Window.Dark };
        // A document from elsewhere is remembered only while it is in the app's own Documents (no lasting access otherwise).
        if (scoped is null) NSUserDefaults.StandardUserDefaults.SetString(path, LastKey);
        _runtime.RequestFrame();
    }

    // A link: a heading in this document is the reader's; another Markdown document opens in
    // its place after this frame; anything else goes to the system (Safari, Mail…).
    private void OpenLink(string link)
    {
        if (link.StartsWith('#')) return;
        if (Uri.TryCreate(link, UriKind.Absolute, out var uri) && !uri.IsFile)
        {
            if (NSUrl.FromString(link) is { } url) UIApplication.SharedApplication.OpenUrl(url, new UIApplicationOpenUrlOptions(), null);
            return;
        }
        string file = Uri.UnescapeDataString(link.Split('#')[0]);
        if (_path is null || !file.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) return;
        string here = Path.GetDirectoryName(_path) ?? "";
        string target = Path.GetFullPath(Path.Combine(here, file));
        if (File.Exists(target)) _openNext = target;
    }

    // An image as PNG: a file beside the document or one on the web, decoded by UIKit whatever its
    // format (JPEG, HEIC, WebP, GIF…) where it is not PNG already.
    private static async Task<byte[]> ReadImage(string source)
    {
        byte[] bytes = source.StartsWith("http://") || source.StartsWith("https://")
            ? await Http.GetByteArrayAsync(source)
            : await File.ReadAllBytesAsync(source);
        if (Bibui.Core.Imaging.Png.IsPng(bytes)) return bytes;
        using var image = UIImage.LoadFromData(NSData.FromArray(bytes)) ?? throw new InvalidDataException("not an image UIKit reads");
        using var png = image.AsPNG() ?? throw new InvalidDataException("could not write it as PNG");
        return png.ToArray();
    }

    private static readonly HttpClient Http = new();
}

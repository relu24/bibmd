// bibmd: a zen Markdown reader and in-place editor on bibui.
//
//   md notes.md                          the window (Wayland, managed rasterizer)
//   md notes.md --png out.png [--size 1000x760] [--scale 1.5] [--script "…"]
//                                        headless: one frame to a PNG, no window
//   md notes.md --png out.png --card    the same, as a link's preview: the document alone, read only
//   md notes.md --check                  headless: each bib block's errors at their file lines,
//                                        and what each button's click would do; exit 1 on an error
//
// --damage draws every frame of the script into three buffers in turn, damage-tracked
// as the window does, and says where each frame differs from that frame drawn whole.
//
// A script is ;-separated steps, fed through the same input path as the window:
//   type:Hello       each character as a key press and its Text
//   key:Escape       a key by name (Ctrl+Z, Shift+Tab, Alt+Up, …)
//   click:block-3    a press and release at the middle of a box by id
//   rclick:block-3   the same with the right button
//   wheel:side-Keys:300  the wheel over a box by id, that far down (negative: up)
//   move:block-3     the pointer to the middle of a box by id, nothing pressed
//   move:bib-plot-0@0.3,0.5   the pointer that far across and down a box (0 to 1 of its width and height)
//   wait:500         frames for half a second (images load, scrolling settles)
//   png:mid.png      this frame to a PNG, as it is: mid-animation, if something moves (a frame is 1/60 s of motion)
//   drag:md-3:200    press at the start of a box's first line and drag that far right (selects text)
//
// --record FILE writes every frame, raw BGRA at the --size, to FILE (a fifo ffmpeg reads),
// a frame each 1/30 s with the pointer drawn on it, for a demo video; typing goes two
// characters a frame. Steps for it:
//   glide:640,400:600  the pointer to a point over that many ms, the button held if it is down
//   down / up          the left button pressed / let go where the pointer is
//   scroll:-12         the wheel where the pointer is, in half notches a frame (negative: down; a notch is three lines of the page)
//   hold:1500          that many ms of frames, nothing done
//   theme:rose-pine    crossfade to an installed Omarchy theme, as the window does when the desktop's changes

using System.Text;
using Bibui.Core;
using Bibui.Core.Runtime;
using Bibui.Core.Runtime.Platform;
using Bibui.Core.Draw;
using Bibui.Platform.Wayland;
using Bibui.Raster.Managed.Backend;
using BibMd;

string? file = args.FirstOrDefault(a => !a.StartsWith("--") && !IsOptionValue(a));
if (file is null)
{
    Console.Error.WriteLine("usage: md FILE.md");
    return 2;
}
if (args.Contains("--check")) return Check.Run(file);

App.RegisterFonts();
var runtime = BibuiRuntime.Create();
// The system's colour emoji and fallback faces, for what bibmd's own fonts do not have (an emoji, CJK).
Bibui.Text.Fontconfig.UseSystemFonts(runtime.Fonts);
runtime.SetTheme(App.ThemeFor(dark: true));
var doc = new Document(file);
string title = Path.GetFileName(doc.Path) + " — md";

int pngAt = Array.IndexOf(args, "--png");
if (pngAt >= 0)
{
    // A picture is drawn, not read: a script's download() goes to a folder in /tmp, not the reader's Downloads.
    if (Environment.GetEnvironmentVariable("XDG_DOWNLOAD_DIR") is not { Length: > 0 })
        Environment.SetEnvironmentVariable("XDG_DOWNLOAD_DIR", Path.Combine(Path.GetTempPath(), "bibmd-downloads"));
    return Headless(args[pngAt + 1]);
}

if (!WaylandWindow.IsSupported)
{
    Console.Error.WriteLine("md needs a Wayland session (WAYLAND_DISPLAY).");
    return 1;
}

using (var window = WaylandWindow.Open(new PlatformWindowOptions(title, 1000, 860), new WaylandHostOptions { AppId = "bibmd" }))
{
    // The window knows no runtime: its wake and clipboard are wired here, and a resize asks for a frame.
    runtime.WakeUp = window.Wake;
    runtime.WindowClipboard = (window.GetClipboardText, window.SetClipboardText);
    window.OnRefresh = runtime.RequestFrame;
    using var managed = new ManagedRenderer(window.Cpu) { DamageTracking = true };
    var images = new Images((w, h, px) => managed.CreateTexture(TextureFormat.RGBA8, TextureKind.Static, w, h, px), runtime.RequestFrame);
    var app = new App(runtime, doc, images);
    app.FollowOmarchy(new Omarchy());   // the window in the desktop's colours, and in its next ones
    // Autosave and the status line's messages need a frame when nothing else asks for one.
    using var tick = new Timer(_ => runtime.RequestFrame(), null, 500, 500);
    // BIBMD_WATCH=1 looks in every frame drawn for a tall white streak, and keeps the frame when there is one.
    string? watch = Environment.GetEnvironmentVariable("BIBMD_WATCH") == "1" ? Path.Combine(Path.GetTempPath(), "bibmd-watch") : null;
    if (watch is not null) Directory.CreateDirectory(watch);
    byte[] seen = [];
    int watched = 0, kept = 0;

    void Watch(RasterPixels px)
    {
        watched++;
        int w = px.Width, h = px.Height, stride = px.RowBytes;
        if (seen.Length != stride * h) seen = new byte[stride * h];
        System.Runtime.InteropServices.Marshal.Copy(px.Address, seen, 0, seen.Length);
        int best = 0, bx = 0, by = 0;
        for (int x = 0; x < w; x++)
            for (int y = 0, run = 0; y < h; y++)
            {
                int i = y * stride + x * 4;
                if (seen[i] > 200 && seen[i + 1] > 200 && seen[i + 2] > 200) { if (++run > best) (best, bx, by) = (run, x, y - run + 1); }
                else run = 0;
            }
        if (best < 60 || kept >= 20) return;
        string at = Path.Combine(watch!, $"frame{watched}.png");
        File.WriteAllBytes(at, Bibui.Core.Imaging.Png.Encode(seen, w, h, stride, Bibui.Core.Imaging.ChannelOrder.Bgra, Bibui.Core.Imaging.AlphaMode.Premultiplied));
        Console.Error.WriteLine($"watch: frame {watched}: white streak {best} px at x {bx}, from y {by}; {at}");
        kept++;
    }

    while (!app.Quit && !window.IsClosing)
    {
        // Wait for input or a frame owed; the compositor's frame callback paces the rest.
        bool idle = runtime.FramesRequested == 0 && window.Events.Count == 0;
        var events = window.GetEvents(wait: idle);
        if (window.IsClosing) break;
        if (runtime.FramesRequested == 0 && events.Count == 0) continue;
        runtime.ConsumeFrame();
        runtime.BeginBuild(window.Frame, events);
        app.Build();
        runtime.EndBuild();
        window.SetTextInput(runtime.TextInput);   // the field that takes typing, for an input method
        WaylandWindow.BeginDraw(window);
        managed.Draw(runtime.DrawList);
        if (watch is not null) Watch(window.Cpu.Pixels);
        WaylandWindow.EndDraw(window);
    }
    app.Close();
    runtime.WakeUp = null;   // the window goes next: nothing may wake it after
}
return 0;

int Headless(string png)
{
    int width = 1000, height = 760;
    int sizeAt = Array.IndexOf(args, "--size");
    if (sizeAt >= 0 && args[sizeAt + 1].Split('x') is [var w, var h]) (width, height) = (int.Parse(w), int.Parse(h));
    int scaleAt = Array.IndexOf(args, "--scale");
    float scale = scaleAt >= 0 ? float.Parse(args[scaleAt + 1], System.Globalization.CultureInfo.InvariantCulture) : 1f;
    var surface = new WindowFrame(WindowFrame.PhysicalSize(width, scale), WindowFrame.PhysicalSize(height, scale), scale, 60f);
    using var managed = new ManagedRenderer();
    // A card is drawn on a server, from a document anyone may have written: its images come only from
    // the document's own folder, never a URL or a path out of it.
    bool card = args.Contains("--card");
    string folder = Path.GetDirectoryName(Path.GetFullPath(doc.Path))! + Path.DirectorySeparatorChar;
    Func<string, Task<byte[]>>? read = !card ? null : source =>
        !source.Contains("://") && Path.GetFullPath(source).StartsWith(folder, StringComparison.Ordinal)
            ? File.ReadAllBytesAsync(Path.GetFullPath(source))
            : throw new FileNotFoundException(source);
    var images = new Images((w, h, px) => managed.CreateTexture(TextureFormat.RGBA8, TextureKind.Static, w, h, px), () => { }, read);
    var app = new App(runtime, doc, images);
    if (card) { app.Bare = app.Card = true; app.Lock(); }
    string? clipboard = null;   // what a script copied
    runtime.ClipboardSet = text => clipboard = text;
    runtime.ClipboardGet = () => clipboard;

    bool damage = args.Contains("--damage");
    using var tracked = new ManagedRenderer { DamageTracking = true };
    int pw = surface.Width, ph = surface.Height;
    // Three buffers in turn, as the window's pool hands them out.
    var buffers = Enumerable.Range(0, 3).Select(_ => GC.AllocateArray<byte>(damage ? pw * ph * 4 : 0, pinned: true)).ToArray();
    var held = buffers[0];
    int frames = 0, badFrames = 0;

    int recordAt = Array.IndexOf(args, "--record");
    using var record = recordAt >= 0 ? File.OpenWrite(args[recordAt + 1]) : null;
    float pointerX = width / 2f, pointerY = height / 2f;
    bool pressed = false;

    void Frame(params WindowEvent[] events)
    {
        if (record is not null) Thread.Sleep(33);   // the clock the page animates by keeps to the video's
        foreach (var e in events)
            if (e.Kind is WindowEventKind.MouseMove or WindowEventKind.MouseDown or WindowEventKind.MouseUp) (pointerX, pointerY) = (e.X, e.Y);
        runtime.BeginBuild(surface, [.. events]);
        app.Build();
        runtime.EndBuild();
        if (record is not null) Record();
        if (!damage) return;
        held = buffers[frames % buffers.Length];
        tracked.Draw(runtime.DrawList, new RasterPixels(System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement(held, 0), pw, ph, pw * 4));
        // Every frame, not only the last: a wrong frame the next one repaints is still seen on screen.
        frames++;
        var whole = FramePixels.Bgra(runtime, managed.Draw, out _, out _);
        int diff = 0, x0 = pw, y0 = ph, x1 = -1, y1 = -1;
        for (int i = 0; i < whole.Length; i += 4)
            if (whole[i] != held[i] || whole[i + 1] != held[i + 1] || whole[i + 2] != held[i + 2])
            {
                diff++;
                int px = i / 4 % pw, py = i / 4 / pw;
                (x0, y0, x1, y1) = (Math.Min(x0, px), Math.Min(y0, py), Math.Max(x1, px), Math.Max(y1, py));
            }
        if (diff == 0) return;
        Console.WriteLine($"damage: frame {frames}: {diff} px differ, in ({x0},{y0})–({x1},{y1})");
        if (badFrames++ == 0)
            File.WriteAllBytes(Path.ChangeExtension(png, $".frame{frames}.png"), Bibui.Core.Imaging.Png.Encode(held, pw, ph, pw * 4,
                Bibui.Core.Imaging.ChannelOrder.Bgra, Bibui.Core.Imaging.AlphaMode.Premultiplied));
    }

    // The frame as BGRA, with the pointer on it: a light dot ringed in dark, ringed in accent while pressed.
    void Record()
    {
        var px = FramePixels.Bgra(runtime, managed.Draw, out int fw, out int fh);
        float cx = pointerX * scale, cy = pointerY * scale, r = 7f * scale;
        for (int y = (int)(cy - r - 4 * scale); y <= cy + r + 4 * scale; y++)
            for (int x = (int)(cx - r - 4 * scale); x <= cx + r + 4 * scale; x++)
            {
                if (x < 0 || y < 0 || x >= fw || y >= fh) continue;
                float d = MathF.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                (byte b, byte g, byte rr)? c = d <= r - 1.5f * scale ? (240, 240, 240)
                    : d <= r ? (20, 20, 20)
                    : pressed && d <= r + 4 * scale ? (220, 180, 90) : null;
                if (c is not { } k) continue;
                int i = (y * fw + x) * 4;
                (px[i], px[i + 1], px[i + 2], px[i + 3]) = (k.b, k.g, k.rr, 255);
            }
        record!.Write(px);
    }

    Frame();
    Frame();
    int scriptAt = Array.IndexOf(args, "--script");
    if (scriptAt >= 0)
    {
        foreach (var step in args[scriptAt + 1].Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var (kind, arg) = step.Contains(':') ? (step[..step.IndexOf(':')], step[(step.IndexOf(':') + 1)..]) : (step, "");
            switch (kind)
            {
                case "type":
                {
                    // Recording, two characters a frame: typing at 60 a second reads as quick typing, not a paste.
                    var runes = arg.EnumerateRunes().ToList();
                    int per = record is null ? 1 : 2;
                    for (int i = 0; i < runes.Count; i += per)
                        Frame([.. runes.Skip(i).Take(per).SelectMany(r => new[] { WindowEvent.KeyPress(KeyOf(r)), WindowEvent.Text(r) })]);
                    break;
                }
                case "key":
                {
                    var mods = KeyModifiers.None;
                    var parts = arg.Split('+');
                    foreach (var m in parts[..^1]) mods |= m switch { "Ctrl" => KeyModifiers.Ctrl, "Shift" => KeyModifiers.Shift, "Alt" => KeyModifiers.Alt, _ => KeyModifiers.None };
                    // A digit or letter as itself (key:2, key:k), else bibui's name (key:Space, key:Left).
                    var named = parts[^1].Length == 1 ? KeyOf(new Rune(parts[^1][0]))
                        : Enum.TryParse<Key>(parts[^1], ignoreCase: true, out var kk) ? kk : Key.Unknown;
                    if (named == Key.Unknown) { Console.Error.WriteLine($"no key {parts[^1]}"); return 1; }
                    Frame(WindowEvent.KeyPress(named, mods));
                    break;
                }
                case "wheel":
                {
                    var (id, notches) = (arg[..arg.LastIndexOf(':')], float.Parse(arg[(arg.LastIndexOf(':') + 1)..]));
                    if (runtime.FindById(id) is not { } node) { Console.Error.WriteLine($"no box {id}"); return 1; }
                    float x = node.Rect.X + node.Extent.X / 2f, y = node.Rect.Y + node.Extent.Y / 2f;
                    Frame(WindowEvent.Mouse(WindowEventKind.MouseMove, x, y, MouseButton.Left));
                    Frame(WindowEvent.Wheel(x, y, 0f, notches));
                    break;
                }
                case "move":
                {
                    var (id, fx, fy) = (arg, 0.5f, 0.5f);
                    if (arg.LastIndexOf('@') is var at and > 0)
                    {
                        var f = arg[(at + 1)..].Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                        (id, fx, fy) = (arg[..at], f[0], f.Length > 1 ? f[1] : 0.5f);
                    }
                    if (runtime.FindById(id) is not { } node) { Console.Error.WriteLine($"no box {id}"); return 1; }
                    Frame(WindowEvent.Mouse(WindowEventKind.MouseMove, node.Rect.X + node.Extent.X * fx, node.Rect.Y + node.Extent.Y * fy, MouseButton.Left));
                    break;
                }
                case "drag":
                {
                    var (id, dx) = (arg[..arg.LastIndexOf(':')], float.Parse(arg[(arg.LastIndexOf(':') + 1)..], System.Globalization.CultureInfo.InvariantCulture));
                    if (runtime.FindById(id) is not { } node) { Console.Error.WriteLine($"no box {id}"); return 1; }
                    float x = node.Rect.X + 2f, y = node.Rect.Y + 8f;
                    Frame(WindowEvent.Mouse(WindowEventKind.MouseMove, x, y, MouseButton.Left));
                    Frame(WindowEvent.Mouse(WindowEventKind.MouseDown, x, y, MouseButton.Left));
                    for (int s = 1; s <= 4; s++) Frame(WindowEvent.Mouse(WindowEventKind.MouseMove, x + dx * s / 4f, y, MouseButton.Left));
                    Frame(WindowEvent.Mouse(WindowEventKind.MouseUp, x + dx, y, MouseButton.Left));
                    break;
                }
                case "glide":
                {
                    var parts = arg.Split(':');
                    var to = parts[0].Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                    int n = Math.Max(1, (parts.Length > 1 ? int.Parse(parts[1]) : 500) / 33);
                    float x0 = pointerX, y0 = pointerY;
                    for (int s = 1; s <= n; s++)
                    {
                        float t = (float)s / n, e = t * t * (3f - 2f * t);   // eased: starts and stops gently
                        Frame(WindowEvent.Mouse(WindowEventKind.MouseMove, x0 + (to[0] - x0) * e, y0 + (to[1] - y0) * e, MouseButton.Left));
                    }
                    break;
                }
                case "down":
                    pressed = true;
                    Frame(WindowEvent.Mouse(WindowEventKind.MouseDown, pointerX, pointerY, MouseButton.Left));
                    break;
                case "up":
                    pressed = false;
                    Frame(WindowEvent.Mouse(WindowEventKind.MouseUp, pointerX, pointerY, MouseButton.Left));
                    break;
                case "scroll":
                {
                    float notches = float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture);
                    for (int t = 0; t < (int)MathF.Abs(notches * 2f); t++) Frame(WindowEvent.Wheel(pointerX, pointerY, 0f, MathF.Sign(notches) * 0.5f));
                    for (int t = 0; t < 6; t++) Frame();
                    break;
                }
                case "hold":
                    for (int t = 0; t < int.Parse(arg) / 33; t++) Frame();
                    break;
                case "theme":
                {
                    string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    var toml = new[] { ".config/omarchy/themes", ".local/share/omarchy/themes" }
                        .Select(d => Path.Combine(home, d, arg, "colors.toml")).FirstOrDefault(File.Exists);
                    if (toml is null || Omarchy.FromToml(File.ReadAllText(toml)) is not { } theme)
                        throw new ArgumentException($"no Omarchy theme {arg} with a colors.toml");
                    runtime.SetTheme(theme, App.ThemeFade);
                    Frame();
                    break;
                }
                case "png":
                    File.WriteAllBytes(arg, FramePixels.Png(runtime, managed.Draw));
                    break;
                case "wait":
                    for (int t = 0; t < int.Parse(arg) / 25; t++) { Thread.Sleep(25); Frame(); }
                    break;
                case "click" or "rclick":
                {
                    var button = kind == "click" ? MouseButton.Left : MouseButton.Right;
                    if (runtime.FindById(arg) is not { } node) { Console.Error.WriteLine($"no box {arg}"); return 1; }
                    float x = node.Rect.X + node.Extent.X / 2f, y = node.Rect.Y + Math.Min(node.Extent.Y / 2f, 12f);
                    Frame(WindowEvent.Mouse(WindowEventKind.MouseMove, x, y, button));
                    Frame(WindowEvent.Mouse(WindowEventKind.MouseDown, x, y, button));
                    Frame(WindowEvent.Mouse(WindowEventKind.MouseUp, x, y, button));
                    break;
                }
            }
            Frame();
        }
    }
    if (record is null)
        for (int i = 0; i < 40; i++) { Thread.Sleep(i < 20 ? 25 : 0); Frame(); }   // images load, scrolling settles
    File.WriteAllBytes(png, FramePixels.Png(runtime, managed.Draw));
    if (damage)
    {
        var whole = FramePixels.Bgra(runtime, managed.Draw, out _, out _);
        int diff = 0, x0 = pw, y0 = ph, x1 = -1, y1 = -1;
        for (int i = 0; i < whole.Length; i += 4)
            if (whole[i] != held[i] || whole[i + 1] != held[i + 1] || whole[i + 2] != held[i + 2])
            {
                diff++;
                int px = i / 4 % pw, py = i / 4 / pw;
                (x0, y0, x1, y1) = (Math.Min(x0, px), Math.Min(y0, py), Math.Max(x1, px), Math.Max(y1, py));
            }
        string held_ = Path.ChangeExtension(png, ".damaged.png");
        File.WriteAllBytes(held_, Bibui.Core.Imaging.Png.Encode(held, pw, ph, pw * 4, Bibui.Core.Imaging.ChannelOrder.Bgra, Bibui.Core.Imaging.AlphaMode.Premultiplied));
        Console.WriteLine(diff == 0 ? "damage: identical to a whole frame" : $"damage: {diff} px differ, in ({x0},{y0})–({x1},{y1}); see {held_}");
    }
    Console.WriteLine($"mode {app.Mode}, selected {app.Selected}, cursor {app.Cursor}");
    if (clipboard is not null) Console.WriteLine("clipboard: " + clipboard.Replace("\n", "⏎"));
    if (app.Mode != Mode.Read) Console.WriteLine("buffer: " + app.Buffer.Replace("\n", "⏎"));
    app.Close();
    return 0;
}

static Key KeyOf(Rune r)
{
    char c = char.ToUpperInvariant((char)r.Value);
    return c is >= 'A' and <= 'Z' or >= '0' and <= '9' ? (Key)c : c == ' ' ? Key.Space : Key.Unknown;
}

bool IsOptionValue(string a)
{
    int i = Array.IndexOf(args, a);
    return i > 0 && args[i - 1] is "--png" or "--size" or "--scale" or "--script" or "--record";
}

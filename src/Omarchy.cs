using System.Globalization;
using Bibui.Core.Theming;

namespace BibMd;

/// <summary>
/// The Omarchy theme the desktop is in, as bibmd's colours: its colors.toml
/// (mode, background, foreground, accent and the terminal colours) laid over
/// the built-in theme of the same mode. <see cref="Poll"/> reads it again, at
/// most once a second, and says when it has changed: omarchy-theme-set swaps
/// the whole directory, so the file is read rather than watched.
/// </summary>
public sealed class Omarchy
{
    private static readonly TimeSpan Every = TimeSpan.FromSeconds(1);

    private readonly string _path;
    private string? _seen;
    private DateTime _lastLook = DateTime.MinValue;

    public Omarchy()
    {
        string state = Environment.GetEnvironmentVariable("XDG_STATE_HOME") is { Length: > 0 } s
            ? s : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "state");
        _path = Path.Combine(state, "omarchy", "current", "theme", "colors.toml");
    }

    /// <summary>The theme now, or null when there is no Omarchy theme or it says too little to draw with.</summary>
    public ThemeTokens? Theme { get; private set; }

    /// <summary>Reads colors.toml if a second has passed; true when the theme it gives differs from the last.</summary>
    public bool Poll()
    {
        var now = DateTime.UtcNow;
        if (now - _lastLook < Every) return false;
        _lastLook = now;
        string? text;
        try { text = File.Exists(_path) ? File.ReadAllText(_path) : null; }
        catch (IOException) { return false; }   // caught mid-swap: the next look sees it whole
        if (text == _seen) return false;
        _seen = text;
        var theme = text is null ? null : FromToml(text);
        if (theme is null && Theme is null) return false;
        Theme = theme;
        return true;
    }

    /// <summary>The colours of a colors.toml over the built-in theme of its mode.</summary>
    public static ThemeTokens? FromToml(string toml)
    {
        var c = new Dictionary<string, string>();
        foreach (var raw in toml.Split('\n'))
        {
            var line = raw.Trim();
            int eq = line.IndexOf('=');
            if (line.StartsWith('#') || eq < 0) continue;
            c[line[..eq].Trim()] = line[(eq + 1)..].Trim().Trim('"', '\'');
        }
        if (Hex(c, "background") is not { } bg || Hex(c, "foreground") is not { } fg) return null;
        bool dark = c.TryGetValue("mode", out var mode) ? mode != "light" : Luminance(bg) < Luminance(fg);
        var accent = Hex(c, "accent") ?? Hex(c, "blue") ?? Mix(bg, fg, 0.7f);
        string Or(string key, string fallback) => Hex(c, key) ?? fallback;

        var basis = App.ThemeFor(dark);
        // The neutral ramp runs from the background to the foreground, the page end at 950 when
        // dark and at 50 when light, so whatever names a step lands between the two.
        var steps = ColorRamp.Steps;
        var neutral = new ColorRamp([.. steps.Select((_, i) =>
        {
            float t = (float)i / (steps.Count - 1);
            return dark ? Mix(fg, bg, t) : Mix(bg, fg, t);
        })]);
        // The accent ramp: the accent at 500, towards white above it and towards black below.
        var accentRamp = new ColorRamp([.. steps.Select(s => s < 500
            ? Mix(accent, "#FFFFFF", (500 - s) / 500f * 0.85f)
            : Mix(accent, "#000000", (s - 500) / 450f * 0.75f))]);
        string red = Or("red", "#DC2626"), green = Or("green", "#22C55E"), yellow = Or("yellow", "#EAB308"), blue = Or("blue", accent);
        return basis with
        {
            Name = "omarchy",
            Colors = new ColorTokens
            {
                Mode = dark ? ThemeMode.Dark : ThemeMode.Light,
                Ramps = new ColorRamps { Neutral = neutral, Accent = accentRamp },
                Background = bg, Surface = bg,
                SurfaceRaised = Mix(bg, fg, 0.07f),
                SurfaceRaisedHover = Mix(bg, fg, 0.12f),
                SurfaceRaisedActive = Mix(bg, fg, 0.18f),
                Border = Mix(bg, fg, 0.15f),
                BorderHover = Mix(bg, fg, 0.3f),
                BorderFocus = accent,
                TextPrimary = fg,
                TextSecondary = Mix(fg, bg, 0.3f),
                TextDisabled = Or("muted", Mix(fg, bg, 0.55f)),
                Accent = accent,
                AccentHover = Mix(accent, fg, 0.15f),
                AccentActive = Mix(accent, bg, 0.15f),
                AccentForeground = bg,
                Destructive = red, DestructiveForeground = bg,
                Success = green, SuccessForeground = bg, SuccessBg = Mix(bg, green, 0.15f),
                Warning = yellow, WarningForeground = bg, WarningBg = Mix(bg, yellow, 0.15f),
                Info = blue, InfoForeground = bg, InfoBg = Mix(bg, blue, 0.15f),
            },
        };
    }

    /// <summary>A key's colour as <c>#RRGGBB</c>, or null when it is missing or not one.</summary>
    private static string? Hex(Dictionary<string, string> c, string key)
    {
        if (!c.TryGetValue(key, out var v)) return null;
        v = v.TrimStart('#');
        if (v.Length == 8) v = v[..6];
        return v.Length == 6 && int.TryParse(v, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _) ? "#" + v.ToUpperInvariant() : null;
    }

    private static (float R, float G, float B) Rgb(string hex)
    {
        int v = int.Parse(hex.AsSpan(1, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return ((v >> 16 & 0xFF) / 255f, (v >> 8 & 0xFF) / 255f, (v & 0xFF) / 255f);
    }

    /// <summary><paramref name="a"/> part way to <paramref name="b"/>, by <paramref name="t"/>.</summary>
    private static string Mix(string a, string b, float t)
    {
        var (ar, ag, ab) = Rgb(a);
        var (br, bg, bb) = Rgb(b);
        static int Ch(float x, float y, float t) => (int)MathF.Round(Math.Clamp(x + (y - x) * t, 0f, 1f) * 255f);
        return $"#{Ch(ar, br, t):X2}{Ch(ag, bg, t):X2}{Ch(ab, bb, t):X2}";
    }

    private static float Luminance(string hex)
    {
        var (r, g, b) = Rgb(hex);
        return 0.2126f * r + 0.7152f * g + 0.0722f * b;
    }
}

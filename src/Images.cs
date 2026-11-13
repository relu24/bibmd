using System.Diagnostics;
using Bibui.Core.Draw;
using Bibui.Core.Imaging;

namespace BibMd;

/// <summary>
/// Images the document points at, loaded off the UI thread: PNG with bibui's
/// own decoder, anything else (JPEG, WebP, GIF, SVG…) through ImageMagick, a
/// URL fetched first. Each is made into a texture once per size it is shown at,
/// scaled down here with an area average so a large photo stays smooth.
/// <paramref name="read"/> replaces the reading where there is no disk or ImageMagick (the browser).
/// </summary>
public sealed class Images(Func<int, int, byte[], TextureHandle> makeTexture, Action wake, Func<string, Task<byte[]>>? read = null)
{
    private sealed class Entry
    {
        public volatile RgbaImage? Image;
        public volatile string? Error;
        public readonly Dictionary<(int, int), TextureHandle> Textures = [];
    }

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly Dictionary<string, Entry> _entries = [];

    /// <summary>The image, if it has loaded; null while it loads; an error once it has failed.</summary>
    public (RgbaImage? Image, string? Error) Get(string source)
    {
        if (!_entries.TryGetValue(source, out var entry))
        {
            _entries[source] = entry = new Entry();
            _ = Task.Run(() => Load(source, entry));
        }
        return (entry.Image, entry.Error);
    }

    public TextureHandle Texture(string source, int width, int height)
    {
        var entry = _entries[source];
        if (!entry.Textures.TryGetValue((width, height), out var texture))
            entry.Textures[(width, height)] = texture = makeTexture(width, height, Scale(entry.Image!, width, height));
        return texture;
    }

    private async Task Load(string source, Entry entry)
    {
        try
        {
            byte[] bytes = read is not null ? await read(source)
                : source.StartsWith("http://") || source.StartsWith("https://") ? await Http.GetByteArrayAsync(source)
                : File.ReadAllBytes(source);
            entry.Image = Png.Decode(Png.IsPng(bytes) || OperatingSystem.IsBrowser() ? bytes : Convert(bytes));
        }
        catch (Exception e)
        {
            entry.Error = e is FileNotFoundException or DirectoryNotFoundException ? "not found" : e.Message;
        }
        wake();
    }

    // Anything that is not a PNG becomes one through ImageMagick.
    [System.Runtime.Versioning.UnsupportedOSPlatform("browser")]
    private static byte[] Convert(byte[] bytes)
    {
        var psi = new ProcessStartInfo("magick", ["-", "-auto-orient", "-background", "none", "png:-"])
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("magick did not start");
        var writer = Task.Run(() => { p.StandardInput.BaseStream.Write(bytes); p.StandardInput.Close(); });
        using var output = new MemoryStream();
        p.StandardOutput.BaseStream.CopyTo(output);
        writer.Wait();
        p.WaitForExit();
        if (p.ExitCode != 0) throw new InvalidOperationException("unsupported image format");
        return output.ToArray();
    }

    // An area average from the source to width × height, straight RGBA.
    private static byte[] Scale(RgbaImage src, int width, int height)
    {
        var dst = new byte[width * height * 4];
        double sx = src.Width / (double)width, sy = src.Height / (double)height;
        for (int y = 0; y < height; y++)
        {
            int y0 = (int)(y * sy), y1 = Math.Max(y0 + 1, Math.Min(src.Height, (int)Math.Ceiling((y + 1) * sy)));
            for (int x = 0; x < width; x++)
            {
                int x0 = (int)(x * sx), x1 = Math.Max(x0 + 1, Math.Min(src.Width, (int)Math.Ceiling((x + 1) * sx)));
                double r = 0, g = 0, b = 0, a = 0;
                for (int yy = y0; yy < y1; yy++)
                {
                    var row = src.Row(yy);
                    for (int xx = x0; xx < x1; xx++)
                    {
                        int i = xx * 4;
                        double alpha = row[i + 3];
                        r += row[i] * alpha; g += row[i + 1] * alpha; b += row[i + 2] * alpha; a += alpha;
                    }
                }
                int o = (y * width + x) * 4, n = (y1 - y0) * (x1 - x0);
                if (a > 0)
                {
                    dst[o] = (byte)Math.Round(r / a);
                    dst[o + 1] = (byte)Math.Round(g / a);
                    dst[o + 2] = (byte)Math.Round(b / a);
                    dst[o + 3] = (byte)Math.Round(a / n);
                }
            }
        }
        return dst;
    }
}

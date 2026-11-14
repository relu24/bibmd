namespace BibMd;

/// <summary>
/// Telling a bib block's fence from any other, and reading what is between its fences: plain text
/// work, kept apart from the drawing so a host without bibui (hosted bibmd's check) can use it.
/// </summary>
public sealed partial class Bib
{
    /// <summary>A fenced block whose info string is bib: a program.</summary>
    public static bool IsBib(string source) => Info(source) is { } info
        && (info == "bib" || info.StartsWith("bib ", StringComparison.Ordinal)) && !IsData(source);

    /// <summary>A fenced block whose info string is <c>bib data</c>: YAML whose keys are added to the document's data.</summary>
    public static bool IsData(string source) => Info(source) is { } info
        && info.Split(' ', StringSplitOptions.RemoveEmptyEntries) is ["bib", "data", ..];

    // A fence's info string, or null for anything that is not a fenced block.
    private static string? Info(string source)
    {
        int nl = source.IndexOf('\n');
        string first = (nl < 0 ? source : source[..nl]).Trim();
        if (!first.StartsWith("```", StringComparison.Ordinal) && !first.StartsWith("~~~", StringComparison.Ordinal)) return null;
        return first.TrimStart('`', '~').Trim();
    }

    // The lines between the fences (a block still being written may have no closing one).
    internal static string Body(string source)
    {
        var lines = source.Split('\n').Skip(1).ToList();
        if (lines.Count > 0 && lines[^1].TrimStart() is var last && (last.StartsWith("```", StringComparison.Ordinal) || last.StartsWith("~~~", StringComparison.Ordinal)))
            lines.RemoveAt(lines.Count - 1);
        return string.Join('\n', lines);
    }
}

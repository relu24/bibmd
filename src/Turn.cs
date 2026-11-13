using System.Text.RegularExpressions;

namespace BibMd;

/// <summary>
/// A block turned into another kind: its words taken out of the Markdown that
/// makes it what it is (the #, the &gt;, the fences, the list marker), then put
/// into the Markdown of the kind asked for.
/// </summary>
public static partial class Turn
{
    public static readonly string[] Kinds =
        ["Paragraph", "Heading 1", "Heading 2", "Heading 3", "Quote", "Code block", "Bullet list", "Numbered list", "Task list"];

    [GeneratedRegex(@"^ {0,3}#{1,6}\s*")] private static partial Regex HeadingMark();
    [GeneratedRegex(@"\s+#+\s*$")] private static partial Regex HeadingClose();
    [GeneratedRegex(@"^ {0,3}> ?")] private static partial Regex QuoteMark();
    [GeneratedRegex(@"^\s*([-+*]|\d{1,9}[.)])\s+(\[[ xX]\]\s+)?")] private static partial Regex ItemMark();

    /// <summary>What kind of the <see cref="Kinds"/> a block is now, or null for one that is none of them.</summary>
    public static string? KindOf(Block b, string source) => b.Kind switch
    {
        BlockKind.Paragraph => "Paragraph",
        BlockKind.Heading => Blocks.HeadingOf(source).Level switch { 1 => "Heading 1", 2 => "Heading 2", 3 => "Heading 3", _ => null },
        BlockKind.Quote => "Quote",
        BlockKind.Code => "Code block",
        BlockKind.ListItem => Regex.IsMatch(source, @"^\s*[-+*]\s+\[[ xX]\]") ? "Task list"
            : Regex.IsMatch(source, @"^\s*\d") ? "Numbered list" : "Bullet list",
        _ => null,
    };

    /// <summary><paramref name="source"/>, a block of kind <paramref name="kind"/>, as a block of kind <paramref name="to"/>.</summary>
    public static string To(string to, BlockKind kind, string source)
    {
        var lines = Content(kind, source);
        while (lines.Count > 1 && lines[^1].Trim().Length == 0) lines.RemoveAt(lines.Count - 1);
        return to switch
        {
            "Heading 1" => "# " + Joined(lines),
            "Heading 2" => "## " + Joined(lines),
            "Heading 3" => "### " + Joined(lines),
            "Quote" => string.Join('\n', lines.Select(l => l.Length == 0 ? ">" : "> " + l)),
            "Code block" => "```\n" + string.Join('\n', lines) + "\n```",
            // A list: a line an item.
            "Bullet list" => string.Join('\n', Items(lines).Select(l => "- " + l)),
            "Numbered list" => string.Join('\n', Items(lines).Select((l, i) => $"{i + 1}. " + l)),
            "Task list" => string.Join('\n', Items(lines).Select(l => "- [ ] " + l)),
            _ => string.Join('\n', lines),
        };
    }

    // The block's words, a line a line, without what made it its kind.
    private static List<string> Content(BlockKind kind, string source)
    {
        var lines = source.Split('\n').ToList();
        switch (kind)
        {
            case BlockKind.Heading:
                if (lines.Count == 2 && !HeadingMark().IsMatch(lines[0])) return [lines[0].Trim()];   // setext
                return [HeadingClose().Replace(HeadingMark().Replace(lines[0], ""), "")];
            case BlockKind.Quote:
                return lines.Select(l => QuoteMark().Replace(l, "")).ToList();
            case BlockKind.Code:
                if (lines.Count >= 2) return lines[1..^1];
                return [];
            case BlockKind.ListItem:
            {
                var m = ItemMark().Match(lines[0]);
                int indent = m.Success ? m.Length : 0;
                lines[0] = lines[0][indent..];
                for (int i = 1; i < lines.Count; i++)
                {
                    int n = 0;
                    while (n < indent && n < lines[i].Length && lines[i][n] == ' ') n++;
                    lines[i] = lines[i][n..];
                }
                return lines;
            }
            default:
                return lines;
        }
    }

    private static string Joined(List<string> lines) => string.Join(' ', lines.Select(l => l.Trim()).Where(l => l.Length > 0));

    private static IEnumerable<string> Items(List<string> lines)
    {
        var items = lines.Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        return items.Count == 0 ? [""] : items;
    }
}

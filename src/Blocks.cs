using System.Text.RegularExpressions;

namespace BibMd;

public enum BlockKind { Paragraph, Heading, Code, Quote, ListItem, Table, Rule, Image, FrontMatter }

/// <summary>
/// One top-level piece of the document: the source in [Start, End), without its
/// trailing newline. <see cref="Tight"/> is true when no blank line separates it
/// from the block before (list items that follow one another).
/// </summary>
public readonly record struct Block(int Start, int End, BlockKind Kind, bool Tight)
{
    public int Length => End - Start;
}

/// <summary>
/// Splits Markdown into the blocks a person would point at: a heading, a
/// paragraph, a fenced code block, a quote, a table, one list item (with what
/// is nested under it), a rule, an image on its own line.
/// </summary>
public static partial class Blocks
{
    [GeneratedRegex(@"^ {0,3}#{1,6}(\s|$)")] private static partial Regex Atx();
    [GeneratedRegex(@"^ {0,3}(`{3,}|~{3,})")] private static partial Regex Fence();
    [GeneratedRegex(@"^ {0,3}([-*_])( *\1){2,} *$")] private static partial Regex Rule();
    [GeneratedRegex(@"^ {0,3}([-+*]|\d{1,9}[.)])(\s|$)")] private static partial Regex Item();
    [GeneratedRegex(@"^ {0,3}>")] private static partial Regex Quote();
    [GeneratedRegex(@"^ *\|? *:?-{1,}:? *(\| *:?-{1,}:? *)*\|? *$")] private static partial Regex TableDelimiter();
    [GeneratedRegex(@"^!\[(?<alt>[^\]]*)\]\((?<src><[^>]+>|[^\s)]+)(\s+""[^""]*"")?\)$")] private static partial Regex ImageLine();

    public static List<Block> Split(string text)
    {
        var lines = Lines(text);
        var blocks = new List<Block>();
        int i = 0;
        bool sawBlank = true;

        // YAML front matter: only as the very first thing in the file.
        if (lines.Count > 1 && Line(text, lines[0]) is "---")
        {
            for (int j = 1; j < lines.Count; j++)
            {
                if (Line(text, lines[j]) is "---" or "...")
                {
                    blocks.Add(new(lines[0].Start, lines[j].End, BlockKind.FrontMatter, false));
                    i = j + 1;
                    sawBlank = false;
                    break;
                }
            }
        }

        while (i < lines.Count)
        {
            string line = Line(text, lines[i]);
            if (IsBlank(line)) { sawBlank = true; i++; continue; }

            int first = i;
            BlockKind kind;
            if (Fence().Match(line) is { Success: true } fence)
            {
                kind = BlockKind.Code;
                string marker = fence.Groups[1].Value;
                i++;
                while (i < lines.Count)
                {
                    string l = Line(text, lines[i]).TrimStart();
                    i++;
                    if (l.StartsWith(marker[0]) && l.TrimEnd().All(c => c == marker[0]) && l.TrimEnd().Length >= marker.Length) break;
                }
            }
            else if (Atx().IsMatch(line)) { kind = BlockKind.Heading; i++; }
            else if (Rule().IsMatch(line)) { kind = BlockKind.Rule; i++; }
            else if (Quote().IsMatch(line))
            {
                kind = BlockKind.Quote;
                i++;
                while (i < lines.Count && !IsBlank(Line(text, lines[i]))) i++;   // lazy continuation
            }
            else if (Item().Match(line) is { Success: true } item)
            {
                kind = BlockKind.ListItem;
                int indent = Indent(line);
                i++;
                while (i < lines.Count)
                {
                    string l = Line(text, lines[i]);
                    if (IsBlank(l))
                    {
                        // A blank line stays in the item only if indented content follows it.
                        int k = i;
                        while (k < lines.Count && IsBlank(Line(text, lines[k]))) k++;
                        if (k < lines.Count && Indent(Line(text, lines[k])) > indent) { i = k; continue; }
                        break;
                    }
                    if (Indent(l) <= indent && (Item().IsMatch(l) || Atx().IsMatch(l) || Fence().IsMatch(l) || Quote().IsMatch(l) || Rule().IsMatch(l)))
                        break;
                    i++;
                }
            }
            else if (line.Contains('|') && i + 1 < lines.Count && TableDelimiter().IsMatch(Line(text, lines[i + 1])) && Line(text, lines[i + 1]).Contains('-'))
            {
                kind = BlockKind.Table;
                i += 2;
                while (i < lines.Count && !IsBlank(Line(text, lines[i])) && Line(text, lines[i]).Contains('|')) i++;
            }
            else
            {
                kind = BlockKind.Paragraph;
                i++;
                while (i < lines.Count)
                {
                    string l = Line(text, lines[i]);
                    if (IsBlank(l)) break;
                    if (Rule().IsMatch(l) && l.TrimStart()[0] == '-') { i++; break; }  // setext underline
                    if (Atx().IsMatch(l) || Fence().IsMatch(l) || Quote().IsMatch(l) || Item().IsMatch(l) && Indent(l) < 4) break;
                    i++;
                }
                if (i - first == 1 && ImageLine().IsMatch(line.Trim())) kind = BlockKind.Image;
                else if (i - first == 2 && Line(text, lines[first + 1]).TrimStart().StartsWith('=') ||
                         i - first == 2 && Rule().IsMatch(Line(text, lines[first + 1])))
                    kind = BlockKind.Heading;
            }

            blocks.Add(new(lines[first].Start, lines[i - 1].End, kind, !sawBlank && blocks.Count > 0));
            sawBlank = false;
        }
        return blocks;
    }

    /// <summary>An image block's alt text and source.</summary>
    public static (string Alt, string Src) ImageOf(string block)
    {
        var m = ImageLine().Match(block.Trim());
        string src = m.Groups["src"].Value;
        if (src.StartsWith('<')) src = src[1..^1];
        return (m.Groups["alt"].Value, Uri.UnescapeDataString(src));
    }

    /// <summary>A heading block's level (1–6) and its words, as the contents list shows them.</summary>
    public static (int Level, string Text) HeadingOf(string block)
    {
        string first = block.Split('\n')[0];
        if (Atx().IsMatch(first))
        {
            string t = first.TrimStart();
            int level = t.TakeWhile(c => c == '#').Count();
            return (level, Plain(t[level..].TrimEnd().TrimEnd('#')));
        }
        // Setext: the line under it says which.
        string under = block.Split('\n').ElementAtOrDefault(1)?.TrimStart() ?? "";
        return (under.StartsWith('=') ? 1 : 2, Plain(first));
    }

    /// <summary>A line of Markdown as words: no block markers, emphasis or link targets.</summary>
    public static string Plain(string line)
    {
        string t = LineMarker().Replace(line, "");
        t = Link().Replace(t, "$1");
        t = Emphasis().Replace(t, "");
        t = t.Trim();
        return t.Length > 80 ? t[..79].TrimEnd() + "…" : t;
    }

    [GeneratedRegex(@"^\s*(#{1,6}\s+|>\s*|[-+*]\s+(\[[ xX]\]\s+)?|\d{1,9}[.)]\s+)*")] private static partial Regex LineMarker();
    [GeneratedRegex(@"!?\[([^\]]*)\]\([^)]*\)")] private static partial Regex Link();
    [GeneratedRegex(@"\*\*|__|~~|`|(?<!\w)[*_]|[*_](?!\w)")] private static partial Regex Emphasis();

    private readonly record struct Span(int Start, int End);

    private static List<Span> Lines(string text)
    {
        var lines = new List<Span>();
        int start = 0;
        for (int i = 0; i <= text.Length; i++)
        {
            if (i == text.Length || text[i] == '\n')
            {
                int end = i > start && text[i - 1] == '\r' ? i - 1 : i;
                if (i < text.Length || start < text.Length) lines.Add(new(start, end));
                start = i + 1;
            }
        }
        return lines;
    }

    private static string Line(string text, Span s) => text[s.Start..s.End];

    private static bool IsBlank(string line) => string.IsNullOrWhiteSpace(line);

    private static int Indent(string line)
    {
        int n = 0;
        foreach (char c in line)
        {
            if (c == ' ') n++;
            else if (c == '\t') n += 4;
            else break;
        }
        return n;
    }
}

using System.Globalization;
using System.Text;

namespace BibMd;

/// <summary>
/// Markdown tables laid out so their pipes line up, column under column, in a
/// monospaced face: every cell padded to its column's widest, the delimiter
/// row as wide, keeping each column's alignment colons.
/// </summary>
public static class Tables
{
    /// <summary><paramref name="text"/> with every table in it aligned; the rest as it was.</summary>
    public static string AlignAll(string text)
    {
        var blocks = Blocks.Split(text);
        for (int i = blocks.Count - 1; i >= 0; i--)
            if (blocks[i].Kind == BlockKind.Table)
                text = text[..blocks[i].Start] + Align(text[blocks[i].Start..blocks[i].End]) + text[blocks[i].End..];
        return text;
    }

    /// <summary>One table, aligned. Rows short of cells are filled out with empty ones.</summary>
    public static string Align(string table)
    {
        var lines = table.Split('\n');
        if (lines.Length < 2) return table;
        string indent = lines[0][..(lines[0].Length - lines[0].TrimStart().Length)];
        var rows = lines.Select(Cells).ToList();
        int cols = rows.Max(r => r.Count);
        var align = new char[cols];   // l, c, r, or ' ' for none
        var widths = new int[cols];
        for (int c = 0; c < cols; c++)
        {
            string d = c < rows[1].Count ? rows[1][c] : "";
            bool left = d.StartsWith(':'), right = d.Length > 1 && d.EndsWith(':');
            align[c] = left && right ? 'c' : right ? 'r' : left ? 'l' : ' ';
            widths[c] = 3;
            for (int r = 0; r < rows.Count; r++)
                if (r != 1 && c < rows[r].Count) widths[c] = Math.Max(widths[c], Width(rows[r][c]));
        }

        var sb = new StringBuilder(table.Length + 16 * lines.Length);
        for (int r = 0; r < rows.Count; r++)
        {
            if (r > 0) sb.Append('\n');
            sb.Append(indent).Append('|');
            for (int c = 0; c < cols; c++)
            {
                int w = widths[c];
                sb.Append(' ');
                if (r == 1)
                    sb.Append(align[c] is 'l' or 'c' ? ':' : '-')
                      .Append('-', w - 2)
                      .Append(align[c] is 'r' or 'c' ? ':' : '-');
                else
                {
                    string cell = c < rows[r].Count ? rows[r][c] : "";
                    int room = w - Width(cell);
                    int before = align[c] == 'r' ? room : align[c] == 'c' ? room / 2 : 0;
                    sb.Append(' ', before).Append(cell).Append(' ', room - before);
                }
                sb.Append(" |");
            }
        }
        return sb.ToString();
    }

    /// <summary>A row's cells, trimmed: split at the pipes that are not escaped, without the outer ones.</summary>
    public static List<string> Cells(string line)
    {
        string t = line.Trim();
        if (t.StartsWith('|')) t = t[1..];
        if (t.EndsWith('|') && !t.EndsWith("\\|")) t = t[..^1];
        var cells = new List<string>();
        int start = 0;
        for (int i = 0; i < t.Length; i++)
        {
            if (t[i] == '\\') { i++; continue; }
            if (t[i] != '|') continue;
            cells.Add(t[start..i].Trim());
            start = i + 1;
        }
        cells.Add(t[start..].Trim());
        return cells;
    }

    /// <summary>The cell a caret at <paramref name="at"/> in a table's text is in: its row (line) and column.</summary>
    public static (int Row, int Col) CellAt(string table, int at)
    {
        at = Math.Clamp(at, 0, table.Length);
        int lineStart = Text.LineStart(table, at);
        int row = table[..lineStart].Count(ch => ch == '\n');
        string before = table[lineStart..at].TrimStart();
        int col = Pipes(before) - (before.StartsWith('|') ? 1 : 0);
        return (row, Math.Max(0, col));
    }

    /// <summary>Where the text of cell (<paramref name="row"/>, <paramref name="col"/>) begins in an aligned table.</summary>
    public static int CellStart(string table, int row, int col)
    {
        int at = 0;
        for (int r = 0; r < row; r++) at = table.IndexOf('\n', at) + 1;
        int seen = 0;
        for (int i = at; i < table.Length && table[i] != '\n'; i++)
        {
            if (table[i] == '\\') { i++; continue; }
            if (table[i] == '|' && seen++ == col) return Math.Min(i + 2, table.Length);
        }
        return at;
    }

    private static int Pipes(string s)
    {
        int n = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\') { i++; continue; }
            if (s[i] == '|') n++;
        }
        return n;
    }

    // What a cell takes up in a monospaced face: one column a character, as a person sees characters.
    private static int Width(string s) => new StringInfo(s).LengthInTextElements;
}

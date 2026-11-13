using System.Text;

namespace BibMd;

/// <summary>One version of the file: its text, when it was made, and what changed from the one before.</summary>
public readonly record struct Version(string Text, DateTime At, string Summary, int Added, int Removed);

/// <summary>
/// The file: its text, its blocks, and every version it has had this session.
/// Each change is one step in the history; undo and redo walk it, and every
/// step is written to disk at once.
/// </summary>
public sealed class Document
{
    private const int MaxHistory = 500;

    private readonly List<Version> _history = [];
    private int _at;

    /// <summary>Every version this session, oldest first; <see cref="At"/> is the one shown.</summary>
    public IReadOnlyList<Version> History => _history;
    public int At => _at;

    public string Path { get; }
    public string Text { get; private set; }
    public List<Block> Blocks { get; private set; }
    public string? SaveError { get; private set; }
    public DateTime SavedAt { get; private set; }
    private readonly bool _crlf;

    public Document(string path)
    {
        Path = System.IO.Path.GetFullPath(path);
        string raw = File.Exists(Path) ? File.ReadAllText(Path) : "";
        _crlf = raw.Contains("\r\n");
        Text = raw.Replace("\r\n", "\n");
        Blocks = BibMd.Blocks.Split(Text);
        _history.Add(new(Text, DateTime.Now, "Opened", 0, 0));
    }

    public string Source(Block b) => Text[b.Start..b.End];

    public bool CanUndo => _at > 0;
    public bool CanRedo => _at < _history.Count - 1;

    /// <summary>The block's source replaced with <paramref name="replacement"/>, as a new step.</summary>
    public void Replace(int start, int end, string replacement) => Commit(Splice(start, end, replacement));

    /// <summary>The text with [start, end) replaced, tidying the blank lines round a block that was emptied.</summary>
    public string Splice(int start, int end, string replacement)
    {
        replacement = replacement.Replace("\r\n", "\n");
        if (replacement.Trim().Length == 0)
        {
            // An emptied block takes the blank lines after it, or before it at the end of the file.
            while (end < Text.Length && Text[end] == '\n') end++;
            if (end == Text.Length)
            {
                while (start > 0 && Text[start - 1] == '\n') start--;
                return start == 0 ? "" : Text[..start] + "\n";
            }
            replacement = "";
        }
        return Text[..start] + replacement + Text[end..];
    }

    public void Commit(string text)
    {
        if (text == Text) return;
        _history.RemoveRange(_at + 1, _history.Count - _at - 1);
        _history.Add(Describe(Text, text));
        if (_history.Count > MaxHistory) _history.RemoveAt(0);
        _at = _history.Count - 1;
        Set(text);
    }

    public bool Undo()
    {
        if (!CanUndo) return false;
        Set(_history[--_at].Text);
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo) return false;
        Set(_history[++_at].Text);
        return true;
    }

    /// <summary>Back (or forward) to version <paramref name="index"/>; the versions after it stay, for redo.</summary>
    public bool GoTo(int index)
    {
        if (index < 0 || index >= _history.Count || index == _at) return false;
        _at = index;
        Set(_history[index].Text);
        return true;
    }

    // What a step changed, in a few words: the line it changed, and how much went in and out.
    private static Version Describe(string before, string after)
    {
        int p = 0, max = Math.Min(before.Length, after.Length);
        while (p < max && before[p] == after[p]) p++;
        int s = 0;
        while (s < max - p && before[^(s + 1)] == after[^(s + 1)]) s++;
        int removed = before.Length - p - s, added = after.Length - p - s;
        string verb = removed == 0 ? "Added" : added == 0 ? "Deleted" : "Edited";
        // The line it happened on, in the version that still has it; past blank lines to the next with words.
        string src = added > 0 ? after : before;
        int start = p == 0 ? 0 : src.LastIndexOf('\n', p - 1) + 1;
        string line = "";
        while (start < src.Length && line.Length == 0)
        {
            int end = src.IndexOf('\n', start);
            if (end < 0) end = src.Length;
            line = BibMd.Blocks.Plain(src[start..end]);
            start = end + 1;
        }
        return new(after, DateTime.Now, line.Length == 0 ? verb : verb + " \u201c" + line + "\u201d", added, removed);
    }

    private void Set(string text)
    {
        Text = text;
        Blocks = BibMd.Blocks.Split(Text);
        Save(Text);
    }

    /// <summary>Writes the text atomically: a temporary file beside it, then a rename.</summary>
    public void Save(string text)
    {
        try
        {
            string tmp = Path + ".bibmd~";
            string data = _crlf ? text.Replace("\n", "\r\n") : text;
            File.WriteAllText(tmp, data, new UTF8Encoding(false));
            File.Move(tmp, Path, overwrite: true);
            SaveError = null;
            SavedAt = DateTime.Now;
        }
        catch (Exception e)
        {
            SaveError = e.Message;
        }
    }
}

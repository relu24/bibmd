using System.Text.RegularExpressions;
using Bibui.Core;
using Bibui.Core.Runtime;
using Bibui.Core.TextEdit;

namespace BibMd;

/// <summary>
/// The insert menu: a short list of the blocks there are — headings, a table,
/// an image, code, a quote, lists, a rule — to put in without remembering
/// their Markdown. Opened with / (typed on the page, or into an empty block) or
/// with the + in the status line. Typing filters it; "table 4x3" (or just
/// "4x3") makes a table of that size. The block goes in after the selected
/// one (or at the end), opened for typing with the caret where the words go.
///
/// A right-click on a block opens it as the block menu: copy, cut, duplicate,
/// move and delete the block, or turn it into another kind. A quote becomes a
/// code block, a paragraph a heading, lines a list, keeping their words.
/// </summary>
public sealed partial class App
{
    private const float PaletteWidth = 380f;
    private const char Caret = '§';             // where the caret goes in a snippet

    private readonly record struct Snippet(string Name, string Hint, string Template);

    private static readonly Snippet[] Snippets =
    [
        new("Heading 1", "#", "# §"),
        new("Heading 2", "##", "## §"),
        new("Heading 3", "###", "### §"),
        new("Table 2 × 2", "| |", TableSnippet(2, 2)),
        new("Table 3 × 3", "| |", TableSnippet(3, 3)),
        new("Image", "![]()", "![](§)"),
        new("Code block", "```", "```§\n\n```"),
        new("Quote", ">", "> §"),
        new("Bullet list", "-", "- §"),
        new("Numbered list", "1.", "1. §"),
        new("Task list", "- [ ]", "- [ ] §"),
        new("Note", "> **Note:**", "> **Note:** §"),
        new("Link", "[]()", "[§](https://)"),
        new("Divider", "---", "---"),
    ];

    private bool _inserting;
    private bool _insertInBlock;                // opened by / in an empty block: the snippet fills that block
    private string _query = "";
    private int _pick;
    private bool _swallowText;
    private int _turning = -1;                  // the block the menu acts on, or -1: it inserts

    // What the block menu does to the block, above the kinds it can turn into.
    private static readonly Snippet[] BlockActions =
    [
        new("Copy", "Ctrl+C", ""),
        new("Cut", "Ctrl+X", ""),
        new("Duplicate", "Ctrl+D", ""),
        new("Move up", "Alt+↑", ""),
        new("Move down", "Alt+↓", ""),
        new("Delete", "Del", ""),
    ];

    [GeneratedRegex(@"^\s*(t(a(b(le?)?)?)?)?\s*(?<c>\d{1,2})\s*[x×*]\s*(?<r>\d{1,3})\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex TableSize();

    /// <summary>An empty table of <paramref name="cols"/> columns and <paramref name="rows"/> rows, the header among them; the caret in its first cell.</summary>
    private static string TableSnippet(int cols, int rows)
    {
        string row = "|" + string.Concat(Enumerable.Repeat("  |", cols));
        string delimiter = "|" + string.Concat(Enumerable.Repeat("---|", cols));
        var lines = new List<string> { row, delimiter };
        for (int r = 1; r < Math.Max(2, rows); r++) lines.Add(row);
        string table = Tables.Align(string.Join('\n', lines));
        return table.Insert(Tables.CellStart(table, 0, 0), Caret.ToString());
    }

    private List<Snippet> Matches()
    {
        var list = new List<Snippet>();
        string q = _query.Trim();
        if (_turning >= 0)
        {
            var b = doc.Blocks[_turning];
            foreach (var act in BlockActions)
                if (q.Length == 0 || act.Name.Contains(q, StringComparison.OrdinalIgnoreCase)) list.Add(act);
            string? now = Turn.KindOf(b, doc.Source(b));
            foreach (var kind in Turn.Kinds)
                if (kind != now && (q.Length == 0 || ("Turn into " + kind).Contains(q, StringComparison.OrdinalIgnoreCase)))
                    list.Add(new("Turn into " + kind, Snippets.FirstOrDefault(s => s.Name == kind).Hint ?? Hint(kind), ""));
            return list;
        }
        if (TableSize().Match(q) is { Success: true } m)
        {
            int c = Math.Clamp(int.Parse(m.Groups["c"].Value), 1, 20), r = Math.Clamp(int.Parse(m.Groups["r"].Value), 2, 200);
            list.Add(new($"Table {c} × {r}", "| |", TableSnippet(c, r)));
        }
        foreach (var s in Snippets)
            if (q.Length == 0 || s.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                || s.Hint.StartsWith(q, StringComparison.Ordinal))
                list.Add(s);
        return list;
    }

    private static string Hint(string kind) => kind switch
    {
        "Paragraph" => "¶",
        "Code block" => "```",
        _ => "",
    };

    /// <summary>The menu as the block menu, for <paramref name="block"/>.</summary>
    private void OpenTurn(int block)
    {
        if (block < 0 || block >= doc.Blocks.Count) return;
        OpenInsert(inBlock: false);
        if (block >= doc.Blocks.Count) return;   // putting back the edited block took it away
        _turning = block;
        _sel = block;
        _showSel = true;
    }

    private void TurnInto(int block, string kind)
    {
        var b = doc.Blocks[block];
        doc.Replace(b.Start, b.End, Turn.To(kind, b.Kind, doc.Source(b)));
        _sel = Math.Clamp(block, 0, Math.Max(0, doc.Blocks.Count - 1));
        _showSel = true;
        _reveal = true;
        Flash("Turned into " + kind.ToLowerInvariant());
    }

    private void BlockAction(int block, string name)
    {
        if (_locked && name != "Copy") { Flash(LockedNote); return; }
        switch (name)
        {
            case "Copy": CopyBlock(block); break;
            case "Cut": CopyBlock(block, quiet: true); DeleteBlock(block, "Cut block"); break;
            case "Duplicate": DuplicateBlock(block); break;
            case "Move up": MoveBlock(block, -1); break;
            case "Move down": MoveBlock(block, 1); break;
            case "Delete": DeleteBlock(block); break;
            default: TurnInto(block, name["Turn into ".Length..]); break;
        }
    }

    private void OpenInsert(bool inBlock)
    {
        if (!inBlock && Editing) FinishEdit();
        _inserting = true;
        _turning = -1;
        _insertInBlock = inBlock;
        _query = "";
        _pick = 0;
    }

    // Closed without a choice: from an empty block, what was typed goes into it, the / too.
    private void CancelInsert()
    {
        _inserting = false;
        if (_insertInBlock && Editing) Edit("/" + _query, 1 + _query.Length);
    }

    private void Choose(Snippet s)
    {
        _inserting = false;
        if (_turning >= 0) { int block = _turning; _turning = -1; BlockAction(block, s.Name); return; }
        int caret = Math.Max(0, s.Template.IndexOf(Caret));
        string text = s.Template.Replace(Caret.ToString(), "");
        bool open = s.Template.Contains(Caret);
        if (_insertInBlock && Editing)
        {
            Edit(text, caret);
            if (!open) FinishEdit();
            return;
        }
        var blocks = doc.Blocks;
        int at = blocks.Count == 0 ? 0 : _showSel ? _sel + 1 : blocks.Count;
        if (!open)
        {
            doc.Commit(Insertion(at, text));
            Select(at);
            return;
        }
        StartEdit(at, isNew: true);
        _forceStep = true;
        SetBuffer(text, caret);
    }

    /// <summary>Every key goes to the menu while it is open.</summary>
    private void InsertMenuEvent(BibUiEvent ev)
    {
        ev.Eat();
        if (ev.Kind == BibUiEventKind.Text)
        {
            char ch = ev.Char ?? '\0';
            if (_swallowText) _swallowText = false;   // the letter of a Ctrl key
            else if (ch >= ' ') { _query += ch; _pick = 0; }
            return;
        }
        if (!ev.IsKeyDown) return;
        _swallowText = ev.Ctrl;
        int count = Matches().Count;
        switch (ev.Key)
        {
            case Key.Escape: CancelInsert(); break;
            case Key.Enter:
                var matches = Matches();
                if (matches.Count > 0) Choose(matches[Math.Clamp(_pick, 0, matches.Count - 1)]);
                break;
            case Key.Backspace:
                if (_query.Length > 0) { _query = _query[..^1]; _pick = 0; }
                else if (_insertInBlock) { _inserting = false; }   // the / taken back
                else CancelInsert();
                break;
            case Key.Down or Key.N when ev.Key == Key.Down || ev.Ctrl: _pick = count == 0 ? 0 : (_pick + 1) % count; break;
            case Key.Up or Key.P when ev.Key == Key.Up || ev.Ctrl: _pick = count == 0 ? 0 : (_pick - 1 + count) % count; break;
            case Key.Tab: _pick = count == 0 ? 0 : (_pick + (ev.Shift ? count - 1 : 1)) % count; break;
        }
    }

    private void InsertMenu()
    {
        float room = _page?.Extent.X ?? 960f;
        float x = (_sidebar ? SidebarWidth + 1f : 0f) + MathF.Max(8f, MathF.Floor((room - PaletteWidth) / 2f));
        // A dropdown, so it is over the page for the pointer as well as to the eye; opened in
        // every frame it is built, so only the menu's own keys and clicks close it.
        runtime.OpenDropDown("insert-menu");
        Box panel = runtime.BeginDropDown("insert-menu")!;
        panel.ChildLayoutAxis = LayoutAxis.Y;
        panel.Sized(Size.Px(PaletteWidth), Size.Fit);
        panel.At(x, 64f);
        panel.Style.Padding = EdgeInsets.All(6f);
        panel.Style.CornerRadius = 10f;
        panel.Style.BackgroundColor = UiColor.Surface;
        panel.Style.BorderColor = UiColor.BorderHover;
        panel.Style.BorderWidth = 1f;
        runtime.BeginParent(panel);

        var head = runtime.BeginParent(runtime.Row().Sized(Size.Fill, Size.Fit));
        head.Style.Padding = new EdgeInsets(6f, 10f, 8f, 10f);
        runtime.Text(_turning >= 0 ? "Block" : "Insert", ComponentSize.Sm, UiColor.TextDisabled);
        runtime.Gap(10f);
        runtime.Text(_query, ComponentSize.Sm, UiColor.TextPrimary, FontSlot.Mono);
        runtime.Gap(1f);
        runtime.Box().Sized(Size.Px(1.5f), Size.Px(15f)).Style.BackgroundColor = UiColor.Accent;   // the caret
        runtime.EndParent(head);

        var matches = Matches();
        _pick = Math.Clamp(_pick, 0, Math.Max(0, matches.Count - 1));
        if (matches.Count == 0) Note("Nothing called that. Try \"table 4x3\".");
        for (int i = 0; i < matches.Count; i++)
        {
            var s = matches[i];
            var item = BeginItem("insert-" + i, i == _pick, axis: LayoutAxis.X);
            runtime.Text(s.Name, ComponentSize.Sm, i == _pick ? UiColor.TextPrimary : UiColor.TextSecondary);
            runtime.Spacer(Size.Fill);
            runtime.Text(s.Hint, ComponentSize.Sm, UiColor.TextDisabled, FontSlot.Mono);
            if (EndItem(item)) { var chosen = s; _sideAction = () => Choose(chosen); }
        }
        runtime.EndParent(panel);
    }
}

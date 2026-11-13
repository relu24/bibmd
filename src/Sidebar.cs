using Bibui.Core;
using Bibui.Core.Runtime;
using Bibui.Core.TextEdit;

namespace BibMd;

public enum Panel { Contents, History, Keys }

/// <summary>
/// The menu on the left, hidden until it is asked for (Ctrl+B, Tab, ?, or the
/// icon in the status line): the document's headings, every version of it this
/// session, or the keys. A heading or a version clicked is gone to.
///
/// Tab puts the keys in it: j and k (or the arrows) walk the list, Enter
/// takes what is under the cursor, Esc goes back to the page. Walking the
/// contents scrolls the page along with it; a version is only gone to on Enter.
/// </summary>
public sealed partial class App
{
    private const float SidebarWidth = 300f, KeysColumn = 116f;
    private static readonly Panel[] Panels = [Panel.Contents, Panel.History, Panel.Keys];

    private bool _sidebar;
    private Panel _panel = Panel.Contents;
    private Action? _sideAction;                // what was clicked in it, done once the frame is built
    private bool _sideFocus;                    // the keys walk the menu, not the page
    private int _sideCursor;                    // contents: the heading's block; history: the version
    private bool _sideReveal;                   // scroll the list to the cursor
    private Box? _sideInner;
    private (float Top, float Bottom) _cursorRect;

    private static readonly (string Keys, string What)[] PageKeys =
    [
        ("click", "edit that block"),
        ("click below", "add a block at the end"),
        ("right-click", "block menu"),
        ("/  +", "insert a heading, table, list…"),
        ("↓ ↑", "select next / previous"),
        ("Enter", "edit the selected block"),
        ("Alt+↑ Alt+↓", "move it up / down"),
        ("Ctrl+C Ctrl+X", "copy / cut it"),
        ("Ctrl+V", "paste after it"),
        ("Ctrl+D", "duplicate it"),
        ("Delete", "delete it"),
        ("Esc", "clear the selection"),
        ("Space PageDown", "scroll"),
        ("Home End", "first / last block"),
    ];

    private static readonly (string Keys, string What)[] EditKeys =
    [
        ("/", "insert (in an empty block)"),
        ("Tab Shift+Tab", "indent / outdent"),
        ("Tab in a table", "align, next cell"),
        ("Esc  Ctrl+Enter", "done"),
    ];

    private static readonly (string Keys, string What)[] AppKeys =
    [
        ("Ctrl+Z Ctrl+Y", "undo / redo"),
        ("Ctrl+S", "save (it saves by itself too)"),
        ("Ctrl+B", "show / hide this menu"),
        ("Ctrl+E", "Markdown source view"),
        ("Ctrl+L", "lock / unlock"),
        ("Ctrl+T", "theme: light / dark"),
        ("Ctrl+= Ctrl+-", "text size"),
        ("F1  Ctrl+/", "these keys"),
        ("Tab", "into the menu, next panel"),
        ("Ctrl+Q", "quit"),
    ];

    private void ToggleSidebar()
    {
        _sidebar = !_sidebar;
        _sideFocus = false;
    }

    /// <summary>Shows <paramref name="panel"/>; asked for again while it shows, hides the menu.</summary>
    private void ShowPanel(Panel panel)
    {
        _sidebar = !(_sidebar && _panel == panel);
        if (!_sidebar) _sideFocus = false;
        SetPanel(panel);
    }

    // Tab: into the menu, on the panel it shows; once in, the next panel.
    private void CyclePanel(int by)
    {
        var panel = _sidebar && _sideFocus ? Panels[(Array.IndexOf(Panels, _panel) + by + Panels.Length) % Panels.Length] : _panel;
        _sidebar = _sideFocus = true;
        SetPanel(panel);
    }

    // A panel shown: its cursor on where the page is, the heading being read or the version shown.
    private void SetPanel(Panel panel)
    {
        _panel = panel;
        _sideCursor = panel == Panel.History ? doc.At : Math.Max(0, CurrentHeading());
        _sideReveal = true;
    }

    // ── Keys, while the menu has them ────────────────────────────────────────

    private bool SideKey(BibUiEvent ev)
    {
        switch (ev.Key)
        {
            case Key.Escape: _sideFocus = false; return true;
            case Key.Tab: CyclePanel(ev.Shift ? -1 : 1); return true;
            case Key.Down: SideStep(1); return true;
            case Key.Up: SideStep(-1); return true;
            case Key.Home: SideEnd(first: true); return true;
            case Key.End: SideEnd(first: false); return true;
            case Key.Enter: SideTake(); return true;
        }
        return false;
    }

    // The headings in order, as blocks; a kicker (a smaller heading right over a bigger one) is part of the one below.
    private List<int> Headings()
    {
        var list = new List<int>();
        for (int i = 0; i < doc.Blocks.Count; i++)
            if (doc.Blocks[i].Kind == BlockKind.Heading && !IsKicker(i)) list.Add(i);
        return list;
    }

    // "###### Step one" over "## The dough": an eyebrow on the heading, not a heading of its own.
    private bool IsKicker(int i) =>
        i + 1 < doc.Blocks.Count && doc.Blocks[i + 1].Kind == BlockKind.Heading
        && Level(i) > Level(i + 1);

    private int Level(int i) => Blocks.HeadingOf(doc.Source(doc.Blocks[i])).Level;

    // Where a heading's part starts: at its kicker, when it has one.
    private int PartStart(int h) => h > 0 && doc.Blocks[h - 1].Kind == BlockKind.Heading && IsKicker(h - 1) ? h - 1 : h;

    // The heading of the part being read: the last one at or above the selection, the editor, or the top of the view.
    private int CurrentHeading()
    {
        int at = Editing ? _editing : _showSel ? _sel : FirstVisibleBlock();
        int current = -1;
        foreach (int h in Headings()) if (PartStart(h) <= at) current = h;
        return current;
    }

    private void SideStep(int by)
    {
        _sideReveal = true;
        if (_panel == Panel.History)
        {
            // The list is newest first: down is older.
            _sideCursor = Math.Clamp(_sideCursor - by, 0, doc.History.Count - 1);
            return;
        }
        if (_panel == Panel.Keys) { ScrollSide(by * 60f); return; }
        var heads = Headings();
        if (heads.Count == 0) return;
        int i = heads.IndexOf(_sideCursor);
        i = i < 0 ? (by > 0 ? heads.FindIndex(h => h > _sideCursor) : heads.FindLastIndex(h => h < _sideCursor)) : i + by;
        _sideCursor = heads[Math.Clamp(i < 0 && by > 0 ? heads.Count - 1 : i, 0, heads.Count - 1)];
        JumpTo(PartStart(_sideCursor));   // the page follows the cursor
    }

    private void SideEnd(bool first)
    {
        _sideReveal = true;
        if (_panel == Panel.History) { _sideCursor = first ? doc.History.Count - 1 : 0; return; }
        if (_panel == Panel.Keys) { ScrollSide(first ? -1e6f : 1e6f); return; }
        var heads = Headings();
        if (heads.Count == 0) return;
        _sideCursor = first ? heads[0] : heads[^1];
        JumpTo(PartStart(_sideCursor));
    }

    // Enter: a heading is gone to and the keys go back to the page; a version is gone to and they stay, to try another.
    private void SideTake()
    {
        if (_panel == Panel.Contents)
        {
            if (Headings().Contains(_sideCursor)) JumpTo(PartStart(_sideCursor));
            _sideFocus = false;
        }
        else if (_panel == Panel.History) GoToVersion(_sideCursor);
    }

    private void ScrollSide(float by)
    {
        if (runtime.FindById("side-" + _panel) is not Box list) return;
        float max = MathF.Max(0f, list.ContentSize.Y - list.Extent.Y);
        list.TargetViewOffset = list.TargetViewOffset with { Y = Math.Clamp(list.TargetViewOffset.Y + by, 0f, max) };
    }

    // The cursor's row where it was drawn last frame, for scrolling it into view.
    private void RecordCursor(Box item)
    {
        if (_sideInner is null || item.Extent.Y <= 0f) return;
        float top = item.Rect.Y - _sideInner.Rect.Y;
        _cursorRect = (top, top + item.Extent.Y);
    }

    private void RevealCursor(Box list)
    {
        if (!_sideReveal || _cursorRect.Bottom <= 0f || list.Extent.Y <= 0f) return;
        _sideReveal = false;
        const float Margin = 24f;
        float at = list.TargetViewOffset.Y;
        if (_cursorRect.Bottom > at + list.Extent.Y - Margin) at = _cursorRect.Bottom - list.Extent.Y + Margin;
        if (_cursorRect.Top < at + Margin) at = _cursorRect.Top - Margin;
        list.TargetViewOffset = list.TargetViewOffset with { Y = MathF.Max(0f, at) };
    }

    private void Sidebar()
    {
        Box side = runtime.BoxFromId("sidebar", InteractionKinds.Clip);   // nothing drawn past its edge
        side.ChildLayoutAxis = LayoutAxis.Y;
        side.Sized(Size.Px(SidebarWidth), Size.Fill);
        runtime.BeginParent(side);
        side.Style.BackgroundColor = UiColor.Surface;

        var tabs = runtime.BeginParent(runtime.Row().Sized(Size.Fill, Size.Fit));
        tabs.Style.Padding = new EdgeInsets(10f, 8f, 6f, 8f);
        foreach (var p in Panels)
        {
            var tab = BeginItem("tab-" + p, p == _panel, axis: LayoutAxis.X, fill: false);
            runtime.Text(p switch { Panel.Contents => "Contents", Panel.History => "History", _ => "Keys" },
                ComponentSize.Sm, p != _panel ? UiColor.TextSecondary : _sideFocus ? UiColor.Accent : UiColor.TextPrimary);
            if (EndItem(tab)) SetPanel(p);
            runtime.Gap(2f);
        }
        runtime.Spacer(Size.Fill);
        var close = BeginItem("side-close", false, axis: LayoutAxis.X, fill: false);
        close.Style.Padding = EdgeInsets.Symmetric(horizontal: 7f, vertical: 7f);
        runtime.Icon("x", 14f).Color = UiColor.TextSecondary;
        if (EndItem(close)) ToggleSidebar();
        runtime.EndParent(tabs);

        Box list = runtime.BoxFromId("side-" + _panel,
            InteractionKinds.ViewScrollY | InteractionKinds.ClampY | InteractionKinds.Clip);
        list.ChildLayoutAxis = LayoutAxis.Y;
        list.Sized(Size.Fill, Size.Fill);
        list.AllowOverflowY = true;
        runtime.BeginParent(list);
        var inner = runtime.BeginParent(runtime.Column().Sized(Size.Fill, Size.Fit));
        inner.Style.Padding = new EdgeInsets(4f, 8f, 24f, 8f);
        _sideInner = inner;
        switch (_panel)
        {
            case Panel.Contents: Contents(); break;
            case Panel.History: History(); break;
            default: Keys(); break;
        }
        runtime.EndParent(inner);
        runtime.EndParent(list);
        runtime.EndParent(side);
        RevealCursor(list);
        runtime.InteractionsFor(list);   // the wheel scrolls a box only when it asks

        runtime.Box().Sized(Size.Px(1f), Size.Fill).Style.BackgroundColor = UiColor.Border;
    }

    // ── Contents ─────────────────────────────────────────────────────────────

    private void Contents()
    {
        var heads = Headings();
        if (heads.Count == 0)
        {
            Note("No headings yet. Lines that start with # show here.");
            return;
        }
        int current = CurrentHeading();
        // Indented by rank among the levels in use, so a # over a ### is one step, not two.
        var levels = heads.Select(Level).Distinct().Order().ToList();
        int top = levels[0];
        foreach (int i in heads)
        {
            var (level, text) = Blocks.HeadingOf(doc.Source(doc.Blocks[i]));
            bool cursor = _sideFocus && i == _sideCursor;
            var item = BeginItem("toc-" + i, i == current, indent: 14f * levels.IndexOf(level), cursor: cursor);
            if (cursor) RecordCursor(item);
            runtime.Text(text.Length == 0 ? "(untitled)" : text, ComponentSize.Sm,
                i == current ? UiColor.Accent : level == top ? UiColor.TextPrimary : UiColor.TextSecondary,
                weight: level == top ? FontWeight.Bold : FontWeight.Regular);
            int target = PartStart(i);
            if (EndItem(item)) _sideAction = () => JumpTo(target);
        }
    }

    // A heading picked from the contents: selected, and scrolled to the top of the page.
    private void JumpTo(int block)
    {
        block = FinishEditKeeping(block);
        if (doc.Blocks.Count == 0) return;
        _sel = Math.Clamp(block, 0, doc.Blocks.Count - 1);
        _showSel = true;
        _reveal = _revealTop = true;
    }

    // ── History ──────────────────────────────────────────────────────────────

    private void History()
    {
        var history = doc.History;
        for (int i = history.Count - 1; i >= 0; i--)
        {
            var v = history[i];
            bool current = i == doc.At, undone = i > doc.At;
            bool cursor = _sideFocus && i == _sideCursor;
            var item = BeginItem("hist-" + i, current, cursor: cursor);
            if (cursor) RecordCursor(item);
            runtime.Text(v.Summary, ComponentSize.Sm,
                current ? UiColor.Accent : undone ? UiColor.TextDisabled : UiColor.TextPrimary);
            runtime.Gap(2f);
            string when = v.At.ToString("HH:mm:ss");
            if (v.Added > 0 || v.Removed > 0) when += $"  +{v.Added} −{v.Removed}";
            if (current) when += "  · now";
            else if (undone) when += "  · undone";
            runtime.Text(when, ComponentSize.Sm, UiColor.TextDisabled, FontSlot.Mono);
            int target = i;
            if (EndItem(item)) _sideAction = () => GoToVersion(target);
        }
    }

    private void GoToVersion(int index)
    {
        int count = doc.History.Count;
        if (Editing) FinishEdit();
        if (doc.History.Count != count) return;   // the block put back was a version of its own, and the ones after are gone
        if (doc.GoTo(index)) Flash(index == 0 ? "Back to the file as opened" : "Version " + index);
        _reveal = true;
    }

    // ── Keys ─────────────────────────────────────────────────────────────────

    private void Keys()
    {
        // The keys for what is open first.
        var groups = Editing
            ? new[] { ("EDITING", EditKeys), ("EVERYWHERE", AppKeys), ("PAGE", PageKeys) }
            : [("PAGE", PageKeys), ("EVERYWHERE", AppKeys), ("EDITING", EditKeys)];
        bool first = true;
        foreach (var (title, keys) in groups)
        {
            runtime.Gap(first ? 6f : 18f);
            var head = runtime.BeginParent(runtime.Row().Sized(Size.Fill, Size.Fit));
            head.Style.Padding = new EdgeInsets(0f, 10f, 6f, 10f);
            runtime.Text(title, ComponentSize.Sm, first ? UiColor.Accent : UiColor.TextSecondary,
                FontSlot.Mono, FontWeight.Bold);
            runtime.EndParent(head);
            foreach (var (k, what) in keys)
            {
                var row = runtime.BeginParent(runtime.Row().Sized(Size.Fill, Size.Fit));
                row.Style.Padding = new EdgeInsets(3f, 10f, 3f, 10f);
                var cell = runtime.BeginParent(runtime.Row().Sized(Size.Px(KeysColumn), Size.Fit));
                runtime.Text(k, ComponentSize.Sm, UiColor.TextPrimary, FontSlot.Mono);
                runtime.EndParent(cell);
                runtime.Text(what, ComponentSize.Sm, UiColor.TextSecondary);
                runtime.EndParent(row);
            }
            first = false;
        }
    }

    // ── Pieces ───────────────────────────────────────────────────────────────

    private void Note(string text)
    {
        var box = runtime.BeginParent(runtime.Column().Sized(Size.Fill, Size.Fit));
        box.Style.Padding = EdgeInsets.Symmetric(horizontal: 10f, vertical: 8f);
        runtime.Text(text, ComponentSize.Sm, UiColor.TextDisabled);
        runtime.EndParent(box);
    }

    // Something in the menu to click: lit while the pointer is on it, and when it is the current one.
    private Box BeginItem(string id, bool current, float indent = 0f, LayoutAxis axis = LayoutAxis.Y, bool fill = true, bool cursor = false)
    {
        Box item = runtime.BoxFromId(id, InteractionKinds.LeftPress | InteractionKinds.Clip);
        item.ChildLayoutAxis = axis;
        item.Sized(fill ? Size.Fill : Size.Fit, Size.Fit);
        item.Style.Padding = new EdgeInsets(5f, 10f, 5f, 10f + indent);
        item.Style.CornerRadius = 6f;
        item.Style.BackgroundColor = UiColor.SurfaceRaised.WithAlpha(MathF.Max(current ? 0.9f : 0f, 0.6f * item.HotT));
        // The keyboard's cursor: outlined, as the page's selection is.
        item.Style.BorderColor = cursor ? UiColor.Accent.WithAlpha(0.6f) : UiColor.Border.WithAlpha(0f);
        item.Style.BorderWidth = cursor ? 1f : 0f;
        runtime.BeginParent(item);
        return item;
    }

    private bool EndItem(Box item)
    {
        runtime.EndParent(item);
        return runtime.InteractionsFor(item).Clicked;
    }
}

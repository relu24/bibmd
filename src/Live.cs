using Bibui.Core;
using Bibui.Core.Runtime;

namespace BibMd;

/// <summary>Someone else in a shared document: the block they hold (a range of the text), and what they are typing in it.</summary>
public sealed record Remote(string Name, string Color, int Start, int End, bool IsNew, string? Typing, int Caret);

/// <summary>
/// A shared document (hosted bibmd): others' changes arrive as splices of the text, applied without a
/// step of the history; a block another person has open is theirs until they leave it, drawn with their
/// name and, as they type, their text. The whole-document undo would undo the others too: it is off.
/// </summary>
public sealed partial class App
{
    private List<Remote> _remotes = [];

    /// <summary>The document is shared live: others' changes arrive through <see cref="ApplyRemote"/>.</summary>
    public bool Live { get; set; }

    /// <summary>The block open for editing, as a range of the text; null in READ.</summary>
    public (int Start, int End, bool IsNew)? EditRange => Editing ? (_editStart, _editEnd, _isNew) : null;

    public void SetRemotes(List<Remote> remotes) => _remotes = remotes;

    /// <summary>Someone else's change: the text [start, end) becomes <paramref name="text"/>; what is open and selected stays put.</summary>
    public void ApplyRemote(int start, int end, string text)
    {
        int delta = text.Length - (end - start);
        int selAt = doc.Blocks.Count > 0 ? doc.Blocks[Math.Clamp(_sel, 0, doc.Blocks.Count - 1)].Start : 0;
        int Map(int p) => p < start ? p : p >= end ? p + delta : start;
        if (Editing)
        {
            if (end <= _editStart && !(start == end && end == _editStart && _isNew)) { _editStart += delta; _editEnd += delta; }
            else if (start < _editEnd && end > _editStart) Flash("Someone else changed the block you are in");
        }
        doc.ApplyRemote(start, end, text);
        var blocks = doc.Blocks;
        if (Editing)
        {
            _editStart = Math.Clamp(_editStart, 0, doc.Text.Length);
            _editEnd = Math.Clamp(_editEnd, _editStart, doc.Text.Length);
            int i = 0;
            while (i < blocks.Count && blocks[i].Start < _editStart) i++;
            _editing = i;
        }
        int sel = Map(selAt), j = 0;
        while (j < blocks.Count - 1 && blocks[j + 1].Start <= sel) j++;
        _sel = j;
    }

    /// <summary>The block was held by someone else after all (they opened it first): it closes, unchanged.</summary>
    public void DropEdit(string who)
    {
        if (!Editing) return;
        _editing = -1;
        _isNew = false;
        _unsaved = false;
        Mode = Mode.Read;
        Flash(who + " is editing this block");
    }

    // Who holds block i, if anyone else does.
    private Remote? HeldBy(int i)
    {
        if (_remotes.Count == 0 || i < 0 || i >= doc.Blocks.Count) return null;
        var b = doc.Blocks[i];
        foreach (var r in _remotes)
            if (!r.IsNew && r.Start < b.End && r.End > b.Start || !r.IsNew && r.Start == b.Start) return r;
        return null;
    }

    // Someone else's new block being written just before block i (or at the end, i = Count).
    private Remote? WritingBefore(int i)
    {
        int at = i < doc.Blocks.Count ? doc.Blocks[i].Start : doc.Text.Length;
        int from = i > 0 ? doc.Blocks[i - 1].End : 0;
        foreach (var r in _remotes)
            if (r.IsNew && r.Start >= from && r.Start <= at && (i == doc.Blocks.Count || r.Start < doc.Blocks[i].End)) return r;
        return null;
    }

    // Their name on the block they hold, in their colour.
    private void HolderTag(Remote r)
    {
        var tag = runtime.BeginParent(runtime.Row());
        tag.Style.Padding = EdgeInsets.Symmetric(horizontal: 6f, vertical: 1f);
        tag.Style.CornerRadius = 4f;
        tag.Style.BackgroundColor = UiColor.Hex(r.Color);
        runtime.Text(r.Name, ComponentSize.Sm, UiColor.Hex("#ffffff"));
        runtime.EndParent(tag);
        runtime.Gap(4f);
    }

    // What they are typing, as its Markdown with their caret: the block as they see it.
    private void RemoteTyping(Remote r, float width)
    {
        string t = r.Typing ?? "";
        int caret = Math.Clamp(r.Caret, 0, t.Length);
        SourceView(t[..caret] + "|" + t[caret..], width);
    }

    // A block someone else has open: outlined in their colour, their name on it, their typing in it.
    private Box HeldView(int i, Remote r, float column)
    {
        Box wrap = runtime.BoxFromId("block-" + i, InteractionKinds.LeftPress);
        wrap.ChildLayoutAxis = LayoutAxis.Y;
        wrap.Sized(Size.Fill, Size.Fit);
        wrap.Style.Padding = EdgeInsets.Symmetric(horizontal: PadX, vertical: PadY);
        wrap.Style.CornerRadius = 8f;
        wrap.Style.BorderColor = UiColor.Hex(r.Color);
        wrap.Style.BorderWidth = 1.5f;
        runtime.BeginParent(wrap);
        HolderTag(r);
        if (r.Typing is not null) RemoteTyping(r, column - 2f * PadX);
        else SourceView(doc.Source(doc.Blocks[i]), column - 2f * PadX);
        runtime.EndParent(wrap);
        if (runtime.InteractionsFor(wrap).Clicked) Flash(r.Name + " is editing this block");
        return wrap;
    }

    // Someone else's new block, as they write it, where it will go.
    private void NewView(Remote r, float column)
    {
        Box wrap = runtime.BoxFromId("new-" + r.Name + r.Start);
        wrap.ChildLayoutAxis = LayoutAxis.Y;
        wrap.Sized(Size.Fill, Size.Fit);
        wrap.Style.Padding = EdgeInsets.Symmetric(horizontal: PadX, vertical: PadY);
        wrap.Style.CornerRadius = 8f;
        wrap.Style.BorderColor = UiColor.Hex(r.Color);
        wrap.Style.BorderWidth = 1.5f;
        runtime.BeginParent(wrap);
        HolderTag(r);
        RemoteTyping(r, column - 2f * PadX);
        runtime.EndParent(wrap);
    }

    // In a shared document, typing reaches the text (and the others) after a pause, not only when the block is left.
    private bool LiveTyping()
    {
        if (!Live || !Editing || _isNew) return false;
        string now = _buffer.ToString();
        if (now.Trim().Length == 0 || now == doc.Text[_editStart.._editEnd]) return true;
        doc.ApplyRemote(_editStart, _editEnd, now);
        _editEnd = _editStart + now.Length;
        return true;
    }

    private const string LiveUndo = "Undo works in the block you are editing; a shared document doesn't step back whole";
}

using System.Text;
using System.Text.Json;
using Bibui.Core;
using Bibui.Core.Draw;
using Bibui.Core.Runtime;
using Bibui.Text;

namespace BibMd;

/// <summary>
/// The reader inside a page that draws everything but the document (the web viewer): <see cref="Bare"/>
/// leaves out the status line, the menu on the left, the section strip and the insert menu, and the page
/// draws them as its own from <see cref="HostState"/>, and asks for what they do through the methods
/// here. The keys still work as on the desktop: Ctrl+B opens the page's menu, / its insert menu.
/// </summary>
public sealed partial class App
{
    /// <summary>Only the document is drawn; the page draws the rest.</summary>
    public bool Bare { get; set; }

    /// <summary>A link's preview picture (<c>--card</c>): bare, and the front matter left out, so the page opens on its title.</summary>
    public bool Card { get; set; }

    // A card's corner: bibmd's mark as the site's header draws it (the favicon's b, light on dark, and the name
    // spaced out in the mono face), on a plate of the page's colour, so a link's preview says where it goes.
    private void CardMark(Box page)
    {
        const float glyph = 28f, pad = 10f, letters = 13f, track = 0.3f;
        const string name = "BIBMD";
        var font = runtime.FontInScope(FontSize.Px(letters), FontSlot.Mono);
        float advance = letters * 0.6f + letters * track;   // a mono face's letters are 0.6em wide
        float w = pad + glyph + 10f + advance * name.Length - letters * track + pad + 2f, h = glyph + 2f * pad;
        float x = page.Extent.X - w - 20f, y = page.Extent.Y - h - 18f;
        if (x <= 0f || y <= 0f) return;   // the first frame, laid out before the page knows its size
        runtime.Box().Sized(Size.Px(w), Size.Px(h)).At(x, y).Draw = (_, ctx) =>
        {
            float sc = ctx.Scale;
            Rgba ground = ctx.Resolve(UiColor.Background), ink = ctx.Resolve(UiColor.TextPrimary);
            ctx.DrawRect(new RectF(0f, 0f, w * sc, h * sc), 12f * sc, new Paint { Fill = ground.WithAlpha(235) });
            ctx.DrawRect(new RectF(0f, 0f, w * sc, h * sc), 12f * sc, new Paint { Stroke = ctx.Resolve(UiColor.Border), StrokeWidth = sc });
            // The favicon's b (32 units): a stem from 7 to 25 at 11.5, and a ring of 5 at (16.5, 19.5).
            float gx = pad * sc, gy = pad * sc, u = glyph / 32f * sc;
            ctx.DrawRect(new RectF(gx, gy, gx + glyph * sc, gy + glyph * sc), 8f * u, new Paint { Fill = ink });
            var stroke = new Paint { Stroke = ground, StrokeWidth = 3.4f * u, Cap = StrokeCap.Round };
            ctx.DrawLine(gx + 11.5f * u, gy + 7f * u, gx + 11.5f * u, gy + 25f * u, stroke);
            var ring = ctx.ScratchPath();
            ring.MoveTo(gx + 21.5f * u, gy + 19.5f * u);
            ring.ArcTo(5f * u, 5f * u, 0f, false, true, gx + 11.5f * u, gy + 19.5f * u);
            ring.ArcTo(5f * u, 5f * u, 0f, false, true, gx + 21.5f * u, gy + 19.5f * u);
            ctx.DrawPath(ring, stroke);
            // Its capitals (0.7em) centred on the plate; DrawText takes the ascent's top.
            float tx = gx + (glyph + 10f) * sc, top = (h / 2f + letters * 0.35f) * sc - MathF.Abs(ctx.FontMetrics(font).Ascent);
            for (int i = 0; i < name.Length; i++)
                ctx.DrawText(name.AsSpan(i, 1), tx + i * advance * sc, top, font, ink);
        };
    }

    private int _flashes;     // each flash counted, so the page shows the same words twice when said twice
    private int _panelFocus;  // Tab asked for the menu's keys: the page moves its focus there

    /// <summary>
    /// What the page's chrome shows, as JSON: small, read each frame and sent on when it changes.
    /// The outline and the versions are asked for apart (<see cref="OutlineJson"/>, <see cref="HistoryJson"/>)
    /// when <c>rev</c> says the text moved.
    /// </summary>
    public string HostState()
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteString("path", doc.Path);
            w.WriteBoolean("locked", _locked);
            w.WriteBoolean("source", _sourceView);
            w.WriteBoolean("dark", _dark);
            w.WriteNumber("size", _size);
            w.WriteNumber("sizes", Sizes.Length);
            w.WriteBoolean("editing", Editing);
            w.WriteBoolean("live", Live);
            w.WriteBoolean("panel", _sidebar);
            w.WriteString("tab", _panel.ToString().ToLowerInvariant());
            w.WriteNumber("panelFocus", _panelFocus);
            w.WriteNumber("current", CurrentHeading());
            w.WriteNumber("rev", doc.History.Count * 100003L + doc.At + doc.Text.Length * 7L);
            w.WriteNumber("versions", doc.History.Count);
            w.WriteNumber("at", doc.At);
            if (_flash is not null) { w.WriteString("flash", _flash); w.WriteNumber("flashes", _flashes); }
            if (doc.SaveError is { } err) w.WriteString("error", "Not saved: " + err);
            w.WriteStartArray("nav");
            foreach (var (block, label) in _nav)
            {
                w.WriteStartObject();
                w.WriteNumber("block", block);
                w.WriteString("label", label);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            if (_inserting)
            {
                w.WriteStartObject("menu");
                w.WriteBoolean("block", _turning >= 0);
                w.WriteBoolean("inBlock", _insertInBlock);
                w.WriteString("query", _query);
                // Where to show it: beside the block it acts on, or the editor, in the page's pixels.
                var (top, bottom) = _turning >= 0 && _turning < _rects.Count ? _rects[_turning]
                    : Editing ? _editorRect : _showSel && _sel < _rects.Count ? _rects[_sel] : (0f, 0f);
                if (_page is { } page && _content is not null && bottom > 0f)
                {
                    float y = bottom - page.ViewOffset.Y + page.Rect.Y;
                    w.WriteNumber("y", MathF.Round(Math.Clamp(y, 0f, page.Rect.Y + page.Extent.Y)));
                }
                w.WriteEndObject();
            }
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>
    /// A command from the page's chrome, by name: what the status line's buttons and the keys do.
    /// panel, contents, history, keys, close-panel, source, lock, theme, bigger, smaller, insert, undo, redo, save.
    /// </summary>
    public void Command(string name)
    {
        switch (name)
        {
            case "panel": ToggleSidebar(); break;
            case "contents": ShowPanel(Panel.Contents); break;
            case "history": ShowPanel(Panel.History); break;
            case "keys": ShowPanel(Panel.Keys); break;
            case "close-panel": _sidebar = _sideFocus = false; break;
            case "source": ToggleSourceView(); break;
            case "lock": ToggleLock(); break;
            case "theme": ToggleTheme(); break;
            case "bigger": _size = Math.Min(Sizes.Length - 1, _size + 1); break;
            case "smaller": _size = Math.Max(0, _size - 1); break;
            case "insert":
                if (_inserting) CancelInsert();
                else if (_locked) Flash(LockedNote);
                else OpenInsert(inBlock: false);
                break;
            case "undo": if (Editing) UndoTyping(); else if (_locked) Flash(LockedNote); else DocUndo(); break;
            case "redo": if (Editing) RedoTyping(); else if (_locked) Flash(LockedNote); else DocRedo(); break;
            case "save":
                if (Editing) SaveTyping(); else doc.Save(doc.Text);
                Flash("Saved");
                break;
        }
    }

    /// <summary>A heading (or a section of the strip) picked in the page's menu: the page scrolls to it.</summary>
    public void GoToBlock(int block) => JumpTo(block);

    /// <summary>A version picked from the history.</summary>
    public void GoToVersionAt(int index)
    {
        if (index >= 0 && index < doc.History.Count) GoToVersion(index);
    }

    /// <summary>The headings: [{heading, block (where a jump lands: its eyebrow, if it has one), level (its rank among those used, from 0), text}].</summary>
    public string OutlineJson()
    {
        var heads = Headings();
        var levels = heads.Select(Level).Distinct().Order().ToList();
        return Json(w =>
        {
            w.WriteStartArray();
            foreach (int i in heads)
            {
                var (level, text) = Blocks.HeadingOf(doc.Source(doc.Blocks[i]));
                w.WriteStartObject();
                w.WriteNumber("heading", i);
                w.WriteNumber("block", PartStart(i));
                w.WriteNumber("level", levels.IndexOf(level));
                w.WriteString("text", Blocks.Plain(text));
                w.WriteEndObject();
            }
            w.WriteEndArray();
        });
    }

    /// <summary>Every version this session, oldest first: [{summary, at (HH:mm:ss), added, removed}].</summary>
    public string HistoryJson() => Json(w =>
    {
        w.WriteStartArray();
        foreach (var v in doc.History)
        {
            w.WriteStartObject();
            w.WriteString("summary", v.Summary);
            w.WriteString("at", v.At.ToString("HH:mm:ss"));
            w.WriteNumber("added", v.Added);
            w.WriteNumber("removed", v.Removed);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    });

    /// <summary>The keys, in groups: [{title, keys: [[keys, what]]}], the group for what is open first.</summary>
    public string KeysJson() => Json(w =>
    {
        var groups = Editing
            ? new[] { ("Editing", EditKeys), ("Everywhere", AppKeys), ("Page", PageKeys) }
            : [("Page", PageKeys), ("Everywhere", AppKeys), ("Editing", EditKeys)];
        w.WriteStartArray();
        foreach (var (title, keys) in groups)
        {
            w.WriteStartObject();
            w.WriteString("title", title);
            w.WriteStartArray("keys");
            foreach (var (k, what) in keys)
            {
                if (k == "Ctrl+Q") continue;   // a tab is closed by the browser
                w.WriteStartArray();
                w.WriteStringValue(k);
                w.WriteStringValue(what);
                w.WriteEndArray();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        w.WriteEndArray();
    });

    /// <summary>The insert (or block) menu's choices for what is typed in its field: [{name, hint}].</summary>
    public string MenuJson(string query)
    {
        _query = query;
        return Json(w =>
        {
            w.WriteStartArray();
            foreach (var s in _inserting ? Matches() : [])
            {
                w.WriteStartObject();
                w.WriteString("name", s.Name);
                w.WriteString("hint", s.Hint);
                w.WriteEndObject();
            }
            w.WriteEndArray();
        });
    }

    /// <summary>The menu's <paramref name="index"/>th choice for <paramref name="query"/>, taken.</summary>
    public void ChooseMenu(string query, int index)
    {
        if (!_inserting) return;
        _query = query;
        var matches = Matches();
        if (index >= 0 && index < matches.Count) Choose(matches[index]); else CancelInsert();
    }

    /// <summary>The menu closed without a choice; <paramref name="back"/>: Backspace in an empty field took the / back.</summary>
    public void CloseMenu(string query, bool back)
    {
        if (!_inserting) return;
        _query = query;
        if (back && _insertInBlock) _inserting = false; else CancelInsert();
    }

    private static string Json(Action<Utf8JsonWriter> write)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms)) write(w);
        return Encoding.UTF8.GetString(ms.ToArray());
    }
}

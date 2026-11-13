using System.Diagnostics;
using Bibui.Core;
using Bibui.Core.Runtime;
using Bibui.Core.TextEdit;
using Bibui.Core.Theming;
using Bibui.Text;
using System.Reflection;

namespace BibMd;

public enum Mode { Read, Edit }

/// <summary>
/// The reader: the document as rendered Markdown in one quiet column. A block
/// clicked (or selected and Enter pressed) shows its source, edited in place as
/// any text is; Escape or a click elsewhere puts the block back rendered and
/// writes the file. A selected block is copied, cut, pasted, moved and deleted
/// with the usual shortcuts. Undo and redo walk every version of the file this
/// session, and inside a block, its typing.
/// </summary>
public sealed partial class App(BibuiRuntime runtime, Document doc, Images images)
{
    private const float MaxColumn = 760f, SideGutter = 32f, TopGutter = 72f;
    private const float PadX = 14f, PadY = 6f;
    private static readonly TimeSpan TypingBurst = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan AutosaveAfter = TimeSpan.FromMilliseconds(600);
    private static readonly ComponentSize[] Sizes = [ComponentSize.Sm, ComponentSize.Md, ComponentSize.Lg, ComponentSize.Xl];

    public Mode Mode { get; private set; } = Mode.Read;

    // ── The block being edited ───────────────────────────────────────────────
    private int _editing = -1;                 // the block it replaces, or where a new one goes (before that index)
    private bool _isNew;
    private int _editStart, _editEnd;          // the range of doc.Text the editor replaces
    private readonly BibuiStr _buffer = new(256);
    private readonly TextEditState _caret = new();
    private readonly List<(string Text, int Cursor)> _typing = [];
    private int _typingAt;
    private DateTime _lastTyped;
    private bool _unsaved, _forceStep;

    // ── Reading ──────────────────────────────────────────────────────────────
    private int _sel;                          // the selected block
    private bool _showSel, _reveal, _revealTop;
    private string _register = "";             // the block copied last, for a paste where there is no clipboard
    private readonly Dictionary<string, MarkdownDocument> _parsed = [];
    private readonly List<(float Top, float Bottom)> _rects = [];   // each block where it was drawn last frame, in page coordinates
    private (float Top, float Bottom) _editorRect;
    private Box? _page, _content;
    private int _size = 1;
    private bool _dark = true;
    private Omarchy? _omarchy;                 // the desktop's theme, followed while _followOmarchy and it has one
    private bool _followOmarchy = true;
    internal static readonly Transition ThemeFade = Transition.Timed(0.4f, Easing.EaseInOut);
    private bool _sourceView;                  // every block as its Markdown, the file as it is written
    private string? _flash;
    private DateTime _flashAt;
    private readonly Bib _bib = new(runtime);   // the document's live bib blocks
    private bool _frontOpen;                     // the front matter shown whole; folded to one line until asked
    private bool _locked, _lockRead;
    private readonly Dictionary<int, (float X, float Y)> _pressAt = [];   // where a press on a block's text went down             // read only: nothing opens for editing (Ctrl+L, or locked: in the front matter)
    private const string LockedNote = "Locked · Ctrl+L to edit";

    public bool Quit { get; private set; }

    /// <summary>Opens a link where there is no process to start (the browser); null opens it on the desktop.</summary>
    public static Action<string>? LinkOpener;

    /// <summary>Read only from the start, whatever the front matter says (the web viewer); Ctrl+L still unlocks.</summary>
    public void Lock() => (_locked, _lockRead) = (true, true);

    /// <summary>Only read: nothing in it can be edited until Ctrl+L.</summary>
    public bool Locked => _locked;

    /// <summary>The built-in theme, light or dark, as Ctrl+T leaves it.</summary>
    public bool Dark
    {
        get => _dark;
        set { _dark = value; runtime.SetTheme(ThemeFor(value)); }
    }

    private const string MonoFamily = "iA Writer Mono S";

    /// <summary>The monospaced face the editor and the keys are set in, from the binary: so a table's pipes line up.</summary>
    public static void RegisterFonts()
    {
        var self = Assembly.GetExecutingAssembly();
        // Each file at the weight its own tables say.
        FontRegistry.RegisterFromEmbeddedResource(MonoFamily, self, "iAWriterMonoS-Regular.ttf", "iAWriterMonoS-Bold.ttf");
    }

    /// <summary>The built-in theme, light or dark, with <see cref="FontSlot.Mono"/> in <see cref="MonoFamily"/>.</summary>
    public static ThemeTokens ThemeFor(bool dark)
    {
        var theme = ThemeRegistry.Load(dark ? "bibui-dark" : "bibui-light");
        var t = theme.Typography;
        return theme with
        {
            // bibui-dark is near-white on near-black (about 19:1), which glares in a long read:
            // a warm off-black and a softer ink bring it to about 12:1. The raised surface steps
            // up with the ground so the selection still shows.
            Colors = dark ? theme.Colors with
            {
                Background = "#161412", Surface = "#161412",
                SurfaceRaised = "#211E1B", SurfaceRaisedHover = "neutral-800",
                TextPrimary = "#DAD6D0",
            } : theme.Colors,
            Typography = new TypographyTokens
            {
                SizeSm = t.SizeSm, SizeMd = t.SizeMd, SizeLg = t.SizeLg, SizeXl = t.SizeXl, Size2Xl = t.Size2Xl,
                WeightNormal = t.WeightNormal, WeightSemiBold = t.WeightSemiBold,
                FontFamilyBody = t.FontFamilyBody, FontFamilyDisplay = t.FontFamilyDisplay,
                FontFamilyHeading = t.FontFamilyHeading, FontFamilyMono = MonoFamily,
            },
        };
    }

    /// <summary>Takes the Omarchy theme now, and from here on crossfades to it whenever it changes.</summary>
    public void FollowOmarchy(Omarchy omarchy)
    {
        _omarchy = omarchy;
        if (omarchy.Poll() && omarchy.Theme is { } theme) runtime.SetTheme(theme);
    }

    private void FollowTheme()
    {
        if (_omarchy?.Poll() != true || !_followOmarchy) return;
        runtime.SetTheme(_omarchy.Theme ?? ThemeFor(_dark), ThemeFade);
    }

    /// <summary>Ctrl+T: between the Omarchy theme and the built-in one of the other mode; between light and dark without one.</summary>
    private void ToggleTheme()
    {
        if (_omarchy?.Theme is { } omarchy)
        {
            _followOmarchy = !_followOmarchy;
            _dark = omarchy.Colors.Mode == ThemeMode.Light;
            runtime.SetTheme(_followOmarchy ? omarchy : ThemeFor(_dark), ThemeFade);
            return;
        }
        _dark = !_dark;
        runtime.SetTheme(ThemeFor(_dark));
    }

    /// <summary>Typing that has not reached the disk yet: the window should wake to save it.</summary>
    public bool Pending => _unsaved;

    private bool Editing => _editing >= 0;

    public void Build()
    {
        FollowTheme();
        HandleKeys();
        if (_unsaved && DateTime.Now - _lastTyped >= AutosaveAfter) SaveTyping();
        if (doc.Blocks.Count > 0) _sel = Math.Clamp(_sel, 0, doc.Blocks.Count - 1);

        var root = runtime.BeginParent(runtime.Column().Sized(Size.Fill, Size.Fill));
        var main = runtime.BeginParent(runtime.Row().Sized(Size.Fill, Size.Fill));
        if (_sidebar) Sidebar();

        ReadFront();
        // The page, under the document's section strip when its front matter asks for one.
        var pane = runtime.BeginParent(runtime.Column().Sized(Size.Fill, Size.Fill));
        if (!_sourceView) SectionStrip();
        Box page = runtime.BoxFromId("page",
            InteractionKinds.ViewScrollY | InteractionKinds.ClampY | InteractionKinds.Clip | InteractionKinds.LeftPress);
        page.ChildLayoutAxis = LayoutAxis.Y;
        page.Sized(Size.Fill, Size.Fill);
        page.AllowOverflowY = true;
        _page = page;

        float room = page.Extent.X > 0f ? page.Extent.X : 960f;
        float column = ColumnFor(room);
        float left = MathF.Floor((room - column) / 2f);

        int? clickedBlock = null;
        float docBottom = 0f;
        // The bib blocks run as one program, once a frame, before any of them draws.
        if (!_sourceView) _bib.Prepare(doc, _size);
        runtime.BeginParent(page);
        // Everything that scrolls, in one retained box: a block's place in it is its rect less this one's.
        Box content = runtime.BoxFromId("content");
        content.ChildLayoutAxis = LayoutAxis.Y;
        content.Sized(Size.Fill, Size.Fit);
        _content = content;
        runtime.BeginParent(content);
        runtime.Gap(TopGutter);
        var row = runtime.BeginParent(runtime.Row().Sized(Size.Fill));
        runtime.Gap(left);
        var col = runtime.BeginParent(runtime.Column().Sized(Size.Px(column)));
        using (runtime.FontSize(Sizes[_size]))
        {
            float body = runtime.FontInScope().Size;
            var blocks = doc.Blocks;
            bool first = true;
            for (int i = 0; i <= blocks.Count; i++)
            {
                if (_isNew && _editing == i)
                {
                    if (!first) runtime.Gap(MathF.Round(body * 0.9f));
                    Editor();
                    first = false;
                }
                if (i == blocks.Count) break;
                var b = blocks[i];
                // A bib data block is the document's data, not something to read: only the source view
                // shows it (and the editor, while it is open).
                if (Hidden(i) && !(!_isNew && i == _editing)) { Record(i, null); continue; }
                if (!first) runtime.Gap(_sourceView ? BlankLinesBefore(i) * LineHeight() : AfterEyebrow(i) ? MathF.Round(body * 0.25f) : GapBefore(b, body));
                first = false;
                if (!_isNew && i == _editing) { Editor(); continue; }
                Box node = BlockView(i, b, column, out bool clicked);
                if (clicked) clickedBlock = i;
                docBottom = node.Rect.Y + node.Extent.Y;
                Record(i, node);
            }
            if (_rects.Count > blocks.Count) _rects.RemoveRange(blocks.Count, _rects.Count - blocks.Count);
            if (blocks.Count == 0 && !Editing)
                runtime.Text("Empty. Click here to start writing.", Sizes[_size], UiColor.TextDisabled);
        }
        runtime.EndParent(col);
        runtime.EndParent(row);
        // Room under the last block: a click here starts a new one.
        runtime.Box().Sized(Size.Fill, Size.Px(MathF.Max(160f, page.Extent.Y * 0.45f)));
        runtime.EndParent(content);
        runtime.EndParent(page);
        runtime.EndParent(pane);
        runtime.EndParent(main);

        StatusLine();
        if (_inserting) InsertMenu();
        runtime.EndParent(root);
        if (_reveal) Reveal(page);

        // The page asks last, so the blocks and links on it take their own presses first.
        Interaction onPage = runtime.InteractionsFor(page);

        if (_sideAction is { } act) { _sideAction = null; act(); return; }
        if ((clickedBlock is not null || onPage.Clicked) && _inserting) { CancelInsert(); return; }
        if (clickedBlock is not null || onPage.Clicked) { _sideFocus = false; _bib.Blur(); }
        if (_locked) { /* reading: a click selects nothing and opens nothing */ }
        else if (clickedBlock is int target) SwitchTo(target);
        else if (onPage.Clicked)
        {
            bool below = onPage.Y > docBottom;
            if (Editing) FinishEdit();
            if (below || doc.Blocks.Count == 0) StartEdit(doc.Blocks.Count, isNew: true);
        }
    }

    // ── Blocks ───────────────────────────────────────────────────────────────

    // An eyebrow (a ###### line) sits right above the heading it names.
    private bool AfterEyebrow(int i) =>
        i > 0 && doc.Blocks[i - 1].Kind == BlockKind.Heading && Blocks.HeadingOf(doc.Source(doc.Blocks[i - 1])).Level == 6;

    private static float GapBefore(Block b, float body) => b switch
    {
        { Tight: true } => 0f,
        { Kind: BlockKind.Heading } => MathF.Round(body * 1.4f),
        _ => MathF.Round(body * 0.9f),
    };

    // In the source view the space between blocks is the blank lines between them in the file.
    private int BlankLinesBefore(int i)
    {
        if (i == 0) return 0;
        var blocks = doc.Blocks;
        int lines = 0;
        for (int at = blocks[i - 1].End; at < blocks[i].Start; at++) if (doc.Text[at] == '\n') lines++;
        return Math.Max(0, lines - 1);
    }

    private float LineHeight() => MathF.Round(runtime.FontInScope(Sizes[_size], FontSlot.Mono).Size * 1.45f);

    // A block's Markdown, wrapped at the column, in the editor's face.
    // A wrapped line goes on under where its own text starts (under the text after "- " for a list
    // item), so nested YAML and lists keep their shape.
    private void SourceView(string source, float width)
    {
        var font = runtime.FontInScope(Sizes[_size], FontSlot.Mono);
        float charWidth = BibMd.Text.Width(runtime, " ", font);
        foreach (string line in source.Split('\n'))
        {
            var lines = BibMd.Text.Lines(runtime, line, font, width);
            if (line.Length == 0 || lines.Length <= 1)
            {
                runtime.Text(line.Length == 0 ? " " : line, Sizes[_size], UiColor.TextPrimary, FontSlot.Mono);
                continue;
            }
            int indent = line.Length - line.TrimStart(' ').Length;
            if (line.AsSpan(indent).StartsWith("- ")) indent += 2;
            float hang = MathF.Min(indent * charWidth, width / 2f);
            var first = lines[0];
            runtime.Text(line.AsSpan(first.Start, first.End - first.Start), Sizes[_size], UiColor.TextPrimary, FontSlot.Mono);
            string rest = line[first.Next..];
            foreach (var part in BibMd.Text.Lines(runtime, rest, font, width - hang))
            {
                var row = runtime.BeginParent(runtime.Row());
                runtime.Gap(hang);
                runtime.Text(rest.AsSpan(part.Start, part.End - part.Start), Sizes[_size], UiColor.TextPrimary, FontSlot.Mono);
                runtime.EndParent(row);
            }
        }
    }

    private Box BlockView(int i, Block b, float column, out bool clicked)
    {
        Box wrap = runtime.BoxFromId("block-" + i, InteractionKinds.LeftPress | InteractionKinds.RightPress);
        wrap.ChildLayoutAxis = LayoutAxis.Y;
        wrap.Sized(Size.Fill, Size.Fit);
        // List items sit close, as a tight list does; the padding is only room for the highlight.
        wrap.Style.Padding = EdgeInsets.Symmetric(horizontal: PadX, vertical: b.Kind == BlockKind.ListItem || _sourceView ? 2f : PadY);
        wrap.Style.CornerRadius = 8f;
        bool selected = _showSel && i == _sel && !Editing;
        wrap.Style.BackgroundColor = UiColor.SurfaceRaised.WithAlpha(MathF.Max(selected ? 0.6f : 0f, 0.45f * wrap.HotT));
        if (selected)
        {
            wrap.Style.BorderColor = UiColor.Accent.WithAlpha(0.45f);
            wrap.Style.BorderWidth = 1f;
        }

        float width = column - 2f * PadX;
        string source = doc.Source(b);
        runtime.BeginParent(wrap);
        string? link = null;
        int task = -1;
        bool live = false, textClicked = false;
        Box? fold = null;
        if (_sourceView) SourceView(source, width);
        else if (b.Kind == BlockKind.Image) ImageView(source, width);
        else if (b.Kind == BlockKind.FrontMatter) fold = FrontMatter(source);
        else if (b.Kind == BlockKind.Code && _bib.View(i, width)) live = true;
        else if (b.Kind == BlockKind.Heading && Blocks.HeadingOf(source) is { Level: 1 or 6 } h) Heading(h.Level, h.Text, width);
        // A wide page reads as a page: its sections' headings without the rule under them.
        else if (b.Kind == BlockKind.Heading && _width is not null && Blocks.HeadingOf(source) is { Level: 2 } h2) Heading(2, h2.Text, width);
        else if (b.Kind != BlockKind.Code || !Mermaid.View(runtime, source, width))
        {
            // Prose keeps the reading column's width on a wide page; tables take the whole.
            if (b.Kind is BlockKind.Paragraph or BlockKind.ListItem or BlockKind.Quote or BlockKind.Heading)
                width = MathF.Min(width, MaxColumn - 2f * PadX);
            // A table is shown squared up — every row as many cells as the widest — as it will be
            // written once edited: a renderer drops a table whose rows disagree, or the cells past the header.
            var md = runtime.Markdown("md-" + i, Parse(b.Kind == BlockKind.Table ? Tables.Align(source) : source), width, Sizes[_size]);
            link = md.ClickedLink;
            task = md.ToggledTask;
            // The text takes a press itself, to select: a press let go where it went down is a click
            // on the block (which opens it, unless the document is locked); one that moved selected.
            // The view reads its own Pressed and Released first, and a read consumes them, so the
            // press is followed here by its count: held while above 0, let go when it drops to 0.
            var text = runtime.InteractionsFor(md.Node);
            bool held = text.PressCount > 0;
            if (held && !_pressAt.ContainsKey(i)) { _pressAt[i] = (text.X, text.Y); _showSel = false; }   // Ctrl+C copies the text, not the block
            else if (!held && _pressAt.Remove(i, out var at) && MathF.Abs(text.X - at.X) + MathF.Abs(text.Y - at.Y) < 5f)
                textClicked = true;
        }
        runtime.EndParent(wrap);

        Interaction ia = runtime.InteractionsFor(wrap);
        // Clicked counts the right button's release too; only the left one opens the block.
        // A press on a bib block's button or input is the widget's, not a request to edit the block;
        // one on the front matter's first line folds or unfolds it.
        bool onFold = fold is not null && ia.X >= fold.Rect.X && ia.X < fold.Rect.X + fold.Extent.X
            && ia.Y >= fold.Rect.Y && ia.Y < fold.Rect.Y + fold.Extent.Y;
        if (ia.Clicked && ia.Released && onFold) _frontOpen = !_frontOpen;
        clicked = (ia.Clicked && ia.Released || textClicked) && link is null && task < 0 && !onFold && !(live && _bib.Claims(i, ia.X, ia.Y));
        if (ia.RightPressed) { int target = i; _sideAction = () => OpenTurn(FinishEditKeeping(target)); }
        if (link is not null) OpenLink(link);
        if (task >= 0 && task < source.Length && _locked) Flash(LockedNote);
        else if (task >= 0 && task < source.Length)
            doc.Replace(b.Start + task, b.Start + task + 1, source[task] == ' ' ? "x" : " ");
        return wrap;
    }

    private MarkdownDocument Parse(string source)
    {
        if (!_parsed.TryGetValue(source, out var parsed))
        {
            if (_parsed.Count > 4000) _parsed.Clear();
            _parsed[source] = parsed = MarkdownDocument.Parse(source);
        }
        return parsed;
    }

    // A title in the display face, set large; and an eyebrow, a ###### line, as a small label
    // in capitals above the heading after it.
    private void Heading(int level, string text, float width)
    {
        if (level == 6)
        {
            runtime.Text(text.ToUpperInvariant(), ComponentSize.Sm, UiColor.TextSecondary, FontSlot.Mono);
            return;
        }
        float size = MathF.Round(runtime.FontInScope(Sizes[_size]).Size * (level == 1 ? 2.4f : 1.55f));
        var font = runtime.FontInScope(FontSize.Px(size), FontSlot.Display);
        foreach (var line in BibMd.Text.Lines(runtime, text, font, width))
            runtime.Text(text.AsSpan(line.Start, line.End - line.Start), FontSize.Px(size), UiColor.TextPrimary, FontSlot.Display, FontWeight.Bold);
    }

    // The front matter's nav: true (the ## headings as they read) or a list of labels (one per
    // ## heading, in order) puts a strip of section pills above the page; a pill goes to its heading.
    private string _navKey = "";
    private List<(int Block, string Label)> _nav = [];

    // What the front matter says about the page: nav (the section strip) and width.
    //   width: wide    the window's width, up to 1200 px, with a narrow edge
    //   width: full    all of it
    //   width: 1000    that many pixels, at most the window
    // Prose (paragraphs, lists, quotes, headings) stays at the reading column's width either way.
    private object? _width;

    private void ReadFront()
    {
        bool has = doc.Blocks.Count > 0 && doc.Blocks[0].Kind == BlockKind.FrontMatter;
        string front = has ? doc.Source(doc.Blocks[0]) : "";
        string key = front + "\u0001" + doc.Blocks.Count;
        if (key == _navKey) return;
        _navKey = key;
        _nav = [];
        _width = null;
        if (!has) return;
        object? nav = null;
        try
        {
            if (BibDoc.Yaml.Parse(front) is Dictionary<string, object?> meta)
            {
                meta.TryGetValue("nav", out nav);
                meta.TryGetValue("width", out _width);
                // locked: true opens the document read only; after that it is the reader's to change.
                if (!_lockRead && meta.TryGetValue("locked", out var locked)) _locked = locked is true;
            }
        }
        catch (BibDoc.DocError) { }
        _lockRead = true;
        if (nav is not true and not List<object?>) return;
        var labels = nav as List<object?>;
        for (int i = 0, n = 0; i < doc.Blocks.Count; i++)
        {
            if (doc.Blocks[i].Kind != BlockKind.Heading) continue;
            var (level, text) = Blocks.HeadingOf(doc.Source(doc.Blocks[i]));
            if (level != 2) continue;
            _nav.Add((i, labels is not null && n < labels.Count ? BibDoc.Values.Str(labels[n]) : text));
            n++;
        }
    }

    private void ToggleLock()
    {
        _locked = !_locked;
        if (_locked && Editing) FinishEdit();
        if (_locked && _inserting) CancelInsert();
        Flash(_locked ? LockedNote : "Unlocked · click a block to edit it");
    }

    private const float WideEdge = 16f;

    // The column the page is laid out in, for a page <paramref name="room"/> wide.
    private float ColumnFor(float room) => _sourceView ? MathF.Min(MaxColumn, room - 2f * SideGutter) : _width switch
    {
        "wide" => MathF.Min(1200f, room - 2f * WideEdge),
        "full" => room - 2f * WideEdge,
        double px => MathF.Min((float)px, room - 2f * WideEdge),
        _ => MathF.Min(MaxColumn, room - 2f * SideGutter),
    };

    private void SectionStrip()
    {
        if (_nav.Count == 0) return;
        float room = _page is { } pg && pg.Extent.X > 0f ? pg.Extent.X : 960f;
        float column = ColumnFor(room);
        var strip = runtime.BeginParent(runtime.Row().Sized(Size.Fill, Size.Px(48f)));
        strip.Style.Padding = EdgeInsets.Symmetric(horizontal: 0f, vertical: 9f);
        strip.Style.BorderColor = UiColor.Border;
        strip.Style.BorderWidth = 0f;
        runtime.Gap(MathF.Floor((room - column) / 2f) + PadX);
        foreach (var (block, label) in _nav)
        {
            Box pill = runtime.BoxFromId("nav-" + block, InteractionKinds.LeftPress);
            pill.ChildLayoutAxis = LayoutAxis.X;
            pill.Sized(Size.Fit, Size.Px(30f));
            pill.Style.Padding = EdgeInsets.Symmetric(horizontal: 11f, vertical: 0f);
            pill.Style.CornerRadius = 9999f;
            pill.Style.BorderWidth = 1f;
            pill.Style.BorderColor = UiColor.Border;
            pill.Style.BackgroundColor = UiColor.SurfaceRaised.WithAlpha(0.5f + 0.4f * pill.HotT);
            using (runtime.Parent(pill))
            {
                runtime.BeginCenterAcross();
                runtime.Text(label, ComponentSize.Sm, UiColor.TextPrimary);
                runtime.EndCenterAcross();
            }
            if (runtime.InteractionsFor(pill).Clicked) { int target = block; _sideAction = () => JumpTo(target); }
            runtime.Gap(6f);
        }
        runtime.EndParent(strip);
    }

    // The front matter, folded to one line (its title and what it holds) until it is unfolded.
    // Returns that line, which folds and unfolds it when pressed.
    private Box FrontMatter(string source)
    {
        var box = runtime.BeginParent(runtime.Column().Sized(Size.Fill));
        box.Style.Padding = EdgeInsets.Symmetric(horizontal: 10f, vertical: _frontOpen ? 10f : 6f);
        box.Style.CornerRadius = 6f;
        box.Style.BorderColor = UiColor.Border;
        box.Style.BorderWidth = 1f;
        Box line = runtime.BoxFromId("front-fold", InteractionKinds.LeftPress);
        line.ChildLayoutAxis = LayoutAxis.X;
        // A height of its own: the chevron is centred across it, which a row sized by its content cannot do.
        line.Sized(Size.Fill, Size.Px(MathF.Round(runtime.FontInScope(ComponentSize.Sm).Size * 1.7f)));
        runtime.BeginParent(line);
        runtime.BeginCenterAcross();
        runtime.Icon(_frontOpen ? "chevron-down" : "chevron-right", 12f).Color = UiColor.TextSecondary;
        runtime.EndCenterAcross();
        runtime.Gap(6f);
        runtime.Text(_frontOpen ? "Front matter" : FrontSummary(source), ComponentSize.Sm, UiColor.TextSecondary);
        runtime.EndParent(line);
        if (_frontOpen)
        {
            runtime.Gap(6f);
            foreach (var l in source.Split('\n'))
                runtime.Text(l.Length == 0 ? " " : l, ComponentSize.Sm, UiColor.TextSecondary, FontSlot.Mono);
        }
        runtime.EndParent(box);
        return line;
    }

    // "Which supplier for the pilot run · data, effects": the title, then the other keys it sets.
    private static string FrontSummary(string source)
    {
        try
        {
            if (BibDoc.Yaml.Parse(source) is Dictionary<string, object?> meta && meta.Count > 0)
            {
                string title = meta.TryGetValue("title", out var t) && t is string ts ? ts : "";
                var keys = meta.Keys.Where(k => k != "title").ToList();
                string rest = keys.Count == 0 ? "" : string.Join(", ", keys);
                return Blocks.Plain(title.Length == 0 ? "Front matter: " + rest : rest.Length == 0 ? title : title + " · " + rest);
            }
        }
        catch (BibDoc.DocError) { /* shown whole when unfolded */ }
        return "Front matter";
    }

    private void ImageView(string source, float width)
    {
        var (alt, src) = Blocks.ImageOf(source);
        string path = src.Contains("://") ? src : Path.Combine(Path.GetDirectoryName(doc.Path)!, src);
        var (image, error) = images.Get(path);
        if (image is null)
        {
            string what = error is null ? "Loading " + src + "…" : "Image " + src + ": " + error;
            var ph = runtime.BeginParent(runtime.Column().Sized(Size.Fill));
            ph.Style.Padding = EdgeInsets.All(16f);
            ph.Style.CornerRadius = 8f;
            ph.Style.BorderColor = UiColor.Border;
            ph.Style.BorderWidth = 1f;
            runtime.Text(what, ComponentSize.Sm, UiColor.TextSecondary);
            runtime.EndParent(ph);
            return;
        }
        float scale = runtime.ScaleFactor;
        float w = MathF.Round(MathF.Min(width, image.Width / scale));
        float h = MathF.Round(w * image.Height / image.Width);
        Box pic = runtime.Box().Sized(Size.Px(w), Size.Px(h));
        int pw = Math.Max(1, (int)MathF.Round(w * scale)), phh = Math.Max(1, (int)MathF.Round(h * scale));
        pic.Draw = (_, ctx) => ctx.DrawTexture(images.Texture(path, pw, phh), new RectF(0f, 0f, w, h),
            new Paint { Fill = Rgba.White });
        if (alt.Length > 0)
        {
            runtime.Gap(6f);
            runtime.Text(alt, ComponentSize.Sm, UiColor.TextSecondary);
        }
    }

    private static void OpenLink(string link)
    {
        if (LinkOpener is not null) { LinkOpener(link); return; }
        if (OperatingSystem.IsBrowser()) return;
        try
        {
            string file = link.Split('#')[0];
            // A relative link to another Markdown file opens in a new window of this app.
            if (!link.Contains("://") && file.EndsWith(".md", StringComparison.OrdinalIgnoreCase) && Environment.ProcessPath is { } self)
                Process.Start(self, [file]);
            else if (!link.StartsWith('#'))
                Process.Start(new ProcessStartInfo("xdg-open", [link]) { UseShellExecute = false });
        }
        catch { /* nothing to open it with */ }
    }

    private void Record(int i, Box? node)
    {
        if (node is null)
        {
            // Not drawn: it sits where the block before it ends, with no height.
            float at = i > 0 && i - 1 < _rects.Count ? _rects[i - 1].Bottom : 0f;
            while (_rects.Count <= i) _rects.Add((0f, 0f));
            _rects[i] = (at, at);
            return;
        }
        if (_content is null || node.Extent.Y <= 0f) return;
        float top = node.Rect.Y - _content.Rect.Y;
        while (_rects.Count <= i) _rects.Add((0f, 0f));
        _rects[i] = (top, top + node.Extent.Y);
    }

    // Scrolls the page so the selected block (or the editor) is in view: from where it was drawn last frame.
    private void Reveal(Box page)
    {
        var (top, bottom) = Editing ? _editorRect : _sel < _rects.Count ? _rects[_sel] : (0f, 0f);
        if (bottom <= 0f || page.Extent.Y <= 0f) return;   // not drawn yet: next frame
        _reveal = false;
        const float Margin = 48f;
        float at = page.TargetViewOffset.Y;
        if (bottom > at + page.Extent.Y - Margin) at = bottom - page.Extent.Y + Margin;
        if (top < at + Margin || _revealTop) at = top - Margin;
        _revealTop = false;
        page.TargetViewOffset = page.TargetViewOffset with { Y = MathF.Max(0f, at) };
    }

    // ── The editor ───────────────────────────────────────────────────────────

    private void Editor()
    {
        Box frame = runtime.BoxFromId("editor-frame");
        if (_content is not null && frame.Extent.Y > 0f)
        {
            float top = frame.Rect.Y - _content.Rect.Y;
            _editorRect = (top, top + frame.Extent.Y);
        }
        frame.ChildLayoutAxis = LayoutAxis.Y;
        frame.Sized(Size.Fill, Size.Fit);
        frame.Style.Padding = EdgeInsets.Symmetric(horizontal: PadX - 1f, vertical: PadY + 4f);
        frame.Style.CornerRadius = 8f;
        frame.Style.BackgroundColor = UiColor.SurfaceRaised.WithAlpha(0.7f);
        frame.Style.BorderColor = UiColor.Accent.WithAlpha(0.7f);
        frame.Style.BorderWidth = 1f;
        runtime.BeginParent(frame);
        runtime.BeginFontFace(FontSlot.Mono);
        runtime.BeginSizeY(SemanticSize.ChildrenSum());
        // The editor has the keyboard for as long as a block is open: the runtime gives it from the next frame.
        runtime.Focus("editor");
        runtime.MultilineTextField("editor", _buffer, _caret, TextField.MultiLineEvents,
            placeholder: "Write, or type / for a heading, list, table…");
        runtime.EndSizeY();
        runtime.EndFontFace();
        runtime.EndParent(frame);
        TrackTyping();
    }

    private void StartEdit(int index, bool isNew, int? cursor = null)
    {
        var blocks = doc.Blocks;
        index = Math.Clamp(index, 0, blocks.Count);
        if (index == blocks.Count) isNew = true;
        string text = "";
        if (isNew) _editStart = _editEnd = index < blocks.Count ? blocks[index].Start : doc.Text.Length;
        else
        {
            (_editStart, _editEnd) = (blocks[index].Start, blocks[index].End);
            text = doc.Source(blocks[index]);
        }
        _editing = index;
        _isNew = isNew;
        Mode = Mode.Edit;
        int at = Math.Clamp(cursor ?? text.Length, 0, text.Length);
        SetBuffer(text, at);
        _typing.Clear();
        _typing.Add((text, at));
        _typingAt = 0;
        _reveal = true;
        _editorRect = default;
    }

    /// <summary>The document with the editor's text in its place.</summary>
    private string Composed(string text) =>
        _isNew ? Insertion(_editing, text) : doc.Splice(_editStart, _editEnd, text);

    /// <summary>
    /// The document with <paramref name="text"/> as new blocks before block
    /// <paramref name="at"/> (or at the end). A list item put beside a list item
    /// joins its list without a blank line, so a tight list stays tight.
    /// </summary>
    private string Insertion(int at, string text)
    {
        text = text.TrimEnd('\n', ' ', '\t');
        string t = doc.Text;
        var blocks = doc.Blocks;
        if (text.Trim().Length == 0) return t;
        bool item = Blocks.Split(text) is [{ Kind: BlockKind.ListItem }, ..];
        if (at < blocks.Count)
        {
            bool join = item && blocks[at].Kind == BlockKind.ListItem && (blocks[at].Tight || at > 0 && blocks[at - 1].Kind == BlockKind.ListItem);
            return t[..blocks[at].Start] + text + (join ? "\n" : "\n\n") + t[blocks[at].Start..];
        }
        string body = t.TrimEnd('\n');
        if (body.Length == 0) return text + "\n";
        bool joinEnd = item && blocks.Count > 0 && blocks[^1].Kind == BlockKind.ListItem;
        return body + (joinEnd ? "\n" : "\n\n") + text + "\n";
    }

    /// <summary>Puts the edited block back into the document, as one step of its history.</summary>
    private void FinishEdit()
    {
        if (!Editing) return;
        string text = Tables.AlignAll(_buffer.ToString().TrimEnd('\n', ' ', '\t'));
        int before = doc.Blocks.Count;
        doc.Commit(Composed(text));
        _sel = Math.Clamp(_editing, 0, Math.Max(0, doc.Blocks.Count - 1));
        if (_isNew && doc.Blocks.Count == before && _editing > 0 && _editing >= doc.Blocks.Count) _sel = doc.Blocks.Count - 1;
        _editing = -1;
        _isNew = false;
        _unsaved = false;
        _showSel = true;
        _reveal = true;
        Mode = Mode.Read;
    }

    private void SwitchTo(int target)
    {
        target = FinishEditKeeping(target);
        _sel = target;
        _showSel = false;
        StartEdit(target, isNew: false);
    }

    /// <summary>Puts back the block being edited; <paramref name="target"/>, a block index, moved with what that did to the ones before it.</summary>
    private int FinishEditKeeping(int target)
    {
        if (!Editing) return target;
        int before = doc.Blocks.Count, editing = _editing;
        bool wasNew = _isNew;
        FinishEdit();
        if (target > editing || wasNew && target >= editing) target += doc.Blocks.Count - before;
        return target;
    }

    private void SetBuffer(string text, int cursor)
    {
        _buffer.ReplaceRange((0, (int)_buffer.Length), text);
        _caret.Cursor = _caret.Mark = Math.Clamp(cursor, 0, text.Length);
    }

    // Typing is a history of its own: a burst of it is one step, a pause or a new word starts the next.
    private void TrackTyping()
    {
        string now = _buffer.ToString();
        if (now == _typing[_typingAt].Text) { _typing[_typingAt] = (now, _caret.Cursor); return; }
        var nowTime = DateTime.Now;
        bool newStep = _forceStep || nowTime - _lastTyped > TypingBurst || _typingAt == 0
            || _caret.Cursor > 0 && _caret.Cursor <= now.Length && char.IsWhiteSpace(now[_caret.Cursor - 1]);
        _forceStep = false;
        _typing.RemoveRange(_typingAt + 1, _typing.Count - _typingAt - 1);
        if (newStep) { _typing.Add((now, _caret.Cursor)); _typingAt++; }
        else _typing[_typingAt] = (now, _caret.Cursor);
        _lastTyped = nowTime;
        _unsaved = true;
    }

    // Autosave while typing: the document with the block as it stands, not yet a step of its history.
    private void SaveTyping()
    {
        _unsaved = false;
        if (Editing) doc.Save(Composed(_buffer.ToString()));
    }

    // ── Keys ─────────────────────────────────────────────────────────────────
    //
    // The shortcuts any editor has: Ctrl+Z, Ctrl+C, Escape, the arrows. Typing
    // goes into the open block; with none open, / opens the insert menu and other
    // letters do nothing, so a stray key never changes the document.

    private void HandleKeys()
    {
        // A bib block's text input has the keyboard: it reads the keys itself, and Escape gives them back.
        if (_bib.HasFocus && !Editing) return;
        bool swallowText = false;
        foreach (var ev in runtime.UnconsumedEvents)
        {
            if (_inserting && (ev.Kind == BibUiEventKind.Text || ev.IsKeyDown)) { InsertMenuEvent(ev); continue; }
            if (ev.Kind == BibUiEventKind.Text)
            {
                // Char is null past the BMP (an emoji): CodePoint says what was typed.
                char ch = ev.Char ?? '\uFFFF';
                if (swallowText || ev.CodePoint < ' ') { ev.Eat(); swallowText = false; continue; }
                if (Editing)
                {
                    // / in an empty block asks what to put there.
                    if (ch == '/' && _buffer.Length == 0) { ev.Eat(); OpenInsert(inBlock: true); }
                    continue;
                }
                ev.Eat();
                if (ch == '/' && !_sideFocus) { if (_locked) Flash(LockedNote); else OpenInsert(inBlock: false); }
                continue;
            }
            if (!ev.IsKeyDown) continue;
            bool handled = GlobalKey(ev) || (Editing ? EditKey(ev) : _sidebar && _sideFocus && SideKey(ev) || ReadKey(ev));
            if (handled) { ev.Eat(); swallowText = ev.Ctrl; }
        }
    }

    // The keys that do the same whatever is open.
    private bool GlobalKey(BibUiEvent ev)
    {
        bool once = !ev.Repeat;
        switch (ev.Key)
        {
            case Key.B when ev.Ctrl: if (once) ToggleSidebar(); return true;
            case Key.L when ev.Ctrl: if (once) ToggleLock(); return true;
            case Key.E when ev.Ctrl: if (once) ToggleSourceView(); return true;
            case Key.T when ev.Ctrl: if (once) ToggleTheme(); return true;
            case Key.F1:
            case Key.Slash when ev.Ctrl: if (once) ShowPanel(Panel.Keys); return true;
            case Key.Equal or Key.KeypadAdd when ev.Ctrl: _size = Math.Min(Sizes.Length - 1, _size + 1); return true;
            case Key.Minus or Key.KeypadSubtract when ev.Ctrl: _size = Math.Max(0, _size - 1); return true;
            case Key.Q or Key.W when ev.Ctrl: if (once) { FinishEdit(); Quit = true; } return true;
            case Key.S when ev.Ctrl:
                if (Editing) SaveTyping(); else doc.Save(doc.Text);
                Flash("Saved");
                return true;
        }
        return false;
    }

    // ── Editing a block ──────────────────────────────────────────────────────

    private bool EditKey(BibUiEvent ev)
    {
        switch (ev.Key)
        {
            // Done: the block goes back, rendered, and stays selected, so the arrows go on from it.
            case Key.Escape:
            case Key.Enter when ev.Ctrl:
                FinishEdit();
                return true;
            case Key.Z when ev.Ctrl && !ev.Shift: UndoTyping(); return true;
            case Key.Z when ev.Ctrl && ev.Shift:
            case Key.Y when ev.Ctrl: RedoTyping(); return true;
            case Key.Tab: if (!TableTab(ev.Shift)) Indent(ev.Shift); return true;
        }
        return false;
    }

    // In a table, Tab lines the table up and steps to the next cell (Shift+Tab, the one before);
    // past the last cell it adds a row.
    private bool TableTab(bool back)
    {
        string text = _buffer.ToString();
        int cursor = Math.Clamp(_caret.Cursor, 0, text.Length);
        var block = Blocks.Split(text).FirstOrDefault(b => b.Kind == BlockKind.Table && cursor >= b.Start && cursor <= b.End);
        if (block.Kind != BlockKind.Table) return false;
        string table = text[block.Start..block.End];
        var (row, col) = Tables.CellAt(table, cursor - block.Start);
        string aligned = Tables.Align(table);
        int rows = aligned.Count(c => c == '\n') + 1, cols = Tables.Cells(aligned.Split('\n')[0]).Count;
        col += back ? -1 : 1;
        if (col >= cols) { col = 0; row++; }
        if (col < 0) { col = cols - 1; row--; }
        if (row == 1) row = back ? 0 : 2;   // the delimiter row has no cells to type in
        if (row < 0) (row, col) = (0, 0);
        if (row >= rows)
        {
            aligned = Tables.Align(aligned + "\n|" + string.Concat(Enumerable.Repeat(" |", cols)));
            row = rows;
        }
        Edit(text[..block.Start] + aligned + text[block.End..], block.Start + Tables.CellStart(aligned, row, col));
        return true;
    }

    // Tab indents the caret's line by two spaces, Shift+Tab takes them away: how a list item nests.
    private void Indent(bool outdent)
    {
        string text = _buffer.ToString();
        int cursor = Math.Clamp(_caret.Cursor, 0, text.Length);
        int lineStart = BibMd.Text.LineStart(text, cursor);
        _forceStep = true;
        if (outdent)
        {
            int n = 0;
            while (n < 2 && lineStart + n < text.Length && text[lineStart + n] == ' ') n++;
            if (n > 0) SetBuffer(text.Remove(lineStart, n), Math.Max(lineStart, cursor - n));
        }
        else SetBuffer(text.Insert(lineStart, "  "), cursor + 2);
    }

    private void UndoTyping()
    {
        if (_typingAt > 0)
        {
            var (t, c) = _typing[--_typingAt];
            SetBuffer(t, c);
            _unsaved = true;
            return;
        }
        // Nothing typed to take back: step out of the block and undo the document.
        _editing = -1;
        _isNew = false;
        _unsaved = false;
        Mode = Mode.Read;
        Flash(doc.Undo() ? "Undo" : "Already at the oldest change");
    }

    private void RedoTyping()
    {
        if (_typingAt >= _typing.Count - 1) return;
        var (t, c) = _typing[++_typingAt];
        SetBuffer(t, c);
        _unsaved = true;
    }


    // A change of the menu's (a snippet, a table's next cell) is one step of the block's history on its own.
    private void Edit(string text, int cursor)
    {
        _forceStep = true;
        SetBuffer(text, cursor);
    }

    // ── Reading: the page, and the selected block ────────────────────────────

    private bool ReadKey(BibUiEvent ev)
    {
        var page = _page;
        float view = page is null ? 600f : page.Extent.Y;
        bool sel = _showSel && doc.Blocks.Count > 0;
        bool alt = (ev.Modifiers & KeyModifiers.Alt) != 0;
        bool changes = ev.Key == Key.Enter || sel && ev.Key is Key.Delete or Key.Backspace
            || ev.Ctrl && ev.Key is Key.Z or Key.Y or Key.X or Key.V or Key.D
            || alt && ev.Key is Key.Up or Key.Down;
        if (_locked && changes)
        {
            Flash(LockedNote);
            return true;
        }
        switch (ev.Key)
        {
            case Key.Z when ev.Ctrl && !ev.Shift: DocUndo(); return true;
            case Key.Z when ev.Ctrl && ev.Shift:
            case Key.Y when ev.Ctrl: DocRedo(); return true;
            case Key.Escape: _showSel = false; return true;
            case Key.Enter: OpenSelected(); return true;
            case Key.Down when alt && sel: MoveBlock(_sel, 1); return true;
            case Key.Up when alt && sel: MoveBlock(_sel, -1); return true;
            case Key.Down: Step(1); return true;
            case Key.Up: Step(-1); return true;
            case Key.Space when ev.Shift: Scroll(page, -view * 0.9f); return true;
            case Key.Space or Key.PageDown: Scroll(page, view * 0.9f); return true;
            case Key.PageUp: Scroll(page, -view * 0.9f); return true;
            case Key.Tab: CyclePanel(ev.Shift ? -1 : 1); return true;
            case Key.Home: Select(0); return true;
            case Key.End: Select(doc.Blocks.Count - 1); return true;
            case Key.Delete or Key.Backspace when sel: DeleteBlock(_sel); return true;
            case Key.C when ev.Ctrl && sel: CopyBlock(_sel); return true;
            case Key.X when ev.Ctrl && sel: CopyBlock(_sel, quiet: true); DeleteBlock(_sel, "Cut block"); return true;
            case Key.D when ev.Ctrl && sel: DuplicateBlock(_sel); return true;
            case Key.V when ev.Ctrl: PasteBlocks(sel ? _sel + 1 : doc.Blocks.Count); return true;
        }
        // Every other Ctrl key is bibmd's, except copy and select all, which go to the text.
        return ev.Ctrl && ev.Key is not (Key.C or Key.A);
    }

    private void CopyBlock(int i, bool quiet = false)
    {
        _register = doc.Source(doc.Blocks[i]);
        try { runtime.ClipboardSet(_register); } catch { /* no clipboard: the paste takes _register */ }
        if (!quiet) Flash("Copied block");
    }

    private void DeleteBlock(int i, string what = "Deleted block")
    {
        var b = doc.Blocks[i];
        doc.Replace(b.Start, b.End, "");
        _showSel = doc.Blocks.Count > 0;
        _sel = Math.Clamp(i, 0, Math.Max(0, doc.Blocks.Count - 1));
        Flash(what + " · Ctrl+Z to undo");
    }

    private void DuplicateBlock(int i)
    {
        doc.Commit(Insertion(i + 1, doc.Source(doc.Blocks[i])));
        Select(i + 1);
    }

    // What the clipboard holds, as blocks before block at (or at the end).
    private void PasteBlocks(int at)
    {
        string? text = null;
        try { text = runtime.ClipboardGet(); } catch { /* no clipboard */ }
        if (string.IsNullOrWhiteSpace(text)) text = _register;
        if (text.Trim().Length == 0) return;
        doc.Commit(Insertion(Math.Clamp(at, 0, doc.Blocks.Count), text.Replace("\r\n", "\n")));
        Select(at);
    }

    // Alt+↑ / Alt+↓: the block changes places with the one above or below; the text between stays.
    // The front matter stays first.
    private void MoveBlock(int i, int dir)
    {
        var blocks = doc.Blocks;
        int j = i + dir;
        if (j < 0 || j >= blocks.Count) return;
        var (a, b) = dir > 0 ? (blocks[i], blocks[j]) : (blocks[j], blocks[i]);
        if (a.Kind == BlockKind.FrontMatter) return;
        string t = doc.Text;
        doc.Commit(t[..a.Start] + t[b.Start..b.End] + t[a.End..b.Start] + t[a.Start..a.End] + t[b.End..]);
        Select(j);
    }

    private void ToggleSourceView()
    {
        _sourceView = !_sourceView;
        _reveal = true;   // stay with the selection (or the editor) as every block changes height
        Flash(_sourceView ? "Markdown source" : "Rendered");
    }

    private void OpenSelected()
    {
        if (doc.Blocks.Count == 0) { StartEdit(0, isNew: true); return; }
        if (!_showSel) { _sel = FirstVisibleBlock(); }
        StartEdit(_sel, isNew: false);
    }

    // The arrows step over what the reading view does not show.
    private void Step(int by)
    {
        int to = _sel + by;
        while (to >= 0 && to < doc.Blocks.Count && Hidden(to)) to += Math.Sign(by);
        if (to < 0 || to >= doc.Blocks.Count) to = _sel;
        Select(to, relative: true);
    }

    // A block the reading view leaves out: a bib data block. The source view shows every block.
    private bool Hidden(int i) =>
        !_sourceView && i >= 0 && i < doc.Blocks.Count && doc.Blocks[i].Kind == BlockKind.Code && Bib.IsData(doc.Source(doc.Blocks[i]));

    private void Select(int index, bool relative = false)
    {
        if (doc.Blocks.Count == 0) return;
        // A first arrow only shows where the selection is: the first block in view.
        _sel = _showSel || !relative ? Math.Clamp(index, 0, doc.Blocks.Count - 1) : FirstVisibleBlock();
        _showSel = true;
        _reveal = true;
    }

    private int NextHeading(int dir)
    {
        var blocks = doc.Blocks;
        for (int i = _sel + dir; i >= 0 && i < blocks.Count; i += dir)
            if (blocks[i].Kind == BlockKind.Heading) return i;
        return dir > 0 ? blocks.Count - 1 : 0;
    }

    private void DocUndo()
    {
        Flash(doc.Undo() ? "Undo" : "Already at the oldest change");
        _reveal = true;
    }

    private void DocRedo()
    {
        Flash(doc.Redo() ? "Redo" : "Already at the newest change");
        _reveal = true;
    }

    private static void Scroll(Box? page, float by)
    {
        if (page is null) return;
        float max = MathF.Max(0f, page.ContentSize.Y - page.Extent.Y);
        page.TargetViewOffset = page.TargetViewOffset with { Y = Math.Clamp(page.TargetViewOffset.Y + by, 0f, max) };
    }

    private int FirstVisibleBlock()
    {
        float at = _page?.ViewOffset.Y ?? 0f;
        for (int i = 0; i < _rects.Count; i++)
            if (_rects[i].Bottom > at + 8f) return i;
        return Math.Max(0, doc.Blocks.Count - 1);
    }

    // ── Status ───────────────────────────────────────────────────────────────

    private void Flash(string message)
    {
        _flash = message;
        _flashAt = DateTime.Now;
    }

    private void StatusLine()
    {
        var bar = runtime.BeginParent(runtime.Row().Sized(Size.Fill, Size.Px(30f)));
        bar.Style.Padding = EdgeInsets.Symmetric(horizontal: 10f, vertical: 0f);
        Box menu = runtime.BoxFromId("menu-toggle", InteractionKinds.LeftPress);
        menu.ChildLayoutAxis = LayoutAxis.X;
        menu.Sized(Size.Fit, Size.Fill);
        menu.Style.Padding = EdgeInsets.Symmetric(horizontal: 6f, vertical: 0f);
        menu.Style.CornerRadius = 6f;
        menu.Style.BackgroundColor = UiColor.SurfaceRaised.WithAlpha(0.6f * menu.HotT);
        runtime.BeginParent(menu);
        runtime.BeginCenterAcross();
        runtime.Icon("menu", 14f).Color = _sidebar ? UiColor.TextSecondary : UiColor.TextDisabled;
        runtime.EndCenterAcross();
        runtime.EndParent(menu);
        if (runtime.InteractionsFor(menu).Clicked) ToggleSidebar();
        Box plus = runtime.BoxFromId("insert-toggle", InteractionKinds.LeftPress);
        plus.ChildLayoutAxis = LayoutAxis.X;
        plus.Sized(Size.Fit, Size.Fill);
        plus.Style.Padding = EdgeInsets.Symmetric(horizontal: 6f, vertical: 0f);
        plus.Style.CornerRadius = 6f;
        plus.Style.BackgroundColor = UiColor.SurfaceRaised.WithAlpha(0.6f * plus.HotT);
        runtime.BeginParent(plus);
        runtime.BeginCenterAcross();
        runtime.Icon("plus", 14f).Color = _inserting ? UiColor.TextSecondary : UiColor.TextDisabled;
        runtime.EndCenterAcross();
        runtime.EndParent(plus);
        if (runtime.InteractionsFor(plus).Clicked) { if (_inserting) CancelInsert(); else if (_locked) Flash(LockedNote); else OpenInsert(inBlock: false); }
        Box view = runtime.BoxFromId("source-toggle", InteractionKinds.LeftPress);
        view.ChildLayoutAxis = LayoutAxis.X;
        view.Sized(Size.Fit, Size.Fill);
        view.Style.Padding = EdgeInsets.Symmetric(horizontal: 6f, vertical: 0f);
        view.Style.CornerRadius = 6f;
        view.Style.BackgroundColor = UiColor.SurfaceRaised.WithAlpha(0.6f * view.HotT);
        runtime.BeginParent(view);
        runtime.BeginCenterAcross();
        runtime.Text("md", ComponentSize.Sm, _sourceView ? UiColor.Accent : UiColor.TextDisabled, FontSlot.Mono, FontWeight.Bold);
        runtime.EndCenterAcross();
        runtime.EndParent(view);
        if (runtime.InteractionsFor(view).Clicked) ToggleSourceView();
        Box lockBox = runtime.BoxFromId("lock-toggle", InteractionKinds.LeftPress);
        lockBox.ChildLayoutAxis = LayoutAxis.X;
        lockBox.Sized(Size.Fit, Size.Fill);
        lockBox.Style.Padding = EdgeInsets.Symmetric(horizontal: 6f, vertical: 0f);
        lockBox.Style.CornerRadius = 6f;
        lockBox.Style.BackgroundColor = UiColor.SurfaceRaised.WithAlpha(0.6f * lockBox.HotT);
        runtime.BeginParent(lockBox);
        runtime.BeginCenterAcross();
        runtime.Icon("lock", 14f).Color = _locked ? UiColor.Accent : UiColor.TextDisabled;
        runtime.EndCenterAcross();
        if (_locked)
        {
            runtime.Gap(6f);
            runtime.BeginCenterAcross();
            runtime.Text("LOCKED", ComponentSize.Sm, UiColor.Accent, FontSlot.Mono);
            runtime.EndCenterAcross();
        }
        runtime.EndParent(lockBox);
        if (runtime.InteractionsFor(lockBox).Clicked) ToggleLock();
        runtime.Gap(8f);
        runtime.BeginCenterAcross();
        runtime.Text(Path.GetFileName(doc.Path), ComponentSize.Sm, UiColor.TextDisabled);
        runtime.EndCenterAcross();
        runtime.Spacer(Size.Fill);
        string right = doc.SaveError is { } err ? "Not saved: " + err
            : _flash is not null && DateTime.Now - _flashAt < TimeSpan.FromSeconds(2) ? _flash
            : "";
        runtime.BeginCenterAcross();
        runtime.Text(right, ComponentSize.Sm, doc.SaveError is null ? UiColor.TextDisabled : UiColor.Destructive);
        runtime.EndCenterAcross();
        runtime.EndParent(bar);
    }

    /// <summary>The window is closing: the block being edited goes back into the file.</summary>
    public void Close()
    {
        if (Editing) FinishEdit();
    }

    // For the headless checks.
    public string Buffer => _buffer.ToString();
    public string DocumentText => doc.Text;
    public int Cursor => _caret.Cursor;
    public int Selected => _sel;
}

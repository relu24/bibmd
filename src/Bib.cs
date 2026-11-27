using System.Numerics;
using Bibui.Core.Draw;
using Bibui.Core;
using Bibui.Core.Runtime;
using Bibui.Core.TextEdit;
using BibDoc;

namespace BibMd;

/// <summary>
/// The document's <c>bib</c> blocks, live. Every frame runs all of them as one
/// program (a let in one block is seen by the next) over the front matter's
/// <c>data:</c>, and each block draws its own part of that frame with bibui's
/// components. A click runs the program once more to learn what the click
/// would do; this is a viewer, so it says so under the block and does nothing.
/// </summary>
public sealed partial class Bib(BibuiRuntime runtime, BibProgram? shared = null)
{
    private static readonly ComponentSize[] Sizes = [ComponentSize.Sm, ComponentSize.Md, ComponentSize.Lg, ComponentSize.Xl];

    private readonly BibProgram _program = shared ?? new BibProgram();
    private FrameResult? _frame;
    private Timer? _wake;         // wakes the window for the clock or the next alarm
    private readonly List<(string Id, Box Box)> _idBoxes = [];    // this frame's boxes with an id, laid out by the next
    private (float X, float Y) _pointer = (float.NaN, float.NaN); // where the pointer was last seen over a box with an id
    private readonly List<(string Id, Box Box)> _around = [];    // the boxes with an id being drawn, outermost first
    private bool _pointerSeen;                                    // a box with an id saw the pointer this frame
    private readonly Dictionary<int, int> _ordinal = [];          // document block -> bib block
    private readonly Dictionary<int, List<UiNode>> _widgets = []; // bib block -> its widgets, drawn this frame
    private readonly Dictionary<string, (BibuiStr Text, TextEditState Edit, string Seen)> _fields = [];   // Seen: the value the field last showed or gave
    private readonly HashSet<string> _fieldIds = [];                // this frame's text inputs, by box id
    private int _ids;                                             // ids for this frame's unnamed nodes
    private int _plots;                                           // this frame's plots, in order: each a box of its own across frames
    private int _sizeIndex = 1;

    /// <summary>A text input of a bib block has the keyboard: the reader's keys are not commands.</summary>
    public bool HasFocus => _fieldIds.Any(runtime.IsFocused);

    /// <summary>Gives the keyboard back to the reader.</summary>
    public void Blur()
    {
        if (HasFocus) runtime.Focus("");
    }

    /// <summary>
    /// Runs the document's bib blocks for this frame, at <paramref name="width"/>, the px width
    /// its blocks are drawn at (the document reads it as page.width; a new width runs it again).
    /// A document whose blocks or data changed gets a new program, keeping the values the reader gave.
    /// </summary>
    public void Prepare(Document doc, int sizeIndex, float width = (float)HostNow.DefaultWidth)
    {
        _sizeIndex = sizeIndex;
        _ordinal.Clear();
        _widgets.Clear();
        _fieldIds.Clear();
        _around.Clear();
        _ids = 0;
        _plots = 0;
        // The boxes with an id under the pointer, as the last frame laid them out (all of them during a
        // drag, which bibui gives to the box it started on).
        if (!_pointerSeen) _pointer = (float.NaN, float.NaN);
        _pointerSeen = false;
        var hovered = new HashSet<string>();
        foreach (var (id, b) in _idBoxes)
            if (_pointer.X >= b.Rect.X && _pointer.X < b.Rect.X + b.Extent.X && _pointer.Y >= b.Rect.Y && _pointer.Y < b.Rect.Y + b.Extent.Y)
                hovered.Add(id);
        _idBoxes.Clear();
        var sources = new List<string>();
        var dataBlocks = new List<string>();
        string? front = null;
        for (int i = 0; i < doc.Blocks.Count; i++)
        {
            var b = doc.Blocks[i];
            if (b.Kind == BlockKind.FrontMatter) front = doc.Source(b);
            else if (b.Kind == BlockKind.Code && IsData(doc.Source(b))) dataBlocks.Add(Body(doc.Source(b)));
            else if (b.Kind == BlockKind.Code && IsBib(doc.Source(b)))
            {
                _ordinal[i] = sources.Count;
                sources.Add(Body(doc.Source(b)));
            }
        }
        _frame = _program.Frame(front, dataBlocks, sources, this, hovered, MathF.Floor(width));
        if (_program.Renewed) _settling = SettleFrames;
        if (_frame is null) return;
        Ring(DateTimeOffset.Now);
        Identify();
    }

    // The alarms whose time has come ring, once each; then the window is woken for the clock or the next one.
    private void Ring(DateTimeOffset now)
    {
        if (_frame is null || _program.Session is not { } session) return;
        double secs = now.ToUnixTimeMilliseconds() / 1000.0;
        bool rang = false;
        foreach (double at in _frame.Alarms.Distinct().Order())
        {
            if (at > secs || !_program.Rang.Add(at)) continue;
            var result = session.Fire(new DocEvent("alarm", DocEvent.AlarmName(at)));
            rang = true;
            if (result.Effects.Count > 0 || result.Errors.Any(e => e is not null)) Told(-1, result, "alarm");
        }
        if (rang) _program.Touch();
        double? next = _frame.Every;
        foreach (double at in _frame.Alarms)
            if (at > secs && !_program.Rang.Contains(at)) next = Math.Min(next ?? double.MaxValue, at - secs);
        _wake?.Dispose();
        _wake = next is { } wait
            ? new Timer(_ => runtime.RequestFrame(), null, TimeSpan.FromSeconds(Math.Clamp(wait, 0.016, 3600)), Timeout.InfiniteTimeSpan)
            : null;
        if (rang) runtime.RequestFrame();
    }

    /// <summary>
    /// A key the document asked for (key() or a button's key:) goes to it, and is taken; any other is the reader's.
    /// </summary>
    public bool Key(BibUiEvent ev)
    {
        if (_frame is null || _program.Session is null || _frame.Keys.Count == 0 || KeyName(ev) is not { } name) return false;
        if (!_frame.Keys.Contains(name)) return false;
        Fire(-1, new DocEvent("key", name), what: "key");
        return true;
    }

    // A key as a reader knows it: "ctrl+z" is Ctrl+Z, "left" ←, "space" Space.
    private static string KeyCap(string name) => string.Join("+", name.Split('+').Select(p => p switch
    {
        "left" => "←", "right" => "→", "up" => "↑", "down" => "↓", "enter" => "↵", "escape" => "Esc",
        "ctrl" or "alt" or "shift" or "space" or "tab" or "delete" or "backspace" or "home" or "end" => char.ToUpperInvariant(p[0]) + p[1..],
        _ => p.ToUpperInvariant(),
    }));

    private static string? KeyName(BibUiEvent ev)
    {
        string? key = ev.Key switch
        {
            >= Bibui.Core.Key.A and <= Bibui.Core.Key.Z => ((char)('a' + (ev.Key - Bibui.Core.Key.A))).ToString(),
            >= Bibui.Core.Key.Number0 and <= Bibui.Core.Key.Number9 => ((char)('0' + (ev.Key - Bibui.Core.Key.Number0))).ToString(),
            >= Bibui.Core.Key.F1 and <= Bibui.Core.Key.F12 => "f" + (ev.Key - Bibui.Core.Key.F1 + 1),
            Bibui.Core.Key.Space => "space", Bibui.Core.Key.Enter or Bibui.Core.Key.KeypadEnter => "enter",
            Bibui.Core.Key.Escape => "escape", Bibui.Core.Key.Tab => "tab", Bibui.Core.Key.Backspace => "backspace",
            Bibui.Core.Key.Delete => "delete", Bibui.Core.Key.Left => "left", Bibui.Core.Key.Right => "right",
            Bibui.Core.Key.Up => "up", Bibui.Core.Key.Down => "down", Bibui.Core.Key.Home => "home", Bibui.Core.Key.End => "end",
            Bibui.Core.Key.PageUp => "pageup", Bibui.Core.Key.PageDown => "pagedown",
            Bibui.Core.Key.Minus => "-", Bibui.Core.Key.Equal => "=", Bibui.Core.Key.Comma => ",", Bibui.Core.Key.Period => ".",
            Bibui.Core.Key.Slash => "/", Bibui.Core.Key.LeftBracket => "[", Bibui.Core.Key.RightBracket => "]",
            _ => null,
        };
        if (key is null) return null;
        var m = ev.Modifiers;
        return DocEvent.KeyName(((m & KeyModifiers.Ctrl) != 0 ? "ctrl+" : "") + ((m & KeyModifiers.Alt) != 0 ? "alt+" : "")
            + ((m & KeyModifiers.Shift) != 0 ? "shift+" : "") + key);
    }

    /// <summary>
    /// Draws document block <paramref name="index"/> if it is a bib block. False,
    /// with nothing built, for any other block.
    /// </summary>
    public bool View(int index, float width)
    {
        if (_frame is null || _program.Session is null || !_ordinal.TryGetValue(index, out int k)) return false;
        _widgets[k] = [];
        var col = runtime.BeginParent(runtime.Column().Sized(Size.Fill));
        _blockBox = col;
        // A block slides what moves inside it once it has settled: not in the first frames of a document, when
        // what is measured is still arriving, nor in a frame its width changed (a window resized).
        _slides = _settling == 0 && _blockWidths.GetValueOrDefault(k) == width;
        _blockWidths[k] = width;
        if (k == 0 && _program.DataError is { } de) Problem("Front matter: " + de, width);
        DrawAll(_frame.Blocks[k], k, width, "block:" + k);
        if (_frame.Errors[k] is { } e) Problem(e.Message, width);
        if (_program.Notices.TryGetValue(k, out var notice))
        {
            runtime.Gap(8f);
            Wrapped(notice, width, Sizes[Math.Max(0, _sizeIndex - 1)], UiColor.TextSecondary);
        }
        runtime.EndParent(col);
        return true;
    }

    /// <summary>Whether a press at (x, y) landed on one of block <paramref name="index"/>'s widgets, not on the block.</summary>
    public bool Claims(int index, float x, float y)
    {
        if (!_ordinal.TryGetValue(index, out int k) || !_widgets.TryGetValue(k, out var nodes)) return false;
        foreach (var n in nodes)
            if (x >= n.Rect.X && x < n.Rect.X + n.Extent.X && y >= n.Rect.Y && y < n.Rect.Y + n.Extent.Y) return true;
        return false;
    }

    // ── drawing a frame ──────────────────────────────────────────────────────

    private ComponentSize Size0 => Sizes[_sizeIndex];
    private ComponentSize Smaller => Sizes[Math.Max(0, _sizeIndex - 1)];
    private ComponentSize Larger => Sizes[Math.Min(Sizes.Length - 1, _sizeIndex + 1)];
    private float Gap => MathF.Round(runtime.FontInScope(Size0).Size * 0.6f);

    private const float TabsPanelPad = 12f;

    private const float KvGap = 12f;

    // A table's column is at least as wide as its longest word, and MinColumn, unless that word is wider than ColumnCap.
    private const float MinColumn = 48f, ColumnCap = 140f;   // between a kv's label and its value   // bibui's Tabs.Panel pads its content so much on each side

    // The narrowest a grid's cell and a row's child are drawn: narrower, they go onto more lines.
    private const float MinCell = 120f, MinRowCell = 240f;

    // The narrowest a box with width: fill is set beside others in a row that wraps, unless it says (minWidth:).
    private const float FillMin = 160f;

    // parent names the container across frames, for its boxes that appear and leave (Motion.cs).
    private void DrawAll(List<FrameNode> nodes, int k, float width, string? parent = null)
    {
        // The boxes placed with at: last, over the rest.
        var all = parent is null ? nodes : Presented(parent, nodes);
        int drawn = 0;
        foreach (var n in all.Where(c => !Floats(c)))
        {
            if (drawn++ > 0) runtime.Gap(Gap);
            Draw(n, k, width);
        }
        foreach (var n in all.Where(Floats)) Draw(n, k, Want(n, width) ?? width);
    }

    private void Claim(int k, UiNode node) => _widgets[k].Add(node);

    private static UiColor Tone(FrameNode n, UiColor otherwise) => n.Text("tone") switch
    {
        "success" => UiColor.Success,
        "danger" => UiColor.Destructive,
        "warning" => Amber,
        "accent" or "primary" => UiColor.Accent,
        "muted" => UiColor.TextSecondary,
        ['#', ..] => Colour(n.Text("tone"), otherwise),
        _ => otherwise,
    };

    // bibui's palette has no warning colour; this is the one bib documents use for "warning".
    private static readonly UiColor Amber = UiColor.Hex("#D9A441");

    // A badge in the tone's colours. Warning is drawn here, as bibui's own success badge is
    // drawn (a tint of the colour, and the colour as ink), because bibui has no warning badge.
    private void Pill(string text, string? tone)
    {
        if (tone != "warning")
        {
            Badge.Build(runtime, text, Variant(tone));
            return;
        }
        Box pill = runtime.Box();
        pill.ChildLayoutAxis = LayoutAxis.X;
        pill.SizeX = pill.SizeY = SemanticSize.ChildrenSum();
        pill.Style.Padding = EdgeInsets.Symmetric(horizontal: 12f, vertical: 4f);
        pill.Style.CornerRadius = 9999f;
        pill.Style.BackgroundColor = Amber.WithAlpha(0.16f);
        using (runtime.Parent(pill)) runtime.Text(text, ComponentSize.Sm, Amber);
    }

    private static BadgeVariant Variant(string? tone) => tone switch
    {
        "success" => BadgeVariant.Success,
        "danger" => BadgeVariant.Destructive,
        "accent" or "primary" => BadgeVariant.Primary,
        "muted" => BadgeVariant.Outline,
        _ => BadgeVariant.Default,
    };

    private void Draw(FrameNode n, int k, float width)
    {
        switch (n.Type)
        {
            case "text":
                TextNode(n, width);
                break;
            case "box":
                DrawBox(n, k, width);
                break;
            case "layer" or "line" or "rect" or "circle" or "label" or "arc" or "path":
                break;   // drawn by the box they are in
            case "heading":
                Wrapped(n.Text("text") ?? "", width, Larger, UiColor.TextPrimary, FontWeight.Bold);
                break;
            case "markdown":
                runtime.Markdown("bib-md-" + _ids++, MarkdownDocument.Parse(n.Text("text") ?? ""), width, Size0);
                break;
            case "badge":
            {
                var row = runtime.BeginParent(runtime.Row());
                Pill(n.Text("text") ?? "", n.Text("tone"));
                runtime.EndParent(row);
                break;
            }
            case "divider":
                Separator.Build(runtime);
                break;
            case "card":
            {
                var card = runtime.BeginParent(runtime.Column().Sized(Size.Fill));
                card.Style.Padding = EdgeInsets.All(14f);
                card.Style.CornerRadius = 10f;
                card.Style.BorderWidth = 1f;
                card.Style.BorderColor = Tone(n, UiColor.Border);
                card.Style.BackgroundColor = UiColor.SurfaceRaised.WithAlpha(0.35f);
                float inner = width - 28f;
                if (n.Text("title") is { } title) TitleLine(title, inner, Size0, n.Text("badge"), n.Text("badgeTone") ?? n.Text("tone"));
                if (n.Text("subtitle") is { } sub) Wrapped(sub, inner, Smaller, UiColor.TextSecondary, FontWeight.Italic);
                if (n.Children.Count > 0) runtime.Gap(Gap);
                DrawAll(n.Children, k, inner, Ident(n));
                if (n.Text("footer") is { } footer)
                {
                    runtime.Gap(Gap);
                    Separator.Build(runtime);
                    runtime.Gap(MathF.Round(Gap * 0.8f));
                    runtime.Markdown("bib-md-" + _ids++, MarkdownDocument.Parse(footer), inner, Smaller);
                }
                runtime.EndParent(card);
                break;
            }
            case "section":
            {
                var section = runtime.BeginParent(Section.Build(runtime, n.Text("title") ?? ""));
                DrawAll(n.Children, k, width, Ident(n));
                runtime.EndParent(section);
                break;
            }
            case "plot":
                Plot(n, width);
                break;
            case "bars":
                Bars(n, width);
                break;
            case "pie":
                Pie(n, width);
                break;
            case "heatmap":
                Heatmap(n, width);
                break;
            case "tabs":
            {
                // The strip, then the panel holding the tab that shows: only its block ran. The label
                // names the tabs (set and value read it), and is not drawn.
                var tabs = n.Children.Where(c => c.Type == "tab").ToList();
                var items = tabs.Select(t => new TabOption(t.Text("title") ?? "", t.Text("title") ?? "")).ToArray();
                var strip = runtime.Tabs("bib-" + n.Key, items, Values.Str(n.Props.GetValueOrDefault("value")));
                Claim(k, strip.Node);
                if (strip.Picked is { } picked) Set(n, picked);
                runtime.Gap(Tabs.PanelGap);
                var panel = runtime.BeginParent(Tabs.Panel(runtime));
                if (tabs.FirstOrDefault(t => Values.Truthy(t.Props.GetValueOrDefault("active"))) is { } shown)
                    DrawAll(shown.Children, k, width - 2f * TabsPanelPad, Ident(shown));
                runtime.EndParent(panel);
                break;
            }
            case "column":
            {
                var col = runtime.BeginParent(runtime.Column().Sized(Size.Fill));
                DrawAll(n.Children, k, width, Ident(n));
                runtime.EndParent(col);
                break;
            }
            case "row" when Values.Truthy(n.Props.GetValueOrDefault("fit")):
            {
                // Each child only as wide as it needs (badges, chips, short texts), wrapping onto
                // a new line where the next would not fit.
                float between = MathF.Round(Gap * 0.8f), used = 0f;
                var row = runtime.BeginParent(runtime.Row());
                foreach (var child in n.Children)
                {
                    float w = Want(child, width) ?? width;
                    if (used > 0f && used + between + w > width)
                    {
                        runtime.EndParent(row);
                        runtime.Gap(MathF.Round(Gap * 0.5f));
                        row = runtime.BeginParent(runtime.Row());
                        used = 0f;
                    }
                    if (used > 0f) runtime.Gap(between);
                    // In a cell of its own, so what it draws in a column (a caption over a control) stays one.
                    var fitCell = runtime.BeginParent(runtime.Column());
                    Draw(child, k, w);
                    runtime.EndParent(fitCell);
                    used += (used > 0f ? between : 0f) + w;
                }
                runtime.EndParent(row);
                break;
            }
            case "grid":
            {
                int columns = Math.Clamp((int)(n.Props.TryGetValue("columns", out var c) && c is not null ? Values.Num(c) : 3), 1, 6);
                float gap = 12f;
                // Fewer columns where the cells would be narrower than what they hold (a phone): a
                // stat as wide as its label and number, anything else MinCell.
                float need = n.Children.Count == 0 ? 0f : n.Children.Max(child => child.Type == "stat" ? Measure(child, width) : MinCell);
                columns = Math.Clamp((int)MathF.Floor((width + gap) / (need + gap)), 1, columns);
                float each = MathF.Floor((width - gap * (columns - 1)) / columns);
                var grid = runtime.BeginParent(runtime.Column().Sized(Size.Fill));
                for (int start = 0; start < n.Children.Count; start += columns)
                {
                    if (start > 0) runtime.Gap(gap);
                    var row = runtime.BeginParent(runtime.Row().Sized(Size.Fill));
                    for (int i = start; i < Math.Min(start + columns, n.Children.Count); i++)
                    {
                        if (i > start) runtime.Gap(gap);
                        var cell = runtime.BeginParent(runtime.Column().Sized(Size.Px(each)));
                        Draw(n.Children[i], k, each);
                        runtime.EndParent(cell);
                    }
                    runtime.EndParent(row);
                }
                runtime.EndParent(grid);
                break;
            }
            case "chips":
                Chips(n.Props.GetValueOrDefault("items") as List<object?> ?? [], n.Text("tone"), width);
                break;
            case "steps":
                Steps(n, k, width);
                break;
            case "step":
                Step(n, k, width, last: true);
                break;
            case "row" when n.Children.Any(c => FixedWidth(c) is not null):
            {
                // A child given a width in px gets it; the others share what is left of their line, each
                // its minWidth at least (a box FillMin, anything else MinRowCell). What does not fit goes onto the next line; a child
                // wider than the whole row keeps its width and runs past the row's edge, visibly.
                float gap = MathF.Round(Gap * 2f);
                var lines = runtime.BeginParent(runtime.Column().Sized(Size.Fill));
                bool firstLine = true;
                foreach (var line in Lines(n.Children, width, gap, c => FixedWidth(c), c => MinWidth(c) ?? (c.Type == "box" ? FillMin : MinRowCell)))
                {
                    if (!firstLine) runtime.Gap(Gap);
                    firstLine = false;
                    var row = runtime.BeginParent(runtime.Row().Sized(Size.Fill));
                    row.AllowOverflowX = true;
                    for (int i = 0; i < line.Count; i++)
                    {
                        if (i > 0) runtime.Gap(gap);
                        var cell = runtime.BeginParent(runtime.Column().Sized(Size.Px(line[i].Width)));
                        Draw(line[i].Node, k, line[i].Width);
                        runtime.EndParent(cell);
                    }
                    runtime.EndParent(row);
                }
                runtime.EndParent(lines);
                break;
            }
            case "row":
            {
                // Side by side, as many to a line as fit at MinRowCell or wider (all of them on a
                // page; on a phone, one under another).
                float gap = MathF.Round(Gap * 2f);
                int count = Math.Max(1, n.Children.Count);
                int across = Math.Clamp((int)MathF.Floor((width + gap) / (MinRowCell + gap)), 1, count);
                float each = (width - gap * (across - 1)) / across;
                var lines = runtime.BeginParent(runtime.Column().Sized(Size.Fill));
                for (int start = 0; start < n.Children.Count; start += across)
                {
                    if (start > 0) runtime.Gap(Gap);
                    var row = runtime.BeginParent(runtime.Row().Sized(Size.Fill));
                    for (int i = start; i < Math.Min(start + across, n.Children.Count); i++)
                    {
                        if (i > start) runtime.Gap(gap);
                        var cell = runtime.BeginParent(runtime.Column().Sized(Size.Px(MathF.Floor(each))));
                        Draw(n.Children[i], k, each);
                        runtime.EndParent(cell);
                    }
                    runtime.EndParent(row);
                }
                runtime.EndParent(lines);
                break;
            }
            case "stat":
            {
                var col = runtime.BeginParent(runtime.Column());
                runtime.Text(n.Text("label") ?? "", Smaller, UiColor.TextSecondary);
                var line = runtime.BeginParent(runtime.Row());
                runtime.Text(Number(n.Props.GetValueOrDefault("value")), Larger, Tone(n, UiColor.TextPrimary), weight: FontWeight.Bold);
                if (n.Text("unit") is { } unit)
                {
                    runtime.Gap(6f);
                    runtime.Text(unit, Smaller, UiColor.TextSecondary);
                }
                if (n.Props.GetValueOrDefault("delta") is double delta)
                {
                    runtime.Gap(10f);
                    var change = runtime.BeginParent(runtime.Column());
                    runtime.Spacer(Size.Px(MathF.Round(runtime.FontInScope(Larger).Size * 1.3f - runtime.FontInScope(Smaller).Size * 1.3f - 2f)));
                    var mark = runtime.BeginParent(runtime.Row());
                    Delta(delta);
                    runtime.EndParent(mark);
                    runtime.EndParent(change);
                }
                runtime.EndParent(line);
                if (n.Props.GetValueOrDefault("spark") is List<object?> spark) Spark(spark, n.Text("tone"), width);
                runtime.EndParent(col);
                break;
            }
            case "progress":
            {
                float max = n.Props.TryGetValue("max", out var m) && m is not null ? (float)Values.Num(m) : 100f;
                Box bar = Progress.Build(runtime, (float)Values.Num(n.Props.GetValueOrDefault("value")), max <= 0 ? 1f : max, n.Text("label") ?? "");
                bar.Sized(Size.Fill);
                break;
            }
            case "kv":
            {
                var col = runtime.BeginParent(runtime.Column().Sized(Size.Fill));
                var pairs = (n.Props.GetValueOrDefault("items") as List<object?> ?? []).OfType<Dictionary<string, object?>>().ToList();
                // The labels take what the longest value leaves, between two fifths and three of the line;
                // each wraps in its column, and so does a value.
                var valueFont = runtime.FontInScope(Size0);
                float longest = pairs.Select(p => Text.Width(runtime, Number(p.GetValueOrDefault("value")), valueFont)).DefaultIfEmpty(0f).Max();
                float labels = MathF.Floor(Math.Clamp(width - longest - KvGap, width * 0.4f, width * 0.6f));
                foreach (var pair in pairs)
                {
                    var row = runtime.BeginParent(runtime.Row().Sized(Size.Fill));
                    var left = runtime.BeginParent(runtime.Column().Sized(Size.Px(labels)));
                    Wrapped(Values.Str(pair.GetValueOrDefault("label")), labels - KvGap, Size0, UiColor.TextSecondary);
                    runtime.EndParent(left);
                    var right = runtime.BeginParent(runtime.Column().Sized(Size.Px(width - labels)));
                    Wrapped(Number(pair.GetValueOrDefault("value")), width - labels, Size0, UiColor.TextPrimary);
                    runtime.EndParent(right);
                    runtime.EndParent(row);
                    runtime.Gap(4f);
                }
                runtime.EndParent(col);
                break;
            }
            case "table":
                Table(n, k, width);
                break;
            case "list":
                List(n, k, width);
                break;
            case "item":
                Item(n, k, width, null);
                break;
            case "button":
            {
                var row = runtime.BeginParent(runtime.Row());
                string label = n.Text("label") ?? "";
                if (n.Text("key") is { } shortcut) label += "  " + KeyCap(shortcut);   // the key that presses it, after its label
                var b = runtime.Button("bib-" + n.Key, label,
                    ghost: n.Text("style") == "ghost", disabled: Values.Truthy(n.Props.GetValueOrDefault("disabled")),
                    emphasis: n.Text("style") switch { "primary" => ButtonEmphasis.Primary, "danger" => ButtonEmphasis.Danger, _ => ButtonEmphasis.None });
                runtime.EndParent(row);
                Claim(k, b.Node);
                if (b.Clicked && n.Key is { } key) Click(k, key);
                break;
            }
            // Segmented, when its options fit side by side; wider than the width, a list of radio buttons.
            case "choice" when n.Text("style") == "segmented" && SegmentsWidth(n) <= width + 1f:
            {
                // Side by side in one outline: the same input, drawn as a segmented control.
                Caption(n);
                var options = n.Props.GetValueOrDefault("options") as List<object?> ?? [];
                var items = options.Select(o => new ToggleOption(Values.Str(o), Values.Str(o))).ToArray();
                var group = runtime.ToggleGroup("bib-" + n.Key, items, Values.Str(n.Props.GetValueOrDefault("value")));
                Claim(k, group.Node);
                if (group.Picked is { } picked)
                    Set(n, options.FirstOrDefault(o => Values.Str(o) == picked) ?? picked);
                break;
            }
            case "choice":
            {
                Caption(n);
                var options = n.Props.GetValueOrDefault("options") as List<object?> ?? [];
                var radio = options.Select(o => new RadioOption(Values.Str(o), Values.Str(o))).ToArray();
                var group = runtime.RadioGroup("bib-" + n.Key, radio, Values.Str(n.Props.GetValueOrDefault("value")));
                Claim(k, group.Node);
                if (group.Picked is { } picked)
                    Set(n, options.FirstOrDefault(o => Values.Str(o) == picked) ?? picked);
                break;
            }
            case "slider":
            {
                float min = (float)Values.Num(n.Props.GetValueOrDefault("min")), max = (float)Values.Num(n.Props.GetValueOrDefault("max") ?? 100d);
                // Floating point unless the block says otherwise: a hundredth of the range, as a power
                // of ten (0.1 on 0–10, 0.01 on 0–1), and the value kept to that many decimals.
                float step = n.Props.TryGetValue("step", out var st) && st is not null ? (float)Values.Num(st) : FineStep(max - min);
                int decimals = step > 0 ? Math.Clamp((int)Math.Ceiling(-Math.Log10(step) - 1e-9), 0, 6) : 4;
                double value = Values.Num(n.Props.GetValueOrDefault("value"));
                // A value off the step's grid (0.1 with step 1) is drawn where its label says: on the grid.
                double snapped = Math.Round(Math.Clamp(step > 0 ? min + Math.Round((value - min) / step) * step : value, min, max), decimals);
                if (snapped != value) { Set(n, snapped); value = snapped; }
                var slider = runtime.Slider("bib-" + n.Key, (float)value, min, max, step,
                    $"{n.Text("label")}: {Math.Round(value, decimals).ToString("F" + decimals, System.Globalization.CultureInfo.InvariantCulture)}");
                slider.Node.Sized(Size.Fill);
                Claim(k, slider.Node);
                double moved = Math.Round(slider.Value, decimals);
                if (Math.Abs(moved - value) > Math.Pow(10, -decimals) / 2) Set(n, moved);
                break;
            }
            case "colour":
            {
                // A swatch and its hex, opening bibui's picker.
                var swatches = (n.Props.GetValueOrDefault("swatches") as List<object?> ?? []).Select(Values.Str).ToList();
                var picker = runtime.ColorPicker("bib-" + n.Key, Values.Str(n.Props.GetValueOrDefault("value")),
                    swatches.Count > 0 ? swatches : null, n.Text("label") ?? "", size: Size0);
                Claim(k, picker.Node);
                if (picker.Changed) Set(n, picker.Value);
                break;
            }
            case "toggle":
            {
                bool on = Values.Truthy(n.Props.GetValueOrDefault("value"));
                var sw = runtime.Switch("bib-" + n.Key, on, n.Text("label") ?? "", maxWidth: width);
                Claim(k, sw.Node);
                if (sw.Clicked) Set(n, !on);
                break;
            }
            case "checklist":
            {
                Caption(n);
                var options = n.Props.GetValueOrDefault("options") as List<object?> ?? [];
                var ticked = n.Props.GetValueOrDefault("value") as List<object?> ?? [];
                for (int i = 0; i < options.Count; i++)
                {
                    var o = options[i];
                    bool on = ticked.Any(t => Values.Eq(t, o));
                    var box = runtime.Checkbox($"bib-{n.Key}-{i}", on, Values.Str(o));
                    Claim(k, box.Node);
                    if (box.Clicked)
                        Set(n, options.Where(x => Values.Eq(x, o) ? !on : ticked.Any(t => Values.Eq(t, x))).ToList());
                    runtime.Gap(6f);
                }
                break;
            }
            case "input":
                Input(n, k, width);
                break;
        }
    }

    // How wide a segmented choice's options are side by side.
    private float SegmentsWidth(FrameNode n) =>
        (n.Props.GetValueOrDefault("options") as List<object?> ?? []).Sum(o => Text.Width(runtime, Values.Str(o), runtime.FontInScope(Size0)) + 26f) + 4f;

    private static float FineStep(float range) =>
        range <= 0f ? 0f : (float)Math.Pow(10, Math.Floor(Math.Log10(range / 100.0)));

    // How wide a node drawn in a fitted row will be: what can be measured before it is drawn.
    private float Measure(FrameNode n, float width)
    {
        var font = runtime.FontInScope(Size0);
        return n.Type switch
        {
            "badge" => Text.Width(runtime, n.Text("text") ?? "", runtime.FontInScope(ComponentSize.Sm)) + 24f,
            "text" => MathF.Min(width, Text.Width(runtime, n.Text("text") ?? "", font)),
            "chips" => MathF.Min(width, (n.Props.GetValueOrDefault("items") as List<object?> ?? [])
                .Sum(i => Text.Width(runtime, Values.Str(i), runtime.FontInScope(Smaller, FontSlot.Mono)) + 20f)),
            "button" => Text.Width(runtime, (n.Text("label") ?? "") + (n.Text("key") is { } cap ? "  " + KeyCap(cap) : ""), font) + 40f,
            "choice" when n.Text("style") == "segmented" => MathF.Min(width, MathF.Max(
                Text.Width(runtime, n.Text("label") ?? "", runtime.FontInScope(Smaller)),
                (n.Props.GetValueOrDefault("options") as List<object?> ?? []).Sum(o => Text.Width(runtime, Values.Str(o), font) + 26f) + 4f)),
            // Its label, or its number (bold, a little wider than measured) and unit.
            "stat" => MathF.Min(width, MathF.Max(Text.Width(runtime, n.Text("label") ?? "", runtime.FontInScope(Smaller)),
                Text.Width(runtime, Number(n.Props.GetValueOrDefault("value")), runtime.FontInScope(Larger)) * 1.08f
                + (n.Text("unit") is { } unit ? 6f + Text.Width(runtime, unit, runtime.FontInScope(Smaller)) : 0f)
                + (n.Props.GetValueOrDefault("delta") is double delta
                    ? 10f + MathF.Round(runtime.FontInScope(Smaller).Size * 0.6f) + 4f + Text.Width(runtime, DeltaText(delta), runtime.FontInScope(Smaller)) * 1.08f : 0f)) + 4f),
            _ => width,   // anything else takes a line of its own
        };
    }

    // ── primitives (bibdoc's docs/two-layers.md) ─────────────────────────────

    // A colour: a role, a role faded ("accent@0.15"), or a hex.
    private static UiColor Colour(object? value, UiColor otherwise)
    {
        if (value is UiColor eased) return eased;   // a colour on its way (Motion.cs)
        if (value is not string s) return otherwise;
        int at = s.LastIndexOf('@');
        if (at > 0 && float.TryParse(s[(at + 1)..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var alpha))
            return Colour(s[..at], otherwise).WithAlpha(alpha);
        if (s.StartsWith('#')) return UiColor.Hex(s);
        return s switch
        {
            "ink" => UiColor.TextPrimary,
            "muted" or "neutral" => UiColor.TextSecondary,
            "faint" => UiColor.TextDisabled,
            "surface" => UiColor.Surface,
            "raised" => UiColor.SurfaceRaised,
            "border" => UiColor.Border,
            "accent" or "primary" => UiColor.Accent,
            "success" => UiColor.Success,
            "warning" => Amber,
            "danger" => UiColor.Destructive,
            _ => otherwise,
        };
    }

    private static float? Px(object? v) => v is double d ? (float)d : null;

    // padding: n, [vertical, horizontal] or [top, right, bottom, left]
    private static EdgeInsets Insets(object? v) => v switch
    {
        double d => EdgeInsets.All((float)d),
        List<object?> { Count: 2 } l => new EdgeInsets((float)Values.Num(l[0]), (float)Values.Num(l[1]), (float)Values.Num(l[0]), (float)Values.Num(l[1])),
        List<object?> { Count: 4 } l => new EdgeInsets((float)Values.Num(l[0]), (float)Values.Num(l[1]), (float)Values.Num(l[2]), (float)Values.Num(l[3])),
        _ => EdgeInsets.Zero,
    };

    private FontSize TextSize(object? v)
    {
        float body = runtime.FontInScope(Size0).Size;
        return v switch
        {
            double px => FontSize.Px((float)px),
            "sm" => Smaller,
            "lg" => Larger,
            "xl" => FontSize.Px(MathF.Round(body * 1.35f)),
            "2xl" => FontSize.Px(MathF.Round(body * 1.7f)),
            "display" => FontSize.Px(MathF.Round(body * 2.4f)),
            _ => Size0,
        };
    }

    private static FontSlot Face(string? v) => v switch
    {
        "mono" => FontSlot.Mono, "display" => FontSlot.Display, "heading" => FontSlot.Heading, _ => FontSlot.Body,
    };

    private static FontWeight Weight(string? v) => v switch
    {
        "bold" => FontWeight.Bold, "italic" => FontWeight.Italic, "boldItalic" => FontWeight.BoldItalic, _ => FontWeight.Regular,
    };

    private string TextOf(FrameNode n) => n.Props.GetValueOrDefault("caps") is true ? (n.Text("text") ?? "").ToUpperInvariant() : n.Text("text") ?? "";

    // A text wrapped at the width, its lines one under another (in a column of their own, so a row or a
    // centring around it sets them as one); with align: center or end, or in a box that centres, each
    // line is set in the middle or at the end of the width.
    private void TextNode(FrameNode n, float width, bool centred = false)
    {
        var size = TextSize(n.Props.GetValueOrDefault("size"));
        var face = Face(n.Text("face"));
        var weight = Weight(n.Text("weight"));
        var color = Colour(n.Props.GetValueOrDefault("color"), Tone(n, UiColor.TextPrimary));
        string text = TextOf(n);
        if (n.Props.GetValueOrDefault("wrap") is false) { runtime.Text(text, size, color, face, weight); return; }
        string align = n.Text("align") ?? (centred ? "center" : "start");
        bool placed = align is "center" or "end" or "right";
        var font = runtime.FontInScope(size, face);
        width = MathF.Max(20f, width);
        var lines = runtime.BeginParent(runtime.Column().Sized(placed ? Size.Px(width) : Size.Fit, Size.Fit));
        foreach (var para in text.Split('\n'))
        {
            if (para.Length == 0) { runtime.Text(" ", size, color, face, weight); continue; }
            foreach (var line in Text.Lines(runtime, para, font, width))
            {
                if (!placed) { runtime.Text(para.AsSpan(line.Start, line.End - line.Start), size, color, face, weight); continue; }
                var row = runtime.BeginParent(runtime.Row().Sized(Size.Px(width), Size.Fit));
                runtime.Spacer(Size.Fill);
                runtime.Text(para.AsSpan(line.Start, line.End - line.Start), size, color, face, weight);
                if (align == "center") runtime.Spacer(Size.Fill);
                runtime.EndParent(row);
            }
        }
        runtime.EndParent(lines);
    }

    // How wide a node wants to be, or null for one that takes what is left (width: fill, prose).
    private float? Want(FrameNode n, float room)
    {
        switch (n.Type)
        {
            case "box":
            {
                if (Px(n.Props.GetValueOrDefault("width")) is { } px) return px;
                if (n.Text("width") == "fill") return null;
                var pad = Insets(n.Props.GetValueOrDefault("padding"));
                float gap = Px(n.Props.GetValueOrDefault("gap")) ?? 0f;
                var kids = n.Children.Where(c => c.Type != "layer" && !Floats(c)).ToList();
                float inner = 0f;
                if (n.Text("dir") == "row")
                {
                    foreach (var c in kids) { if (Want(c, room) is not { } w) return null; inner += w; }
                    inner += gap * Math.Max(0, kids.Count - 1);
                }
                else foreach (var c in kids) { if (Want(c, room) is not { } w) return null; inner = MathF.Max(inner, w); }
                return MathF.Min(room, inner + pad.Left + pad.Right);
            }
            case "text":
                if (n.Text("width") == "fill") return null;
                // Measured in the regular weight: bold is drawn a little wider.
                return MathF.Min(room, Text.Width(runtime, TextOf(n), runtime.FontInScope(TextSize(n.Props.GetValueOrDefault("size")), Face(n.Text("face"))))
                    * (Weight(n.Text("weight")) == FontWeight.Regular ? 1f : 1.08f) + 1f);
            case "markdown" or "table" or "list" or "card" or "grid" or "steps" or "kv" or "tabs" or "plot" or "bars" or "pie" or "heatmap" or "input":
                return null;
            default:
                return Measure(n, room);
        }
    }

    private void DrawBox(FrameNode n, int k, float width)
    {
        // Motion: a box that is appearing or leaving, turning over, or easing to new props (Motion.cs).
        (float Progress, bool Leaving, string Key)? presence = _presenceOf.Remove(n, out var pr) ? pr : null;
        string ident = presence?.Key ?? Ident(n);
        var tr = TransitionOf(n.Props.GetValueOrDefault("animate"));
        n = Turned(n, ident, out float scaleX);
        bool wasGhost = _ghost;
        _ghost |= presence is { Leaving: true };
        string? boxId = _ghost ? null : n.Key;

        var pad = Insets(n.Props.GetValueOrDefault("padding"));
        float w = Px(n.Props.GetValueOrDefault("width")) ?? width;
        float? h = Px(n.Props.GetValueOrDefault("height"));
        if (tr is { } sized)
        {
            if (Px(n.Props.GetValueOrDefault("width")) is not null) w = runtime.Animate("bib:" + ident, "width", w, sized);
            if (h is { } hh0) h = runtime.Animate("bib:" + ident, "height", hh0, sized);
        }
        bool row = n.Text("dir") == "row";
        float gap = Px(n.Props.GetValueOrDefault("gap")) ?? 0f;
        Box box = boxId is not null ? runtime.BoxFromId("bibbox-" + boxId, InteractionKinds.LeftPress | InteractionKinds.RightPress) : runtime.Box();
        bool wraps = row && n.Props.GetValueOrDefault("wrap") is true;
        // A row that wraps is a column of lines, each a row.
        box.ChildLayoutAxis = row && !wraps ? LayoutAxis.X : LayoutAxis.Y;
        box.SizeX = SemanticSize.Pixels(w);
        box.SizeY = h is { } hh ? SemanticSize.Pixels(hh) : SemanticSize.ChildrenSum();
        // at: [x, y] floats it at that place in its parent, out of the flow, over what is there.
        if (n.Props.GetValueOrDefault("at") is List<object?> { Count: 2 } at && at[0] is double ax && at[1] is double ay)
        {
            box.FloatingX = box.FloatingY = true;
            box.FixedPosition = new Vector2((float)ax, (float)ay);
        }
        box.Style.Padding = pad;
        if (n.Props.GetValueOrDefault("background") is { } bg) box.Style.BackgroundColor = Colour(bg, UiColor.SurfaceRaised);
        if (Px(n.Props.GetValueOrDefault("border")) is { } bw && bw > 0f)
        {
            box.Style.BorderWidth = bw;
            box.Style.BorderColor = Colour(n.Props.GetValueOrDefault("borderColor") ?? "border", UiColor.Border);
        }
        if (Px(n.Props.GetValueOrDefault("radius")) is { } r) box.Style.CornerRadius = r;
        if (Px(n.Props.GetValueOrDefault("opacity")) is { } o) box.Style.Opacity = o;
        Move(n, ident, box, tr, scaleX, presence);
        // Given a size in px, a box keeps to it: what does not fit is cut off, not laid over its neighbours.
        if (Px(n.Props.GetValueOrDefault("width")) is not null || h is not null) box.Claims |= InteractionKinds.Clip;
        // A layer draws under the children, clipped to the box: w and h are the box's size.
        if (n.Children.FirstOrDefault(c => c.Type == "layer") is { } layer)
        {
            layer = EasedLayer(layer, ident);
            var labels = Labels(layer);
            box.Draw = (b, ctx) => DrawLayer(ctx, layer, labels, b.Extent.X, b.Extent.Y);
        }
        var all = Presented(ident, n.Children.Where(c => c.Type != "layer").ToList());
        var kids = all.Where(c => !Floats(c)).ToList();
        float inner = MathF.Max(0f, w - pad.Left - pad.Right);
        // Centring needs a size of its own along that axis: only a box given one centres.
        bool along = n.Text("justify") == "center" && (row ? Px(n.Props.GetValueOrDefault("width")) : h) is not null;
        bool across = n.Text("align") == "center" && (row ? h : (float?)w) is not null;
        if (boxId is { } around) _around.Add((around, box));
        using (runtime.Parent(box))
        {
            if (along) runtime.Spacer(Size.Fill);
            if (row) RowOf(kids, k, inner, gap, n.Props.GetValueOrDefault("wrap") is true, across, n.Text("justify") == "between");
            else
            {
                // Each child centred on its own: one centring around them all would set them in a row.
                for (int i = 0; i < kids.Count; i++)
                {
                    if (i > 0) runtime.Gap(gap);
                    // A text wraps at the box's width, each of its lines centred.
                    if (across && kids[i].Type == "text") { TextNode(kids[i], inner, centred: true); continue; }
                    if (across) runtime.BeginCenterAcross();
                    Draw(kids[i], k, across ? Want(kids[i], inner) ?? inner : inner);
                    if (across) runtime.EndCenterAcross();
                }
            }
            if (along) runtime.Spacer(Size.Fill);
            // The boxes placed with at:, last, so they are drawn over the rest.
            foreach (var f in all.Where(Floats)) Draw(f, k, Want(f, inner) ?? inner);
        }
        // After its children: a box with an id inside it asks about the pointer first, so a press on it is its own.
        if (boxId is not null)
        {
            _around.RemoveAt(_around.Count - 1);
            Pointer(n, k, box);
        }
        _ghost = wasGhost;
    }

    // A box placed with at: is out of the flow of its parent.
    private static bool Floats(FrameNode n) => n.Type == "box" && n.Props.GetValueOrDefault("at") is List<object?> { Count: 2 };

    // A box's width when it is given in px.
    private static float? FixedWidth(FrameNode n) => n.Type == "box" ? Px(n.Props.GetValueOrDefault("width")) : null;

    // The narrowest a child that fills is set beside others, when it says (minWidth:).
    private static float? MinWidth(FrameNode n) => n.Type == "box" ? Px(n.Props.GetValueOrDefault("minWidth")) : null;

    // Children set in lines across `room`: each line holds what fits, a child of a width of its own (fixed) at
    // that width and the others at their least (least) at first; then the others share what the line has
    // left, evenly. A child wider than the room is a line of its own, at its own width.
    private static List<List<(FrameNode Node, float Width)>> Lines(List<FrameNode> kids, float room, float gap,
        Func<FrameNode, float?> fixedWidth, Func<FrameNode, float> least)
    {
        var lines = new List<List<(FrameNode Node, float Width)>>();
        var line = new List<FrameNode>();
        float used = 0f;
        void Close()
        {
            if (line.Count == 0) return;
            float set = line.Sum(c => fixedWidth(c) ?? 0f) + gap * (line.Count - 1);
            int flexible = line.Count(c => fixedWidth(c) is null);
            float share = flexible == 0 ? 0f : MathF.Max(0f, (room - set) / flexible);
            lines.Add(line.Select(c => (c, MathF.Floor(fixedWidth(c) ?? MathF.Max(MathF.Min(least(c), room), share)))).ToList());
            line = [];
            used = 0f;
        }
        foreach (var c in kids)
        {
            float w = fixedWidth(c) ?? MathF.Min(least(c), room);
            if (line.Count > 0 && used + gap + w > room) Close();
            used += (line.Count > 0 ? gap : 0f) + w;
            line.Add(c);
        }
        Close();
        return lines;
    }

    // A row's children: each as wide as it wants, the ones that fill sharing what is left.
    private void RowOf(List<FrameNode> kids, int k, float inner, float gap, bool wrap, bool across, bool between = false)
    {
        var wants = kids.Select(c => Want(c, inner)).ToList();
        if (wrap)
        {
            // Lines of what fits. A child that fills is set at its minWidth (FillMin by default) to find its
            // line, and then takes what that line has left.
            var want = kids.Select((c, i) => (c, wants[i])).ToDictionary(x => x.c, x => x.Item2);
            bool firstLine = true;
            foreach (var set in Lines(kids, inner, gap, c => want[c], c => MinWidth(c) ?? FillMin))
            {
                if (!firstLine) runtime.Gap(gap);
                firstLine = false;
                var line = runtime.BeginParent(runtime.Row());
                for (int i = 0; i < set.Count; i++)
                {
                    if (i > 0) runtime.Gap(gap);
                    Cell(set[i].Node, k, set[i].Width, across);
                }
                runtime.EndParent(line);
            }
            return;
        }
        float fixedWidth = wants.Sum(x => x ?? 0f) + gap * Math.Max(0, kids.Count - 1);
        int fills = wants.Count(x => x is null);
        float share = fills == 0 ? 0f : MathF.Max(0f, (inner - fixedWidth) / fills);
        // Spread out: what no child fills goes between them.
        if (between && fills == 0 && kids.Count > 1) gap += MathF.Max(0f, inner - fixedWidth) / (kids.Count - 1);
        for (int i = 0; i < kids.Count; i++)
        {
            if (i > 0) runtime.Gap(gap);
            Cell(kids[i], k, MathF.Floor(wants[i] ?? share), across);
        }
    }

    private void Cell(FrameNode child, int k, float width, bool across)
    {
        // Centred across a row: the cell is as tall as the row, and the child sits in its middle.
        var cell = runtime.BeginParent(runtime.Column().Sized(Size.Px(width), across ? Size.Fill : Size.Fit));
        if (across) runtime.Spacer(Size.Fill);
        Draw(child, k, width);
        if (across) runtime.Spacer(Size.Fill);
        runtime.EndParent(cell);
    }

    // A layer's labels, measured while the frame is built: each one's font and width.
    private Dictionary<FrameNode, (Bibui.Text.Font Font, float Width)> Labels(FrameNode layer)
    {
        var labels = new Dictionary<FrameNode, (Bibui.Text.Font, float)>();
        foreach (var s in layer.Children.Where(c => c.Type == "label"))
        {
            var font = runtime.FontInScope(TextSize(s.Props.GetValueOrDefault("size")), Face(s.Text("face")));
            labels[s] = (font, Text.Width(runtime, s.Text("text") ?? "", font));
        }
        return labels;
    }

    // A layer's shapes, in the box's own coordinates, clipped to the box. A label's y is the
    // middle of its line, and its x the start, middle or end of it by its align.
    private static void DrawLayer(DrawContext ctx, FrameNode layer, Dictionary<FrameNode, (Bibui.Text.Font Font, float Width)> labels, float w, float h)
    {
        float sc = ctx.Scale;
        float At(object? v, bool vertical) => v switch
        {
            double d => (float)d,
            Dim dim => (float)(dim.K * (dim.Axis == "w" ? w : h) + dim.C),
            _ => 0f,
        };
        ctx.PushClip(new RectF(0f, 0f, w * sc, h * sc));
        foreach (var s in layer.Children)
        {
            var p = s.Props;
            float stroke = (float)(p.GetValueOrDefault("width") is double sw ? sw : 1d) * sc;
            // A line is stroked in the border's colour unless it says otherwise, an arc in the accent,
            // a path when it is not filled; the rest only when given a colour.
            Rgba? edge = p.ContainsKey("color") || s.Type is "line" or "arc" || (s.Type == "path" && !p.ContainsKey("fill"))
                ? ctx.Resolve(Colour(p.GetValueOrDefault("color") ?? (s.Type == "arc" ? "accent" : "border"), UiColor.Border)) : null;
            Rgba? fill = p.GetValueOrDefault("fill") is { } f ? ctx.Resolve(Colour(f, UiColor.SurfaceRaised)) : null;
            var paint = new Paint
            {
                Fill = fill, Stroke = edge, StrokeWidth = stroke,
                Dash = p.GetValueOrDefault("dash") is List<object?> { Count: 2 } dash && dash[0] is double on && dash[1] is double off && on > 0 && off > 0
                    ? new DashPattern((float)on * sc, (float)off * sc) : null,
                Cap = Values.Str(p.GetValueOrDefault("cap")) switch { "round" => StrokeCap.Round, "square" => StrokeCap.Square, _ => StrokeCap.Butt },
                Join = StrokeJoin.Round,
            };
            switch (s.Type)
            {
                case "line":
                    ctx.DrawLine(At(p.GetValueOrDefault("x1"), false) * sc, At(p.GetValueOrDefault("y1"), true) * sc,
                        At(p.GetValueOrDefault("x2"), false) * sc, At(p.GetValueOrDefault("y2"), true) * sc, paint);
                    break;
                case "rect":
                {
                    // RectF is left, top, right, bottom: w and h are added to x and y.
                    float x = At(p.GetValueOrDefault("x"), false), y = At(p.GetValueOrDefault("y"), true);
                    ctx.DrawRect(new RectF(x * sc, y * sc, (x + At(p.GetValueOrDefault("w"), false)) * sc, (y + At(p.GetValueOrDefault("h"), true)) * sc),
                        (float)(p.GetValueOrDefault("radius") is double rr ? rr : 0d) * sc, paint);
                    break;
                }
                case "circle":
                {
                    float cx = At(p.GetValueOrDefault("cx"), false) * sc, cy = At(p.GetValueOrDefault("cy"), true) * sc;
                    float r = At(p.GetValueOrDefault("r"), false) * sc;
                    var path = ctx.ScratchPath();
                    path.MoveTo(cx + r, cy);
                    path.ArcTo(r, r, 0f, false, true, cx - r, cy);
                    path.ArcTo(r, r, 0f, false, true, cx + r, cy);
                    path.Close();
                    ctx.DrawPath(path, paint);
                    break;
                }
                case "arc":
                {
                    // Turns from the top, clockwise: arc(cx, cy, r, 0, 0.75) is three quarters of a ring.
                    double from = Values.Num(p.GetValueOrDefault("from")), to = Values.Num(p.GetValueOrDefault("to"));
                    if (!double.IsFinite(from) || !double.IsFinite(to) || from == to) break;
                    if (Math.Abs(to - from) > 1) to = from + Math.Sign(to - from);
                    var path = ctx.ScratchPath();
                    Around(path, At(p.GetValueOrDefault("cx"), false) * sc, At(p.GetValueOrDefault("cy"), true) * sc,
                        At(p.GetValueOrDefault("r"), false) * sc, from, to, move: true);
                    ctx.DrawPath(path, paint with { Fill = null });
                    break;
                }
                case "path" when p.GetValueOrDefault("points") is List<object?> pts:
                {
                    var at = pts.OfType<List<object?>>().Select(pt => (X: At(pt[0], false) * sc, Y: At(pt[1], true) * sc)).ToList();
                    if (at.Count < 2) break;
                    bool closed = Values.Truthy(p.GetValueOrDefault("closed"));
                    string arrow = closed ? "" : Values.Str(p.GetValueOrDefault("arrow"));
                    // An arrowhead, as long as four strokes and seven px at least, in the stroke's
                    // colour; the line stops short of its tip so a wide stroke does not show past it.
                    float head = MathF.Max(7f * sc, stroke * 4f);
                    var tips = new List<((float X, float Y) Tip, (float X, float Y) From)>();
                    if (arrow is "end" or "both") tips.Add((at[^1], at[^2]));
                    if (arrow is "start" or "both") tips.Add((at[0], at[1]));
                    (float, float) Back((float X, float Y) tip, (float X, float Y) from, float by)
                    {
                        float dx = tip.X - from.X, dy = tip.Y - from.Y, len = MathF.Max(1e-3f, MathF.Sqrt(dx * dx + dy * dy));
                        return (tip.X - dx / len * by, tip.Y - dy / len * by);
                    }
                    var line = new List<(float X, float Y)>(at);
                    if (arrow is "end" or "both") line[^1] = Back(at[^1], at[^2], head * 0.7f);
                    if (arrow is "start" or "both") line[0] = Back(at[0], at[1], head * 0.7f);
                    var path = ctx.ScratchPath();
                    path.MoveTo(line[0].X, line[0].Y);
                    foreach (var pt in line.Skip(1)) path.LineTo(pt.X, pt.Y);
                    if (closed) path.Close();
                    ctx.DrawPath(path, paint);
                    if (edge is { } tone)
                        foreach (var (tip, from) in tips)
                        {
                            float dx = tip.X - from.X, dy = tip.Y - from.Y, len = MathF.Max(1e-3f, MathF.Sqrt(dx * dx + dy * dy));
                            float ux = dx / len, uy = dy / len, half = head * 0.5f;
                            float bx = tip.X - ux * head, by = tip.Y - uy * head;
                            var tri = ctx.ScratchPath();
                            tri.MoveTo(tip.X, tip.Y);
                            tri.LineTo(bx - uy * half, by + ux * half);
                            tri.LineTo(bx + uy * half, by - ux * half);
                            tri.Close();
                            ctx.DrawPath(tri, new Paint { Fill = tone });
                        }
                    break;
                }
                case "label" when labels.TryGetValue(s, out var label):
                {
                    float x = At(p.GetValueOrDefault("x"), false), y = At(p.GetValueOrDefault("y"), true);
                    x -= s.Text("align") switch { "center" => label.Width / 2f, "end" or "right" => label.Width, _ => 0f };
                    var m = ctx.FontMetrics(label.Font);
                    ctx.DrawText(s.Text("text") ?? "", x * sc, y * sc - (m.Descent - m.Ascent) / 2f, label.Font,
                        ctx.Resolve(Colour(p.GetValueOrDefault("color") ?? "ink", UiColor.TextPrimary)));
                    break;
                }
            }
        }
        ctx.PopClip();
    }

    // A title wrapped at the width, with a badge at the right of its first line, or above the
    // title when the badge would take more than a third of the line.
    private void TitleLine(string title, float width, ComponentSize size, string? badge, string? tone, FontWeight weight = FontWeight.Bold)
    {
        if (badge is null) { Wrapped(title, width, size, UiColor.TextPrimary, weight); return; }
        float badgeWidth = Text.Width(runtime, badge, runtime.FontInScope(ComponentSize.Sm)) + 24f;
        if (badgeWidth > width / 3f)
        {
            var above = runtime.BeginParent(runtime.Row());
            Pill(badge, tone);
            runtime.EndParent(above);
            runtime.Gap(6f);
            Wrapped(title, width, size, UiColor.TextPrimary, weight);
            return;
        }
        var line = runtime.BeginParent(runtime.Row().Sized(Size.Fill));
        var left = runtime.BeginParent(runtime.Column().Sized(Size.Px(MathF.Max(60f, width - badgeWidth - 10f))));
        Wrapped(title, MathF.Max(60f, width - badgeWidth - 10f), size, UiColor.TextPrimary, weight);
        runtime.EndParent(left);
        runtime.Spacer(Size.Fill);
        Pill(badge, tone);
        runtime.EndParent(line);
    }

    // Chips: small pills in the code face, wrapped onto as many lines as they need.
    private void Chips(List<object?> items, string? tone, float width)
    {
        var font = runtime.FontInScope(Smaller, FontSlot.Mono);
        const float PadX = 7f, Between = 6f;
        var (ink, fill, edge) = tone switch
        {
            "warning" => (Amber, Amber.WithAlpha(0.10f), (UiColor?)Amber.WithAlpha(0.8f)),
            "muted" => (UiColor.TextSecondary, UiColor.SurfaceRaised, (UiColor?)null),
            "success" or "danger" or "accent" or "primary" => (ChipInk(tone), ChipInk(tone).WithAlpha(0.14f), (UiColor?)null),
            _ => (Violet, Violet.WithAlpha(0.14f), (UiColor?)null),
        };
        float used = 0f;
        var line = runtime.BeginParent(runtime.Row());
        foreach (var item in items)
        {
            string text = Values.Str(item);
            float w = Text.Width(runtime, text, font) + 2 * PadX;
            if (used > 0f && used + Between + w > width)
            {
                runtime.EndParent(line);
                runtime.Gap(Between);
                line = runtime.BeginParent(runtime.Row());
                used = 0f;
            }
            if (used > 0f) runtime.Gap(Between);
            Box chip = runtime.Box();
            chip.ChildLayoutAxis = LayoutAxis.X;
            chip.SizeX = chip.SizeY = SemanticSize.ChildrenSum();
            chip.Style.Padding = EdgeInsets.Symmetric(horizontal: PadX, vertical: 2f);
            chip.Style.CornerRadius = 5f;
            chip.Style.BackgroundColor = fill;
            if (edge is { } e) { chip.Style.BorderColor = e; chip.Style.BorderWidth = 1f; }
            using (runtime.Parent(chip)) runtime.Text(text, Smaller, ink, FontSlot.Mono);
            used += (used > 0f ? Between : 0f) + w;
        }
        runtime.EndParent(line);
    }

    // References read as code, in violet.
    private static readonly UiColor Violet = UiColor.Hex("#B8A4F0");

    private static UiColor ChipInk(string? tone) => tone switch
    {
        "success" => UiColor.Success,
        "danger" => UiColor.Destructive,
        _ => UiColor.Accent,
    };

    // Steps: each step's number in a circle of its tone, joined to the next by a line, and a
    // phase's label above its first step.
    private void Steps(FrameNode n, int k, float width)
    {
        var col = runtime.BeginParent(runtime.Column().Sized(Size.Fill));
        string? phase = null;
        var steps = n.Children;
        for (int i = 0; i < steps.Count; i++)
        {
            var s = steps[i];
            if (s.Type != "step") { Draw(s, k, width); continue; }
            if (s.Text("phase") is { } p && p != phase)
            {
                if (i > 0) runtime.Gap(Gap);
                Eyebrow(p, UiColor.Accent);
                runtime.Gap(MathF.Round(Gap * 0.6f));
                phase = p;
            }
            bool last = i == steps.Count - 1 || steps[i + 1].Text("phase") is { } next && next != phase;
            Step(s, k, width, last);
        }
        runtime.EndParent(col);
    }

    private const float Rail = 30f, Dot = 24f;

    private void Step(FrameNode n, int k, float width, bool last)
    {
        var tone = n.Text("tone");
        UiColor ring = tone is null or "neutral" or "muted" ? UiColor.TextSecondary : Tone(n, UiColor.TextSecondary);
        var row = runtime.BeginParent(runtime.Row().Sized(Size.Fill));
        if (!last)
        {
            // The line to the next step, under the circle. The space before the next step is the
            // row's own padding, so the line never leaves the row: the renderer repaints a box's
            // rectangle, and anything drawn outside it is left on the screen when the page scrolls.
            row.Style.Padding = new EdgeInsets(0f, 0f, Gap, 0f);
            row.Draw = (b, ctx) =>
            {
                float sc = ctx.Scale, x = Dot / 2f * sc;
                var path = ctx.ScratchPath();
                path.MoveTo(x, (Dot + 4f) * sc);
                path.LineTo(x, b.Extent.Y * sc);
                ctx.DrawPath(path, new Paint { Stroke = ctx.Resolve(UiColor.Border), StrokeWidth = 1.5f * sc });
            };
        }
        // The number in its circle.
        var rail = runtime.BeginParent(runtime.Column().Sized(Size.Px(Rail)));
        Box circle = runtime.Box();
        circle.Sized(Size.Px(Dot), Size.Px(Dot));
        circle.ChildLayoutAxis = LayoutAxis.X;
        circle.Style.CornerRadius = Dot / 2f;
        circle.Style.BorderWidth = 1.5f;
        circle.Style.BorderColor = ring;
        circle.Style.BackgroundColor = ring.WithAlpha(0.12f);
        using (runtime.Parent(circle))
        {
            runtime.Spacer(Size.Fill);
            runtime.BeginCenterAcross();
            runtime.Text(Number(n.Props.GetValueOrDefault("n")), ComponentSize.Sm, ring, weight: FontWeight.Bold);
            runtime.EndCenterAcross();
            runtime.Spacer(Size.Fill);
        }
        runtime.EndParent(rail);
        runtime.Gap(10f);
        float inner = width - Rail - 10f;
        var body = runtime.BeginParent(runtime.Column().Sized(Size.Px(inner)));
        body.Style.Padding = EdgeInsets.Symmetric(horizontal: 0f, vertical: 2f);
        TitleLine(n.Text("title") ?? "", inner, Size0, n.Text("status"), tone);
        if (n.Text("who") is { } who) Wrapped(who, inner, Smaller, UiColor.TextSecondary);
        if (n.Children.Count > 0) runtime.Gap(MathF.Round(Gap * 0.6f));
        DrawAll(n.Children, k, inner, Ident(n));
        runtime.EndParent(body);
        runtime.EndParent(row);
    }

    // A small label in capitals above what it names: a phase, a section.
    private void Eyebrow(string text, UiColor color) =>
        runtime.Text(text.ToUpperInvariant(), Smaller, color, FontSlot.Mono);

    private void Caption(FrameNode n)
    {
        if (n.Text("label") is not { } label) return;
        runtime.Text(label, Smaller, UiColor.TextSecondary);
        runtime.Gap(4f);
    }

    // What went wrong in a block, in a red box, wrapped at its width.
    private void Problem(string message, float width)
    {
        runtime.Gap(6f);
        var box = runtime.BeginParent(runtime.Column().Sized(Size.Fill));
        box.Style.Padding = EdgeInsets.All(10f);
        box.Style.CornerRadius = 6f;
        box.Style.BorderWidth = 1f;
        box.Style.BorderColor = UiColor.Destructive;
        Wrapped(message, width - 22f, Smaller, UiColor.Destructive, family: FontSlot.Mono);
        runtime.EndParent(box);
    }

    // Text wrapped at the width, in the size and colour given.
    private void Wrapped(string text, float width, ComponentSize size, UiColor color, FontWeight weight = FontWeight.Regular,
        FontSlot? family = null)
    {
        var font = runtime.FontInScope(size, family);
        foreach (var para in text.Split('\n'))
        {
            if (para.Length == 0) { runtime.Text(" ", size, color); continue; }
            foreach (var line in Text.Lines(runtime, para, font, MathF.Max(40f, width)))
                runtime.Text(line.End > line.Start ? para.AsSpan(line.Start, line.End - line.Start) : " ", size, color, family, weight: weight);
        }
    }

    private static string Number(object? v) => v switch
    {
        double d when Math.Abs(d) >= 1000 && d == Math.Floor(d) => d.ToString("N0", System.Globalization.CultureInfo.InvariantCulture),
        double d => Values.NumStr(Math.Round(d, 4)),
        _ => Values.Str(v),
    };

    private void Table(FrameNode n, int k, float width)
    {
        var columns = n.Props.GetValueOrDefault("columns") as List<object?> ?? [];
        var rows = n.Props.GetValueOrDefault("rows") as List<object?> ?? [];
        int count = Math.Max(1, Math.Max(columns.Count, rows.OfType<List<object?>>().Select(r => r.Count).DefaultIfEmpty(0).Max()));
        // widths: [1, 2, 2] shares the width in proportion; without it, equally.
        var weights = (n.Props.GetValueOrDefault("widths") as List<object?> ?? []).Select(w => Math.Max(0.1, Values.Num(w))).ToList();
        while (weights.Count < count) weights.Add(weights.Count == 0 ? 1 : weights.Average());
        float[] cellWidths = ColumnWidths(columns, rows, count, weights, width, out float tableWidth);
        var align = n.Props.GetValueOrDefault("align") as List<object?> ?? [];
        bool End(int i) => Values.Str(align.ElementAtOrDefault(i)) == "end";
        // A table that sorts or pages is an input (the interpreter sorted and cut its rows): {sort, desc, page}.
        var st = n.Key is null ? null : n.Props.GetValueOrDefault("value") as Dictionary<string, object?>;
        bool sortable = st is not null && Values.Truthy(n.Props.GetValueOrDefault("sort"));
        int? by = st?.GetValueOrDefault("sort") is double c ? (int)c : null;
        bool desc = Values.Truthy(st?.GetValueOrDefault("desc"));
        int page = st?.GetValueOrDefault("page") is double p ? (int)p : 1;
        int pages = n.Props.GetValueOrDefault("pages") is double ps ? (int)ps : 1;
        const float CellGap = 12f;   // room between a cell's text and the next column
        Box? sideways = null;
        if (tableWidth > width)
        {
            sideways = runtime.BoxFromId($"bib-tscroll-{n.Key ?? k + "-" + _ids++}");
            bool wide = sideways.ContentSize.X > sideways.Extent.X + 0.5f;
            sideways.Claims = InteractionKinds.Clip | (wide ? InteractionKinds.ViewScrollX | InteractionKinds.ClampX : InteractionKinds.None);
            runtime.InteractionsFor(sideways);   // asking is what moves its view by the wheel
            if (!wide) sideways.TargetViewOffset = sideways.ViewOffset = default;
            sideways.ChildLayoutAxis = LayoutAxis.Y;
            sideways.SizeX = SemanticSize.Pixels(width);
            sideways.SizeY = SemanticSize.ChildrenSum();
            sideways.AllowOverflowX = true;
            runtime.BeginParent(sideways);
        }
        var table = runtime.BeginParent(runtime.Column().Sized(Size.Px(tableWidth)));
        // Each cell wraps at its column's width, so a row is as tall as its longest cell.
        void Row(List<object?> cells, bool head)
        {
            var row = runtime.BeginParent(runtime.Row().Sized(Size.Fill));
            row.Style.Padding = EdgeInsets.Symmetric(horizontal: 0f, vertical: 6f);
            for (int i = 0; i < count; i++)
            {
                var v = i < cells.Count ? cells[i] : null;
                var rec = head ? null : v as Dictionary<string, object?>;
                Box cell;
                if (head && sortable)
                {
                    // A column's head sorts by it; again, the other way.
                    cell = runtime.BoxFromId($"bib-th-{n.Key}-{i}", InteractionKinds.LeftPress);
                    cell.Sized(Size.Px(cellWidths[i]), Size.Fit);
                    cell.ChildLayoutAxis = LayoutAxis.Y;
                    cell.Style.HoverBackgroundColor = UiColor.SurfaceRaised;
                    cell.Style.CornerRadius = 4f;
                    var ia = runtime.InteractionsFor(cell);
                    if (ia.Clicked) Set(n, new Dictionary<string, object?> { ["sort"] = (double)i, ["desc"] = by == i && !desc, ["page"] = 1d });
                    Claim(k, cell);
                    runtime.BeginParent(cell);
                }
                else cell = runtime.BeginParent(runtime.Column().Sized(Size.Px(cellWidths[i])));
                if (rec?.GetValueOrDefault("background") is { } bg)
                {
                    cell.Style.BackgroundColor = Colour(bg, UiColor.SurfaceRaised);
                    cell.Style.CornerRadius = 4f;
                    cell.Style.Padding = EdgeInsets.Symmetric(horizontal: 6f, vertical: 2f);
                }
                float room = cellWidths[i] - CellGap - (rec?.GetValueOrDefault("background") is not null ? 12f : 0f);
                if (rec is not null && rec.TryGetValue("badge", out var pill))
                {
                    var holder = runtime.BeginParent(runtime.Row().Sized(End(i) ? Size.Px(room) : Size.Fit, Size.Fit));
                    if (End(i)) runtime.Spacer(Size.Fill);
                    Pill(Values.Str(pill), rec.TryGetValue("tone", out var t) && t is not null ? Values.Str(t) : null);
                    runtime.EndParent(holder);
                }
                else
                {
                    string text = head ? Values.Str(v) + (by == i ? (desc ? " ▼" : " ▲") : "")
                        : Number(rec is not null ? rec.GetValueOrDefault("text") ?? rec.GetValueOrDefault("value") : v);
                    var ink = head ? UiColor.TextSecondary : rec?.GetValueOrDefault("color") is { } col ? Colour(col, UiColor.TextPrimary) : UiColor.TextPrimary;
                    var weight = head ? FontWeight.Bold : FontWeight.Regular;
                    if (!End(i)) Wrapped(text, room, Smaller, ink, weight);
                    else
                    {
                        // Each line at the end of the cell.
                        var font = runtime.FontInScope(Smaller);
                        foreach (var l in Text.Lines(runtime, text, font, MathF.Max(20f, room)))
                        {
                            var line = runtime.BeginParent(runtime.Row().Sized(Size.Px(room), Size.Fit));
                            runtime.Spacer(Size.Fill);
                            runtime.Text(l.End > l.Start ? text.AsSpan(l.Start, l.End - l.Start) : " ", Smaller, ink, weight: weight);
                            runtime.EndParent(line);
                        }
                    }
                }
                runtime.EndParent(cell);
            }
            runtime.EndParent(row);
        }
        if (columns.Count > 0) { Row(columns, head: true); Separator.Build(runtime); }
        // height: the rows scroll under the head, which stays.
        Box? body = null;
        if (Px(n.Props.GetValueOrDefault("height")) is { } bodyHeight)
        {
            body = runtime.BoxFromId($"bib-tbody-{n.Key ?? n.Text("id") ?? _ids++.ToString()}",
                InteractionKinds.ViewScrollY | InteractionKinds.ClampY | InteractionKinds.Clip);
            body.Sized(Size.Fill, Size.Px(bodyHeight));
            body.ChildLayoutAxis = LayoutAxis.Y;
            body.AllowOverflowY = true;
            runtime.BeginParent(body);
        }
        for (int r = 0; r < rows.Count; r++)
        {
            if (r > 0) Separator.Build(runtime);
            Row(rows[r] as List<object?> ?? [rows[r]], head: false);
        }
        if (body is not null)
        {
            runtime.EndParent(body);
            runtime.InteractionsFor(body);   // asking is what moves its view by the wheel, before the page asks
        }
        runtime.EndParent(table);
        if (sideways is not null) runtime.EndParent(sideways);
        if (pages > 1)
        {
            // The pages: back, where it is, on; under the table, not scrolling with it.
            Separator.Build(runtime);
            var pager = runtime.BeginParent(runtime.Row().Sized(Size.Fill, Size.Fit));
            pager.Style.Padding = EdgeInsets.Symmetric(horizontal: 0f, vertical: 6f);
            var back = runtime.Button($"bib-prev-{n.Key}", "‹ Back", ghost: true, disabled: page <= 1, size: Smaller);
            runtime.Spacer(Size.Fill);
            runtime.Text($"Page {page} of {pages} · {Number(n.Props.GetValueOrDefault("total"))} rows", Smaller, UiColor.TextSecondary);
            runtime.Spacer(Size.Fill);
            var next = runtime.Button($"bib-next-{n.Key}", "Next ›", ghost: true, disabled: page >= pages, size: Smaller);
            runtime.EndParent(pager);
            Claim(k, back.Node);
            Claim(k, next.Node);
            if (back.Clicked || next.Clicked)
                Set(n, new Dictionary<string, object?>(st!) { ["page"] = (double)(page + (next.Clicked ? 1 : -1)) });
        }
    }

    // A table's columns: shared in proportion to their weights, but none narrower than its longest word (and
    // MinColumn at least, ColumnCap at most), so a word is broken only when it is longer than that. When
    // those least widths do not fit, each column is its least, and the table is wider than the width:
    // it then scrolls sideways in its box.
    private float[] ColumnWidths(List<object?> columns, List<object?> rows, int count, List<double> weights, float width, out float tableWidth)
    {
        const float CellGap = 12f;
        var font = runtime.FontInScope(Smaller);
        var least = new float[count];
        void Words(int i, object? cell, float bold)
        {
            var rec = cell as Dictionary<string, object?>;
            // A badge is one pill, as wide as its text and its padding.
            if (rec?.GetValueOrDefault("badge") is { } pill)
            {
                least[i] = MathF.Max(least[i], Text.Width(runtime, Values.Str(pill), runtime.FontInScope(ComponentSize.Sm)) + 24f);
                return;
            }
            string text = rec is not null ? Number(rec.GetValueOrDefault("text") ?? rec.GetValueOrDefault("value")) : Number(cell);
            float shade = rec?.GetValueOrDefault("background") is not null ? 12f : 0f;   // a cell on a colour is padded
            foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                least[i] = MathF.Max(least[i], Text.Width(runtime, word, font) * bold + shade);
        }
        for (int i = 0; i < Math.Min(count, columns.Count); i++) Words(i, columns[i], 1.06f);   // the head is bold
        foreach (var row in rows.Take(200).OfType<List<object?>>())
            for (int i = 0; i < Math.Min(count, row.Count); i++) Words(i, row[i], 1f);
        for (int i = 0; i < count; i++) least[i] = Math.Clamp(MathF.Ceiling(least[i]) + CellGap + 2f, MinColumn, ColumnCap);
        var w = weights.Take(count).ToArray();
        if (least.Sum() >= width)
        {
            tableWidth = MathF.Max(width, least.Sum());
            return least.Select(MathF.Floor).ToArray();
        }
        // Columns held at their least give up their share; the rest share what is left, again, until none is under.
        var widths = new float[count];
        var held = new bool[count];
        for (bool again = true; again; )
        {
            again = false;
            float left = width - Enumerable.Range(0, count).Where(i => held[i]).Sum(i => least[i]);
            double weight = Enumerable.Range(0, count).Where(i => !held[i]).Sum(i => w[i]);
            for (int i = 0; i < count; i++)
            {
                widths[i] = held[i] ? least[i] : (float)(left * w[i] / weight);
                if (!held[i] && widths[i] < least[i]) { held[i] = true; again = true; }
            }
        }
        tableWidth = width;
        return widths.Select(MathF.Floor).ToArray();
    }

    private void List(FrameNode n, int k, float width)
    {
        bool selectable = Values.Truthy(n.Props.GetValueOrDefault("selectable"));
        var selected = n.Props.GetValueOrDefault("selected") as List<object?> ?? [];
        var col = runtime.BeginParent(runtime.Column().Sized(Size.Fill));
        if (n.Text("label") is { } label) { runtime.Text(label, Smaller, UiColor.TextSecondary); runtime.Gap(4f); }
        foreach (var child in n.Children)
        {
            if (child.Type == "item") Item(child, k, width, selectable ? (n, selected) : null);
            else Draw(child, k, width);
            runtime.Gap(4f);
        }
        if (n.Children.Count == 0) runtime.Text("Nothing here.", Smaller, UiColor.TextDisabled);
        runtime.EndParent(col);
    }

    private void Item(FrameNode n, int k, float width, (FrameNode List, List<object?> Selected)? pick)
    {
        var row = runtime.BeginParent(runtime.Row().Sized(Size.Fill));
        row.Style.Padding = EdgeInsets.Symmetric(horizontal: 8f, vertical: 6f);
        row.Style.CornerRadius = 6f;
        row.Style.BackgroundColor = UiColor.SurfaceRaised.WithAlpha(0.3f);
        var id = n.Props.GetValueOrDefault("id");
        if (pick is var (list, selected))
        {
            bool on = selected.Any(s => Values.Eq(s, id));
            var box = runtime.Checkbox($"bib-{list.Key}-{Values.Str(id)}", on);
            Claim(k, box.Node);
            if (box.Clicked)
                Set(list, on ? selected.Where(s => !Values.Eq(s, id)).ToList() : [.. selected, id]);
            runtime.Gap(10f);
        }
        var text = runtime.BeginParent(runtime.Column());
        runtime.Text(n.Text("title") ?? Values.Str(id), Size0, Tone(n, UiColor.TextPrimary));
        if (n.Text("subtitle") is { } sub) runtime.Text(sub, Smaller, UiColor.TextSecondary);
        runtime.EndParent(text);
        runtime.Spacer(Size.Fill);
        if (n.Text("badge") is { } badge) Pill(badge, n.Text("tone"));
        runtime.EndParent(row);
    }

    private void Input(FrameNode n, int k, float width)
    {
        Caption(n);
        string key = n.Key ?? "";
        string value = Values.Str(n.Props.GetValueOrDefault("value") ?? "");
        if (!_fields.TryGetValue(key, out var field))
        {
            field = (new BibuiStr(64), new TextEditState(), value);
            field.Text.ReplaceRange((0, 0), value);
        }
        else if (value != field.Seen)
        {
            // The document set() it: the field shows the new text, its caret at the end.
            field.Text.ReplaceRange((0, (int)field.Text.Length), value);
            field.Edit = new TextEditState();
        }
        string id = "bib-" + key;
        _fieldIds.Add(id);
        Box frame = runtime.Box();
        TextField.InputStyle(runtime, frame, runtime.IsFocused(id));
        frame.SizeX = SemanticSize.Pixels(MathF.Min(width, 420f));
        frame.SizeY = SemanticSize.ChildrenSum();
        runtime.BeginParent(frame);
        // The field takes the keyboard with a press and gives it up on Enter or Escape.
        var typed = runtime.TextField(id, field.Text, field.Edit, placeholder: n.Text("placeholder") ?? "");
        // Enter in a field is the document's when it asked for enter (a button with key: "enter"): a form sends on Enter.
        bool submitted = typed.Interaction.Submitted && _frame?.Keys.Contains("enter") == true;
        runtime.EndParent(frame);
        Claim(k, frame);
        string now = field.Text.ToString();
        _fields[key] = field with { Seen = now };
        if (now != value) Set(n, now);
        if (submitted) Fire(-1, new DocEvent("key", "enter"), what: "key");
    }

    // ── what a reader does ───────────────────────────────────────────────────

    // A box with an id: what the pointer did to it is an event of the document's, its hover style
    // and its tip are drawn, and a press on it is the box's, not the block's.
    private void Pointer(FrameNode n, int k, Box box)
    {
        string id = n.Key!;
        _idBoxes.Add((id, box));
        Claim(k, box);
        if (n.Props.GetValueOrDefault("hover") is Dictionary<string, object?> hover)
        {
            if (hover.GetValueOrDefault("background") is { } hb) box.Style.HoverBackgroundColor = Colour(hb, UiColor.SurfaceRaised);
            if (hover.GetValueOrDefault("borderColor") is { } hc) box.Style.HoverBorderColor = Colour(hc, UiColor.BorderHover);
        }
        var ia = runtime.InteractionsFor(box);
        if (ia.Hovering || ia.Dragging || ia.Released) (_pointer, _pointerSeen) = ((ia.X, ia.Y), true);
        if (ia.Hovering && n.Text("tip") is { Length: > 0 } tip) Tooltip.Build(runtime, tip, box);
        double x = ia.X - box.Rect.X, y = ia.Y - box.Rect.Y;
        // The boxes with an id around this one hear its events too, innermost first, each in its own terms.
        var around = _around.AsEnumerable().Reverse().Select(a => (a.Id, (double)(box.Rect.X - a.Box.Rect.X), (double)(box.Rect.Y - a.Box.Rect.Y))).ToList();
        DocEvent Ev(string kind, double dx = 0, double dy = 0) => new(kind, id, x, y, dx, dy) { Around = around };
        // Quiet: a box promises nothing, so one that does nothing with a press says nothing.
        if (ia.Pressed) Fire(k, Ev("press"), quiet: true);
        else if (ia.Dragging && (ia.DragDeltaX != 0f || ia.DragDeltaY != 0f)) Fire(k, Ev("drag", ia.DragDeltaX, ia.DragDeltaY), quiet: true);
        if (ia.Released) Fire(k, Ev("release", ia.DragDeltaX, ia.DragDeltaY), quiet: true);
        if (ia.Clicked) Fire(k, Ev("click"), quiet: true);
        if (ia.RightPressed) Fire(k, Ev("rclick"), quiet: true);
        if (ia.Dragging) runtime.RequestFrame();
    }

    private void Set(FrameNode n, object? value)
    {
        if (_program.Session is null || n.Key is null) return;
        _program.Session.Set(n.Key, value);
        if (_program.UsesChanged) Fire(-1, new DocEvent("change", n.Key), quiet: true);
        runtime.RequestFrame();
        _program.Touch();
    }

    // The click's run: what the document would do. A viewer does none of it.
    private void Click(int k, string key) => Fire(k, DocEvent.Click(key));

    // An event's run. A quiet one (a drag's moves, an input's change) says only what it would do or how it failed,
    // not that it did nothing: most of them do.
    private void Fire(int k, DocEvent ev, bool quiet = false, string what = "click")
    {
        if (_program.Session is null) return;
        var result = _program.Session.Fire(ev);
        _program.Touch();   // the states it set()
        if (quiet && result.Effects.Count == 0 && result.Errors.All(e => e is null)) { runtime.RequestFrame(); return; }
        Told(k, result, ev.Kind == "change" ? "change" : what);
    }

    // What an event's run would do, or why it failed, under the block it came from (k < 0: the block that failed, else the first).
    /// <summary>
    /// Saves a document's download() and says where it went; null saves it in the Downloads folder
    /// (the browser's download, in the web viewer).
    /// </summary>
    public static Func<string, string, string>? Saver;

    private static string SaveInDownloads(string name, string text)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string dir = Environment.GetEnvironmentVariable("XDG_DOWNLOAD_DIR") is { Length: > 0 } x ? x : Path.Combine(home, "Downloads");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, name);
        // Never over a file already there: plan.csv, plan (2).csv, …
        for (int n = 2; File.Exists(path); n++)
            path = Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(name)} ({n}){Path.GetExtension(name)}");
        File.WriteAllText(path, text);
        return "Saved " + (path.StartsWith(home + "/") ? "~" + path[home.Length..] : path) + ".";
    }

    private void Told(int k, FrameResult result, string what)
    {
        if (k < 0) k = result.Errors.Select((e, i) => (e, i)).FirstOrDefault(x => x.e is not null).i;
        // copy() and download() are the reader's own, and bibmd does them; the rest it only says.
        var done = new List<string>();
        if (result.Errors.All(e => e is null))
            foreach (var fx in result.Effects.Where(x => x.Kind is "copy" or "download"))
            {
                if (fx.Kind == "copy") { runtime.ClipboardSet(Values.Str(fx.Payload)); done.Add("Copied."); }
                else
                {
                    try { done.Add((Saver ?? SaveInDownloads)(fx.Tool ?? "download.txt", Values.Str(fx.Payload))); }
                    catch (Exception x) when (x is IOException or UnauthorizedAccessException) { done.Add($"Could not save {fx.Tool}: {x.Message}"); }
                }
            }
        result.Effects.RemoveAll(x => x.Kind is "copy" or "download");
        string? notice = result.Errors.FirstOrDefault(e => e is not null) is { } e
            ? $"That {what} fails: " + e.Message
            : result.Effects.Count > 0
                ? "Would " + string.Join("; then ", result.Effects.Select(x => x.Describe()))
                  + ". This viewer does not run effects."
                : result.Sets.Count == 0 ? $"That {what} does nothing here." : null;   // a click that set() changed the page: it says so itself
        if (done.Count > 0) notice = string.Join(" ", done.Append(notice ?? "").Where(t => t.Length > 0 && !t.EndsWith("does nothing here.")));
        if (notice is null) _program.Notices.Remove(k);
        else _program.Notices[k] = notice;
        runtime.RequestFrame();
    }
}

/// <summary>
/// A document's bib blocks as one program: its session (the values the reader gave), its data,
/// and what each block's last click would have done. A <see cref="Bib"/> has one of its own;
/// a host that draws a document's blocks in places of their own (one canvas per block, in a
/// page) gives each place's <see cref="Bib"/> the same one, so a slider in one block moves
/// what another draws, and hears <see cref="Changed"/> to draw them all again.
/// </summary>
public sealed class BibProgram
{
    private string _key = "";
    private FrameResult? _frame;
    private bool _rerun = true;   // a value was set since _frame was run, or the pointer moved onto another box with an id
    private DateTimeOffset _ran;  // when _frame was run: a document that reads the clock runs again every _frame.Every seconds
    private readonly Dictionary<object, HashSet<string>> _hovered = [];   // each place's boxes under the pointer

    public DocSession? Session { get; private set; }
    public string? DataError { get; private set; }

    /// <summary>Bib block -> what its last click would have done.</summary>
    public Dictionary<int, string> Notices { get; } = [];

    /// <summary>The alarms rung, by their time: each rings once, whichever place draws it.</summary>
    internal HashSet<double> Rang { get; } = [];

    /// <summary>The last <see cref="Frame"/> made a new session: the document or its data changed, or it was the first.</summary>
    internal bool Renewed { get; private set; }

    /// <summary>The program calls changed(): an input's change is an event.</summary>
    internal bool UsesChanged { get; private set; }

    /// <summary>A reader changed a value or clicked, in any of the places that draw the program.</summary>
    public event Action? Changed;

    internal void Touch()
    {
        _rerun = true;
        Changed?.Invoke();
    }

    /// <summary>
    /// This frame of the program the document's blocks make now. A document whose blocks or
    /// data changed gets a new session, keeping the values the reader gave. Null with no bib blocks.
    /// Run again only when what it reads changed: a frame that scrolls the page draws the last run.
    /// </summary>
    internal FrameResult? Frame(string? front, List<string> dataBlocks, List<string> sources, object place, HashSet<string> hovered, double width)
    {
        if (sources.Count == 0) { Session = null; _frame = null; return null; }
        string key = (front ?? "") + "\u0001" + string.Join("\u0002", dataBlocks) + "\u0001" + string.Join("\u0002", sources);
        Renewed = key != _key || Session is null;
        if (key != _key || Session is null)
        {
            var data = DocSession.DataFrom(front, dataBlocks, out var error);
            var next = new DocSession(sources, data);
            if (Session is not null) next.CarryFrom(Session);
            Session = next;
            DataError = error;
            _key = key;
            UsesChanged = sources.Any(src => src.Contains("changed(", StringComparison.Ordinal));
            Notices.Clear();
            _rerun = true;
        }
        // The boxes under the pointer, in any place that draws the program; and the clock, when it reads it.
        _hovered[place] = hovered;
        var all = _hovered.Values.SelectMany(h => h).ToHashSet();
        if (!all.SetEquals(Session.Host.Hovered)) { Session.Host.Hovered = all; _rerun = true; }
        // page.width in whole px, so a column that settles by a fraction does not run the program again.
        if (width != Session.Host.Width) { Session.Host.Width = width; _rerun = true; }
        var now = DateTimeOffset.Now;
        if (_frame?.Every is { } every && (now - _ran).TotalSeconds >= every * 0.98) _rerun = true;
        if (_rerun || _frame is null)
        {
            Session.Host.Now = now;
            (_frame, _rerun, _ran) = (Session.Frame(), false, now);
        }
        return _frame;
    }
}

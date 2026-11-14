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
    private readonly Dictionary<int, int> _ordinal = [];          // document block -> bib block
    private readonly Dictionary<int, List<UiNode>> _widgets = []; // bib block -> its widgets, drawn this frame
    private readonly Dictionary<string, (BibuiStr Text, TextEditState Edit)> _fields = [];
    private readonly HashSet<string> _fieldIds = [];                // this frame's text inputs, by box id
    private int _ids;                                             // ids for this frame's unnamed nodes
    private int _sizeIndex = 1;

    /// <summary>A text input of a bib block has the keyboard: the reader's keys are not commands.</summary>
    public bool HasFocus => _fieldIds.Any(runtime.IsFocused);

    /// <summary>Gives the keyboard back to the reader.</summary>
    public void Blur()
    {
        if (HasFocus) runtime.Focus("");
    }

    /// <summary>
    /// Runs the document's bib blocks for this frame. A document whose blocks or
    /// data changed gets a new program, keeping the values the reader gave.
    /// </summary>
    public void Prepare(Document doc, int sizeIndex)
    {
        _sizeIndex = sizeIndex;
        _ordinal.Clear();
        _widgets.Clear();
        _fieldIds.Clear();
        _ids = 0;
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
        _frame = _program.Frame(front, dataBlocks, sources);
    }

    /// <summary>
    /// Draws document block <paramref name="index"/> if it is a bib block. False,
    /// with nothing built, for any other block.
    /// </summary>
    public bool View(int index, float width)
    {
        if (_frame is null || !_ordinal.TryGetValue(index, out int k)) return false;
        _widgets[k] = [];
        var col = runtime.BeginParent(runtime.Column().Sized(Size.Fill));
        if (k == 0 && _program.DataError is { } de) Problem("Front matter: " + de);
        DrawAll(_frame.Blocks[k], k, width);
        if (_frame.Errors[k] is { } e) Problem(e.Message);
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

    // The narrowest a grid's cell and a row's child are drawn: narrower, they go onto more lines.
    private const float MinCell = 120f, MinRowCell = 240f;

    private void DrawAll(List<FrameNode> nodes, int k, float width)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (i > 0) runtime.Gap(Gap);
            Draw(nodes[i], k, width);
        }
    }

    private void Claim(int k, UiNode node) => _widgets[k].Add(node);

    private static UiColor Tone(FrameNode n, UiColor otherwise) => n.Text("tone") switch
    {
        "success" => UiColor.Success,
        "danger" => UiColor.Destructive,
        "warning" => Amber,
        "accent" or "primary" => UiColor.Accent,
        "muted" => UiColor.TextSecondary,
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
            case "layer" or "line" or "rect" or "circle" or "label":
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
                DrawAll(n.Children, k, inner);
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
                DrawAll(n.Children, k, width);
                runtime.EndParent(section);
                break;
            }
            case "column":
            {
                var col = runtime.BeginParent(runtime.Column().Sized(Size.Fill));
                DrawAll(n.Children, k, width);
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
                    float w = Measure(child, width);
                    if (used > 0f && used + between + w > width)
                    {
                        runtime.EndParent(row);
                        runtime.Gap(MathF.Round(Gap * 0.5f));
                        row = runtime.BeginParent(runtime.Row());
                        used = 0f;
                    }
                    if (used > 0f) runtime.Gap(between);
                    Draw(child, k, width);
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
                runtime.EndParent(line);
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
                foreach (var it in n.Props.GetValueOrDefault("items") as List<object?> ?? [])
                {
                    if (it is not Dictionary<string, object?> pair) continue;
                    var row = runtime.BeginParent(runtime.Row().Sized(Size.Fill));
                    var left = runtime.BeginParent(runtime.Column().Sized(Size.Px(MathF.Floor(width * 0.4f))));
                    runtime.Text(Values.Str(pair.GetValueOrDefault("label")), Size0, UiColor.TextSecondary);
                    runtime.EndParent(left);
                    // The value wraps in the rest of the line, as long as it is.
                    var right = runtime.BeginParent(runtime.Column().Sized(Size.Px(width - MathF.Floor(width * 0.4f))));
                    Wrapped(Number(pair.GetValueOrDefault("value")), width - MathF.Floor(width * 0.4f), Size0, UiColor.TextPrimary);
                    runtime.EndParent(right);
                    runtime.EndParent(row);
                    runtime.Gap(4f);
                }
                runtime.EndParent(col);
                break;
            }
            case "table":
                Table(n, width);
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
                var b = runtime.Button("bib-" + n.Key, n.Text("label") ?? "",
                    ghost: n.Text("style") == "ghost", disabled: Values.Truthy(n.Props.GetValueOrDefault("disabled")));
                runtime.EndParent(row);
                Claim(k, b.Node);
                if (b.Clicked && n.Key is { } key) Click(k, key);
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
            case "toggle":
            {
                bool on = Values.Truthy(n.Props.GetValueOrDefault("value"));
                var sw = runtime.Switch("bib-" + n.Key, on, n.Text("label") ?? "");
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
            "button" => Text.Width(runtime, n.Text("label") ?? "", font) + 40f,
            // Its label, or its number (bold, a little wider than measured) and unit.
            "stat" => MathF.Min(width, MathF.Max(Text.Width(runtime, n.Text("label") ?? "", runtime.FontInScope(Smaller)),
                Text.Width(runtime, Number(n.Props.GetValueOrDefault("value")), runtime.FontInScope(Larger)) * 1.08f
                + (n.Text("unit") is { } unit ? 6f + Text.Width(runtime, unit, runtime.FontInScope(Smaller)) : 0f)) + 4f),
            _ => width,   // anything else takes a line of its own
        };
    }

    // ── primitives (bibdoc's docs/two-layers.md) ─────────────────────────────

    // A colour: a role, a role faded ("accent@0.15"), or a hex.
    private static UiColor Colour(object? value, UiColor otherwise)
    {
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

    private void TextNode(FrameNode n, float width)
    {
        var size = TextSize(n.Props.GetValueOrDefault("size"));
        var face = Face(n.Text("face"));
        var weight = Weight(n.Text("weight"));
        var color = Colour(n.Props.GetValueOrDefault("color"), Tone(n, UiColor.TextPrimary));
        string text = TextOf(n);
        if (n.Props.GetValueOrDefault("wrap") is false) { runtime.Text(text, size, color, face, weight); return; }
        var font = runtime.FontInScope(size, face);
        foreach (var para in text.Split('\n'))
        {
            if (para.Length == 0) { runtime.Text(" ", size, color, face, weight); continue; }
            foreach (var line in Text.Lines(runtime, para, font, MathF.Max(20f, width)))
                runtime.Text(para.AsSpan(line.Start, line.End - line.Start), size, color, face, weight);
        }
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
                var kids = n.Children.Where(c => c.Type != "layer").ToList();
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
                return MathF.Min(room, Text.Width(runtime, TextOf(n), runtime.FontInScope(TextSize(n.Props.GetValueOrDefault("size")), Face(n.Text("face")))) + 1f);
            case "markdown" or "table" or "list" or "card" or "grid" or "steps" or "kv":
                return null;
            default:
                return Measure(n, room);
        }
    }

    private void DrawBox(FrameNode n, int k, float width)
    {
        var pad = Insets(n.Props.GetValueOrDefault("padding"));
        float w = Px(n.Props.GetValueOrDefault("width")) ?? width;
        float? h = Px(n.Props.GetValueOrDefault("height"));
        bool row = n.Text("dir") == "row";
        float gap = Px(n.Props.GetValueOrDefault("gap")) ?? 0f;
        Box box = runtime.Box();
        box.ChildLayoutAxis = row ? LayoutAxis.X : LayoutAxis.Y;
        box.SizeX = SemanticSize.Pixels(w);
        box.SizeY = h is { } hh ? SemanticSize.Pixels(hh) : SemanticSize.ChildrenSum();
        box.Style.Padding = pad;
        if (n.Props.GetValueOrDefault("background") is { } bg) box.Style.BackgroundColor = Colour(bg, UiColor.SurfaceRaised);
        if (Px(n.Props.GetValueOrDefault("border")) is { } bw && bw > 0f)
        {
            box.Style.BorderWidth = bw;
            box.Style.BorderColor = Colour(n.Props.GetValueOrDefault("borderColor") ?? "border", UiColor.Border);
        }
        if (Px(n.Props.GetValueOrDefault("radius")) is { } r) box.Style.CornerRadius = r;
        if (Px(n.Props.GetValueOrDefault("opacity")) is { } o) box.Style.Opacity = o;
        // A layer draws under the children, clipped to the box: w and h are the box's size.
        if (n.Children.FirstOrDefault(c => c.Type == "layer") is { } layer)
        {
            var labels = Labels(layer);
            box.Draw = (b, ctx) => DrawLayer(ctx, layer, labels, b.Extent.X, b.Extent.Y);
        }
        var kids = n.Children.Where(c => c.Type != "layer").ToList();
        float inner = MathF.Max(0f, w - pad.Left - pad.Right);
        // Centring needs a size of its own along that axis: only a box given one centres.
        bool along = n.Text("justify") == "center" && (row ? Px(n.Props.GetValueOrDefault("width")) : h) is not null;
        bool across = n.Text("align") == "center" && (row ? h : (float?)w) is not null;
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
                    if (across) runtime.BeginCenterAcross();
                    Draw(kids[i], k, across ? Want(kids[i], inner) ?? inner : inner);
                    if (across) runtime.EndCenterAcross();
                }
            }
            if (along) runtime.Spacer(Size.Fill);
        }
    }

    // A row's children: each as wide as it wants, the ones that fill sharing what is left.
    private void RowOf(List<FrameNode> kids, int k, float inner, float gap, bool wrap, bool across, bool between = false)
    {
        var wants = kids.Select(c => Want(c, inner)).ToList();
        if (wrap)
        {
            // Lines of what fits; a child that fills takes a line of its own.
            float used = 0f;
            var line = runtime.BeginParent(runtime.Row());
            for (int i = 0; i < kids.Count; i++)
            {
                float cw = wants[i] ?? inner;
                if (used > 0f && used + gap + cw > inner)
                {
                    runtime.EndParent(line);
                    runtime.Gap(gap);
                    line = runtime.BeginParent(runtime.Row());
                    used = 0f;
                }
                if (used > 0f) runtime.Gap(gap);
                Cell(kids[i], k, cw, across);
                used += (used > 0f ? gap : 0f) + cw;
            }
            runtime.EndParent(line);
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
        var cell = runtime.BeginParent(runtime.Column().Sized(Size.Px(width)));
        if (across) runtime.BeginCenterAcross();
        Draw(child, k, width);
        if (across) runtime.EndCenterAcross();
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
            Rgba? edge = p.ContainsKey("color") || s.Type == "line" ? ctx.Resolve(Colour(p.GetValueOrDefault("color") ?? "border", UiColor.Border)) : null;
            Rgba? fill = p.GetValueOrDefault("fill") is { } f ? ctx.Resolve(Colour(f, UiColor.SurfaceRaised)) : null;
            var paint = new Paint { Fill = fill, Stroke = edge, StrokeWidth = stroke };
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
        DrawAll(n.Children, k, inner);
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

    private void Problem(string message)
    {
        runtime.Gap(6f);
        var box = runtime.BeginParent(runtime.Column().Sized(Size.Fill));
        box.Style.Padding = EdgeInsets.All(10f);
        box.Style.CornerRadius = 6f;
        box.Style.BorderWidth = 1f;
        box.Style.BorderColor = UiColor.Destructive;
        runtime.Text(message, Smaller, UiColor.Destructive, FontSlot.Mono);
        runtime.EndParent(box);
    }

    // Text wrapped at the width, in the size and colour given.
    private void Wrapped(string text, float width, ComponentSize size, UiColor color, FontWeight weight = FontWeight.Regular)
    {
        var font = runtime.FontInScope(size);
        foreach (var para in text.Split('\n'))
        {
            if (para.Length == 0) { runtime.Text(" ", size, color); continue; }
            foreach (var line in Text.Lines(runtime, para, font, MathF.Max(40f, width)))
                runtime.Text(line.End > line.Start ? para.AsSpan(line.Start, line.End - line.Start) : " ", size, color, weight: weight);
        }
    }

    private static string Number(object? v) => v switch
    {
        double d when Math.Abs(d) >= 1000 && d == Math.Floor(d) => d.ToString("N0", System.Globalization.CultureInfo.InvariantCulture),
        double d => Values.NumStr(Math.Round(d, 4)),
        _ => Values.Str(v),
    };

    private void Table(FrameNode n, float width)
    {
        var columns = n.Props.GetValueOrDefault("columns") as List<object?> ?? [];
        var rows = n.Props.GetValueOrDefault("rows") as List<object?> ?? [];
        int count = Math.Max(1, Math.Max(columns.Count, rows.OfType<List<object?>>().Select(r => r.Count).DefaultIfEmpty(0).Max()));
        // widths: [1, 2, 2] shares the width in proportion; without it, equally.
        var weights = (n.Props.GetValueOrDefault("widths") as List<object?> ?? []).Select(w => Math.Max(0.1, Values.Num(w))).ToList();
        while (weights.Count < count) weights.Add(weights.Count == 0 ? 1 : weights.Average());
        double total = weights.Take(count).Sum();
        float[] cellWidths = weights.Take(count).Select(w => MathF.Floor((float)(width * w / total))).ToArray();
        const float CellGap = 12f;   // room between a cell's text and the next column
        var table = runtime.BeginParent(runtime.Column().Sized(Size.Fill));
        // Each cell wraps at its column's width, so a row is as tall as its longest cell.
        void Row(List<object?> cells, bool head)
        {
            var row = runtime.BeginParent(runtime.Row().Sized(Size.Fill));
            row.Style.Padding = EdgeInsets.Symmetric(horizontal: 0f, vertical: 6f);
            for (int i = 0; i < count; i++)
            {
                var cell = runtime.BeginParent(runtime.Column().Sized(Size.Px(cellWidths[i])));
                var v = i < cells.Count ? cells[i] : null;
                if (!head && v is Dictionary<string, object?> rec && rec.TryGetValue("badge", out var pill))
                {
                    var holder = runtime.BeginParent(runtime.Row());
                    Pill(Values.Str(pill), rec.TryGetValue("tone", out var t) && t is not null ? Values.Str(t) : null);
                    runtime.EndParent(holder);
                    runtime.EndParent(cell);
                    continue;
                }
                Wrapped(head ? Values.Str(v) : Number(v), cellWidths[i] - CellGap, Smaller,
                    head ? UiColor.TextSecondary : UiColor.TextPrimary, head ? FontWeight.Bold : FontWeight.Regular);
                runtime.EndParent(cell);
            }
            runtime.EndParent(row);
        }
        if (columns.Count > 0) { Row(columns, head: true); Separator.Build(runtime); }
        for (int r = 0; r < rows.Count; r++)
        {
            if (r > 0) Separator.Build(runtime);
            Row(rows[r] as List<object?> ?? [rows[r]], head: false);
        }
        runtime.EndParent(table);
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
            field = (new BibuiStr(64), new TextEditState());
            field.Text.ReplaceRange((0, 0), value);
            _fields[key] = field;
        }
        string id = "bib-" + key;
        _fieldIds.Add(id);
        Box frame = runtime.Box();
        TextField.InputStyle(runtime, frame, runtime.IsFocused(id));
        frame.SizeX = SemanticSize.Pixels(MathF.Min(width, 420f));
        frame.SizeY = SemanticSize.ChildrenSum();
        runtime.BeginParent(frame);
        // The field takes the keyboard with a press and gives it up on Enter or Escape.
        runtime.TextField(id, field.Text, field.Edit, placeholder: n.Text("placeholder") ?? "");
        runtime.EndParent(frame);
        Claim(k, frame);
        string now = field.Text.ToString();
        if (now != value) Set(n, now);
    }

    // ── what a reader does ───────────────────────────────────────────────────

    private void Set(FrameNode n, object? value)
    {
        if (_program.Session is null || n.Key is null) return;
        _program.Session.Set(n.Key, value);
        _program.Touch();
        runtime.RequestFrame();
    }

    // The click's run: what the document would do. A viewer does none of it.
    private void Click(int k, string key)
    {
        if (_program.Session is null) return;
        var result = _program.Session.Click(key);
        _program.Notices[k] = result.Errors.FirstOrDefault(e => e is not null) is { } e
            ? "That click fails: " + e.Message
            : result.Effects.Count == 0
                ? "That click does nothing here."
                : "Would " + string.Join("; then ", result.Effects.Select(x => x.Describe()))
                  + ". This viewer does not run effects.";
        runtime.RequestFrame();
        _program.Touch();
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
    private bool _rerun = true;   // a value was set since _frame was run (the program has no clock: nothing else changes it)

    public DocSession? Session { get; private set; }
    public string? DataError { get; private set; }

    /// <summary>Bib block -> what its last click would have done.</summary>
    public Dictionary<int, string> Notices { get; } = [];

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
    internal FrameResult? Frame(string? front, List<string> dataBlocks, List<string> sources)
    {
        if (sources.Count == 0) { Session = null; _frame = null; return null; }
        string key = (front ?? "") + "\u0001" + string.Join("\u0002", dataBlocks) + "\u0001" + string.Join("\u0002", sources);
        if (key != _key || Session is null)
        {
            var data = DocSession.DataFrom(front, dataBlocks, out var error);
            var next = new DocSession(sources, data);
            if (Session is not null) next.CarryFrom(Session);
            Session = next;
            DataError = error;
            _key = key;
            Notices.Clear();
            _rerun = true;
        }
        if (_rerun || _frame is null) (_frame, _rerun) = (Session.Frame(), false);
        return _frame;
    }
}

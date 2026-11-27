using Bibui.Core;
using Bibui.Core.Draw;
using Bibui.Core.Runtime;
using BibDoc;

namespace BibMd;

/// <summary>
/// bib's charts (bibdoc's docs/charts.md): <c>plot</c> with its series, dots, rules and points,
/// and <c>bars</c>. What depends only on the numbers (ranges, ticks, their labels and the fonts
/// they are measured in) is worked out while the frame is built; where things go, when the box is
/// drawn at its size.
/// </summary>
public sealed partial class Bib
{
    private const float PlotHeight = 220f, PlotPad = 10f;

    // The tones a series takes in turn when it names none.
    private static readonly string[] SeriesTones = ["accent", "warning", "success", "danger", "muted"];

    private sealed record Axis(double Min, double Max, bool Log, List<(double At, string Text, float Width)> Ticks)
    {
        // 0 at Min, 1 at Max, on a log axis by the logarithm.
        public double Of(double v) => Log
            ? (Math.Log10(Math.Max(v, double.Epsilon)) - Math.Log10(Min)) / (Math.Log10(Max) - Math.Log10(Min))
            : (v - Min) / (Max - Min);
    }

    private sealed record Note(string Text, float Width);

    private void Plot(FrameNode n, float width)
    {
        string log = n.Text("log") ?? "";
        var marks = n.Children;
        var font = runtime.FontInScope(Smaller);
        Note Measure(string s) => new(s, Text.Width(runtime, s, font));

        // Series in a stack add up: each one's band goes from the top of the one below to its own.
        static Dictionary<FrameNode, (double[] Base, double[] Top)> Stacks(List<FrameNode> marks)
        {
            var stacks = new Dictionary<FrameNode, (double[] Base, double[] Top)>();
            var running = new Dictionary<string, double[]>();
            foreach (var m in marks.Where(m => m.Type == "series" && m.Text("stack") is not null))
            {
                var ys = ((List<object?>)m.Props["ys"]!).Select(v => (double)v!).ToArray();
                string name = m.Text("stack")!;
                var below = running.TryGetValue(name, out var r) && r.Length == ys.Length ? r : new double[ys.Length];
                var top = ys.Select((v, i) => below[i] + (double.IsFinite(v) ? v : 0)).ToArray();
                stacks[m] = (below, top);
                running[name] = top;
            }
            return stacks;
        }
        var stacks = Stacks(marks);

        // The values each axis must show: what is drawn on it, or the range the plot gives.
        IEnumerable<double> On(string axis) => marks.SelectMany(m => m.Type switch
        {
            "series" when axis == "y" && stacks.TryGetValue(m, out var st) => st.Top.Append(0d),
            "series" or "dots" => Numbers(m.Props.GetValueOrDefault(axis + "s")),
            "rule" or "point" => m.Props.GetValueOrDefault(axis) is double d ? [d] : [],
            _ => [],
        });
        var x = AxisOf(n.Props.GetValueOrDefault("x"), On("x"), log.Contains('x'), font, tight: true);
        var y = AxisOf(n.Props.GetValueOrDefault("y"), On("y"), log.Contains('y'), font, tight: false);

        // Motion: the axes are the values', and the lines and dots ease to them (Motion.cs).
        if (ChartTransition(n) is not null)
        {
            marks = marks.Select((m, i) =>
            {
                if (m.Type is not ("series" or "dots") || m.Props.GetValueOrDefault("ys") is not List<object?> ys) return m;
                var copy = new FrameNode(m.Type);
                foreach (var (key, v) in m.Props) copy.Props[key] = v;
                copy.Props["ys"] = Eased(n, "ys" + i, ys.Select(Values.Num)).Select(v => (object?)v).ToList();
                return copy;
            }).ToList();
            stacks = Stacks(marks);
        }

        var notes = marks.Where(m => m.Type is "rule" or "point" && m.Text("label") is not null)
            .ToDictionary(m => m, m => Measure(m.Text("label")!));
        float height = Px(n.Props.GetValueOrDefault("height")) ?? PlotHeight;
        float lineHeight = font.Size * 1.3f;
        float left = y.Ticks.Count == 0 ? PlotPad : y.Ticks.Max(t => t.Width) + 2f * PlotPad;
        var tones = new Dictionary<FrameNode, string>();
        int turn = 0;
        foreach (var m in marks.Where(m => m.Type is "series" or "dots"))
            tones[m] = m.Text("tone") ?? SeriesTones[turn++ % SeriesTones.Length];

        if (n.Text("yLabel") is { } yLabel) { runtime.Text(yLabel, Smaller, UiColor.TextSecondary); runtime.Gap(4f); }
        // A box of its own across frames: where it was drawn last tells where the pointer is over it.
        Box box = runtime.BoxFromId("bib-plot-" + _plots++);
        var pointer = runtime.InteractionsFor(box);
        var hover = HoverOf(pointer.X - box.Rect.X, pointer.Y - box.Rect.Y, width, height, left, lineHeight, x, y,
            marks, stacks, n.Text("xLabel"), font);
        box.SizeX = SemanticSize.Pixels(width);
        box.SizeY = SemanticSize.Pixels(height);
        box.Style.BackgroundColor = UiColor.SurfaceRaised;
        box.Style.BorderWidth = 1f;
        box.Style.BorderColor = UiColor.Border;
        box.Style.CornerRadius = 10f;
        box.Draw = (b, ctx) => DrawPlot(ctx, b.Extent.X, b.Extent.Y, left, lineHeight, x, y, marks, tones, notes, font, stacks, hover);

        // The legend, when there is more than one thing to tell apart, and the x axis' name.
        var named = marks.Where(m => m.Type is "series" or "dots" && !string.IsNullOrEmpty(m.Text("label"))).ToList();
        string? xLabel = n.Text("xLabel");
        if (named.Count < 2 && xLabel is null) return;
        runtime.Gap(6f);
        var row = runtime.BeginParent(runtime.Row().Sized(Size.Px(width)));
        if (named.Count >= 2)
            foreach (var m in named)
            {
                bool square = m.Type == "dots" || stacks.ContainsKey(m);
                float sw = square ? (m.Type == "dots" ? 8f : 10f) : 14f, sh = square ? sw : 3f;
                var cell = runtime.BeginParent(runtime.Column().Sized(Size.Px(sw)));
                runtime.Spacer(Size.Px(MathF.Round(lineHeight / 2f - sh / 2f)));
                Box swatch = runtime.Box();
                swatch.SizeX = SemanticSize.Pixels(sw);
                swatch.SizeY = SemanticSize.Pixels(sh);
                swatch.Style.BackgroundColor = Colour(tones[m], UiColor.Accent);
                swatch.Style.CornerRadius = m.Type == "dots" || !square ? sh / 2f : 2.5f;
                runtime.EndParent(cell);
                runtime.Gap(6f);
                runtime.Text(m.Text("label")!, Smaller, UiColor.TextSecondary);
                runtime.Gap(14f);
            }
        if (xLabel is not null)
        {
            runtime.Spacer(Size.Fill);
            runtime.Text(xLabel, Smaller, UiColor.TextSecondary);
        }
        runtime.EndParent(row);
    }

    private static IEnumerable<double> Numbers(object? v) =>
        v is List<object?> xs ? xs.Select(Values.Num).Where(double.IsFinite) : [];

    // An axis over the range given, or what is drawn on it. A linear axis takes ticks at 1, 2 or 5
    // × 10ⁿ, about six; without a range given, y widens to its outer ticks (x stays tight to the
    // data). A log axis takes ticks at the powers of ten, and 2 and 5 between when it spans few.
    private Axis AxisOf(object? given, IEnumerable<double> drawn, bool log, Bibui.Text.Font font, bool tight)
    {
        var values = drawn.Where(v => !log || v > 0).ToList();
        double min, max;
        if (given is List<object?> { Count: 2 } r) (min, max) = (Values.Num(r[0]), Values.Num(r[1]));
        else if (values.Count > 0) (min, max) = (values.Min(), values.Max());
        else (min, max) = log ? (1, 10) : (0, 1);
        if (log) { min = Math.Max(min, 1e-300); max = Math.Max(max, min * 10); }
        if (!(max > min)) { double pad = Math.Abs(min) > 0 ? Math.Abs(min) * 0.1 : 1; (min, max) = (min - pad, max + pad); }

        var ticks = new List<double>();
        if (log)
        {
            int lo = (int)Math.Floor(Math.Log10(min)), hi = (int)Math.Ceiling(Math.Log10(max));
            bool fine = hi - lo <= 2;
            for (int e = lo; e <= hi; e++)
                foreach (double m in fine ? new[] { 1d, 2d, 5d } : new[] { 1d })
                {
                    double t = m * Math.Pow(10, e);
                    if (t >= min * 0.999 && t <= max * 1.001) ticks.Add(t);
                }
        }
        else
        {
            double raw = (max - min) / 5, mag = Math.Pow(10, Math.Floor(Math.Log10(raw))), f = raw / mag;
            double step = (f < 1.5 ? 1 : f < 3.5 ? 2 : f < 7.5 ? 5 : 10) * mag;
            if (given is null && !tight) { min = Math.Floor(min / step) * step; max = Math.Ceiling(max / step) * step; }
            for (double t = Math.Ceiling(min / step - 1e-9) * step; t <= max + step * 1e-9; t += step)
                ticks.Add(Math.Abs(t) < step * 1e-9 ? 0 : t);
        }
        // As many decimals as tell the ticks apart: a linear axis by its step, a log axis by each tick.
        bool compact = ticks.Count > 0 && ticks.Max(Math.Abs) >= 1_000_000;
        double spacing = !log && ticks.Count > 1 ? ticks[1] - ticks[0] : 0;
        int Decimals(double t) => compact ? 1
            : Math.Clamp((int)Math.Ceiling(-Math.Log10(log ? Math.Abs(t) : spacing) - 1e-9), 0, 12);
        return new Axis(min, max, log, ticks.Select(t =>
        {
            string s = Evaluator.Fmt(t, t == 0 ? 0 : Decimals(t), compact, false);
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\.0+(?=[kMBT]?$)|(\.\d*?)0+(?=[kMBT]?$)", "$1");
            return (t, s, Text.Width(runtime, s, font));
        }).ToList());
    }

    // Where a plot's marks go in its box: the area inside the ticks.
    private static (float Top, float Right, float Bottom, float Pw, float Ph) AreaOf(float w, float h, float left, float lineHeight)
    {
        float top = PlotPad + 2f, right = w - PlotPad - 4f, bottom = h - lineHeight - PlotPad;
        return (top, right, bottom, MathF.Max(1f, right - left), MathF.Max(1f, bottom - top));
    }

    // What the pointer over a plot shows: a cursor at the x of the series nearest it, and each
    // series' y there. Worked out where the frame is built, so its text can be measured.
    private sealed record Hover(double X, float Px, float PointerY, Note Head, List<(FrameNode Series, double Y, double Drawn, Note Label, Note Value)> Rows);

    private Hover? HoverOf(float px, float py, float w, float h, float left, float lineHeight, Axis x, Axis y,
        List<FrameNode> marks, Dictionary<FrameNode, (double[] Base, double[] Top)> stacks, string? xLabel, Bibui.Text.Font font)
    {
        var (top, right, bottom, pw, _) = AreaOf(w, h, left, lineHeight);
        if (!(px >= left - 4f && px <= right + 4f && py >= top - 4f && py <= bottom + 4f)) return null;
        float X(double v) => left + (float)(x.Of(v) * pw);
        bool Drawable(double vx, double vy) => double.IsFinite(vx) && double.IsFinite(vy) && (!x.Log || vx > 0) && (!y.Log || vy > 0);
        var series = marks.Where(m => m.Type == "series").ToList();
        // The nearest x any series has a point at, by distance on the screen.
        double best = double.NaN; float bestPx = 0f, bestD = float.MaxValue;
        foreach (var s in series)
        {
            var xs = (List<object?>)s.Props["xs"]!; var ys = (List<object?>)s.Props["ys"]!;
            for (int i = 0; i < xs.Count; i++)
            {
                double vx = (double)xs[i]!;
                if (!Drawable(vx, (double)ys[i]!)) continue;
                float d = MathF.Abs(X(vx) - px);
                if (d < bestD) (best, bestPx, bestD) = (vx, X(vx), d);
            }
        }
        if (double.IsNaN(best) || bestPx < left - 1f || bestPx > right + 1f) return null;
        Note Measure(string t) => new(t, Text.Width(runtime, t, font));
        string Shown(double v) => Evaluator.Fmt(v, null, Math.Abs(v) >= 1_000_000, false);
        var rows = new List<(FrameNode, double, double, Note, Note)>();
        foreach (var s in series)
        {
            var xs = (List<object?>)s.Props["xs"]!; var ys = (List<object?>)s.Props["ys"]!;
            int at = -1; float near = 1.5f;
            for (int i = 0; i < xs.Count; i++)
            {
                double vx = (double)xs[i]!;
                if (!Drawable(vx, (double)ys[i]!)) continue;
                float d = MathF.Abs(X(vx) - bestPx);
                if (d <= near) (at, near) = (i, d);
            }
            if (at < 0) continue;
            double vy = (double)ys[at]!;
            double drawn = stacks.TryGetValue(s, out var st) ? st.Top[at] : vy;
            rows.Add((s, vy, drawn, Measure(s.Text("label") ?? ""), Measure(Shown(vy))));
        }
        return new Hover(best, bestPx, py, Measure(xLabel is null ? Shown(best) : $"{xLabel} {Shown(best)}"), rows);
    }

    private static void DrawPlot(DrawContext ctx, float w, float h, float left, float lineHeight, Axis x, Axis y,
        List<FrameNode> marks, Dictionary<FrameNode, string> tones, Dictionary<FrameNode, Note> notes, Bibui.Text.Font font,
        Dictionary<FrameNode, (double[] Base, double[] Top)> stacks, Hover? hover)
    {
        float sc = ctx.Scale;
        var (top, right, bottom, pw, ph) = AreaOf(w, h, left, lineHeight);
        float X(double v) => left + (float)(x.Of(v) * pw);
        float Y(double v) => bottom - (float)(y.Of(v) * ph);
        Rgba ink = ctx.Resolve(UiColor.TextSecondary), grid = ctx.Resolve(UiColor.Border);
        var m = ctx.FontMetrics(font);
        float textH = (m.Descent - m.Ascent) / sc;
        void Write(DrawContext c, string s, float atX, float midY, Rgba colour) => c.DrawText(s, atX * sc, (midY - textH / 2f) * sc, font, colour);
        Rgba Of(DrawContext c, FrameNode node, string fallback, float alpha = 1f) =>
            c.Resolve(Colour(node.Text("tone") ?? (tones.TryGetValue(node, out var t) ? t : fallback), UiColor.Accent).WithAlpha(alpha));

        ctx.PushClip(new RectF(0f, 0f, w * sc, h * sc));
        // The grid and the ticks.
        foreach (var (at, text, width) in y.Ticks)
        {
            float py = Y(at);
            ctx.DrawLine(left * sc, py * sc, right * sc, py * sc, new Paint { Stroke = grid, StrokeWidth = sc });
            Write(ctx, text, left - PlotPad - width, py, ink);
        }
        foreach (var (at, text, width) in x.Ticks)
        {
            float px = X(at);
            ctx.DrawLine(px * sc, bottom * sc, px * sc, (bottom + 4f) * sc, new Paint { Stroke = grid, StrokeWidth = sc });
            Write(ctx, text, Math.Clamp(px - width / 2f, 2f, w - width - 2f), bottom + 4f + lineHeight / 2f, ink);
        }
        ctx.DrawLine(left * sc, bottom * sc, right * sc, bottom * sc, new Paint { Stroke = ctx.Resolve(UiColor.TextDisabled), StrokeWidth = sc });

        // The marks, in the plot's area only: stacked bands, series, then dots, then rules, then points on top.
        ctx.PushClip(new RectF(left * sc, (top - 6f) * sc, (right + 6f) * sc, (bottom + 1f) * sc));
        foreach (var (s, (lower, upper)) in stacks)
        {
            var xs = (List<object?>)s.Props["xs"]!;
            var at = Enumerable.Range(0, xs.Count).Where(i => xs[i] is double vx && double.IsFinite(vx) && (!x.Log || vx > 0)).ToList();
            if (at.Count == 0) continue;
            float Py(double v) => Math.Clamp(y.Log && v <= 0 ? bottom : Y(v), -1e5f, 1e5f);
            // Its band: along its top, and back along the top of the one below.
            var band = ctx.ScratchPath();
            for (int j = 0; j < at.Count; j++)
            {
                float px = Math.Clamp(X((double)xs[at[j]]!), -1e5f, 1e5f), py = Py(upper[at[j]]);
                if (j == 0) band.MoveTo(px * sc, py * sc); else band.LineTo(px * sc, py * sc);
            }
            for (int j = at.Count - 1; j >= 0; j--)
                band.LineTo(Math.Clamp(X((double)xs[at[j]]!), -1e5f, 1e5f) * sc, Py(lower[at[j]]) * sc);
            band.Close();
            ctx.DrawPath(band, new Paint { Fill = Of(ctx, s, "accent", 0.85f) });
            var edge = ctx.ScratchPath();
            for (int j = 0; j < at.Count; j++)
            {
                float px = Math.Clamp(X((double)xs[at[j]]!), -1e5f, 1e5f), py = Py(upper[at[j]]);
                if (j == 0) edge.MoveTo(px * sc, py * sc); else edge.LineTo(px * sc, py * sc);
            }
            float stroke = (float)(s.Props.GetValueOrDefault("width") is double sw ? sw : 1.5d);
            ctx.DrawPath(edge, new Paint { Stroke = Of(ctx, s, "accent"), StrokeWidth = stroke * sc, Join = StrokeJoin.Round });
        }
        foreach (var s in marks.Where(k => k.Type == "series" && !stacks.ContainsKey(k)))
        {
            var xs = (List<object?>)s.Props["xs"]!; var ys = (List<object?>)s.Props["ys"]!;
            var tone = Of(ctx, s, "accent");
            bool fill = Values.Truthy(s.Props.GetValueOrDefault("fill"));
            var path = ctx.ScratchPath();
            bool open = false;
            float firstX = 0f, lastX = 0f;
            for (int i = 0; i < xs.Count; i++)
            {
                double vx = (double)xs[i]!, vy = (double)ys[i]!;
                if (!double.IsFinite(vx) || !double.IsFinite(vy) || (x.Log && vx <= 0) || (y.Log && vy <= 0))
                {
                    if (fill && open) { path.LineTo(lastX * sc, bottom * sc); path.LineTo(firstX * sc, bottom * sc); path.Close(); }
                    open = false;
                    continue;
                }
                float px = Math.Clamp(X(vx), -1e5f, 1e5f), py = Math.Clamp(Y(vy), -1e5f, 1e5f);
                if (!open) { path.MoveTo(px * sc, py * sc); firstX = px; open = true; } else path.LineTo(px * sc, py * sc);
                lastX = px;
            }
            if (fill)
            {
                if (open) { path.LineTo(lastX * sc, bottom * sc); path.LineTo(firstX * sc, bottom * sc); path.Close(); }
                ctx.DrawPath(path, new Paint { Fill = Of(ctx, s, "accent", 0.18f) });
                // The line again on top of its own fill, without the edges down to the axis.
                path = ctx.ScratchPath();
                open = false;
                for (int i = 0; i < xs.Count; i++)
                {
                    double vx = (double)xs[i]!, vy = (double)ys[i]!;
                    if (!double.IsFinite(vx) || !double.IsFinite(vy) || (x.Log && vx <= 0) || (y.Log && vy <= 0)) { open = false; continue; }
                    float px = Math.Clamp(X(vx), -1e5f, 1e5f), py = Math.Clamp(Y(vy), -1e5f, 1e5f);
                    if (!open) { path.MoveTo(px * sc, py * sc); open = true; } else path.LineTo(px * sc, py * sc);
                }
            }
            float stroke = (float)(s.Props.GetValueOrDefault("width") is double sw ? sw : 2d);
            ctx.DrawPath(path, new Paint { Stroke = tone, StrokeWidth = stroke * sc });
        }
        foreach (var d in marks.Where(k => k.Type == "dots"))
        {
            var xs = (List<object?>)d.Props["xs"]!; var ys = (List<object?>)d.Props["ys"]!;
            float r = (float)(d.Props.GetValueOrDefault("r") is double rr ? rr : 3d);
            var paint = new Paint { Fill = Of(ctx, d, "accent") };
            for (int i = 0; i < xs.Count; i++)
                if (xs[i] is double vx && ys[i] is double vy && double.IsFinite(vx) && double.IsFinite(vy))
                    Disc(ctx, X(vx) * sc, Y(vy) * sc, r * sc, paint);
        }
        ctx.PopClip();

        foreach (var rule in marks.Where(k => k.Type == "rule"))
        {
            var tone = Of(ctx, rule, "muted", 0.9f);
            var paint = new Paint { Stroke = tone, StrokeWidth = 1.25f * sc };
            notes.TryGetValue(rule, out var note);
            if (rule.Props.GetValueOrDefault("y") is double ry && double.IsFinite(ry))
            {
                float py = Y(ry);
                if (py < top - 1f || py > bottom + 1f) continue;
                ctx.DrawLine(left * sc, py * sc, right * sc, py * sc, paint);
                if (note is not null) Write(ctx, note.Text, right - note.Width - 4f, py - lineHeight / 2f - 1f, tone);
            }
            else if (rule.Props.GetValueOrDefault("x") is double rx && double.IsFinite(rx))
            {
                float px = X(rx);
                if (px < left - 1f || px > right + 1f) continue;
                ctx.DrawLine(px * sc, top * sc, px * sc, bottom * sc, paint);
                // The note beside the line, on the side with room.
                if (note is not null) Write(ctx, note.Text, px + 6f + note.Width > right ? px - 6f - note.Width : px + 6f, top + lineHeight / 2f, tone);
            }
        }
        foreach (var p in marks.Where(k => k.Type == "point"))
        {
            if (p.Props.GetValueOrDefault("x") is not double vx || p.Props.GetValueOrDefault("y") is not double vy) continue;
            if (!double.IsFinite(vx) || !double.IsFinite(vy)) continue;
            float px = X(vx), py = Y(vy);
            Disc(ctx, px * sc, py * sc, 8f * sc, new Paint { Fill = Of(ctx, p, "accent", 0.25f) });
            Disc(ctx, px * sc, py * sc, 4.5f * sc, new Paint { Fill = Of(ctx, p, "accent"), Stroke = ctx.Resolve(UiColor.SurfaceRaised), StrokeWidth = 1.5f * sc });
            if (notes.TryGetValue(p, out var note))
            {
                float nx = px + 12f + note.Width > right ? px - 12f - note.Width : px + 12f;
                Write(ctx, note.Text, nx, Math.Clamp(py - 10f, top + lineHeight / 2f, bottom - lineHeight / 2f), ctx.Resolve(UiColor.TextPrimary));
            }
        }

        // Under the pointer: the cursor, each series' point on it, and their values in a tip beside it.
        if (hover is not null)
        {
            ctx.DrawLine(hover.Px * sc, top * sc, hover.Px * sc, bottom * sc, new Paint { Stroke = ctx.Resolve(UiColor.TextSecondary.WithAlpha(0.6f)), StrokeWidth = sc });
            foreach (var row in hover.Rows)
                Disc(ctx, hover.Px * sc, Y(row.Drawn) * sc, 4f * sc, new Paint { Fill = Of(ctx, row.Series, "accent"), Stroke = ctx.Resolve(UiColor.SurfaceRaised), StrokeWidth = 1.5f * sc });
            const float Pad = 8f, Swatch = 8f;
            float labels = hover.Rows.Count == 0 ? 0f : hover.Rows.Max(r => r.Label.Width);
            float values = hover.Rows.Count == 0 ? 0f : hover.Rows.Max(r => r.Value.Width);
            float bw = MathF.Max(hover.Head.Width, Swatch + 6f + labels + (labels > 0f ? 14f : 0f) + values) + 2f * Pad;
            float bh = lineHeight * (1 + hover.Rows.Count) + Pad * 1.5f;
            float bx = hover.Px + 12f + bw > w - 4f ? hover.Px - 12f - bw : hover.Px + 12f;
            float by = Math.Clamp(hover.PointerY - bh / 2f, 4f, MathF.Max(4f, h - bh - 4f));
            bx = MathF.Max(4f, bx);
            ctx.DrawRect(new RectF(bx * sc, by * sc, (bx + bw) * sc, (by + bh) * sc), 6f * sc,
                new Paint { Fill = ctx.Resolve(UiColor.Surface), Stroke = ctx.Resolve(UiColor.Border), StrokeWidth = sc });
            float ly = by + Pad * 0.75f + lineHeight / 2f;
            Write(ctx, hover.Head.Text, bx + Pad, ly, ink);
            foreach (var row in hover.Rows)
            {
                ly += lineHeight;
                ctx.DrawRect(new RectF((bx + Pad) * sc, (ly - Swatch / 2f) * sc, (bx + Pad + Swatch) * sc, (ly + Swatch / 2f) * sc), 2f * sc,
                    new Paint { Fill = Of(ctx, row.Series, "accent") });
                Write(ctx, row.Label.Text, bx + Pad + Swatch + 6f, ly, ink);
                Write(ctx, row.Value.Text, bx + bw - Pad - row.Value.Width, ly, ctx.Resolve(UiColor.TextPrimary));
            }
        }
        ctx.PopClip();
    }

    private static void Disc(DrawContext ctx, float cx, float cy, float r, Paint paint)
    {
        var path = ctx.ScratchPath();
        path.MoveTo(cx + r, cy);
        path.ArcTo(r, r, 0f, false, true, cx - r, cy);
        path.ArcTo(r, r, 0f, false, true, cx + r, cy);
        path.Close();
        ctx.DrawPath(path, paint);
    }

    // A chart item's parts, each a value and a tone: one for a bar, its parts for a stacked bar.
    // A part without a tone takes the one its label took before, or the next in turn.
    private static List<(double Value, string Tone)> Segments(Dictionary<string, object?> item, bool stacked, List<(string Label, string Tone)> legend)
    {
        static double Of(object? v) => Values.Num(v) is var d && double.IsFinite(d) ? Math.Max(0, d) : 0;
        if (!stacked) return [(Of(item.GetValueOrDefault("value")), item.GetValueOrDefault("tone") is string t ? t : "accent")];
        var parts = new List<(double, string)>();
        foreach (var part in (item.GetValueOrDefault("parts") as List<object?> ?? []).OfType<Dictionary<string, object?>>())
        {
            string label = Values.Str(part.GetValueOrDefault("label") ?? "");
            int known = legend.FindIndex(l => l.Label == label);
            string tone = part.GetValueOrDefault("tone") is string pt ? pt
                : known >= 0 ? legend[known].Tone : SeriesTones[legend.Count % SeriesTones.Length];
            if (known < 0) legend.Add((label, tone));
            parts.Add((Of(part.GetValueOrDefault("value")), tone));
        }
        return parts;
    }

    // A legend: a swatch and a label for each, wrapped at the width.
    private void Legend(List<(string Label, string Tone)> entries, float width)
    {
        var font = runtime.FontInScope(Smaller);
        float lineHeight = font.Size * 1.3f, used = 0f;
        var row = runtime.BeginParent(runtime.Row());
        foreach (var (label, tone) in entries)
        {
            float w = 10f + 6f + Text.Width(runtime, label, font) + 14f;
            if (used > 0f && used + w > width)
            {
                runtime.EndParent(row);
                runtime.Gap(4f);
                row = runtime.BeginParent(runtime.Row());
                used = 0f;
            }
            Swatch(tone, lineHeight);
            runtime.Gap(6f);
            runtime.Text(label, Smaller, UiColor.TextSecondary);
            runtime.Gap(14f);
            used += w;
        }
        runtime.EndParent(row);
    }

    // A square of a tone, in the middle of a line of small text.
    private void Swatch(string tone, float lineHeight, float size = 10f)
    {
        var cell = runtime.BeginParent(runtime.Column().Sized(Size.Px(size)));
        runtime.Spacer(Size.Px(MathF.Round(lineHeight / 2f - size / 2f)));
        Box swatch = runtime.Box();
        swatch.SizeX = swatch.SizeY = SemanticSize.Pixels(size);
        swatch.Style.BackgroundColor = Colour(tone, UiColor.Accent);
        swatch.Style.CornerRadius = 2.5f;
        runtime.EndParent(cell);
    }

    // bars: a bar per {label, value, tone?}, laid across (the default) or standing up; with
    // stack: true, a bar per {label, parts: [{label, value, tone?}]}, its parts end to end.
    private void Bars(FrameNode n, float width)
    {
        var items = (n.Props.GetValueOrDefault("items") as List<object?> ?? []).OfType<Dictionary<string, object?>>().ToList();
        if (items.Count == 0) return;
        bool stacked = Values.Truthy(n.Props.GetValueOrDefault("stack"));
        var legend = new List<(string Label, string Tone)>();
        var segments = items.Select(i => Segments(i, stacked, legend)).ToList();
        var values = segments.Select(s => s.Sum(p => p.Value)).ToList();
        double max = n.Props.GetValueOrDefault("max") is double mx && mx > 0 ? mx : Math.Max(values.Max(), 1e-12);
        string unit = n.Text("unit") is { } u ? " " + u : "";
        var font = runtime.FontInScope(Smaller);
        var labels = items.Select(i => Values.Str(i.GetValueOrDefault("label") ?? "")).ToList();
        var shown = values.Select(v => Evaluator.Fmt(v, null, Math.Abs(v) >= 100_000, false) + unit).ToList();
        // Motion: the bars ease to new values and a new top (Motion.cs); their numbers are written as they are.
        var eased = Eased(n, "bars", segments.SelectMany(sg => sg.Select(p => p.Value)).Append(max));
        int e = 0;
        segments = segments.Select(sg => sg.Select(p => (Value: eased[e++], p.Tone)).ToList()).ToList();
        values = segments.Select(sg => sg.Sum(p => p.Value)).ToList();
        max = Math.Max(eased[^1], 1e-12);

        if (n.Text("dir") == "column")
        {
            float height = Px(n.Props.GetValueOrDefault("height")) ?? 160f;
            var labelW = labels.Select(l => Text.Width(runtime, l, font)).ToList();
            var valueW = shown.Select(s => Text.Width(runtime, s, font)).ToList();
            float lineHeight = font.Size * 1.3f;
            Box box = runtime.Box();
            box.SizeX = SemanticSize.Pixels(width);
            box.SizeY = SemanticSize.Pixels(height);
            box.Draw = (b, ctx) =>
            {
                float sc = ctx.Scale, w = b.Extent.X, h = b.Extent.Y;
                var m = ctx.FontMetrics(font);
                float textH = (m.Descent - m.Ascent) / sc;
                float slot = w / items.Count, bar = MathF.Min(slot * 0.7f, 48f);
                float baseY = h - lineHeight - 2f, room = baseY - lineHeight - 4f;
                ctx.PushClip(new RectF(0f, 0f, w * sc, h * sc));
                for (int i = 0; i < items.Count; i++)
                {
                    float cx = slot * (i + 0.5f), bh = (float)Math.Clamp(values[i] / max, 0, 1) * room;
                    // From the top part down, each a bar from the base to its own top, so the rounded
                    // top of each shows over the one under it.
                    double to = values[i];
                    for (int j = segments[i].Count - 1; j >= 0; j--)
                    {
                        float ph = (float)Math.Clamp(to / max, 0, 1) * room;
                        to -= segments[i][j].Value;
                        if (ph <= 0f && segments[i].Count > 1) continue;
                        var tone = ctx.Resolve(Colour(segments[i][j].Tone, UiColor.Accent));
                        ctx.DrawRect(new RectF((cx - bar / 2f) * sc, (baseY - MathF.Max(ph, 1f)) * sc, (cx + bar / 2f) * sc, baseY * sc), 3f * sc, new Paint { Fill = tone });
                    }
                    // Values above the bars only where each has room to be read.
                    if (valueW[i] <= slot - 2f)
                        ctx.DrawText(shown[i], (cx - valueW[i] / 2f) * sc, (baseY - bh - 4f - textH) * sc, font, ctx.Resolve(UiColor.TextSecondary));
                    if (labelW[i] <= slot - 2f || i % 2 == 0)
                        ctx.DrawText(labels[i], (cx - labelW[i] / 2f) * sc, (baseY + 4f) * sc, font, ctx.Resolve(UiColor.TextSecondary));
                }
                ctx.PopClip();
            };
            if (stacked) { runtime.Gap(6f); Legend(legend, width); }
            return;
        }

        // Across: the labels in a column as wide as the widest (a third of the width at most), the
        // bar, and its value after it.
        float labelCol = MathF.Min(labels.Max(l => Text.Width(runtime, l, font)) + 4f, MathF.Floor(width / 3f));
        float valueCol = shown.Max(s => Text.Width(runtime, s, font)) + 4f;
        float track = MathF.Max(20f, width - labelCol - valueCol - 24f);
        for (int i = 0; i < items.Count; i++)
        {
            if (i > 0) runtime.Gap(6f);
            var row = runtime.BeginParent(runtime.Row());
            var lc = runtime.BeginParent(runtime.Column().Sized(Size.Px(labelCol)));
            runtime.Text(labels[i], Smaller, UiColor.TextSecondary);
            runtime.EndParent(lc);
            runtime.Gap(12f);
            var cell = runtime.BeginParent(runtime.Column().Sized(Size.Px(track)));
            runtime.Spacer(Size.Px(MathF.Max(0f, MathF.Round(font.Size * 0.65f - 6f))));
            Box bar = runtime.Box();
            bar.SizeX = SemanticSize.Pixels(track);
            bar.SizeY = SemanticSize.Pixels(12f);
            var parts = segments[i];
            bar.Style.BackgroundColor = Colour((stacked ? "muted" : parts[0].Tone) + "@0.14", UiColor.SurfaceRaised);
            bar.Style.CornerRadius = 6f;
            bar.Draw = (b, ctx) =>
            {
                float sc = ctx.Scale;
                // The last part first, as long as all of them; each before it over it, as long as it and those before.
                double to = parts.Sum(p => p.Value);
                for (int j = parts.Count - 1; j >= 0; j--)
                {
                    double share = Math.Clamp(to / max, 0, 1);
                    to -= parts[j].Value;
                    if (share <= 0) continue;
                    ctx.DrawRect(new RectF(0f, 0f, (float)(b.Extent.X * share) * sc, b.Extent.Y * sc), 6f * sc,
                        new Paint { Fill = ctx.Resolve(Colour(parts[j].Tone, UiColor.Accent)) });
                }
            };
            runtime.EndParent(cell);
            runtime.Gap(12f);
            runtime.Text(shown[i], Smaller, UiColor.TextPrimary);
            runtime.EndParent(row);
        }
        if (stacked) { runtime.Gap(8f); Legend(legend, width); }
    }

    // pie: a slice per {label, value, tone?} around from the top, a donut with hole:, its label
    // in the hole, and a legend of the values and their shares beside it (under it where narrow).
    private void Pie(FrameNode n, float width)
    {
        var items = (n.Props.GetValueOrDefault("items") as List<object?> ?? []).OfType<Dictionary<string, object?>>().ToList();
        var values = items.Select(i => Values.Num(i.GetValueOrDefault("value"))).Select(v => double.IsFinite(v) ? Math.Max(0, v) : 0).ToList();
        double total = values.Sum();
        var tones = items.Select((it, i) => it.GetValueOrDefault("tone") is string t ? t : SeriesTones[i % SeriesTones.Length]).ToList();
        float size = MathF.Min(width, Px(n.Props.GetValueOrDefault("height")) ?? 160f);
        float hole = (float)Math.Clamp(Values.Num(n.Props.GetValueOrDefault("hole") ?? 0d), 0, 0.95);
        string unit = n.Text("unit") is { } u ? " " + u : "";
        var font = runtime.FontInScope(Smaller);
        float lineHeight = font.Size * 1.3f;

        // The label in the hole: its first line large, the rest small.
        var big = runtime.FontInScope(Larger);
        var lines = hole > 0 && n.Text("label") is { } label
            ? label.Split('\n').Select((l, i) => (Text: l, Font: i == 0 ? big : font, Width: Text.Width(runtime, l, i == 0 ? big : font))).ToList()
            : [];

        var labels = items.Select(i => Values.Str(i.GetValueOrDefault("label") ?? "")).ToList();
        var amounts = values.Select(v => Evaluator.Fmt(v, null, Math.Abs(v) >= 1_000_000, false) + unit).ToList();
        var shares = values.Select(v => total > 0 ? Evaluator.Fmt(100 * v / total, v / total < 0.1 ? 1 : 0, false, false) + "%" : "").ToList();
        float labelCol = labels.Count == 0 ? 0f : labels.Max(l => Text.Width(runtime, l, font)) + 4f;
        float amountCol = amounts.Count == 0 ? 0f : amounts.Max(a => Text.Width(runtime, a, font)) + 2f;
        float shareCol = shares.Count == 0 ? 0f : shares.Max(s => Text.Width(runtime, s, font)) + 2f;
        float legendW = 10f + 8f + labelCol + 12f + amountCol + 10f + shareCol;
        bool beside = width >= size + 24f + legendW;
        // Motion: the slices ease to new shares (Motion.cs); the legend writes the values as they are.
        values = Eased(n, "pie", values).ToList();
        total = values.Sum();

        var outer = runtime.BeginParent(beside ? runtime.Row() : runtime.Column());
        Box box = runtime.Box();
        box.SizeX = box.SizeY = SemanticSize.Pixels(size);
        box.Draw = (b, ctx) =>
        {
            float sc = ctx.Scale, r = MathF.Min(b.Extent.X, b.Extent.Y) / 2f - 1f, ri = r * hole;
            float cx = b.Extent.X / 2f, cy = b.Extent.Y / 2f;
            int drawn = values.Count(v => v > 0);
            if (drawn == 0)
            {
                var empty = ctx.ScratchPath();
                Slice(empty, cx * sc, cy * sc, r * sc, ri * sc, 0, 1);
                ctx.DrawPath(empty, new Paint { Fill = ctx.Resolve(UiColor.Border) });
            }
            // A sliver between slices, as wide at the rim whatever the radius.
            double gap = drawn > 1 ? 1.5 / (2 * Math.PI * r) : 0;
            double at = 0;
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] <= 0) continue;
                double from = at / total, to = (at + values[i]) / total;
                at += values[i];
                if (to - from <= gap) continue;
                var path = ctx.ScratchPath();
                Slice(path, cx * sc, cy * sc, r * sc, ri * sc, from + gap / 2, to - gap / 2);
                ctx.DrawPath(path, new Paint { Fill = ctx.Resolve(Colour(tones[i], UiColor.Accent)) });
            }
            if (lines.Count == 0) return;
            float lh(int i) => lines[i].Font.Size * 1.25f;
            float y = cy - Enumerable.Range(0, lines.Count).Sum(lh) / 2f;
            for (int i = 0; i < lines.Count; i++)
            {
                var m = ctx.FontMetrics(lines[i].Font);
                float th = (m.Descent - m.Ascent) / sc;
                ctx.DrawText(lines[i].Text, (cx - lines[i].Width / 2f) * sc, (y + lh(i) / 2f - th / 2f) * sc, lines[i].Font,
                    ctx.Resolve(i == 0 ? UiColor.TextPrimary : UiColor.TextSecondary));
                y += lh(i);
            }
        };
        if (beside) runtime.Gap(24f); else runtime.Gap(10f);

        // The legend: swatch, label, value and share, a line each.
        var list = runtime.BeginParent(runtime.Column());
        if (beside) runtime.Spacer(Size.Px(MathF.Max(0f, MathF.Round((size - items.Count * (lineHeight + 4f)) / 2f))));
        for (int i = 0; i < items.Count; i++)
        {
            if (i > 0) runtime.Gap(4f);
            var row = runtime.BeginParent(runtime.Row());
            Swatch(tones[i], lineHeight);
            runtime.Gap(8f);
            var lc = runtime.BeginParent(runtime.Column().Sized(Size.Px(labelCol)));
            runtime.Text(labels[i], Smaller, UiColor.TextSecondary);
            runtime.EndParent(lc);
            runtime.Gap(12f);
            var ac = runtime.BeginParent(runtime.Row().Sized(Size.Px(amountCol)));
            runtime.Spacer(Size.Fill);
            runtime.Text(amounts[i], Smaller, UiColor.TextPrimary);
            runtime.EndParent(ac);
            runtime.Gap(10f);
            var sc2 = runtime.BeginParent(runtime.Row().Sized(Size.Px(shareCol)));
            runtime.Spacer(Size.Fill);
            runtime.Text(shares[i], Smaller, UiColor.TextSecondary);
            runtime.EndParent(sc2);
            runtime.EndParent(row);
        }
        runtime.EndParent(list);
        runtime.EndParent(outer);
    }

    // A point on a circle at a turn: 0 the top, a quarter the right, clockwise.
    private static (float X, float Y) OnCircle(float cx, float cy, float r, double turn) =>
        (cx + r * (float)Math.Sin(2 * Math.PI * turn), cy - r * (float)Math.Cos(2 * Math.PI * turn));

    // Along a circle from one turn to another, in arcs of a quarter at most (an arc of SVG's from
    // a point to itself draws nothing), clockwise when to is past from.
    private static void Around(Bibui.Core.Draw.VectorPath path, float cx, float cy, float r, double from, double to, bool move)
    {
        int pieces = Math.Max(1, (int)Math.Ceiling(Math.Abs(to - from) / 0.25 - 1e-9));
        var (sx, sy) = OnCircle(cx, cy, r, from);
        if (move) path.MoveTo(sx, sy); else path.LineTo(sx, sy);
        for (int i = 1; i <= pieces; i++)
        {
            var (ex, ey) = OnCircle(cx, cy, r, from + (to - from) * i / pieces);
            path.ArcTo(r, r, 0f, false, to > from, ex, ey);
        }
    }

    // A slice of a ring from one turn to another, in physical px; a wedge when the hole is 0.
    private static void Slice(Bibui.Core.Draw.VectorPath path, float cx, float cy, float r, float ri, double from, double to)
    {
        Around(path, cx, cy, r, from, to, move: true);
        if (ri > 0f) Around(path, cx, cy, ri, to, from, move: false);
        else path.LineTo(cx, cy);
        path.Close();
    }

    // heatmap: a grid of cells, column by column (value i in column i / rows, row i % rows),
    // shaded from faint to the tone by value; null is an empty cell. Labels over the columns and
    // before the rows. Cells are square, as large as the width allows (and 28 px at most).
    private void Heatmap(FrameNode n, float width)
    {
        var values = n.Props.GetValueOrDefault("values") as List<object?> ?? [];
        int rows = (int)Values.Num(n.Props.GetValueOrDefault("rows") ?? 7d), columns = (int)Values.Num(n.Props.GetValueOrDefault("columns") ?? 1d);
        rows = Math.Max(1, rows); columns = Math.Max(1, columns);
        float gap = Px(n.Props.GetValueOrDefault("gap")) ?? 3f;
        string tone = n.Text("tone") ?? "accent";
        var finite = values.OfType<double>().Where(double.IsFinite).ToList();
        double max = n.Props.GetValueOrDefault("max") is double mx && mx > 0 ? mx : finite.Count > 0 && finite.Max() > 0 ? finite.Max() : 1;
        var font = runtime.FontInScope(Smaller);
        float lineHeight = font.Size * 1.3f;
        var given = n.Props.GetValueOrDefault("labels") as Dictionary<string, object?>;
        List<Note> Labels(string axis) => (given?.GetValueOrDefault(axis) as List<object?> ?? [])
            .Select(l => l is null ? "" : Values.Str(l)).Select(l => new Note(l, l.Length == 0 ? 0f : Text.Width(runtime, l, font))).ToList();
        var xLabels = Labels("x");
        var yLabels = Labels("y");
        float side = yLabels.Count == 0 ? 0f : yLabels.Max(l => l.Width) + 8f;
        float cell = MathF.Floor(MathF.Min(28f, (width - side - gap * (columns - 1)) / columns));
        cell = MathF.Max(2f, cell);
        float top = xLabels.Any(l => l.Width > 0f) ? lineHeight + 2f : 0f;
        float height = top + rows * cell + (rows - 1) * gap;
        Box box = runtime.Box();
        box.SizeX = SemanticSize.Pixels(width);
        box.SizeY = SemanticSize.Pixels(height);
        box.Draw = (b, ctx) =>
        {
            float sc = ctx.Scale, radius = MathF.Min(3f, cell * 0.2f);
            var m = ctx.FontMetrics(font);
            float textH = (m.Descent - m.Ascent) / sc;
            Rgba ink = ctx.Resolve(UiColor.TextSecondary);
            Rgba empty = ctx.Resolve(UiColor.Border.WithAlpha(0.45f));
            float lastEnd = float.MinValue;
            for (int c = 0; c < Math.Min(columns, xLabels.Count); c++)
            {
                float x = side + c * (cell + gap);
                if (xLabels[c].Width <= 0f || x < lastEnd + 6f) continue;
                ctx.DrawText(xLabels[c].Text, x * sc, (top / 2f - textH / 2f - 1f) * sc, font, ink);
                lastEnd = x + xLabels[c].Width;
            }
            for (int r = 0; r < Math.Min(rows, yLabels.Count); r++)
                if (yLabels[r].Width > 0f)
                    ctx.DrawText(yLabels[r].Text, 0f, (top + r * (cell + gap) + cell / 2f - textH / 2f) * sc, font, ink);
            for (int i = 0; i < values.Count; i++)
            {
                int c = i / rows, r = i % rows;
                float x = side + c * (cell + gap), y = top + r * (cell + gap);
                Rgba fill = values[i] is double v && double.IsFinite(v)
                    ? ctx.Resolve(Colour(tone, UiColor.Accent).WithAlpha(0.14f + 0.86f * (float)Math.Clamp(v / max, 0, 1)))
                    : empty;
                ctx.DrawRect(new RectF(x * sc, y * sc, (x + cell) * sc, (y + cell) * sc), radius * sc, new Paint { Fill = fill });
            }
        };
    }

    // stat's extras: the change, ▲ in green or ▼ in red, and a small line of the values under it.
    private void Delta(double delta)
    {
        UiColor tone = delta > 0 ? UiColor.Success : delta < 0 ? UiColor.Destructive : UiColor.TextSecondary;
        var font = runtime.FontInScope(Smaller);
        float lineHeight = font.Size * 1.3f, size = MathF.Round(font.Size * 0.6f);
        var cell = runtime.BeginParent(runtime.Column().Sized(Size.Px(size)));
        runtime.Spacer(Size.Px(MathF.Round(lineHeight / 2f - size / 2f)));
        Box mark = runtime.Box();
        mark.SizeX = mark.SizeY = SemanticSize.Pixels(size);
        mark.Draw = (b, ctx) =>
        {
            if (delta == 0) return;
            float sc = ctx.Scale, w = b.Extent.X, h = b.Extent.Y;
            var path = ctx.ScratchPath();
            if (delta > 0) { path.MoveTo(w / 2f * sc, 0f); path.LineTo(w * sc, h * sc); path.LineTo(0f, h * sc); }
            else { path.MoveTo(0f, 0f); path.LineTo(w * sc, 0f); path.LineTo(w / 2f * sc, h * sc); }
            path.Close();
            ctx.DrawPath(path, new Paint { Fill = ctx.Resolve(tone) });
        };
        runtime.EndParent(cell);
        runtime.Gap(4f);
        runtime.Text(DeltaText(delta), Smaller, tone, weight: FontWeight.Bold);
    }

    private static string DeltaText(double delta) => Evaluator.Fmt(Math.Round(delta, 1), null, false, delta != 0) + "%";

    private void Spark(List<object?> values, string? tone, float width)
    {
        var ys = values.Select(v => v is double d ? d : double.NaN).ToList();
        var finite = ys.Where(double.IsFinite).ToList();
        if (finite.Count < 2) return;
        double lo = finite.Min(), hi = finite.Max();
        if (!(hi > lo)) (lo, hi) = (lo - 1, hi + 1);
        runtime.Gap(6f);
        Box box = runtime.Box();
        box.SizeX = SemanticSize.Pixels(MathF.Min(width, 220f));
        box.SizeY = SemanticSize.Pixels(30f);
        box.Draw = (b, ctx) =>
        {
            float sc = ctx.Scale, w = b.Extent.X, h = b.Extent.Y, pad = 3f;
            var colour = Colour(tone ?? "accent", UiColor.Accent);
            float X(int i) => pad + (w - 2f * pad) * i / (ys.Count - 1);
            float Y(double v) => h - pad - (float)((v - lo) / (hi - lo)) * (h - 2f * pad);
            var line = ctx.ScratchPath();
            var area = ctx.ScratchPath();
            bool open = false; int last = -1;
            for (int i = 0; i < ys.Count; i++)
            {
                if (!double.IsFinite(ys[i])) continue;
                if (!open) { line.MoveTo(X(i) * sc, Y(ys[i]) * sc); area.MoveTo(X(i) * sc, h * sc); open = true; }
                else line.LineTo(X(i) * sc, Y(ys[i]) * sc);
                area.LineTo(X(i) * sc, Y(ys[i]) * sc);
                last = i;
            }
            area.LineTo(X(last) * sc, h * sc);
            area.Close();
            ctx.DrawPath(area, new Paint { Fill = ctx.Resolve(colour.WithAlpha(0.14f)) });
            ctx.DrawPath(line, new Paint { Stroke = ctx.Resolve(colour), StrokeWidth = 1.5f * sc, Join = StrokeJoin.Round });
            Disc(ctx, X(last) * sc, Y(ys[last]) * sc, 2.5f * sc, new Paint { Fill = ctx.Resolve(colour) });
        };
    }
}

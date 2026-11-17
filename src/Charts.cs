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

        // The values each axis must show: what is drawn on it, or the range the plot gives.
        IEnumerable<double> On(string axis) => marks.SelectMany(m => m.Type switch
        {
            "series" or "dots" => Numbers(m.Props.GetValueOrDefault(axis + "s")),
            "rule" or "point" => m.Props.GetValueOrDefault(axis) is double d ? [d] : [],
            _ => [],
        });
        var x = AxisOf(n.Props.GetValueOrDefault("x"), On("x"), log.Contains('x'), font, tight: true);
        var y = AxisOf(n.Props.GetValueOrDefault("y"), On("y"), log.Contains('y'), font, tight: false);

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
        Box box = runtime.Box();
        box.SizeX = SemanticSize.Pixels(width);
        box.SizeY = SemanticSize.Pixels(height);
        box.Style.BackgroundColor = UiColor.SurfaceRaised;
        box.Style.BorderWidth = 1f;
        box.Style.BorderColor = UiColor.Border;
        box.Style.CornerRadius = 10f;
        box.Draw = (b, ctx) => DrawPlot(ctx, b.Extent.X, b.Extent.Y, left, lineHeight, x, y, marks, tones, notes, font);

        // The legend, when there is more than one thing to tell apart, and the x axis' name.
        var named = marks.Where(m => m.Type is "series" or "dots" && !string.IsNullOrEmpty(m.Text("label"))).ToList();
        string? xLabel = n.Text("xLabel");
        if (named.Count < 2 && xLabel is null) return;
        runtime.Gap(6f);
        var row = runtime.BeginParent(runtime.Row().Sized(Size.Px(width)));
        if (named.Count >= 2)
            foreach (var m in named)
            {
                float sw = m.Type == "dots" ? 8f : 14f, sh = m.Type == "dots" ? 8f : 3f;
                var cell = runtime.BeginParent(runtime.Column().Sized(Size.Px(sw)));
                runtime.Spacer(Size.Px(MathF.Round(lineHeight / 2f - sh / 2f)));
                Box swatch = runtime.Box();
                swatch.SizeX = SemanticSize.Pixels(sw);
                swatch.SizeY = SemanticSize.Pixels(sh);
                swatch.Style.BackgroundColor = Colour(tones[m], UiColor.Accent);
                swatch.Style.CornerRadius = sh / 2f;
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

    private static void DrawPlot(DrawContext ctx, float w, float h, float left, float lineHeight, Axis x, Axis y,
        List<FrameNode> marks, Dictionary<FrameNode, string> tones, Dictionary<FrameNode, Note> notes, Bibui.Text.Font font)
    {
        float sc = ctx.Scale;
        float top = PlotPad + 2f, right = w - PlotPad - 4f, bottom = h - lineHeight - PlotPad;
        float pw = MathF.Max(1f, right - left), ph = MathF.Max(1f, bottom - top);
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

        // The marks, in the plot's area only: series, then dots, then rules, then points on top.
        ctx.PushClip(new RectF(left * sc, (top - 6f) * sc, (right + 6f) * sc, (bottom + 1f) * sc));
        foreach (var s in marks.Where(k => k.Type == "series"))
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

    // bars: a bar per {label, value, tone?}, laid across (the default) or standing up.
    private void Bars(FrameNode n, float width)
    {
        var items = (n.Props.GetValueOrDefault("items") as List<object?> ?? []).OfType<Dictionary<string, object?>>().ToList();
        if (items.Count == 0) return;
        var values = items.Select(i => Values.Num(i.GetValueOrDefault("value"))).Select(v => double.IsFinite(v) ? v : 0).ToList();
        double max = n.Props.GetValueOrDefault("max") is double mx && mx > 0 ? mx : Math.Max(values.Max(), 1e-12);
        string unit = n.Text("unit") is { } u ? " " + u : "";
        var font = runtime.FontInScope(Smaller);
        var labels = items.Select(i => Values.Str(i.GetValueOrDefault("label") ?? "")).ToList();
        var shown = values.Select(v => Evaluator.Fmt(v, null, Math.Abs(v) >= 100_000, false) + unit).ToList();
        string Tone(int i) => items[i].GetValueOrDefault("tone") is string t ? t : "accent";

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
                    var tone = ctx.Resolve(Colour(Tone(i), UiColor.Accent));
                    ctx.DrawRect(new RectF((cx - bar / 2f) * sc, (baseY - MathF.Max(bh, 1f)) * sc, (cx + bar / 2f) * sc, baseY * sc), 3f * sc, new Paint { Fill = tone });
                    // Values above the bars only where each has room to be read.
                    if (valueW[i] <= slot - 2f)
                        ctx.DrawText(shown[i], (cx - valueW[i] / 2f) * sc, (baseY - bh - 4f - textH) * sc, font, ctx.Resolve(UiColor.TextSecondary));
                    if (labelW[i] <= slot - 2f || i % 2 == 0)
                        ctx.DrawText(labels[i], (cx - labelW[i] / 2f) * sc, (baseY + 4f) * sc, font, ctx.Resolve(UiColor.TextSecondary));
                }
                ctx.PopClip();
            };
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
            bar.Style.BackgroundColor = Colour(Tone(i) + "@0.14", UiColor.SurfaceRaised);
            bar.Style.CornerRadius = 6f;
            double share = Math.Clamp(values[i] / max, 0, 1);
            string tone = Tone(i);
            bar.Draw = (b, ctx) =>
            {
                float sc = ctx.Scale;
                ctx.DrawRect(new RectF(0f, 0f, (float)(b.Extent.X * share) * sc, b.Extent.Y * sc), 6f * sc,
                    new Paint { Fill = ctx.Resolve(Colour(tone, UiColor.Accent)) });
            };
            runtime.EndParent(cell);
            runtime.Gap(12f);
            runtime.Text(shown[i], Smaller, UiColor.TextPrimary);
            runtime.EndParent(row);
        }
    }
}

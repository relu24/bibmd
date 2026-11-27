using System.Numerics;
using Bibui.Core;
using Bibui.Core.Runtime;
using BibDoc;

namespace BibMd;

/// <summary>
/// Motion (bibdoc's reference, Motion): the program says where things are each frame, and the
/// renderer eases between frames with bibui's motion. Nothing here runs the program: a box that
/// slides or a bar that grows is drawn from the last run, frame after frame, until it arrives.
/// </summary>
public sealed partial class Bib
{
    // What makes a box (or a chart, or a shape) the same one across runs: its id, else its key:,
    // else its place in the tree, block by block and child by child.
    private readonly Dictionary<FrameNode, string> _paths = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<string> _idents = [];               // this run's boxes, by identity
    private FrameResult? _pathsOf;
    private HashSet<string> _appearing = [], _appearingNext = [];   // the boxes whose children appear or leave
    private readonly Dictionary<FrameNode, (float Progress, bool Leaving, string Key)> _presenceOf = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, Dictionary<string, FrameNode>> _sides = [];   // a box that turns: its node per side
    private Box? _blockBox;   // the block being drawn: a box slides only when it moves within it
    private bool _ghost;
    private const int SettleFrames = 3;
    private int _settling = SettleFrames;   // frames until a new document's layout has settled
    private bool _slides;                     // whether the block being drawn slides what moves in it
    private readonly Dictionary<int, float> _blockWidths = [];   // drawing a box that has left: no id, no events, no slide

    private static readonly Transition ChartMotion = Transition.Timed(0.4f, Easing.Standard);

    // After a run: every node's identity, and the set of the boxes'.
    private void Identify()
    {
        // The children that appear and leave, as the last frame drew them, are this frame's to finish.
        (_appearing, _appearingNext) = (_appearingNext, _appearing);
        if (_settling > 0) _settling--;
        _appearingNext.Clear();
        if (_frame is null || _frame == _pathsOf) return;
        _pathsOf = _frame;
        _paths.Clear();
        _idents.Clear();
        // Anything else (a chart) is the nth of its kind in its block, so a box drawn before it does not make it another.
        var nth = new Dictionary<string, int>();
        for (int k = 0; k < _frame.Blocks.Length; k++)
            Walk(_frame.Blocks[k], k.ToString(), k);

        void Walk(List<FrameNode> nodes, string at, int k)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                string path = at + "/" + i;
                if (nodes[i].Type == "box") _paths[nodes[i]] = path;
                else
                {
                    string kind = k + ":" + nodes[i].Type;
                    int n = nth.GetValueOrDefault(kind);
                    nth[kind] = n + 1;
                    _paths[nodes[i]] = kind + "#" + n;
                }
                if (nodes[i].Type == "box") _idents.Add(Ident(nodes[i]));
                Walk(nodes[i].Children, path, k);
            }
        }
    }

    private string Ident(FrameNode n) =>
        n.Type == "box" && n.Key is { } id ? "id:" + id
        : n.Props.GetValueOrDefault("key") is { } key ? "key:" + Values.Str(key)
        : "at:" + (_paths.GetValueOrDefault(n) ?? "?");

    // animate: {ms, ease} as bibui's transition; null when it does not animate, or the reader asks for less motion
    // (bibui's ReducedMotion): then a change is drawn in the frame it happens, not eased from the one before.
    private Transition? TransitionOf(object? animate)
    {
        if (animate is not Dictionary<string, object?> a || runtime.ReducedMotion) return null;
        float s = (float)Math.Max(0.001, Values.Num(a.GetValueOrDefault("ms")) / 1000.0);
        return Values.Str(a.GetValueOrDefault("ease")) switch
        {
            "linear" => Transition.Timed(s, Easing.Linear),
            "in-out" => Transition.Timed(s, Easing.EaseInOut),
            "spring" => Transition.OnSpring(Spring.FromResponse(s, 0.7f)),
            _ => Transition.Timed(s, Easing.Standard),
        };
    }

    // A chart eases unless it says animate: false.
    private Transition? ChartTransition(FrameNode n) =>
        n.Props.GetValueOrDefault("animate") is false || runtime.ReducedMotion ? null : TransitionOf(n.Props.GetValueOrDefault("animate")) ?? ChartMotion;

    private static PresenceStyle AppearStyle(string? how) => how switch
    {
        "pop" => PresenceStyle.Pop(0.6f),
        "slide-up" => PresenceStyle.FadeUp(16f),
        "slide-down" => PresenceStyle.FadeDown(16f),
        "slide-left" => PresenceStyle.SlideFromRight(24f),
        "slide-right" => PresenceStyle.SlideFromLeft(24f),
        _ => PresenceStyle.Fade,
    };

    /// <summary>
    /// A container's children to draw: this run's, and the boxes with <c>appear:</c> that the run
    /// no longer draws, in the place they were, while they leave. Each child that appears or leaves
    /// is told how far in it is (<see cref="_presenceOf"/>), for its <see cref="DrawBox"/>.
    /// </summary>
    private List<FrameNode> Presented(string parent, List<FrameNode> kids)
    {
        if (runtime.ReducedMotion) return kids;
        bool any = kids.Any(c => c.Type == "box" && c.Props.ContainsKey("appear"));
        if (!any && !_appearing.Contains(parent)) return kids;
        var first = kids.FirstOrDefault(c => c.Props.ContainsKey("appear"));
        var tr = TransitionOf(first?.Props.GetValueOrDefault("animate"));
        // Keys are the children's identities, made distinct where two share one.
        var seen = new Dictionary<string, int>();
        var keyed = kids.Select(c =>
        {
            string id = Ident(c);
            int n = seen.GetValueOrDefault(id);
            seen[id] = n + 1;
            return (Node: c, Key: n == 0 ? id : id + "#" + n);
        }).ToList();
        var rows = runtime.PresenceList("bib-kids:" + parent, keyed, x => x.Key,
            enter: tr ?? Transition.Timed(0.25f, Easing.Emphasized), exit: tr ?? Transition.Timed(0.2f, Easing.EmphasizedAccelerate), appear: true);
        var drawn = new List<FrameNode>(rows.Count);
        bool moving = any;
        foreach (var row in rows)
        {
            var node = row.Item.Node;
            if (!node.Props.ContainsKey("appear"))
            {
                if (!row.Exiting) drawn.Add(node);
                continue;
            }
            // A box that left here and is drawn somewhere else this run has moved, not gone.
            if (row.Exiting && _idents.Contains(Ident(node))) continue;
            if (row.Progress < 1f) { _presenceOf[node] = (row.Progress, row.Exiting, row.Item.Key); moving = true; }
            drawn.Add(node);
        }
        if (moving) _appearingNext.Add(parent);
        return drawn;
    }

    /// <summary>
    /// What a box's motion props do to it this frame: its size, colours and opacity eased, its
    /// appearing, turning, shaking and pulsing, and its slide to a new place. Returns the node to
    /// draw: while a box turns over, its first half shows the side it turned from.
    /// </summary>
    private FrameNode Turned(FrameNode n, string ident, out float scaleX)
    {
        scaleX = 1f;
        if (!n.Props.TryGetValue("turn", out var turn)) return n;
        string side = Values.Str(turn);
        var cue = runtime.Cue("bib-turn:" + ident, side, TransitionOf(n.Props.GetValueOrDefault("animate")) ?? Transition.Timed(0.5f, Easing.EaseInOut));
        if (!_sides.TryGetValue(ident, out var sides)) _sides[ident] = sides = [];
        var shown = cue.Running && Cues.FlipShowsFrom(cue.Progress) && sides.TryGetValue(cue.From!, out var old) ? old : n;
        // The side drawn last for each value; only the one it may turn from is kept besides this one.
        foreach (var other in sides.Keys.Where(s => s != side && s != cue.From).ToList()) sides.Remove(other);
        sides[side] = n;
        if (cue.Running) scaleX = Cues.Flip(cue.Progress);
        return shown;
    }

    private void Move(FrameNode n, string ident, Box box, Transition? tr, float scaleX, (float Progress, bool Leaving, string Key)? presence)
    {
        string key = "bib:" + ident;
        var style = box.Style;
        if (tr is { } t)
        {
            style.BackgroundColor = runtime.AnimateColor(key, "bg", style.BackgroundColor ?? UiColor.Rgba(0, 0, 0, 0), t);
            if (style.BorderColor is { } bc) style.BorderColor = runtime.AnimateColor(key, "border", bc, t);
            style.Opacity = runtime.Animate(key, "opacity", style.Opacity, t);
            style.CornerRadius = runtime.Animate(key, "radius", style.CornerRadius, t);
        }
        if (presence is { } p) AppearStyle(n.Text("appear")).ApplyTo(style, p.Progress);
        if (scaleX != 1f) style.Scale *= new Vector2(scaleX, 1f + 0.04f * (1f - scaleX));
        if (n.Props.TryGetValue("shake", out var shake))
        {
            var cue = runtime.Cue("bib-shake:" + ident, Values.Str(shake), Transition.Timed(0.45f, Easing.Linear));
            if (cue.Running) style.Translate += new Vector2(Cues.Shake(cue.Progress, 8f, 3), 0f);
        }
        if (n.Props.TryGetValue("pulse", out var pulse))
        {
            var cue = runtime.Cue("bib-pulse:" + ident, Values.Str(pulse), Transition.Timed(0.3f, Easing.EaseOut));
            if (cue.Running) style.Scale *= Cues.Pulse(cue.Progress, 0.14f);
        }
        if (tr is { } slide && !_ghost && _slides) runtime.AnimateLayout(key, box, slide, within: _blockBox);
    }

    /// <summary>
    /// A layer whose shapes say <c>animate:</c>, with their numbers and colours where they have
    /// got to: each shape's numbers on one leg (a number, or a w or h with its factor and its
    /// offset), so a bar grows and a ring drains smoothly. A shape whose numbers change in count
    /// (a path's points) is where it is at once.
    /// </summary>
    private FrameNode EasedLayer(FrameNode layer, string ident)
    {
        if (!layer.Children.Any(s => s.Props.ContainsKey("animate"))) return layer;
        var eased = new FrameNode("layer");
        for (int i = 0; i < layer.Children.Count; i++)
        {
            var s = layer.Children[i];
            if (TransitionOf(s.Props.GetValueOrDefault("animate")) is not { } tr) { eased.Children.Add(s); continue; }
            string key = "bib-shape:" + ident + "/" + i + s.Type;
            var copy = new FrameNode(s.Type);
            foreach (var (k, v) in s.Props) copy.Props[k] = v;
            // Every number of the shape, a pair each: a w or h's factor and offset, or 0 and the number.
            var names = Numbers(s.Type);
            var flat = new List<float>();
            foreach (var name in names) Pair(s.Props.GetValueOrDefault(name), flat);
            if (s.Props.GetValueOrDefault("points") is List<object?> pts && s.Type == "path")
                foreach (var pt in pts.OfType<List<object?>>()) { Pair(pt[0], flat); Pair(pt[1], flat); }
            var at = runtime.Animate(key, "numbers", flat.ToArray(), tr).ToArray();
            int j = 0;
            foreach (var name in names) copy.Props[name] = Unpair(s.Props.GetValueOrDefault(name), at, ref j);
            if (s.Props.GetValueOrDefault("points") is List<object?> pts2 && s.Type == "path")
            {
                var points = new List<object?>();
                foreach (var pt in pts2.OfType<List<object?>>()) points.Add(new List<object?> { Unpair(pt[0], at, ref j), Unpair(pt[1], at, ref j) });
                copy.Props["points"] = points;
            }
            foreach (var c in new[] { "fill", "color" })
                if (s.Props.GetValueOrDefault(c) is { } colour)
                    copy.Props[c] = runtime.AnimateColor(key, c, Colour(colour, UiColor.Accent), tr);
            eased.Children.Add(copy);
        }
        return eased;

        static string[] Numbers(string type) => type switch
        {
            "rect" => ["x", "y", "w", "h", "radius", "width"],
            "circle" => ["cx", "cy", "r", "width"],
            "line" => ["x1", "y1", "x2", "y2", "width"],
            "arc" => ["cx", "cy", "r", "from", "to", "width"],
            "label" => ["x", "y"],
            _ => ["width"],
        };

        static void Pair(object? v, List<float> into)
        {
            switch (v)
            {
                case double d: into.Add(0f); into.Add((float)d); break;
                case Dim dim: into.Add((float)dim.K); into.Add((float)dim.C); break;
                default: into.Add(0f); into.Add(0f); break;
            }
        }

        static object? Unpair(object? was, float[] at, ref int j)
        {
            float k = at[j], c = at[j + 1];
            j += 2;
            return was switch
            {
                double => (double)c,
                Dim dim => dim with { K = k, C = c },
                _ => was,
            };
        }
    }

    // A row of numbers a chart draws, eased; the same numbers when it does not animate.
    private double[] Eased(FrameNode chart, string what, IEnumerable<double> values)
    {
        var target = values.Select(v => double.IsFinite(v) ? (float)v : float.NaN).ToArray();
        if (ChartTransition(chart) is not { } tr || target.Any(float.IsNaN)) return values.ToArray();
        return runtime.Animate("bib-chart:" + Ident(chart), what, target, tr).ToArray().Select(v => (double)v).ToArray();
    }
}

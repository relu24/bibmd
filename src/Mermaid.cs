using System.Numerics;
using System.Text.RegularExpressions;
using Bibui.Core;
using Bibui.Core.Draw;
using Bibui.Core.Runtime;
using Bibui.Text;

namespace BibMd;

/// <summary>
/// A ```mermaid fenced block drawn as the diagram it describes, out of the
/// runtime's own boxes and texts, with the connectors in one custom draw: a
/// flowchart (graph) or a state diagram laid out in layers, or a sequence diagram. What it cannot
/// read it says so by returning false, and the block is shown as code.
/// </summary>
public static partial class Mermaid
{
    private enum NodeShape { Rect, Round, Stadium, Circle, Diamond, Hexagon, Subroutine, Cylinder, Participant, Note, Label, Start, End, Bar }
    private enum EdgeLine { Solid, Dotted, Thick, Invisible }
    private enum EdgeHead { None, Arrow, Open, Circle, Cross }

    // A diagram as it is drawn, in logical px from its top-left: boxes, texts and connectors.
    private readonly record struct Piece(float X, float Y, float W, float H, NodeShape Shape);
    private readonly record struct Words(string Text, float X, float Y, float Size, bool Muted);
    private sealed record Wire(Vector2[] Points, bool Curved, bool Vertical, EdgeLine Line, EdgeHead Start, EdgeHead End, bool Muted = false);

    private sealed class Scene
    {
        public float W, H, Font;
        public readonly List<Piece> Pieces = [];
        public readonly List<Words> Texts = [];
        public readonly List<Wire> Wires = [];
    }

    private static readonly Dictionary<(string, float, float, float), Scene?> s_scenes = [];

    /// <summary>
    /// Draws the diagram in a fenced block <paramref name="source"/> whose info
    /// string is mermaid, at most <paramref name="width"/> wide and in the font
    /// size in scope. False, with nothing built, for any other block or one it
    /// cannot read.
    /// </summary>
    public static bool View(BibuiRuntime runtime, string source, float width)
    {
        if (Body(source) is not { } body) return false;
        float size = runtime.FontInScope().Size;
        var key = (source, size, width, runtime.ScaleFactor);
        if (!s_scenes.TryGetValue(key, out var scene))
        {
            if (s_scenes.Count > 64) s_scenes.Clear();
            try { scene = Fit(runtime, body, size, width); }
            catch (FormatException) { scene = null; }
            s_scenes[key] = scene;
        }
        if (scene is null) return false;
        Build(runtime, scene, width);
        return true;
    }

    // The lines between the fences, or null when the block is not a mermaid one.
    private static string[]? Body(string source)
    {
        var lines = source.Split('\n');
        string open = lines[0].Trim();
        if (open.Length < 3 || open[0] is not ('`' or '~')) return null;
        if (!open.TrimStart(open[0]).Trim().Equals("mermaid", StringComparison.OrdinalIgnoreCase)) return null;
        int end = lines.Length;
        if (end > 1 && lines[^1].Trim() is { Length: >= 3 } close && close.All(c => c == open[0])) end--;
        return lines[1..end];
    }

    // Lays the diagram out at the font size given, then smaller until it fits the width:
    // a left-to-right flowchart that would be too small turns top-down first.
    private static Scene Fit(BibuiRuntime rt, string[] body, float size, float width)
    {
        var lines = Statements(body, false);
        if (lines.Count == 0) throw new FormatException("empty");
        string head = lines[0];
        bool sequence = head.StartsWith("sequenceDiagram", StringComparison.Ordinal);
        bool states = head.StartsWith("stateDiagram", StringComparison.Ordinal);
        if (sequence || states) lines = Statements(body, true);
        if (sequence) return Shrink(rt, size, width, fs => Sequence(rt, lines, fs));
        var graph = states ? States(lines) : Flowchart(lines);
        var scene = Shrink(rt, size, width, fs => Flow(rt, graph, fs));
        if (graph.Horizontal && scene.Font < size * 0.75f)
        {
            graph.Horizontal = false;
            graph.Reversed = false;
            var down = Shrink(rt, size, width, fs => Flow(rt, graph, fs));
            if (down.Font > scene.Font) scene = down;
        }
        return scene;
    }

    private static Scene Shrink(BibuiRuntime rt, float size, float width, Func<float, Scene> layout)
    {
        float fs = size;
        var scene = layout(fs);
        for (int i = 0; i < 4 && scene.W > width && fs > 8f; i++)
        {
            fs = MathF.Max(8f, MathF.Floor(fs * width / scene.W * 10f) / 10f);
            scene = layout(fs);
        }
        return scene;
    }

    // The statements of a diagram: comments, directives and front matter out, ; splits a line
    // (but not in the text after a colon, where a sequence or state diagram keeps it).
    private static List<string> Statements(string[] body, bool colonText)
    {
        var list = new List<string>();
        int i = 0;
        while (i < body.Length && string.IsNullOrWhiteSpace(body[i])) i++;
        if (i < body.Length && body[i].Trim() == "---")
            for (i++; i < body.Length && body[i].Trim() != "---"; i++) { }
        for (; i < body.Length; i++)
        {
            string line = body[i].Trim();
            if (line.Length == 0 || line.StartsWith("%%", StringComparison.Ordinal) || line == "---") continue;
            int start = 0, depth = 0;
            bool quoted = false;
            for (int k = 0; k <= line.Length; k++)
            {
                char c = k < line.Length ? line[k] : ';';
                if (colonText && c == ':' && !quoted) { list.Add(line[start..].Trim()); break; }
                if (c == '"') quoted = !quoted;
                else if (!quoted && c is '[' or '(' or '{') depth++;
                else if (!quoted && c is ']' or ')' or '}') depth--;
                else if (c == ';' && !quoted && depth <= 0)
                {
                    if (line[start..k].Trim() is { Length: > 0 } part) list.Add(part);
                    start = k + 1;
                }
            }
        }
        return list;
    }

    // ── Measuring ────────────────────────────────────────────────────────────

    private sealed class Ink(BibuiRuntime rt, float size)
    {
        private readonly Font _font = rt.FontInScope(FontSize.Px(size));
        public float Size => size;
        public float Width(string s) => Text.Width(rt, s, _font);
        public float LineH => rt.Fonts.LineHeight(_font);
        public float Advance => rt.Fonts.LineAdvance(_font);

        // Its lines, broken at <br> and wrapped at max, as wide and tall as they are set.
        public (string[] Lines, float W, float H) Block(string text, float max)
        {
            var lines = new List<string>();
            foreach (var part in text.Split('\n'))
            {
                string p = part.Trim();
                if (p.Length == 0 || Width(p) <= max) { lines.Add(p); continue; }
                foreach (var l in Text.Lines(rt, p, _font, max))
                    lines.Add(p[l.Start..l.End].Trim());
            }
            float w = 0f;
            foreach (var l in lines) w = MathF.Max(w, Width(l));
            return ([.. lines], MathF.Ceiling(w), LineH + (lines.Count - 1) * Advance);
        }

        public void Place(Scene scene, string[] lines, float cx, float cy, bool muted = false)
        {
            float top = cy - (LineH + (lines.Length - 1) * Advance) / 2f;
            for (int i = 0; i < lines.Length; i++)
                scene.Texts.Add(new(lines[i], MathF.Round(cx - Width(lines[i]) / 2f), MathF.Round(top + i * Advance), size, muted));
        }
    }

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)] private static partial Regex Break();

    private static string Clean(string text)
    {
        text = text.Trim();
        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"') text = text[1..^1];
        if (text.Length >= 2 && text[0] == '`' && text[^1] == '`') text = text[1..^1];
        text = Break().Replace(text, "\n").Replace("**", "").Replace("#quot;", "\"").Replace("&amp;", "&")
            .Replace("&lt;", "<").Replace("&gt;", ">");
        return text;
    }

    // ── Flowcharts ───────────────────────────────────────────────────────────

    private sealed class Vertex
    {
        public string Text = "";
        public NodeShape Shape = NodeShape.Rect;
        public int Index;
        public bool Dummy;
        public Edge? LabelOf;
        public string[] Lines = [];
        public float W, H, TextW, TextH;                  // as drawn
        public float Cross, Main, CrossSize, MainSize;    // in the layout's own axes: layers run along main
        public float Depth;                               // of its layer
        public int Rank, Pos;
        public readonly List<Vertex> Up = [], Down = [];
    }

    private sealed class Edge(Vertex from, Vertex to)
    {
        public Vertex From = from, To = to;
        public string? Label;
        public EdgeLine Line;
        public EdgeHead Start, End;
        public int Length = 1;
        public bool Reversed;
        public readonly List<Vertex> Chain = [];
    }

    private sealed class Graph
    {
        public bool Horizontal, Reversed;
        public readonly List<Vertex> Nodes = [];
        public readonly Dictionary<string, Vertex> ById = [];
        public readonly List<Edge> Edges = [];
    }

    [GeneratedRegex(@"^(flowchart|graph)(-elk)?(\s+(?<dir>TD|TB|BT|LR|RL))?$", RegexOptions.IgnoreCase)] private static partial Regex FlowHeader();
    [GeneratedRegex(@"^(classDef|class|style|linkStyle|click|direction|subgraph|end|accTitle|accDescr|title)\b")] private static partial Regex FlowIgnored();
    [GeneratedRegex(@"\G[\p{L}\p{N}_]+")] private static partial Regex Id();
    [GeneratedRegex(@"\G(?<l><)?(?<b>-{2,}|={2,}|-\.+-|~{3,})(?<h>>|[ox](?![\p{L}\p{N}_]))?")] private static partial Regex Arrow();
    [GeneratedRegex(@"\G(?<l><)?(?<b>--|==|-\.)(?=\s)")] private static partial Regex TextArrow();
    [GeneratedRegex(@"-{2,}(>|[ox](?![\p{L}\p{N}_]))|-{3,}")] private static partial Regex SolidClose();
    [GeneratedRegex(@"={2,}(>|[ox](?![\p{L}\p{N}_]))|={3,}")] private static partial Regex ThickClose();
    [GeneratedRegex(@"\.+-+(>|[ox](?![\p{L}\p{N}_]))?")] private static partial Regex DottedClose();

    private static readonly (string Open, string Close, NodeShape Shape)[] Shapes =
    [
        ("(((", ")))", NodeShape.Circle), ("((", "))", NodeShape.Circle), ("([", "])", NodeShape.Stadium),
        ("[[", "]]", NodeShape.Subroutine), ("[(", ")]", NodeShape.Cylinder), ("{{", "}}", NodeShape.Hexagon),
        ("[/", "/]", NodeShape.Rect), ("[/", "\\]", NodeShape.Rect), ("[\\", "\\]", NodeShape.Rect), ("[\\", "/]", NodeShape.Rect),
        ("[", "]", NodeShape.Rect), ("(", ")", NodeShape.Round), ("{", "}", NodeShape.Diamond), (">", "]", NodeShape.Rect),
    ];

    private static Graph Flowchart(List<string> lines)
    {
        var m = FlowHeader().Match(lines[0]);
        if (!m.Success) throw new FormatException("not a flowchart");
        string dir = m.Groups["dir"].Success ? m.Groups["dir"].Value.ToUpperInvariant() : "TB";
        var g = new Graph { Horizontal = dir is "LR" or "RL", Reversed = dir is "BT" or "RL" };
        foreach (var line in lines.Skip(1))
            if (!FlowIgnored().IsMatch(line)) Statement(g, line);
        if (g.Nodes.Count == 0) throw new FormatException("no nodes");
        return g;
    }

    // A ⟶ B ⟶ C, where each side may be A & B, and each node may give its shape and text.
    private static void Statement(Graph g, string s)
    {
        int p = 0;
        var left = Group(g, s, ref p);
        while (true)
        {
            Skip(s, ref p);
            if (p >= s.Length) return;
            var link = Link(s, ref p);
            Skip(s, ref p);
            var right = Group(g, s, ref p);
            foreach (var a in left)
                foreach (var b in right)
                    g.Edges.Add(new Edge(a, b) { Label = link.Label, Line = link.Line, Start = link.Start, End = link.End, Length = link.Length });
            left = right;
        }
    }

    private static void Skip(string s, ref int p)
    {
        while (p < s.Length && char.IsWhiteSpace(s[p])) p++;
    }

    private static List<Vertex> Group(Graph g, string s, ref int p)
    {
        var list = new List<Vertex>();
        while (true)
        {
            Skip(s, ref p);
            list.Add(Node(g, s, ref p));
            Skip(s, ref p);
            if (p < s.Length && s[p] == '&') { p++; continue; }
            return list;
        }
    }

    private static Vertex Node(Graph g, string s, ref int p)
    {
        var id = Id().Match(s, p);
        if (!id.Success) throw new FormatException("a node at " + p);
        p += id.Length;
        if (!g.ById.TryGetValue(id.Value, out var v))
        {
            v = new Vertex { Text = id.Value, Index = g.Nodes.Count };
            g.ById[id.Value] = v;
            g.Nodes.Add(v);
        }
        foreach (var (open, close, shape) in Shapes)
        {
            if (string.CompareOrdinal(s, p, open, 0, open.Length) != 0) continue;
            int from = p + open.Length, end;
            int q = from;
            Skip(s, ref q);
            if (q < s.Length && s[q] == '"')
            {
                int quote = s.IndexOf('"', q + 1);
                if (quote < 0) throw new FormatException("an open quote");
                end = s.IndexOf(close, quote + 1, StringComparison.Ordinal);
            }
            else end = s.IndexOf(close, from, StringComparison.Ordinal);
            if (end < 0) continue;
            v.Text = Clean(s[from..end]);
            v.Shape = shape;
            p = end + close.Length;
            break;
        }
        if (string.CompareOrdinal(s, p, ":::", 0, 3) == 0)
        {
            p += 3;
            var cls = Id().Match(s, p);
            p += cls.Success ? cls.Length : 0;
        }
        return v;
    }

    // ── State diagrams ───────────────────────────────────────────────────────

    [GeneratedRegex(@"^stateDiagram(-v2)?$")] private static partial Regex StateHeader();
    [GeneratedRegex(@"^direction\s+(?<dir>TD|TB|BT|LR|RL)$", RegexOptions.IgnoreCase)] private static partial Regex StateDirection();
    [GeneratedRegex(@"^(?<a>\S+?)\s*-->\s*(?<b>[^\s:]+)\s*(:(?<t>.*))?$")] private static partial Regex Transition();
    [GeneratedRegex(@"^state\s+""(?<t>[^""]*)""\s+as\s+(?<id>[^\s{]+)\s*(?<open>\{)?$")] private static partial Regex StateAs();
    [GeneratedRegex(@"^state\s+(?<id>[^\s:{]+)\s*(<<(?<kind>choice|fork|join)>>)?\s*(?<open>\{)?$")] private static partial Regex StateDecl();
    [GeneratedRegex(@"^(state\s+)?(?<id>[^\s:]+)\s*:(?<t>.*)$")] private static partial Regex StateText();
    [GeneratedRegex(@"^note\s+(left of|right of)\s+[^:]+$", RegexOptions.IgnoreCase)] private static partial Regex StateNoteOpen();
    [GeneratedRegex(@"^(note\b|classDef|class|style|scale|hide|accTitle|accDescr|title|--$)")] private static partial Regex StateIgnored();

    // States and the transitions between them, as a graph: a composite state's inner states sit
    // beside it, with their own start and end; notes are left out.
    private static Graph States(List<string> lines)
    {
        if (!StateHeader().IsMatch(lines[0])) throw new FormatException("not a state diagram");
        var g = new Graph();
        var scopes = new Stack<string>();
        scopes.Push("");
        Vertex Get(string id, bool from)
        {
            int cls = id.IndexOf(":::", StringComparison.Ordinal);
            if (cls >= 0) id = id[..cls];
            bool pseudo = id == "[*]";
            string key = pseudo ? (from ? "[*]>" : "[*]<") + scopes.Peek() : id;
            if (!g.ById.TryGetValue(key, out var v))
            {
                v = new Vertex { Text = pseudo ? "" : id, Index = g.Nodes.Count, Shape = pseudo ? (from ? NodeShape.Start : NodeShape.End) : NodeShape.Round };
                g.ById[key] = v;
                g.Nodes.Add(v);
            }
            return v;
        }
        bool inNote = false;
        foreach (var line in lines.Skip(1))
        {
            if (inNote) { inNote = !line.StartsWith("end note", StringComparison.OrdinalIgnoreCase); continue; }
            if (StateDirection().Match(line) is { Success: true } d)
            {
                if (scopes.Count == 1)
                {
                    string dir = d.Groups["dir"].Value.ToUpperInvariant();
                    (g.Horizontal, g.Reversed) = (dir is "LR" or "RL", dir is "BT" or "RL");
                }
            }
            else if (line == "}") { if (scopes.Count > 1) scopes.Pop(); }
            else if (Transition().Match(line) is { Success: true } t)
            {
                var a = Get(t.Groups["a"].Value, true);
                var b = Get(t.Groups["b"].Value, false);
                string? label = t.Groups["t"].Success ? Clean(t.Groups["t"].Value) : null;
                g.Edges.Add(new Edge(a, b) { Label = string.IsNullOrWhiteSpace(label) ? null : label, Line = EdgeLine.Solid, End = EdgeHead.Arrow });
            }
            else if (StateAs().Match(line) is { Success: true } sa)
            {
                Get(sa.Groups["id"].Value, true).Text = Clean(sa.Groups["t"].Value);
                if (sa.Groups["open"].Success) scopes.Push(sa.Groups["id"].Value);
            }
            else if (StateDecl().Match(line) is { Success: true } sd)
            {
                var v = Get(sd.Groups["id"].Value, true);
                switch (sd.Groups["kind"].Value)
                {
                    case "choice": (v.Shape, v.Text) = (NodeShape.Diamond, ""); break;
                    case "fork" or "join": (v.Shape, v.Text) = (NodeShape.Bar, ""); break;
                }
                if (sd.Groups["open"].Success) scopes.Push(sd.Groups["id"].Value);
            }
            else if (StateNoteOpen().IsMatch(line)) inNote = true;
            else if (StateIgnored().IsMatch(line)) { }
            else if (StateText().Match(line) is { Success: true } st)
            {
                var v = Get(st.Groups["id"].Value, true);
                string text = Clean(st.Groups["t"].Value);
                v.Text = v.Text == st.Groups["id"].Value ? text : v.Text + "\n" + text;
            }
            else if (!line.Contains(' ') && !line.Contains("-->")) Get(line, true);
            else throw new FormatException("a state step: " + line);
        }
        if (g.Nodes.Count == 0) throw new FormatException("no states");
        return g;
    }

    private readonly record struct LinkSpec(string? Label, EdgeLine Line, EdgeHead Start, EdgeHead End, int Length);

    private static LinkSpec Link(string s, ref int p)
    {
        string? label = null;
        string body;
        bool left;
        string head;
        var m = Arrow().Match(s, p);
        var t = TextArrow().Match(s, p);
        if (t.Success && (!m.Success || m.Groups["b"].Value == t.Groups["b"].Value && !m.Groups["h"].Success))
        {
            // -- text -->, == text ==>, -. text .->
            string open = t.Groups["b"].Value;
            var closer = open == "--" ? SolidClose() : open == "==" ? ThickClose() : DottedClose();
            var c = closer.Match(s, p + t.Length);
            if (!c.Success) throw new FormatException("an unclosed link");
            label = Clean(s[(p + t.Length)..c.Index]);
            left = t.Groups["l"].Success;
            body = open == "-." ? "-" + c.Value.TrimEnd('>', 'o', 'x') : c.Value.TrimEnd('>', 'o', 'x');
            head = c.Groups[1].Value;
            p = c.Index + c.Length;
        }
        else if (m.Success)
        {
            left = m.Groups["l"].Success;
            body = m.Groups["b"].Value;
            head = m.Groups["h"].Value;
            p += m.Length;
        }
        else throw new FormatException("a link at " + p);

        var line = body[0] == '~' ? EdgeLine.Invisible : body[0] == '=' ? EdgeLine.Thick : body.Contains('.') ? EdgeLine.Dotted : EdgeLine.Solid;
        var end = head switch { ">" => EdgeHead.Arrow, "o" => EdgeHead.Circle, "x" => EdgeHead.Cross, _ => EdgeHead.None };
        int length = line == EdgeLine.Dotted ? body.Count(c => c == '.') : Math.Max(1, body.Length - (head.Length > 0 ? 1 : 2));
        int q = p;
        Skip(s, ref q);
        if (q < s.Length && s[q] == '|')
        {
            int close = s.IndexOf('|', q + 1);
            if (close < 0) throw new FormatException("an unclosed label");
            label = Clean(s[(q + 1)..close]);
            p = close + 1;
        }
        return new(string.IsNullOrWhiteSpace(label) ? null : label, line, left ? EdgeHead.Arrow : EdgeHead.None, end, Math.Clamp(length, 1, 4));
    }

    // The layout: cycles broken by turning edges round, ranks by the longest path (each edge
    // two ranks, a label in the one between), long edges through dummies, each layer ordered by
    // the barycentres of its neighbours, then placed as close to them as the spacing allows.
    private static Scene Flow(BibuiRuntime rt, Graph g, float fs)
    {
        var ink = new Ink(rt, fs);
        var small = new Ink(rt, MathF.Round(fs * 0.88f * 10f) / 10f);
        bool horizontal = g.Horizontal;
        foreach (var v in g.Nodes) Measure(v, ink, horizontal);
        foreach (var e in g.Edges) { e.Reversed = false; e.Chain.Clear(); }

        var nodes = g.Nodes;
        int n = nodes.Count;
        var outs = new List<Edge>[n];
        for (int i = 0; i < n; i++) { outs[i] = []; nodes[i].Up.Clear(); nodes[i].Down.Clear(); nodes[i].Rank = 0; }
        var edges = g.Edges.Where(e => e.From != e.To).ToList();
        foreach (var e in edges) outs[e.From.Index].Add(e);

        // Cycles: an edge back to a node still being visited is turned round.
        var state = new int[n];
        void Visit(int v)
        {
            state[v] = 1;
            foreach (var e in outs[v])
            {
                if (state[e.To.Index] == 1) e.Reversed = true;
                else if (state[e.To.Index] == 0) Visit(e.To.Index);
            }
            state[v] = 2;
        }
        for (int i = 0; i < n; i++) if (state[i] == 0) Visit(i);

        Vertex Top(Edge e) => e.Reversed ? e.To : e.From;
        Vertex Bottom(Edge e) => e.Reversed ? e.From : e.To;
        var below = new List<Edge>[n];
        var indeg = new int[n];
        for (int i = 0; i < n; i++) below[i] = [];
        foreach (var e in edges) { below[Top(e).Index].Add(e); indeg[Bottom(e).Index]++; }

        var queue = new Queue<int>();
        for (int i = 0; i < n; i++) if (indeg[i] == 0) queue.Enqueue(i);
        var topo = new List<int>();
        var left = (int[])indeg.Clone();
        while (queue.Count > 0)
        {
            int v = queue.Dequeue();
            topo.Add(v);
            foreach (var e in below[v])
            {
                var b = Bottom(e);
                b.Rank = Math.Max(b.Rank, nodes[v].Rank + 2 * e.Length);
                if (--left[b.Index] == 0) queue.Enqueue(b.Index);
            }
        }
        // A node nothing points to sits just above the nearest it points to, not at the very top.
        foreach (int v in topo.AsEnumerable().Reverse())
            if (indeg[v] == 0 && below[v].Count > 0)
                nodes[v].Rank = below[v].Min(e => Bottom(e).Rank - 2 * e.Length);

        int maxRank = nodes.Max(v => v.Rank);
        var layers = new List<Vertex>[maxRank + 1];
        for (int r = 0; r <= maxRank; r++) layers[r] = [];
        foreach (var v in nodes) layers[v.Rank].Add(v);
        foreach (var e in edges)
        {
            Vertex top = Top(e), bottom = Bottom(e);
            e.Chain.Add(top);
            int span = bottom.Rank - top.Rank;
            int labelAt = top.Rank + 1 + 2 * ((span / 2 - 1) / 2);
            for (int r = top.Rank + 1; r < bottom.Rank; r++)
            {
                var d = new Vertex { Dummy = true, Rank = r, Index = top.Index };
                if (e.Label is not null && r == labelAt)
                {
                    d.LabelOf = e;
                    (d.Lines, d.TextW, d.TextH) = small.Block(e.Label, fs * 12f);
                    d.W = d.TextW + fs * 0.6f;
                    d.H = d.TextH + fs * 0.3f;
                    (d.CrossSize, d.MainSize) = horizontal ? (d.H, d.W) : (d.W, d.H);
                }
                layers[r].Add(d);
                e.Chain.Add(d);
            }
            e.Chain.Add(bottom);
            for (int k = 1; k < e.Chain.Count; k++)
            {
                e.Chain[k - 1].Down.Add(e.Chain[k]);
                e.Chain[k].Up.Add(e.Chain[k - 1]);
            }
        }

        Order(layers);
        Place(layers, fs, horizontal);

        // Along the layers: each as deep as its deepest box, with a gap either side.
        float gap = horizontal ? fs * 2f : fs * 1.35f;
        float main = 0f;
        bool first = true;
        foreach (var layer in layers)
        {
            if (layer.Count == 0) continue;
            float depth = layer.Max(v => v.MainSize);
            // Edges that run far across get more room to turn.
            float across = 0f;
            foreach (var v in layer) foreach (var u in v.Up) across = MathF.Max(across, MathF.Abs(v.Cross - u.Cross));
            if (!first) main += MathF.Max(gap, MathF.Min(gap * 2.2f, across * 0.22f));
            first = false;
            foreach (var v in layer) (v.Main, v.Depth) = (main + depth / 2f, depth);
            main += depth;
        }

        var all = layers.SelectMany(l => l).ToList();
        float loop = g.Edges.Any(e => e.From == e.To) ? fs * 1.6f : 0f;
        float minC = all.Min(v => v.Cross - v.CrossSize / 2f), maxC = all.Max(v => v.Cross + v.CrossSize / 2f) + loop;
        const float Margin = 2f;
        foreach (var v in all) { v.Cross += Margin - minC; v.Main += Margin; }
        float crossTotal = maxC - minC + 2f * Margin, mainTotal = main + 2f * Margin;

        var scene = new Scene { Font = fs };
        (scene.W, scene.H) = horizontal ? (mainTotal, crossTotal) : (crossTotal, mainTotal);
        Vector2 At(float c, float m)
        {
            if (g.Reversed) m = mainTotal - m;
            return horizontal ? new(m, c) : new(c, m);
        }

        // Where each edge leaves and enters a box: spread along its side, in the order of where they go.
        var leave = new Dictionary<Edge, float>();
        var enter = new Dictionary<Edge, float>();
        foreach (var v in nodes)
        {
            Spread(edges.Where(e => e.Chain[0] == v).OrderBy(e => e.Chain[1].Cross).ToList(), v, fs, horizontal, leave);
            Spread(edges.Where(e => e.Chain[^1] == v).OrderBy(e => e.Chain[^2].Cross).ToList(), v, fs, horizontal, enter);
        }
        foreach (var e in edges)
        {
            // Through a layer straight, top to bottom, so it bends only in the gaps between.
            Vertex top = e.Chain[0], bottom = e.Chain[^1];
            float a = leave[e], b = enter[e];
            var pts = new List<Vector2> { At(top.Cross + a, top.Main + Extent(top, a, horizontal)) };
            foreach (var d in e.Chain.Skip(1).SkipLast(1))
            {
                if (d.Depth > 0f) pts.Add(At(d.Cross, d.Main - d.Depth / 2f));
                pts.Add(At(d.Cross, d.Main + d.Depth / 2f));
            }
            pts.Add(At(bottom.Cross + b, bottom.Main - Extent(bottom, b, horizontal)));
            var (start, end) = e.Reversed ? (e.End, e.Start) : (e.Start, e.End);
            scene.Wires.Add(new Wire([.. pts], true, !horizontal, e.Line, start, end));
        }
        foreach (var e in g.Edges.Where(e => e.From == e.To))
        {
            var v = e.From;
            float c = v.Cross + CrossExtent(v, v.MainSize / 4f, horizontal), m0 = v.Main - v.MainSize / 4f, m1 = v.Main + v.MainSize / 4f;
            scene.Wires.Add(new Wire([At(c, m0), At(c + fs * 1.3f, m0), At(c + fs * 1.3f, m1), At(c, m1)], false, !horizontal, e.Line, e.Start, e.End));
        }

        foreach (var v in nodes)
        {
            var c = At(v.Cross, v.Main);
            scene.Pieces.Add(new(MathF.Round(c.X - v.W / 2f), MathF.Round(c.Y - v.H / 2f), MathF.Round(v.W), MathF.Round(v.H), v.Shape));
            ink.Place(scene, v.Lines, c.X, c.Y);
        }
        foreach (var d in all.Where(v => v.LabelOf is not null))
        {
            var c = At(d.Cross, d.Main);
            scene.Pieces.Add(new(MathF.Round(c.X - d.W / 2f), MathF.Round(c.Y - d.H / 2f), MathF.Round(d.W), MathF.Round(d.H), NodeShape.Label));
            small.Place(scene, d.Lines, c.X, c.Y, muted: true);
        }
        return scene;
    }

    // A node's size from its text and shape.
    private static void Measure(Vertex v, Ink ink, bool horizontal)
    {
        float fs = ink.Size;
        (v.Lines, v.TextW, v.TextH) = ink.Block(v.Text, fs * 14f);
        float w = v.TextW + 2f * fs, h = v.TextH + 1.3f * fs;
        switch (v.Shape)
        {
            case NodeShape.Diamond:
            {
                // A rhombus holds the text when w/W + h/H ≤ 1: half again as wide, the rest in height.
                float tw = v.TextW + fs * 0.6f, th = v.TextH + fs * 0.3f;
                (w, h) = (tw * 1.6f, th * 1.6f / 0.6f);
                h = MathF.Max(h, fs * 3f);
                break;
            }
            case NodeShape.Circle:
                w = h = MathF.Sqrt(v.TextW * v.TextW + v.TextH * v.TextH) + fs * 0.8f;
                break;
            case NodeShape.Stadium:
                w = v.TextW + 1.2f * fs + h;
                break;
            case NodeShape.Hexagon:
                w = v.TextW + 1.2f * fs + h;
                break;
            case NodeShape.Cylinder:
                h += fs * 0.6f;
                break;
            case NodeShape.Start:
                w = h = MathF.Round(fs * 0.9f);
                break;
            case NodeShape.End:
                w = h = MathF.Round(fs * 1.1f);
                break;
            case NodeShape.Bar:
                (w, h) = horizontal ? (fs * 0.4f, fs * 4f) : (fs * 4f, fs * 0.4f);
                break;
        }
        (v.W, v.H) = (MathF.Round(w), MathF.Round(h));
        (v.CrossSize, v.MainSize) = horizontal ? (v.H, v.W) : (v.W, v.H);
    }

    // How far from a node's centre its side is along the main axis, at cross offset d.
    private static float Extent(Vertex v, float d, bool horizontal)
    {
        if (v.Dummy) return 0f;
        float half = v.MainSize / 2f, across = v.CrossSize / 2f;
        d = MathF.Abs(d);
        return v.Shape switch
        {
            NodeShape.Diamond => half * (1f - d / across),
            NodeShape.Circle or NodeShape.Start or NodeShape.End => MathF.Sqrt(MathF.Max(0f, half * half - d * d)),
            NodeShape.Stadium when horizontal => half - across + MathF.Sqrt(MathF.Max(0f, across * across - d * d)),
            NodeShape.Hexagon when horizontal => half - d,
            _ => half,
        };
    }

    // The same across: how far its side is along the cross axis, at main offset d (for a loop back to itself).
    private static float CrossExtent(Vertex v, float d, bool horizontal)
    {
        float half = v.CrossSize / 2f, along = v.MainSize / 2f;
        return v.Shape switch
        {
            NodeShape.Diamond => half * (1f - MathF.Abs(d) / along),
            NodeShape.Circle or NodeShape.Start or NodeShape.End => MathF.Sqrt(MathF.Max(0f, half * half - d * d)),
            NodeShape.Stadium or NodeShape.Hexagon when !horizontal => half - along * 0.4f,
            _ => half,
        };
    }

    // How far along its side the edges of a node may spread, either way from the centre.
    private static float Room(Vertex v, float fs, bool horizontal)
    {
        float across = v.CrossSize / 2f;
        return v.Shape switch
        {
            NodeShape.Diamond => across * 0.45f,
            NodeShape.Circle => across * 0.55f,
            NodeShape.Start or NodeShape.End => 0f,
            NodeShape.Bar => across * 0.8f,
            NodeShape.Stadium or NodeShape.Hexagon => horizontal ? across * 0.6f : across - v.MainSize / 2f,
            _ => across - fs * 0.6f,
        };
    }

    private static void Spread(List<Edge> list, Vertex v, float fs, bool horizontal, Dictionary<Edge, float> into)
    {
        int k = list.Count;
        float room = MathF.Max(0f, Room(v, fs, horizontal));
        float step = k > 1 ? MathF.Min(fs * 1.1f, 2f * room / (k - 1)) : 0f;
        for (int i = 0; i < k; i++) into[list[i]] = (i - (k - 1) / 2f) * step;
    }

    // Each layer ordered by the barycentres of the one before, sweeping down and up, keeping the order with fewest crossings.
    private static void Order(List<Vertex>[] layers)
    {
        for (int i = 0; i < layers[0].Count; i++) layers[0][i].Pos = i;
        for (int r = 1; r < layers.Length; r++) Sort(layers[r], up: true);
        int fewest = Transpose(layers);
        var best = layers.Select(l => l.ToList()).ToArray();
        for (int it = 0; it < 24 && fewest > 0; it++)
        {
            if (it % 2 == 0) for (int r = 1; r < layers.Length; r++) Sort(layers[r], up: true);
            else for (int r = layers.Length - 2; r >= 0; r--) Sort(layers[r], up: false);
            int c = Transpose(layers);
            if (c < fewest)
            {
                fewest = c;
                best = layers.Select(l => l.ToList()).ToArray();
            }
        }
        for (int r = 0; r < layers.Length; r++)
        {
            layers[r] = best[r];
            for (int i = 0; i < layers[r].Count; i++) layers[r][i].Pos = i;
        }
    }

    private static void Sort(List<Vertex> layer, bool up)
    {
        var sorted = layer.Select((v, i) =>
        {
            var near = up ? v.Up : v.Down;
            float key = near.Count > 0 ? (float)near.Average(u => u.Pos) : v.Pos;
            return (v, key, i);
        }).OrderBy(x => x.key).ThenBy(x => x.v.Pos).ThenBy(x => x.i).Select(x => x.v).ToList();
        layer.Clear();
        layer.AddRange(sorted);
        for (int i = 0; i < layer.Count; i++) layer[i].Pos = i;
    }

    // Neighbours in a layer swapped wherever that crosses fewer edges: what the barycentres leave on a tie.
    private static int Transpose(List<Vertex>[] layers)
    {
        int count = Crossings(layers);
        if (layers.Sum(l => l.Count) > 200) return count;
        // The first pass also swaps on a tie, to step off a level stretch.
        for (bool better = true, ties = true; better && count > 0; ties = false)
        {
            better = false;
            foreach (var layer in layers)
                for (int i = 0; i + 1 < layer.Count; i++)
                {
                    (layer[i], layer[i + 1]) = (layer[i + 1], layer[i]);
                    (layer[i].Pos, layer[i + 1].Pos) = (i, i + 1);
                    int c = Crossings(layers);
                    if (c < count || ties && c == count) { better |= c < count || ties; count = c; continue; }
                    (layer[i], layer[i + 1]) = (layer[i + 1], layer[i]);
                    (layer[i].Pos, layer[i + 1].Pos) = (i, i + 1);
                }
        }
        return count;
    }

    private static int Crossings(List<Vertex>[] layers)
    {
        int count = 0;
        var segs = new List<(int A, int B)>();
        for (int r = 0; r + 1 < layers.Length; r++)
        {
            segs.Clear();
            foreach (var u in layers[r]) foreach (var d in u.Down) segs.Add((u.Pos, d.Pos));
            for (int i = 0; i < segs.Count; i++)
                for (int j = i + 1; j < segs.Count; j++)
                    if ((segs[i].A - segs[j].A) * (segs[i].B - segs[j].B) < 0) count++;
        }
        return count;
    }

    // Across the layers: each node as near the mean of its neighbours as the order and spacing allow,
    // sweeping down and up a few times.
    private static void Place(List<Vertex>[] layers, float fs, bool horizontal)
    {
        float Sep(Vertex a, Vertex b)
        {
            float g = a.Dummy && b.Dummy ? fs * 0.5f : a.Dummy || b.Dummy ? fs * 0.8f : horizontal ? fs * 1.1f : fs * 1.5f;
            if (a.LabelOf is not null || b.LabelOf is not null) g = MathF.Max(g, fs * 0.8f);
            return (a.CrossSize + b.CrossSize) / 2f + g;
        }
        foreach (var layer in layers)
        {
            float x = 0f;
            for (int i = 0; i < layer.Count; i++)
            {
                if (i > 0) x += Sep(layer[i - 1], layer[i]);
                layer[i].Cross = x;
            }
            foreach (var v in layer) v.Cross -= x / 2f;
        }
        for (int it = 0; it < 8; it++)
        {
            for (int r = 1; r < layers.Length; r++) Settle(layers[r], v => v.Up, Sep);
            for (int r = layers.Length - 2; r >= 0; r--) Settle(layers[r], v => v.Down, Sep);
        }
        for (int r = 0; r < layers.Length; r++) Settle(layers[r], v => [.. v.Up, .. v.Down], Sep);
    }

    // The positions nearest the wanted ones that keep the order and the spacing: an isotonic fit
    // (pool adjacent violators) of the wanted positions less each one's least offset from the first.
    private static void Settle(List<Vertex> layer, Func<Vertex, List<Vertex>> near, Func<Vertex, Vertex, float> sep)
    {
        int n = layer.Count;
        if (n == 0) return;
        var offset = new float[n];
        for (int i = 1; i < n; i++) offset[i] = offset[i - 1] + sep(layer[i - 1], layer[i]);
        var blocks = new List<(float Sum, int Count, int End)>();
        for (int i = 0; i < n; i++)
        {
            var nb = near(layer[i]);
            float want = (nb.Count > 0 ? nb.Average(u => u.Cross) : layer[i].Cross) - offset[i];
            var cur = (Sum: want, Count: 1, End: i);
            while (blocks.Count > 0 && blocks[^1].Sum / blocks[^1].Count > cur.Sum / cur.Count)
            {
                var prev = blocks[^1];
                blocks.RemoveAt(blocks.Count - 1);
                cur = (prev.Sum + cur.Sum, prev.Count + cur.Count, i);
            }
            blocks.Add(cur);
        }
        int at = 0;
        foreach (var b in blocks)
            for (; at <= b.End; at++) layer[at].Cross = b.Sum / b.Count + offset[at];
    }

    // ── Sequence diagrams ────────────────────────────────────────────────────

    [GeneratedRegex(@"^(participant|actor)\s+(?<id>.+?)(\s+as\s+(?<as>.+))?$")] private static partial Regex Participant();
    [GeneratedRegex(@"^(?<a>[^:]+?)\s*(?<arrow>-->>|->>|--x|-x|--\)|-\)|-->|->)\s*[+-]?\s*(?<b>[^:]+?)\s*:(?<t>.*)$")] private static partial Regex Message();
    [GeneratedRegex(@"^note\s+(?<where>left of|right of|over)\s+(?<who>[^:]+?)\s*:(?<t>.*)$", RegexOptions.IgnoreCase)] private static partial Regex Note();
    [GeneratedRegex(@"^(autonumber|activate|deactivate|loop|alt|else|opt|par|and|critical|break|rect|end|title|box)\b")] private static partial Regex SequenceIgnored();

    private sealed class Actor
    {
        public string Text = "";
        public string[] Lines = [];
        public float W, X, TextW;
    }

    private sealed record Step(Actor A, Actor? B, string Text, bool Dotted, EdgeHead Head, string? Where);

    private static Scene Sequence(BibuiRuntime rt, List<string> lines, float fs)
    {
        var ink = new Ink(rt, fs);
        var actors = new List<Actor>();
        var byId = new Dictionary<string, Actor>();
        Actor Get(string id, string? text = null)
        {
            id = id.Trim();
            if (!byId.TryGetValue(id, out var a)) actors.Add(byId[id] = a = new Actor { Text = id });
            if (text is not null) a.Text = Clean(text);
            return a;
        }
        var steps = new List<Step>();
        foreach (var line in lines.Skip(1))
        {
            if (Participant().Match(line) is { Success: true } p) Get(p.Groups["id"].Value, p.Groups["as"].Success ? p.Groups["as"].Value : null);
            else if (Note().Match(line) is { Success: true } n)
            {
                var who = n.Groups["who"].Value.Split(',');
                steps.Add(new(Get(who[0]), who.Length > 1 ? Get(who[1]) : null, Clean(n.Groups["t"].Value), false, EdgeHead.None,
                    n.Groups["where"].Value.ToLowerInvariant()));
            }
            else if (Message().Match(line) is { Success: true } m)
            {
                string arrow = m.Groups["arrow"].Value;
                var head = arrow.EndsWith(">>") ? EdgeHead.Arrow : arrow.EndsWith('x') ? EdgeHead.Cross : arrow.EndsWith(')') ? EdgeHead.Open : EdgeHead.None;
                steps.Add(new(Get(m.Groups["a"].Value), Get(m.Groups["b"].Value), Clean(m.Groups["t"].Value), arrow.StartsWith("--"), head, null));
            }
            else if (!SequenceIgnored().IsMatch(line)) throw new FormatException("a sequence step: " + line);
        }
        if (actors.Count == 0) throw new FormatException("no participants");

        float pad = fs, boxH = 0f;
        foreach (var a in actors)
        {
            (a.Lines, a.TextW, float h) = ink.Block(a.Text, fs * 12f);
            a.W = MathF.Round(a.TextW + 2f * pad);
            boxH = MathF.Max(boxH, MathF.Round(h + 1.3f * fs));
        }
        var blocks = steps.Select(s => ink.Block(s.Text, fs * 18f)).ToList();

        // Across: each participant clear of the one before, and far enough from the others for what is said between them.
        for (int i = 0; i < actors.Count; i++)
            actors[i].X = i == 0 ? actors[0].W / 2f : actors[i - 1].X + (actors[i - 1].W + actors[i].W) / 2f + fs * 1.5f;
        float lo = 0f, hi = actors[^1].X + actors[^1].W / 2f;
        void Push(int from, float by)
        {
            if (by <= 0f) return;
            for (int k = from; k < actors.Count; k++) actors[k].X += by;
        }
        for (int s = 0; s < steps.Count; s++)
        {
            var st = steps[s];
            int a = actors.IndexOf(st.A), b = st.B is null ? a : actors.IndexOf(st.B);
            float w = blocks[s].W;
            if (st.Where is null && a != b)
                Push(Math.Max(a, b), w + fs * 2f - MathF.Abs(actors[b].X - actors[a].X));
            else if (st.Where is null || st.Where == "right of")
            {
                float need = w + fs * (st.Where is null ? 3f : 2.5f);
                if (a + 1 < actors.Count) Push(a + 1, need - (actors[a + 1].X - actors[a].X));
            }
            else if (st.Where == "left of" && a > 0)
                Push(a, w + fs * 2.5f - (actors[a].X - actors[a - 1].X));
        }

        var scene = new Scene { Font = fs };
        float y = boxH + fs * 1.4f;
        float arrowRise = fs * 0.35f;
        var notes = new List<Piece>();
        for (int s = 0; s < steps.Count; s++)
        {
            var st = steps[s];
            var (tl, tw, th) = blocks[s];
            float xa = st.A.X, xb = (st.B ?? st.A).X;
            if (st.Where is { } where)
            {
                float w = tw + fs * 1.2f, h = th + fs * 0.8f, x;
                if (where == "over")
                {
                    float l = MathF.Min(xa, xb), r = MathF.Max(xa, xb);
                    w = MathF.Max(w, r - l + fs * 2f);
                    x = (l + r) / 2f - w / 2f;
                }
                else x = where == "right of" ? xa + fs * 0.6f : xa - fs * 0.6f - w;
                notes.Add(new(MathF.Round(x), MathF.Round(y), MathF.Round(w), MathF.Round(h), NodeShape.Note));
                ink.Place(scene, tl, x + w / 2f, y + h / 2f);
                lo = MathF.Min(lo, x);
                hi = MathF.Max(hi, x + w);
                y += h + fs * 0.9f;
                continue;
            }
            var line = st.Dotted ? EdgeLine.Dotted : EdgeLine.Solid;
            if (st.A == st.B)
            {
                float loopW = fs * 1.8f, loopH = fs * 1.2f;
                ink.Place(scene, tl, xa + loopW + fs * 0.4f + tw / 2f, y + th / 2f);
                float ay = y + th + arrowRise;
                scene.Wires.Add(new Wire([new(xa, ay - loopH * 0.5f), new(xa + loopW, ay - loopH * 0.5f), new(xa + loopW, ay + loopH * 0.5f), new(xa, ay + loopH * 0.5f)],
                    false, false, line, EdgeHead.None, st.Head));
                hi = MathF.Max(hi, xa + loopW + fs * 0.4f + tw);
                y = ay + loopH * 0.5f + fs * 1.1f;
            }
            else
            {
                ink.Place(scene, tl, (xa + xb) / 2f, y + th / 2f);
                float ay = MathF.Round(y + th + arrowRise);
                scene.Wires.Add(new Wire([new(xa, ay), new(xb, ay)], false, false, line, EdgeHead.None, st.Head));
                y = ay + fs * 1.3f;
            }
        }
        float bottom = MathF.Round(y + fs * 0.2f);
        foreach (var a in actors)
            scene.Wires.Insert(0, new Wire([new(a.X, boxH), new(a.X, bottom)], false, true, EdgeLine.Dotted, EdgeHead.None, EdgeHead.None, Muted: true));
        foreach (var a in actors)
            foreach (float top in new[] { 0f, bottom })
            {
                scene.Pieces.Add(new(MathF.Round(a.X - a.W / 2f), top, a.W, boxH, NodeShape.Participant));
                ink.Place(scene, a.Lines, a.X, top + boxH / 2f);
            }
        scene.Pieces.AddRange(notes);
        lo = MathF.Min(lo, actors[0].X - actors[0].W / 2f);
        hi = MathF.Max(hi, actors[^1].X + actors[^1].W / 2f);

        // Everything moved right so the leftmost of it is at the margin.
        const float Margin = 2f;
        float dx = Margin - lo;
        for (int i = 0; i < scene.Pieces.Count; i++) scene.Pieces[i] = scene.Pieces[i] with { X = MathF.Round(scene.Pieces[i].X + dx), Y = scene.Pieces[i].Y + Margin };
        for (int i = 0; i < scene.Texts.Count; i++) scene.Texts[i] = scene.Texts[i] with { X = MathF.Round(scene.Texts[i].X + dx), Y = scene.Texts[i].Y + Margin };
        foreach (var w in scene.Wires)
            for (int i = 0; i < w.Points.Length; i++) w.Points[i] += new Vector2(MathF.Round(dx), Margin);
        scene.W = MathF.Ceiling(hi - lo + 2f * Margin);
        scene.H = bottom + boxH + 2f * Margin;
        return scene;
    }

    // ── Drawing ──────────────────────────────────────────────────────────────

    private static void Build(BibuiRuntime rt, Scene scene, float width)
    {
        var row = rt.BeginParent(rt.Row().Sized(Size.Fill));
        rt.Gap(MathF.Max(0f, MathF.Floor((width - scene.W) / 2f)));
        var canvas = rt.BeginParent(rt.Box().Sized(Size.Px(scene.W), Size.Px(scene.H)));
        rt.Box().Sized(Size.Px(scene.W), Size.Px(scene.H)).At(0f, 0f).Draw = (_, ctx) => Wires(ctx, scene);
        float stroke = scene.Font >= 13f ? 1.25f : 1f;
        foreach (var p in scene.Pieces)
        {
            Box box = rt.Box().Sized(Size.Px(p.W), Size.Px(p.H)).At(p.X, p.Y);
            var s = box.Style;
            switch (p.Shape)
            {
                case NodeShape.Start or NodeShape.Bar:
                    s.BackgroundColor = UiColor.TextSecondary;
                    s.CornerRadius = p.Shape == NodeShape.Start ? p.H / 2f : 2f;
                    break;
                case NodeShape.Diamond or NodeShape.Hexagon or NodeShape.Cylinder or NodeShape.Subroutine or NodeShape.End:
                    var shape = p.Shape;
                    box.Draw = (b, ctx) => Outline(ctx, shape, b.Extent.X, b.Extent.Y, stroke);
                    break;
                case NodeShape.Label:
                    s.BackgroundColor = UiColor.SurfaceRaised;
                    s.CornerRadius = 4f;
                    break;
                case NodeShape.Note:
                    s.BackgroundColor = UiColor.Accent.WithAlpha(0.12f);
                    s.BorderColor = UiColor.Accent.WithAlpha(0.4f);
                    s.BorderWidth = stroke;
                    s.CornerRadius = 3f;
                    break;
                default:
                    s.BackgroundColor = UiColor.SurfaceRaised;
                    s.BorderColor = UiColor.Accent.WithAlpha(0.55f);
                    s.BorderWidth = stroke;
                    s.CornerRadius = p.Shape switch
                    {
                        NodeShape.Round => MathF.Min(p.H / 2f, scene.Font * 0.7f),
                        NodeShape.Stadium or NodeShape.Circle => p.H / 2f,
                        _ => 4f,
                    };
                    break;
            }
        }
        foreach (var t in scene.Texts)
            rt.Text(t.Text, FontSize.Px(t.Size), t.Muted ? UiColor.TextSecondary : UiColor.TextPrimary).At(t.X, t.Y);
        rt.EndParent(canvas);
        rt.EndParent(row);
    }

    // The shapes a box's border cannot draw: a rhombus, a hexagon, a cylinder, a subroutine's inner bars.
    private static void Outline(DrawContext ctx, NodeShape shape, float w, float h, float stroke)
    {
        float s = ctx.Scale, i = stroke * s / 2f;
        w *= s;
        h *= s;
        var paint = new Paint
        {
            Fill = ctx.Resolve(UiColor.SurfaceRaised),
            Stroke = ctx.Resolve(UiColor.Accent.WithAlpha(0.55f)),
            StrokeWidth = stroke * s,
            Join = StrokeJoin.Round,
        };
        var path = ctx.ScratchPath();
        switch (shape)
        {
            case NodeShape.Diamond:
                path.MoveTo(w / 2f, i);
                path.LineTo(w - i, h / 2f);
                path.LineTo(w / 2f, h - i);
                path.LineTo(i, h / 2f);
                path.Close();
                ctx.DrawPath(path, paint);
                break;
            case NodeShape.Hexagon:
            {
                float d = h / 2f;
                path.MoveTo(d, i);
                path.LineTo(w - d, i);
                path.LineTo(w - i, h / 2f);
                path.LineTo(w - d, h - i);
                path.LineTo(d, h - i);
                path.LineTo(i, h / 2f);
                path.Close();
                ctx.DrawPath(path, paint);
                break;
            }
            case NodeShape.Cylinder:
            {
                float ry = MathF.Min(h * 0.12f, 8f * s);
                path.MoveTo(i, ry);
                path.ArcTo(w / 2f - i, ry - i, 0f, false, true, w - i, ry);
                path.LineTo(w - i, h - ry);
                path.ArcTo(w / 2f - i, ry - i, 0f, false, true, i, h - ry);
                path.Close();
                ctx.DrawPath(path, paint);
                var rim = ctx.ScratchPath();
                rim.MoveTo(i, ry);
                rim.ArcTo(w / 2f - i, ry - i, 0f, false, false, w - i, ry);
                ctx.DrawPath(rim, paint with { Fill = null });
                break;
            }
            case NodeShape.End:
            {
                var ring = paint with { Fill = null, Stroke = ctx.Resolve(UiColor.TextSecondary) };
                float r = MathF.Min(w, h) / 2f;
                path.AddArc(new RectF(i, i, w - i, h - i), 0f, 360f);
                ctx.DrawPath(path, ring);
                var dot = ctx.ScratchPath();
                float k = r * 0.55f;
                dot.AddArc(new RectF(w / 2f - k, h / 2f - k, w / 2f + k, h / 2f + k), 0f, 360f);
                ctx.DrawPath(dot, new Paint { Fill = ctx.Resolve(UiColor.TextSecondary) });
                break;
            }
            case NodeShape.Subroutine:
            {
                ctx.DrawRect(new RectF(i, i, w - i, h - i), 4f * s, paint);
                float bar = 8f * s;
                ctx.DrawLine(bar, i, bar, h - i, paint);
                ctx.DrawLine(w - bar, i, w - bar, h - i, paint);
                break;
            }
        }
    }

    private static void Wires(DrawContext ctx, Scene scene)
    {
        float s = ctx.Scale;
        Rgba ink = ctx.Resolve(UiColor.TextSecondary), faint = ctx.Resolve(UiColor.BorderHover);
        float head = MathF.Max(6f, scene.Font * 0.6f) * s;
        Span<Vector2> pts = stackalloc Vector2[64];
        foreach (var w in scene.Wires)
        {
            if (w.Line == EdgeLine.Invisible || w.Points.Length < 2 || w.Points.Length > 64) continue;
            var color = w.Muted ? faint : ink;
            int n = w.Points.Length;
            for (int k = 0; k < n; k++) pts[k] = w.Points[k] * s;
            Vector2 tipEnd = pts[n - 1], tipStart = pts[0];
            Vector2 dirEnd = Direction(pts[n - 2], pts[n - 1], w), dirStart = Direction(pts[1], pts[0], w);
            pts[n - 1] -= dirEnd * Inset(w.End, head);
            pts[0] -= dirStart * Inset(w.Start, head);

            var path = ctx.ScratchPath();
            path.MoveTo(pts[0].X, pts[0].Y);
            for (int k = 1; k < n; k++)
            {
                Vector2 a = pts[k - 1], b = pts[k];
                if (!w.Curved) path.LineTo(b.X, b.Y);
                else if (w.Vertical)
                {
                    float m = (a.Y + b.Y) / 2f;
                    path.CubicTo(a.X, m, b.X, m, b.X, b.Y);
                }
                else
                {
                    float m = (a.X + b.X) / 2f;
                    path.CubicTo(m, a.Y, m, b.Y, b.X, b.Y);
                }
            }
            float width = (w.Line == EdgeLine.Thick ? 2.6f : w.Muted ? 1f : 1.3f) * s;
            ctx.DrawPath(path, new Paint
            {
                Stroke = color,
                StrokeWidth = width,
                Cap = StrokeCap.Butt,
                Join = StrokeJoin.Round,
                Dash = w.Line == EdgeLine.Dotted ? new DashPattern(3f * s, 3f * s) : null,
            });
            Tip(ctx, w.End, tipEnd, dirEnd, head, color, width);
            Tip(ctx, w.Start, tipStart, dirStart, head, color, width);
        }
    }

    // Which way a wire runs into its last point: along the main axis for a curve, which leaves and enters square.
    private static Vector2 Direction(Vector2 from, Vector2 to, Wire w)
    {
        Vector2 d = to - from;
        if (w.Curved) d = w.Vertical ? new(0f, MathF.Sign(d.Y)) : new(MathF.Sign(d.X), 0f);
        return d.LengthSquared() > 0f ? Vector2.Normalize(d) : new(0f, 1f);
    }

    private static float Inset(EdgeHead head, float size) => head switch
    {
        EdgeHead.Arrow => size * 0.9f,
        EdgeHead.Circle => size * 0.8f,
        _ => 0f,
    };

    private static void Tip(DrawContext ctx, EdgeHead head, Vector2 tip, Vector2 dir, float size, Rgba color, float width)
    {
        if (head == EdgeHead.None) return;
        Vector2 side = new(-dir.Y, dir.X);
        switch (head)
        {
            case EdgeHead.Arrow:
            {
                Vector2 b = tip - dir * size, l = b + side * size * 0.45f, r = b - side * size * 0.45f;
                var path = ctx.ScratchPath();
                path.MoveTo(tip.X, tip.Y);
                path.LineTo(l.X, l.Y);
                path.LineTo(r.X, r.Y);
                path.Close();
                ctx.DrawPath(path, new Paint { Fill = color, Stroke = color, StrokeWidth = width * 0.5f, Join = StrokeJoin.Round });
                break;
            }
            case EdgeHead.Open:
            {
                Vector2 b = tip - dir * size, l = b + side * size * 0.45f;
                var path = ctx.ScratchPath();
                path.MoveTo(l.X, l.Y);
                path.LineTo(tip.X, tip.Y);
                ctx.DrawPath(path, new Paint { Stroke = color, StrokeWidth = width, Cap = StrokeCap.Round });
                break;
            }
            case EdgeHead.Circle:
            {
                Vector2 c = tip - dir * size * 0.4f;
                float r = size * 0.4f;
                var path = ctx.ScratchPath();
                path.AddArc(new RectF(c.X - r, c.Y - r, c.X + r, c.Y + r), 0f, 360f);
                ctx.DrawPath(path, new Paint { Fill = ctx.Resolve(UiColor.SurfaceRaised), Stroke = color, StrokeWidth = width });
                break;
            }
            case EdgeHead.Cross:
            {
                Vector2 c = tip - dir * size * 0.5f;
                float r = size * 0.4f;
                Vector2 u = (dir + side) * r * 0.7071f, v = (dir - side) * r * 0.7071f;
                var paint = new Paint { Stroke = color, StrokeWidth = width * 1.2f, Cap = StrokeCap.Round };
                ctx.DrawLine(c.X - u.X, c.Y - u.Y, c.X + u.X, c.Y + u.Y, paint);
                ctx.DrawLine(c.X - v.X, c.Y - v.Y, c.X + v.X, c.Y + v.Y, paint);
                break;
            }
        }
    }
}

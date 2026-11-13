using BibDoc;

namespace BibMd;

/// <summary>
/// <c>md FILE --check</c>: runs a document's bib blocks headless, as the window would, and
/// says what is wrong where, in the file's own line numbers. Then clicks each button once,
/// from a fresh session, and says what that click would do. For an agent writing a
/// document: write, check, read the errors, fix. Exit 1 when anything fails.
/// </summary>
public static class Check
{
    public static int Run(string path)
    {
        if (!File.Exists(path)) { Console.Error.WriteLine($"{path}: no such file"); return 2; }
        var doc = new Document(path);
        string name = Path.GetFileName(path);
        var sources = new List<string>();
        var starts = new List<int>();   // the file line of each bib block's opening fence
        var dataBlocks = new List<string>();
        string? front = null;
        foreach (var b in doc.Blocks)
        {
            string src = doc.Source(b);
            if (b.Kind == BlockKind.FrontMatter) front = src;
            else if (b.Kind == BlockKind.Code && Bib.IsData(src)) dataBlocks.Add(Bib.Body(src));
            else if (b.Kind == BlockKind.Code && Bib.IsBib(src))
            {
                sources.Add(Bib.Body(src));
                starts.Add(doc.Text.AsSpan(0, b.Start).Count('\n') + 1);
            }
        }
        if (sources.Count == 0) { Console.WriteLine($"{name}: no bib blocks"); return 0; }

        int errors = 0, warnings = 0;
        void Error(string message) { errors++; Console.WriteLine($"{name}: error: {message}"); }
        void Warn(string message) { warnings++; Console.WriteLine($"{name}: warning: {message}"); }
        // "… at line 3:5" in a block is "… at line 14:5" in the file, with the block named.
        string At(int k, string message) =>
            System.Text.RegularExpressions.Regex.Replace(message, @" at line (\d+):(\d+)",
                m => $" at line {starts[k] + int.Parse(m.Groups[1].Value)}:{m.Groups[2].Value}")
            + $" (block {k + 1}, from line {starts[k]})";

        var data = DocSession.DataFrom(front, dataBlocks, out var dataError);
        if (dataError is not null) Error("data: " + dataError);

        var session = new DocSession(sources, data);
        var frame = session.Frame();
        for (int k = 0; k < sources.Count; k++)
            if (frame.Errors[k] is { } e) Error(At(k, e.Message));
        foreach (var w in frame.Warnings.Distinct()) Warn(w);

        var nodes = frame.Blocks.SelectMany((ns, k) => All(ns).Select(n => (n, k))).ToList();
        // `dir: row` names the row widget, not the word: a function where a value goes draws as nothing.
        foreach (var (n, k, prop) in nodes.SelectMany(x => x.n.Props.Where(p => p.Value is Callable).Select(p => (x.n, x.k, p.Key))).Distinct())
            Warn($"{n.Type}: {prop} is a function, not a value; a name that is also a widget (row, column, text, …) needs quotes: {prop}: \"…\" (block {k + 1}, from line {starts[k]})");
        foreach (var (n, k) in nodes.Where(x => x.n.Type == "draw").DistinctBy(x => x.k))
            Warn($"draw() is not drawn by bibmd; draw in a box's layer {{ … }} instead (block {k + 1}, from line {starts[k]})");

        var allowed = Allowed(front);
        foreach (var (n, k) in nodes.Where(x => x.n.Type == "button" && x.n.Key is not null))
        {
            string label = n.Text("label") ?? "";
            if (Values.Truthy(n.Props.GetValueOrDefault("disabled")))
            {
                Console.WriteLine($"{name}: button “{label}”: disabled at first, not clicked");
                continue;
            }
            var click = new DocSession(sources, data).Click(n.Key!);
            bool failed = false;
            for (int j = 0; j < sources.Count; j++)
                if (click.Errors[j] is { } e && frame.Errors[j] is null) { Error($"clicking “{label}”: " + At(j, e.Message)); failed = true; }
            if (failed) continue;
            var did = click.Effects.Select(x => x.Describe())
                .Concat(click.Sets.Select(s => $"set {s.Key}")).ToList();
            Console.WriteLine($"{name}: button “{label}”: " + (did.Count == 0 ? "does nothing" : string.Join("; then ", did)));
            foreach (var x in click.Effects.Where(x => !allowed.Contains(x.Kind) && !(x.Tool is { } t && allowed.Contains(t))))
                Warn($"button “{label}” would {x.Kind}{(x.Tool is null ? "" : " " + x.Tool)}, which the front matter's effects: does not list, so a host refuses it");
        }

        Console.WriteLine(errors == 0
            ? $"{name}: ok, {sources.Count} bib block(s){(warnings > 0 ? $", {warnings} warning(s)" : "")}"
            : $"{name}: {errors} error(s), {warnings} warning(s)");
        return errors == 0 ? 0 : 1;
    }

    private static IEnumerable<FrameNode> All(IEnumerable<FrameNode> nodes) =>
        nodes.SelectMany(n => All(n.Children).Prepend(n));

    // The effects the front matter lists: kinds (respond, send, call) and tool names (effects: [call: [orders.receive]]).
    private static HashSet<string> Allowed(string? front)
    {
        var allowed = new HashSet<string>();
        if (string.IsNullOrWhiteSpace(front) || Yaml.Parse(front) is not Dictionary<string, object?> meta) return allowed;
        void Add(object? v)
        {
            switch (v)
            {
                case string s: allowed.Add(s); break;
                case List<object?> list: foreach (var x in list) Add(x); break;
                case Dictionary<string, object?> d: foreach (var (key, x) in d) { allowed.Add(key); Add(x); } break;
            }
        }
        Add(meta.GetValueOrDefault("effects"));
        return allowed;
    }
}

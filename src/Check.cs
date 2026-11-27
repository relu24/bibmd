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
        var (lines, errors) = Report(File.ReadAllText(path), Path.GetFileName(path));
        foreach (var line in lines) Console.WriteLine(line);
        return errors == 0 ? 0 : 1;
    }

    /// <summary>The check of <paramref name="text"/>, a document named <paramref name="name"/>: its lines, and how many are errors.</summary>
    public static (List<string> Lines, int Errors) Report(string text, string name)
    {
        text = text.Replace("\r\n", "\n");
        var output = new List<string>();
        var sources = new List<string>();
        var starts = new List<int>();   // the file line of each bib block's opening fence
        var dataBlocks = new List<string>();
        var pending = new List<string>();   // parts still to be written
        string? front = null;
        foreach (var b in Blocks.Split(text))
        {
            string src = text[b.Start..b.End];
            if (b.Kind == BlockKind.FrontMatter) front = src;
            else if (b.Kind == BlockKind.Code && Bib.IsData(src)) dataBlocks.Add(Bib.Body(src));
            else if (b.Kind == BlockKind.Code && Bib.Pending(src) is { } p)
                pending.Add($"still to be written at line {text.AsSpan(0, b.Start).Count('\n') + 1}: pending{(p.Name.Length > 0 ? " " + p.Name : "")}{(p.Intent.Length > 0 ? ", " + p.Intent.ReplaceLineEndings(" ") : "")}");
            else if (b.Kind == BlockKind.Code && Bib.IsBib(src))
            {
                sources.Add(Bib.Body(src));
                starts.Add(text.AsSpan(0, b.Start).Count('\n') + 1);
            }
        }
        if (sources.Count == 0)
        {
            output.AddRange(pending.Select(p => $"{name}: warning: {p}"));
            output.Add($"{name}: no bib blocks");
            return (output, 0);
        }

        int errors = 0, warnings = 0;
        void Error(string message) { errors++; output.Add($"{name}: error: {message}"); }
        void Warn(string message) { warnings++; output.Add($"{name}: warning: {message}"); }
        // "… at line 3:5" in a block is "… at line 14:5" in the file, with the block named.
        string At(int k, string message) =>
            System.Text.RegularExpressions.Regex.Replace(message, @" at line (\d+):(\d+)",
                m => $" at line {starts[k] + int.Parse(m.Groups[1].Value)}:{m.Groups[2].Value}")
            + $" (block {k + 1}, from line {starts[k]})";

        foreach (var p in pending) Warn(p);

        var data = DocSession.DataFrom(front, dataBlocks, out var dataError);
        if (dataError is not null) Error("data: " + dataError);

        var session = new DocSession(sources, data);
        var frame = session.Frame();
        for (int k = 0; k < sources.Count; k++)
            if (frame.Errors[k] is { } e) Error(At(k, e.Message));
        foreach (var w in frame.Warnings.Distinct())
        {
            // "block 2, line 3:5: …" from the interpreter is "… at line 14:5 (block 2, from line 12)", as errors are.
            var m = System.Text.RegularExpressions.Regex.Match(w, @"^block (\d+), line (\d+):(\d+): (.*)$", System.Text.RegularExpressions.RegexOptions.Singleline);
            int k = m.Success ? int.Parse(m.Groups[1].Value) - 1 : -1;
            if (k >= 0 && k < sources.Count) Warn(At(k, $"{m.Groups[4].Value} at line {m.Groups[2].Value}:{m.Groups[3].Value}"));
            else Warn(w);
        }
        // Inputs moved can cost more than their defaults: a run close to the budget is a warning.
        if (frame.Steps > Budget.Limit * 0.8)
            Warn($"the first run takes {Count(frame.Steps)} of {Count(Budget.Limit)} steps; moving an input may run out (precompute in data:, or sample fewer points)");
        if (frame.Size > Budget.SizeLimit * 0.8)
            Warn($"the first run builds and goes through {Count(frame.Size)} of {Count(Budget.SizeLimit)} values; moving an input may run out (build lists once, not again in a loop)");

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
                output.Add($"{name}: button “{label}”: disabled at first, not clicked");
                continue;
            }
            var click = new DocSession(sources, data).Click(n.Key!);
            bool failed = false;
            for (int j = 0; j < sources.Count; j++)
                if (click.Errors[j] is { } e && frame.Errors[j] is null) { Error($"clicking “{label}”: " + At(j, e.Message)); failed = true; }
            if (failed) continue;
            var did = click.Effects.Select(x => x.Describe())
                .Concat(click.Sets.Select(s => $"set {s.Key}")).ToList();
            output.Add($"{name}: button “{label}”: " + (did.Count == 0 ? "does nothing" : string.Join("; then ", did)));
            foreach (var x in click.Effects.Where(x => x.Kind is not ("copy" or "download") && !allowed.Contains(x.Kind) && !(x.Tool is { } t && allowed.Contains(t))))
                Warn($"button “{label}” would {x.Kind}{(x.Tool is null ? "" : " " + x.Tool)}, which the front matter's effects: does not list, so a host refuses it");
        }

        // Each tab that does not show at first, shown: only the tab on show runs, so its errors would wait for a reader.
        foreach (var (tabs, k) in nodes.Where(x => x.n.Type == "tabs" && x.n.Key is not null))
            foreach (var tab in tabs.Children.Where(c => c.Type == "tab" && c.Props.GetValueOrDefault("active") is false))
            {
                string title = tab.Text("title") ?? "";
                var shown = new DocSession(sources, data);
                shown.Set(tabs.Key!, title);
                var f = shown.Frame();
                for (int j = 0; j < sources.Count; j++)
                    if (f.Errors[j] is { } e && frame.Errors[j] is null) Error($"in the tab “{title}”: " + At(j, e.Message));
                if (f.Steps > Budget.Limit * 0.8) Warn($"in the tab “{title}”, a run takes {Count(f.Steps)} of {Count(Budget.Limit)} steps");
            }

        // The other events: each key it asks for (those of no button), a click, a right click and a press on each
        // box with an id (300 at most), each alarm, and a change of each input when it reads changed(). Each runs
        // from the first frame; a failure is an error, and what each kind sets is said once.
        var buttonKeys = nodes.Where(x => x.n.Type == "button").Select(x => x.n.Text("key")).OfType<string>().ToHashSet();
        var events = frame.Keys.Where(key => !buttonKeys.Contains(key)).Select(key => (Say: $"key “{key}”", Ev: new DocEvent("key", key)))
            .Concat(nodes.Where(x => x.n.Type == "box" && x.n.Key is not null).Take(300)
                .SelectMany(x => new[] { "click", "rclick", "press" }.Select(kind => (Say: $"{kind} on a box", Ev: new DocEvent(kind, x.n.Key!, 1, 1)))))
            .Concat(frame.Alarms.Distinct().Select(at => (Say: "an alarm", Ev: new DocEvent("alarm", DocEvent.AlarmName(at)))))
            .Concat(sources.Any(src => src.Contains("changed(")) ? nodes.Where(x => Interpreter.Inputs.Contains(x.n.Type) && x.n.Key is not null)
                .Select(x => (Say: $"a change of “{x.n.Text("label")}”", Ev: new DocEvent("change", x.n.Key!))) : []);
        var said = new Dictionary<string, HashSet<string>>();
        foreach (var (say, ev) in events)
        {
            var run = new DocSession(sources, data).Fire(ev);
            bool failed = false;
            for (int j = 0; j < sources.Count; j++)
                if (run.Errors[j] is { } e && frame.Errors[j] is null) { Error($"{say}: " + At(j, e.Message)); failed = true; }
            if (failed) break;   // one is enough: the others likely fail alike
            string kind = ev.Kind == "change" ? "a change" : say;   // a key is said by its name; a click on any box as one
            var did = said.TryGetValue(kind, out var d) ? d : said[kind] = [];
            foreach (var x in run.Effects) did.Add(x.Describe());
            foreach (var key in run.Sets.Keys) did.Add($"set {key}");
        }
        foreach (var (kind, did) in said.Where(x => x.Value.Count > 0))
            output.Add($"{name}: {kind}: " + string.Join("; ", did.Take(8)) + (did.Count > 8 ? $"; and {did.Count - 8} more" : ""));

        output.Add(errors == 0
            ? $"{name}: ok, {sources.Count} bib block(s), {Count(frame.Steps)} steps, {Count(frame.Size)} values{(warnings > 0 ? $", {warnings} warning(s)" : "")}"
            : $"{name}: {errors} error(s), {warnings} warning(s)");
        return (output, errors);
    }

    private static string Count(long n) => n.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

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

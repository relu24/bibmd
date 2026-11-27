// How bib blocks are laid out at a width: a document built a few frames headless, as the window
// builds it, and the boxes it laid out measured. Each check names what it guards.
using Bibui.Core;
using Bibui.Core.Runtime;
using Bibui.Core.Runtime.Platform;
using BibMd;
using Text = Bibui.Core.Text;

int failed = 0, passed = 0;
string? filter = args.FirstOrDefault();
void Run(string name, Action test)
{
    if (filter is not null && !name.Contains(filter, StringComparison.OrdinalIgnoreCase)) return;
    try { test(); passed++; Console.WriteLine($"  [PASS] {name}"); }
    catch (Exception e) { failed++; Console.WriteLine($"  [FAIL] {name}\n         {e.Message}"); }
}
void IsTrue(bool ok, string why) { if (!ok) throw new Exception(why); }

App.RegisterFonts();
const string Fence = "```";

// The document of these bib blocks, laid out in a window `width` px wide: its runtime after the frames have settled.
BibuiRuntime Laid(int width, string bib)
{
    string path = Path.Combine(Path.GetTempPath(), $"bibmd-layout-{Environment.ProcessId}.bib.md");
    File.WriteAllText(path, $"---\nbib: 1\n---\n{Fence}bib\n{bib}\n{Fence}\n");
    var runtime = BibuiRuntime.Create();
    runtime.SetTheme(App.ThemeFor(dark: true));
    var app = new App(runtime, new Document(path), new Images((_, _, _) => default, () => { }));
    var surface = new WindowFrame(width, 1400, 1f, 60f);
    for (int i = 0; i < 8; i++)
    {
        runtime.BeginBuild(surface, []);
        app.Build();
        runtime.EndBuild();
    }
    File.Delete(path);
    return runtime;
}
// A document kept across frames, for checks that click or change the window's width between them:
// frame(width, events) builds a frame (and a few more to settle) and gives its runtime.
Func<int, WindowEvent[], BibuiRuntime> Live(string bib, string front = "")
{
    string path = Path.Combine(Path.GetTempPath(), $"bibmd-layout-{Environment.ProcessId}-{Guid.NewGuid():N}.bib.md");
    File.WriteAllText(path, $"---\nbib: 1\n{front}---\n{Fence}bib\n{bib}\n{Fence}\n");
    var runtime = BibuiRuntime.Create();
    runtime.SetTheme(App.ThemeFor(dark: true));
    var app = new App(runtime, new Document(path), new Images((_, _, _) => default, () => { }));
    File.Delete(path);
    return (width, events) =>
    {
        var surface = new WindowFrame(width, 1400, 1f, 60f);
        runtime.BeginBuild(surface, [.. events]);
        app.Build();
        runtime.EndBuild();
        for (int i = 0; i < 6; i++)
        {
            runtime.BeginBuild(surface, []);
            app.Build();
            runtime.EndBuild();
        }
        return runtime;
    };
}
// A press and release at the middle of a node, as the window gives them.
WindowEvent[] ClickOn(UiNode n)
{
    float x = n.Rect.X + n.Extent.X / 2f, y = n.Rect.Y + n.Extent.Y / 2f;
    return [WindowEvent.Mouse(WindowEventKind.MouseMove, x, y, MouseButton.Left), WindowEvent.Mouse(WindowEventKind.MouseDown, x, y, MouseButton.Left),
        WindowEvent.Mouse(WindowEventKind.MouseUp, x, y, MouseButton.Left)];
}
// A sample of bibmd's, laid out in a window `width` px wide.
string Sample(string name)
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "samples", "tools", name))) return Path.Combine(dir.FullName, "samples", "tools", name);
    throw new Exception($"no sample {name}");
}
BibuiRuntime LaidSample(int width, string name, params WindowEvent[] first)
{
    var runtime = BibuiRuntime.Create();
    runtime.SetTheme(App.ThemeFor(dark: true));
    var app = new App(runtime, new Document(Sample(name)), new Images((_, _, _) => default, () => { }));
    var surface = new WindowFrame(width, 1400, 1f, 60f);
    for (int i = 0; i < 8; i++)
    {
        runtime.BeginBuild(surface, i == 0 ? [.. first] : []);
        app.Build();
        runtime.EndBuild();
    }
    return runtime;
}
bool IsUnder(UiNode n, UiNode above) { for (var p = n.Parent; p is not null; p = p.Parent) if (p == above) return true; return false; }
UiNode Node(BibuiRuntime rt, string id) => rt.FindById(id) ?? throw new Exception($"no node {id}");
IEnumerable<UiNode> Under(UiNode n)
{
    for (var c = n.FirstChild; c is not null; c = c.Next)
    {
        yield return c;
        foreach (var d in Under(c)) yield return d;
    }
}
IEnumerable<Text> Texts(UiNode n) => Under(n).OfType<Text>();
float Right(UiNode n) => n.Rect.X + n.Extent.X;
// The width a bib block is drawn at, read from the document itself.
float PageWidth(BibuiRuntime rt) => Node(rt, "bibbox-page").Extent.X;
// page.width as the block lays it out: its width less its padding.
float PageWidthOf(BibuiRuntime rt) => Node(rt, "block-1").Extent.X - 28f;

Run("page.width is the width the block is drawn at, and a box of that width fills it", () =>
{
    foreach (int w in new[] { 460, 640, 1100 })
    {
        var rt = Laid(w, "box(id: \"page\", width: page.width, height: 10) { }\ntext(\"{page.width}\")");
        var block = Node(rt, "block-1");
        float inner = block.Extent.X - 28f;   // the block's padding, 14 a side
        IsTrue(MathF.Abs(PageWidth(rt) - inner) < 1f, $"at {w}: page.width {PageWidth(rt)}, the block draws at {inner}");
        IsTrue(Texts(block).Any(t => t.Content.ToString() == ((int)PageWidth(rt)).ToString()), $"at {w}: the text says page.width");
    }
});

Run("row: a child with a px width gets it, and a fill beside it shares what is left", () =>
{
    var rt = Laid(640, "box(id: \"page\", width: page.width, height: 1) { }\nrow {\n  box(id: \"a\", width: 300, height: 40, background: accent)\n  box(id: \"b\", width: fill, height: 40)\n}");
    var a = Node(rt, "bibbox-a");
    var b = Node(rt, "bibbox-b");
    IsTrue(MathF.Abs(a.Extent.X - 300f) < 0.5f, $"the 300 px box is {a.Extent.X} wide");
    IsTrue(MathF.Abs(a.Rect.Y - b.Rect.Y) < 0.5f, "the fill is on the same line");
    IsTrue(Right(b) <= Node(rt, "bibbox-page").Rect.X + PageWidth(rt) + 0.5f, "the fill ends inside the column");

    var narrow = Laid(460, "row {\n  box(id: \"a\", width: 300, height: 40)\n  box(id: \"b\", width: fill, height: 40)\n}");
    IsTrue(MathF.Abs(Node(narrow, "bibbox-a").Extent.X - 300f) < 0.5f, "at 460 too, 300 px");
    IsTrue(Node(narrow, "bibbox-b").Rect.Y > Node(narrow, "bibbox-a").Rect.Y + 39f, "at 460 the fill goes under it");
});

Run("text in a box that centres wraps at the box's width, and each line is centred", () =>
{
    const string Long = "A sentence long enough that it has to wrap onto a second line in a narrow column, and then some.";
    foreach (var bib in new[]
    {
        $"box(id: \"c\", width: fill, padding: 16, border: 1, align: center) {{ text(\"{Long}\") }}",
        $"box(id: \"c\", width: fill) {{ text(\"{Long}\", align: center) }}",
    })
    {
        var rt = Laid(460, bib);
        var box = Node(rt, "bibbox-c");
        var lines = Texts(box).ToList();
        IsTrue(lines.Count >= 2, $"{bib[..30]}…: {lines.Count} line(s)");
        float mid = box.Rect.X + box.Extent.X / 2f;
        foreach (var l in lines)
        {
            IsTrue(l.Rect.X >= box.Rect.X && Right(l) <= Right(box) + 0.5f, $"a line inside the box: {l.Rect.X}..{Right(l)} in {box.Rect.X}..{Right(box)}");
            IsTrue(MathF.Abs(l.Rect.X + l.Extent.X / 2f - mid) < 1.5f, $"“{l.Content}” centred: its middle {l.Rect.X + l.Extent.X / 2f}, the box's {mid}");
        }
    }
});

Run("row(fit: true): a segmented choice keeps its caption above it, and the next one wraps rather than run off", () =>
{
    var rt = Laid(460, "row(fit: true) {\n  choice(\"Schedule of\", [\"Scenario A\", \"Scenario B\", \"Scenario C\"], style: segmented)\n  choice(\"By\", [\"Monthly\", \"Yearly\"], style: segmented)\n}");
    var block = Node(rt, "block-1");
    // A segmented choice's options are boxes bib-<key>-<option>, side by side.
    var options = Under(block).Where(n => n.Id?.StartsWith("bib-") == true).ToList();
    var groups = options.GroupBy(n => n.Id![..n.Id!.LastIndexOf('-')])
        .Select(g => (Key: g.Key, X: g.Min(n => n.Rect.X), Y: g.Min(n => n.Rect.Y), Right: g.Max(Right))).ToList();
    IsTrue(groups.Count == 2, $"two choices, found {groups.Count}");
    foreach (var g in groups) IsTrue(g.Right <= Right(block) + 0.5f, $"{g.Key} ends at {g.Right}, past the block ({Right(block)})");
    var captions = Texts(block).Where(t => t.Content.ToString() is "Schedule of" or "By").ToList();
    IsTrue(captions.Count == 2, "both captions drawn");
    foreach (var c in captions)
        IsTrue(groups.Any(g => MathF.Abs(g.X - c.Rect.X) < 2f && g.Y > c.Rect.Y), $"“{c.Content}” above its control, not beside it");
});

Run("table: a word longer than its column is broken, and no cell runs into the next", () =>
{
    var rt = Laid(460, "table([\"Name\", \"Note\"], [[\"Supercalifragilisticexpialidociously long\", \"short\"], [\"a\", \"b\"]])");
    var block = Node(rt, "block-1");
    var texts = Texts(block).ToList();
    var note = texts.First(t => t.Content.ToString() == "Note");
    foreach (var t in texts.Where(t => t.Rect.X < note.Rect.X - 1f))
        IsTrue(Right(t) <= note.Rect.X, $"“{t.Content}” ends at {Right(t)}, past the next column at {note.Rect.X}");
});

Run("table: columns that would be narrower than their words scroll sideways in the table's box", () =>
{
    var rt = Laid(460, "table([\"Product\", \"Segment\", \"Region\", \"Price\", \"Units\", \"Revenue\", \"Margin\"], [[\"Tandem\", \"Consumer\", \"Nordics\", 1299, 12, 15588, 0.25]])");
    var scroll = Under(Node(rt, "block-1")).FirstOrDefault(n => n.Id?.StartsWith("bib-tscroll-") == true) ?? throw new Exception("no box that scrolls");
    IsTrue(scroll.Extent.X <= Node(rt, "block-1").Extent.X, "the box is the block's width");
    IsTrue(scroll.ContentSize.X > scroll.Extent.X + 1f, $"the table inside it is wider: {scroll.ContentSize.X} in {scroll.Extent.X}");
});

Run("table: a sort clicked on a head stays on that column when the document shows other columns at another width", () =>
{
    const string Switching = "let wide = page.width >= 700\n"
        + "let rows = [[\"pear\", \"north\", 12], [\"Apple\", \"south\", 3], [\"fig\", \"east\", 30]].map(r => wide ? r : [r[0], r[2]])\n"
        + "table(wide ? [\"Item\", \"Region\", \"Units\"] : [\"Item\", \"Units\"], rows, id: \"orders\", sort: true)";
    var frame = Live(Switching);
    var wide = frame(1100, []);
    IsTrue(PageWidthOf(wide) >= 700, "the wide window gives page.width 700 or more");
    frame(1100, ClickOn(Node(wide, "bib-th-orders-2")));   // Units, the third column here
    var narrow = frame(640, []);
    string Head(BibuiRuntime rt, int i) => string.Concat(Texts(Node(rt, $"bib-th-orders-{i}")).Select(t => t.Content.ToString()));
    IsTrue(Head(narrow, 1) == "Units ▲", $"at 640 the sort is on Units, the second column: “{Head(narrow, 0)}” “{Head(narrow, 1)}”");
    var cells = Texts(Node(narrow, "block-1")).Select(t => t.Content.ToString()).Where(t => t is "pear" or "Apple" or "fig").ToList();
    IsTrue(string.Join(",", cells) == "Apple,pear,fig", $"and the rows by units: {string.Join(",", cells)}");
});

Run("table: one that scrolls sideways shows a fade where there is more and a bar under it, and a drag or Shift+wheel scrolls it", () =>
{
    // The palette's contrast matrix at 460: eight columns of text on colour.
    const string Matrix = "let names = [\"White\", \"Primary\", \"Secondary\", \"Accent\", \"Neutral\", \"Surface\", \"Ink\"]\n"
        + "table([\"Text \\\\ background\", ...names], names.map(a => [a, ...names.map(b => ({text: \"Aa  4.98  AA large\", background: \"#2f6fd0\", color: \"#ffffff\"}))]))";
    var frame = Live(Matrix);
    var rt = frame(460, []);
    UiNode View(BibuiRuntime r) => Under(Node(r, "block-1")).FirstOrDefault(n => n.Id?.StartsWith("bib-tscroll-") == true && !n.Id.Contains("-bar")) ?? throw new Exception("no box that scrolls");
    var view = View(rt);
    IsTrue(view.ContentSize.X > view.Extent.X + 1f, $"the matrix is wider than its box: {view.ContentSize.X} in {view.Extent.X}");
    // The fades are boxes beside the view in its frame, drawn over it, with a gradient.
    List<UiNode> Fades(BibuiRuntime r)
    {
        var v = View(r);
        return Under(v.Parent!).OfType<Box>().Where(b => b.Style.BackgroundGradient is not null && b.Parent == v.Parent).Cast<UiNode>().ToList();
    }
    float left = view.Rect.X, right = Right(view);
    IsTrue(Fades(rt).Count == 1 && MathF.Abs(Right(Fades(rt)[0]) - right) < 1f, $"at the start, one fade, at the right edge: {string.Join(", ", Fades(rt).Select(f => $"{f.Rect.X}–{Right(f)}"))}");
    var thumb = Node(rt, view.Id + "-bar-thumb");
    IsTrue(thumb.Rect.Y >= view.Rect.Y + view.Extent.Y - 1f && thumb.Extent.X < view.Extent.X, $"a thumb under the table, narrower than it: {thumb.Extent.X} of {view.Extent.X}");

    // A drag across a row moves the view with the pointer, and the left edge fades then.
    var cell = Texts(view).First(t => t.Content.ToString() == "Primary" && t.Rect.X < left + 80f);
    float x = cell.Rect.X + 4f, y = cell.Rect.Y + 4f;
    frame(460, [WindowEvent.Mouse(WindowEventKind.MouseMove, x, y, MouseButton.Left), WindowEvent.Mouse(WindowEventKind.MouseDown, x, y, MouseButton.Left)]);
    frame(460, [WindowEvent.Mouse(WindowEventKind.MouseMove, x - 60f, y, MouseButton.Left)]);
    rt = frame(460, [WindowEvent.Mouse(WindowEventKind.MouseMove, x - 100f, y, MouseButton.Left), WindowEvent.Mouse(WindowEventKind.MouseUp, x - 100f, y, MouseButton.Left)]);
    view = View(rt);
    IsTrue(MathF.Abs(view.ViewOffset.X - 100f) < 1f, $"a drag of 100 px scrolls it 100: {view.ViewOffset.X}");
    IsTrue(Fades(rt).Any(f => MathF.Abs(f.Rect.X - left) < 1f), "after the drag, the left edge fades");

    // Shift+wheel scrolls it on.
    rt = frame(460, [WindowEvent.Wheel(x, y, 0f, -2f, KeyModifiers.Shift)]);
    for (int i = 0; i < 30; i++) rt = frame(460, []);
    IsTrue(View(rt).ViewOffset.X > 101f, $"Shift+wheel scrolls it further: {View(rt).ViewOffset.X}");
});

Run("table: a click on a head sorts a table that scrolls sideways, and a drag across a head scrolls it without sorting", () =>
{
    var frame = Live("table([\"Product\", \"Segment\", \"Region\", \"Price\", \"Units\", \"Revenue\", \"Margin\", \"Channel\"], "
        + "[[\"Tandem\", \"Consumer\", \"Nordics\", 1299, 12, 15588, 0.25, \"Online\"]], id: \"wide\", sort: true)");
    var rt = frame(460, []);
    IsTrue(Node(rt, "bib-tscroll-wide").ContentSize.X > Node(rt, "bib-tscroll-wide").Extent.X + 1f, "the table scrolls sideways");
    rt = frame(460, ClickOn(Node(rt, "bib-th-wide-1")));
    string Head(BibuiRuntime r, int i) => string.Concat(Texts(Node(r, $"bib-th-wide-{i}")).Select(t => t.Content.ToString())).Replace(" ", "");
    IsTrue(Head(rt, 1) == "Segment▲", $"the click sorted by Segment: “{Head(rt, 1)}”");
    var head = Node(rt, "bib-th-wide-2");
    float x = head.Rect.X + 4f, y = head.Rect.Y + 4f;
    frame(460, [WindowEvent.Mouse(WindowEventKind.MouseMove, x, y, MouseButton.Left), WindowEvent.Mouse(WindowEventKind.MouseDown, x, y, MouseButton.Left)]);
    frame(460, [WindowEvent.Mouse(WindowEventKind.MouseMove, x - 30f, y, MouseButton.Left)]);
    rt = frame(460, [WindowEvent.Mouse(WindowEventKind.MouseUp, x - 30f, y, MouseButton.Left)]);
    IsTrue(Node(rt, "bib-tscroll-wide").ViewOffset.X > 20f, $"the drag on a head scrolled it: {Node(rt, "bib-tscroll-wide").ViewOffset.X}");
    IsTrue(Head(rt, 2) == "Region" && Head(rt, 1) == "Segment▲", $"and did not sort: “{Head(rt, 1)}” “{Head(rt, 2)}”");
});

Run("kv: a long label wraps in its column, clear of the value", () =>
{
    var rt = Laid(460, "card(\"K\") { kv([{label: \"Interest saved by the extra payments each month\", value: \"$151,925\"}]) }");
    var texts = Texts(Node(rt, "block-1")).ToList();
    var value = texts.First(t => t.Content.ToString() == "$151,925");
    var label = texts.Where(t => t.Content.ToString().Contains("Interest") || t.Rect.Y > value.Rect.Y && t.Rect.X < value.Rect.X).ToList();
    IsTrue(label.Count >= 2, $"the label takes {label.Count} line(s)");
    foreach (var l in label) IsTrue(Right(l) <= value.Rect.X, $"“{l.Content}” ends at {Right(l)}, over the value at {value.Rect.X}");
});

Run("bars: a long label wraps in its column, not cut", () =>
{
    var rt = Laid(460, "bars([{label: \"Tandem Bike Lightweight Carbon\", value: 40}, {label: \"Road\", value: 25}])");
    string all = string.Join(" ", Texts(Node(rt, "block-1")).Select(t => t.Content.ToString()));
    IsTrue(all.Contains("Tandem Bike") && all.Contains("Carbon"), $"every word of the label is drawn: {all}");
});

Run("box(wrap: true): a fill shares a line with fixed children when its minWidth fits", () =>
{
    var rt = Laid(640, "box(dir: \"row\", wrap: true, gap: 8) {\n  box(id: \"f\", width: 120, height: 30)\n  box(id: \"g\", width: fill, height: 30)\n}\nbox(dir: \"row\", wrap: true, gap: 8) {\n  box(id: \"h\", width: 120, height: 30)\n  box(id: \"i\", width: fill, minWidth: 500, height: 30)\n}");
    IsTrue(MathF.Abs(Node(rt, "bibbox-f").Rect.Y - Node(rt, "bibbox-g").Rect.Y) < 0.5f, "the fill sits beside the 120 px box");
    IsTrue(Node(rt, "bibbox-g").Extent.X > 300f, $"and takes the rest of the line: {Node(rt, "bibbox-g").Extent.X}");
    IsTrue(Node(rt, "bibbox-i").Rect.Y > Node(rt, "bibbox-h").Rect.Y + 29f, "a fill whose minWidth does not fit goes under");
});

Run("plot: the legend and the x axis' name do not share a line they do not fit on", () =>
{
    var rt = Laid(460, "plot(xLabel: \"Months since the first payment\") { series(\"Scenario A balance\", [0, 1], [1, 0]); series(\"Scenario B balance\", [0, 1], [2, 0]) }");
    var texts = Texts(Node(rt, "block-1")).ToList();
    var name = texts.First(t => t.Content.ToString() == "Months since the first payment");
    foreach (var t in texts.Where(t => t.Content.ToString().StartsWith("Scenario")))
        IsTrue(t.Rect.Y + t.Extent.Y <= name.Rect.Y || Right(t) <= name.Rect.X, $"“{t.Content}” overlaps the axis' name");
});

Run("toggle: a long label wraps within the column", () =>
{
    var rt = Laid(460, "toggle(\"Show the interest saved by every extra payment, month by month, as a table\")");
    var block = Node(rt, "block-1");
    var texts = Texts(block).ToList();
    IsTrue(texts.Count >= 2, $"the label takes {texts.Count} line(s)");
    foreach (var t in texts) IsTrue(Right(t) <= Right(block), $"“{t.Content}” ends at {Right(t)}, past the block's {Right(block)}");
});

Run("habits: a day's square follows page.width, so the months fill the card's width, at 460, 640 and 1100", () =>
{
    foreach (int w in new[] { 460, 640, 1100 })
    {
        var rt = LaidSample(w, "05-habits.bib.md");
        var days = new List<UiNode>();
        void Find(UiNode n) { if (n.Id?.StartsWith("bibbox-day-0-") == true) days.Add(n); for (var c = n.FirstChild; c is not null; c = c.Next) Find(c); }
        Find(Node(rt, "page"));
        IsTrue(days.Count > 300, $"at {w}: the first habit's days are drawn ({days.Count})");
        var block = days[0].Parent;
        while (block is not null && block.Id?.StartsWith("block-") != true) block = block.Parent;
        float left = block!.Rect.X + 14f, room = block.Extent.X - 28f;   // page.width
        float right = days.Max(Right);
        IsTrue(right <= left + room, $"at {w}: a square ends at {right}, past the column's {left + room}");
        IsTrue(right - left >= 0.9f * room, $"at {w}: the squares fill {right - left} of {room} px");
        IsTrue(days[0].Extent.X >= 9f, $"at {w}: a square is {days[0].Extent.X} px");
    }
    var (lines, errors) = Check.Report(File.ReadAllText(Sample("05-habits.bib.md")), "05-habits.bib.md");
    IsTrue(errors == 0 && !lines.Any(l => l.Contains("steps of")), "the habits are within the step budget: " + string.Join("; ", lines.Where(l => l.Contains("error") || l.Contains("steps"))));
});

Run("measureText() says how high and wide a text is drawn, and page.fontSize follows the reader's size (Ctrl+=)", () =>
{
    const string Doc = "let s = \"Information architecture, and choosing the CMS\"\n"
        + "let m = measureText(s, size: sm, weight: bold, width: 90)\nlet one = measureText(s, size: sm, weight: bold)\n"
        + "box(id: \"t\", width: 90) { text(s, size: sm, weight: bold) }\n"
        + "text(\"m {m.width} {m.height} {m.lines} {one.width} {page.fontSize}\")";
    var frame = Live(Doc);
    foreach (bool larger in new[] { false, true })
    {
        var rt = frame(700, larger ? [WindowEvent.KeyPress(Key.Equal, KeyModifiers.Ctrl)] : []);
        var said = Texts(Node(rt, "block-1")).First(t => t.Content.ToString().StartsWith("m ")).Content.ToString().Split(' ')[1..].Select(float.Parse).ToArray();
        var box = Node(rt, "bibbox-t");
        var lines = Texts(box).ToList();
        IsTrue(said[2] == lines.Count, $"{(larger ? "larger" : "default")}: {said[2]} lines said, {lines.Count} drawn");
        IsTrue(MathF.Abs(said[1] - box.Extent.Y) <= 1f, $"{(larger ? "larger" : "default")}: {said[1]} px high said, {box.Extent.Y} drawn");
        IsTrue(said[0] <= 90f && said[0] >= lines.Max(t => t.Extent.X) - 1f, $"{(larger ? "larger" : "default")}: {said[0]} wide said, widest line {lines.Max(t => t.Extent.X)}");
        IsTrue(said[3] > 90f, "on one line, wider than 90");
        if (larger) IsTrue(said[4] > 15f, $"page.fontSize grew with Ctrl+=: {said[4]}");
    }
});

Run("gantt: every task's name is drawn whole within its column at the reader's larger sizes, and its row holds it beside its bar", () =>
{
    string[] ids = ["kick", "res", "ia", "cms", "vis", "copy", "signoff", "build", "content", "qa", "train", "launch"];
    foreach (var (w, larger) in new[] { (700, 1), (640, 2), (1100, 2), (460, 1) })
    {
        var keys = Enumerable.Repeat(WindowEvent.KeyPress(Key.Equal, KeyModifiers.Ctrl), larger).ToArray();
        var rt = LaidSample(w, "09-gantt.bib.md", keys);
        float lastBottom = float.NaN;
        foreach (var id in ids)
        {
            var bar = Node(rt, "bibbox-bar-" + id);
            // The task's row: the box around its bar that also holds its name; the name's column, its child that does.
            var row = bar.Parent!;
            while (!Texts(row).Any(t => !IsUnder(t, bar))) row = row.Parent!;
            var names = Texts(row).First(t => !IsUnder(t, bar)) as UiNode;
            while (names.Parent != row) names = names.Parent!;
            var texts = Texts(names).ToList();
            foreach (var t in texts)
            {
                // As wide as its words in its font: a text cut at its box is laid out narrower than they are.
                float natural = BibMd.Text.Width(rt, t.Content.ToString(), rt.FontOf(t));
                IsTrue(t.Rect.X + natural <= Right(names) + 0.5f, $"at {w}, {larger} size(s) larger: “{t.Content}” of {id} ends at {t.Rect.X + natural}, past its column's {Right(names)}");
                IsTrue(t.Rect.Y + t.Extent.Y <= row.Rect.Y + row.Extent.Y + 0.5f, $"at {w}, {larger} larger: “{t.Content}” of {id} runs under its row");
            }
            IsTrue(texts.Count > 0 && string.Concat(texts.Select(t => t.Content.ToString())).Replace(" ", "").StartsWith(id switch { "ia" => "Information", "cms" => "Chooseand", _ => "" }), $"at {w}: {id}'s name is drawn");
            IsTrue(MathF.Abs(bar.Rect.Y - row.Rect.Y) < 0.5f && MathF.Abs(bar.Extent.Y - row.Extent.Y) < 0.5f, $"at {w}: {id}'s bar spans its row");
            if (!float.IsNaN(lastBottom)) IsTrue(MathF.Abs(row.Rect.Y - lastBottom) < 0.5f, $"at {w}: {id}'s row starts where the one before ends");
            lastBottom = row.Rect.Y + row.Extent.Y;
        }
    }
});

Console.WriteLine($"{passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;

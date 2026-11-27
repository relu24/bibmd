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

Console.WriteLine($"{passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;

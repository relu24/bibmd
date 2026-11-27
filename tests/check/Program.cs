// md --check on documents written here: what it says, and at which line of the file.
using BibMd;

int failed = 0, passed = 0;
void Expect(string name, bool ok, IEnumerable<string> lines)
{
    if (ok) { passed++; return; }
    failed++;
    Console.WriteLine($"FAIL {name}:\n  " + string.Join("\n  ", lines));
}

const string Fence = "```";
string Doc(params string[] blocks) =>
    "---\nbib: 1\n---\n# A document\n\n" + string.Join("\n\n", blocks.Select(b => $"{Fence}bib\n{b}\n{Fence}"));

{
    // Block 2 opens at line 11 of the file: its line 2 is line 13.
    var (lines, errors) = Check.Report(Doc("text(\"one\")\ntext(\"two\")", "let c = 1\n  - 1\ntext(\"{c}\")"), "minus.bib.md");
    Expect("a line that starts with - is a warning at its line in the file",
        errors == 0 && lines.Any(l => l.StartsWith("minus.bib.md: warning: a line that starts with -") && l.Contains(" at line 13:3 (block 2, from line 11)")),
        lines);
}
{
    var (lines, errors) = Check.Report(Doc("text(\"a\")", "text(nope)"), "error.bib.md");
    Expect("an error is at its line in the file", errors == 1 && lines.Any(l => l.Contains("error:") && l.Contains(" at line 11:1 (block 2, from line 10)")), lines);
}
{
    var (lines, _) = Check.Report(Doc("slider(\"w(AA)\", 0, 2)\nslider(\"w(aa)\", 0, 2)"), "same.bib.md");
    Expect("a warning with no line is said as it is", lines.Any(l => l.StartsWith("same.bib.md: warning: slider: 'w(aa)' is the same input")), lines);
}

{
    // A click on a box with an id runs, and fails, though no button does.
    var (lines, errors) = Check.Report(Doc("let c = box(id: \"cell\", width: 20, height: 20)\nif c.clicked { set(\"n\", nothing.at) }\nif key(\"k\").pressed { set(\"k\", 1) }"), "events.bib.md");
    Expect("--check clicks a box with an id and presses the keys asked for",
        errors == 1 && lines.Any(l => l.Contains("click on a box:") && l.Contains("error")) && lines.Any(l => l == "events.bib.md: key “k”: set k"), lines);
}

{
    // Motion is checked like the rest: a wrong ease is an error at its line, the props it knows warn of nothing.
    var (lines, errors) = Check.Report(Doc("box(animate: 200, appear: \"pop\", turn: 1, shake: 2, pulse: 3, at: [0, 0]) { }", "box(animate: {ease: \"wobbly\"}) { }"), "motion.bib.md");
    Expect("motion props warn of nothing, and a wrong ease is an error at its line",
        errors == 1 && !lines.Any(l => l.Contains("warning:")) && lines.Any(l => l.Contains("error:") && l.Contains("ease") && l.Contains(" at line 11:")), lines);
}

{
    // A document that takes another way at a phone's width fails there: --check runs it at page.width 400 too.
    var (lines, errors) = Check.Report(Doc("let cols = page.width < 500 ? nothing : 3\ntext(\"{cols}\")"), "narrow.bib.md");
    Expect("--check runs the document at page.width 400 and says what fails there",
        errors == 1 && lines.Any(l => l.Contains("error: at page.width 400") && l.Contains(" at line 7:")), lines);
    var (wide, wideErrors) = Check.Report(Doc("text(\"{page.width}\")"), "wide.bib.md");
    Expect("page.width is known to --check", wideErrors == 0, wide);
}

Console.WriteLine($"{passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;

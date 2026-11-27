# 04 Loan calculator: where the bib clone falls short

Artifact: `artifacts/04-loan.html`. Clone: `04-loan.bib.md` (three blocks, 181 lines, was 211;
no `locked:`, no hand-made month lists or ring data in the front matter). Round 2 rewrite:
typed fields (amount, rate, term, extra, first payment as `YYYY-MM`), money with
`fmt(…, currency: "USD")`, dates with `date`/`addMonths`/`fmtDate`/`dateParts`, a `pie(…, hole:)`
per scenario, the balance split as three `series(…, stack: "money")` with the plot's own hover,
the whole amortization table in a 440 px scrolling `table(height:, align:, sort:)` with December
rows shaded, and Download CSV through `download()`. Same numbers as round 1 and the artifact:
$2,654.69 a month, $535,687 interest for A; B with $300 extra pays off Sep 2049, 87 months
sooner, $151,925 less interest. Cost: 97,000 steps a frame (was 101,800), 58,000 in the yearly view.

Proved headless with `/tmp/bibmd-tw-next/bibmd --script` and a PNG each: a preset
(`click:bib-baon15years#0`: B's typed fields become 15 years, Dec 2041, 180 months sooner), typing
(`type:` into Loan amount A: $423,750 gives $2,678.39 a month; a typo `420000x` says "Not a
number: amount"), the plot hover (`move:bib-plot-0@0.4,0.5`: cursor, dots and a tooltip with the
three values), sorting (`click:bib-th-schedule-3` twice: Principal descending, ▼ on the head),
scrolling the table (`wheel:bib-tbody-schedule:-3`, the head stays), and the CSV
(`click:bib-downloadcsv#0` with `XDG_DOWNLOAD_DIR=/tmp/rw/04dl`: `amortization-a-monthly.csv`
written, 361 lines, and "Saved …" under the block; the yearly view writes `amortization-a-yearly.csv`).

## Gaps

### 1. No number or date input: sliders for everything — **narrowed**
`set` moves a text input now, so the fields are `input`s, typed as in the artifact ($423,750 is
possible) and still set by the presets. Still missing: a number field (`num("420000x")` is NaN,
written "nan", and `??` does not catch it, so the clone tests `v >= 0`; a lambda strips the
commas), units drawn in the field, nudging with arrows, and a month picker (the first payment is
typed `2027-01` and parsed by hand, a bad month falling back to January).
- **Proposed API:** `number(label, value:, min:, max:, step:, unit: "USD")` returning a number
  or null; `num(s)` returning null for text that is not a number; `month(label, value: "2027-01")`.

### 2. No pie or donut chart — **closed**
`pie([{label: "Principal", …}, {label: "Interest", …}], hole: 0.6, label: "56%\ninterest", height: 150)`,
one call in place of the 18-line `fn donut` that drew 72 short lines. Its legend writes the values with plain `fmt`
(420,000), not as money: `currency:` on `pie` would finish it.

### 3. No stacked area: fills overlap and blend — **closed**
`series(…, stack: "money")` on interest, principal and balance: opaque bands, each on the one
below, the legend right, and no cumulative sums computed for the drawing.

### 4. No hover on a chart — **closed** (narrowed for its text)
The plot draws a cursor, a dot on each band and a tooltip with the three values at the nearest
month; the Month slider, its rule and the three stats under the chart are gone. The tooltip
writes x as a number ("years from the first payment 10.92") and the values as plain numbers
(275,617.39), where the artifact says "Nov 2037, Balance $347,854".
- **Proposed API:** `plot(…, xFormat: x => …)` and `series(…, unit:)`/`currency:`, or a
  `let h = plot(…)` returning `{x}` at the cursor for a document to describe.

### 5. The long table: no scrolling box, no sticky header, no right-aligned numbers — **closed**
`table(head, rows, id: "schedule", sort: true, height: 440, align: ["end", "start", "end", …])`:
all 360 rows in a box that scrolls under a fixed head, numbers at the end of their column,
any column sorted by its number (`{text: "$2,654.69", value: 2654.69}`; the Month cell sorts by
its index). December closes a year with a shaded row (cell `background:`), though see bug 2.
The page slider and "Rows 1–24" are gone. Formatting every row as money is what the monthly
view costs: some 36,000 steps for 360 rows once the cells are built directly (54,000 through a
`map` over the columns).

### 6. No file download — **closed**
`download(name, csv)` in the button's branch: bibmd saves it in the Downloads folder and says
where. `effects: [download]` is needed in the front matter, or `--check` warns that a host
refuses it; the reference does not yet document `download()` or `copy()` (signature, the
effects line, where the file goes).

### 7. Currency and dates are written by hand — **closed**
`fmt(x, 0, currency: "USD")` and `fmt(x, currency: "USD")`; `date(y, m, 1)`, `addMonths(start, k)`,
`fmtDate(d, "MMM YYYY")`, `dateParts(d)`. The `names` list and the `months`/`ring` lists in
`data:` are gone (`range(n)`).

### 8. Two scenarios: everything twice — **unchanged (cosmetic)**
`fn inputs(tag, extra)` and `fn summary` serve both. Inside `grid { }` a `let` is local, so the
five fields are gathered as `f = {...f, amount: input(…)}`, and `A`, `B`, `RA`, `RB` are declared
before the cards.

### 9. Presets: copying A to B is a `set` per input — **still open (cosmetic)**
`fn copyAtoB(change)` merges `{...A, ...change}` and sets five keys, each turned back into text.
- **Proposed API:** `set({"amount B": …, "rate B": …})`.

### 10. Shareable link — **still open**
State lasts while the document is open; the artifact keeps both scenarios in the URL.

## Bugs

Fixed since round 1: **`set()` on a text input** (the presets now set typed fields) and **"That
click does nothing here" after a preset**.

1. **`value(key)` of an input at its default is still not its value** (narrowed: `value(key, d)`
   gives `d`). Repro:
   ```bib
   let a = slider("Start", 1, 192, value: 25, step: 1, id: "start A")
   text("{value("start A", -1)} {value("start A")}")
   ```
   draws `-1 null` while the slider shows 25. A drawn input's value is known; `value` should
   give it. The clone avoids `value` by keeping `A` as a record.
2. **A table cell with `background:` is drawn as a separate rounded pill**, with a gap between
   cells, a taller row and its text moved in from the column's end, so a shaded row does not
   line up with the rows around it. Repro:
   ```bib
   table(["#", "Amount"], [
     [{text: "1", value: 1}, {text: "$1.00", value: 1}],
     [{text: "2", value: 2, background: fade(accent, 0.2)}, {text: "$2.00", value: 2, background: fade(accent, 0.2)}],
   ], align: ["end", "end"])
   ```
   A row-level band (`{row: [...], background:}`), or cells filled edge to edge, would draw
   the artifact's year-end line.
3. **`--check`'s click line lists a key set twice once** (still): `set("Terms", 9); set("Terms", 11)`
   prints "set terms" once. Minor; the presets no longer do it.

A cost trap, not a bug: grouping the yearly rows as `unique(rows.map(yearOf)).map(y => rows.filter(…))`
(31 × 360 lambda calls) ran out of the 200,000 steps; slicing by month count fits easily (the whole yearly view is 58,000).

## Ranked: what is still missing here

1. A number input (and `num` giving null for non-numbers) and a month picker: the main inputs
   of a calculator are still text fields parsed by hand.
2. Formatting in charts: `currency:` on `pie` and the plot's tooltip, and an x formatter, so
   the hover says "Nov 2037, $347,854".
3. A table row drawn as one band (bug 2), and `set` with a record / state kept in a link.

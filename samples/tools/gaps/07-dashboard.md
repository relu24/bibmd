# Gaps: 07 sales dashboard

Artifact: `artifacts/07-dashboard.html` (419 lines). Clone: `07-dashboard.bib.md` (234 lines,
5 blocks, ~29,900 steps a frame, `--check` clean). Same 60 invented orders for the invented
company Fernwood Goods in both.

**Round 2** (rewritten with box events, `key()`, `changed()`, `table(id:, sort:, page:)`,
`stat(delta:, spark:)`, plot hover, `fmt(currency:)` and `range()`; checked with
`/tmp/bibmd-tw-next/bibmd`). The hand-built table (~40 lines), the hand-drawn sparklines
(12 lines), the Inspect-month slider and the region buttons are gone. Proved headless, each
with a PNG:

- `click:bibbox-region-South;click:bib-th-orders-7;click:bib-th-orders-7;key:Right`: the
  region filters every view (others faded), Revenue sorts descending (▼), → goes to page 2 of 3.
- `key:Right;key:Right;click:bib-search;type:o`: back on page 1 of 6. The same script with the
  `changed(…)` line taken out stays on page 3 of 6, so the reset is what `changed` does.
- `click:bibbox-region-East;click:bib-search;type:SO-1082;key:Escape;key:Escape`: region and
  search cleared (see new bug 2 for why Esc twice).
- `drag:bib-tomonth:-110;drag:bib-frommonth:140`: From moved past To pushes To along (both Sep).
- `move:bib-plot-0@0.36,0.5`: the line's cursor and value at April;
  `move:bibbox-region-South`: the tip "South: $14.5k, 37% of revenue".

## 1. Hover tooltips on charts — **narrowed**

- **Artifact:** moving over the revenue line snaps a cursor and dot to the nearest month and
  shows a floating tooltip (month, revenue, order count, change against the month before);
  hovering a bar shows its share; hovering a dot shows the order.
- **Now:** a plot's built-in hover gives the cursor, the dot and the revenue at the nearest month,
  so the Inspect slider is gone. A region bar is a `box(id:, tip:)` and shows its share on hover.
- **Still open:** the hover tooltip shows only each series' y: no order count or change, and its
  title is the x label and number ("month (1 = Jan) 4"), not "Apr". `dots` show no hover at all
  (the scatter still needs the search to label a dot).
- **Fix belongs:** bibmd Charts.cs.
- **Proposed API:** `series(…, tip: i => "{months[i]}: {money(y[i])}, {n[i]} orders")` and the
  same on `dots`; `plot(xTicks: data.months)` names the cursor's x too.

## 2. Clicking a chart to cross-filter — **closed** for the bars, **still open** for dots

- **Now:** each region's row is a `box(id: "region-{r}", hover:, tip:)` with its bar in a layer;
  `.clicked` toggles the region (and sends the table to page 1). About the same lines as before,
  but the whole row is the target, as in the artifact, and the ghost buttons are gone.
- **Still open:** `bars()` itself cannot be clicked (the layer bar stands in for one call), and a
  dot in a `plot` cannot be clicked to look up its order.
- **Proposed API:** `bars(items, id: "region")` returning the clicked label; `dots(…, id:)` the index.

## 3. Sortable table headers — **closed**

`table(…, id: "orders", sort: true, page: 10, align:, widths:)` sorts on a head click (▲▼),
sorts `{text, value}` cells by number, pages with Back / Next, and returns `{sort, desc, page,
pages}`. Region cells are coloured with `{text, color}`. The header alignment bug is gone with
the hand-built table.

- **Left over (small):** there is no starting sort (`value: {sort: 1, desc: true}` is not
  read), so the doc sorts the rows by date first; a first click on a number column sorts
  ascending, where the artifact starts numbers descending.

## 4. Pagination resets when a filter changes — **closed**

`if changed("Search") || changed("Segment") || changed("From month") || changed("To month")
{ set("orders", {...st, page: 1}) }`; the region click and Esc do the same. `range()` replaced
the written-out month list. Proved above against a control run.

## 5. Sparklines in KPI tiles — **closed**

`stat("Revenue", money(v), delta: 12.4, spark: monthly)`: the green ▲ / red ▼ and the line are
drawn by `stat`. **Narrowed, not gone:** the selected months are no longer shaded on the
sparkline (`spark:` has no range), and `delta: null` simply omits the change, so a note under
the tiles says what it is compared with.

## 6. Date range picker — **narrowed**

- **Now:** `changed("From month")` pushes To along (and To pulls From back), as the artifact's
  two selects do; proved by script.
- **Still open:** the sliders read "From month: 9", not "Sep"; no range slider.
- **Proposed API:** `slider(…, labels: data.months)`; `range("Months", 1, 12, value: [1, 12])`.

## 7. Categorical x axis on a plot — **still open**

Ticks are still numbers ("2, 4, 6…") with `xLabel: "month (1 = Jan)"`, and the hover inherits
it (gap 1). **Proposed API:** `plot(xTicks: data.months)`.

## 8. Currency units — **narrowed**

`fmt(v, currency: "USD", compact: true)` gives `$10.8k`, and `fmt(v, 0, currency: "USD")`
`$9,074`, so the hand-made `money` is a one-liner and the table and tiles read as the artifact.
**Still open:** `bars(…, unit: "$")` still writes `10,812 $`. **Proposed API:**
`bars(items, currency: "USD")` or a `format:` lambda.

## 9. Draw-in animation, transitions, keyboard — **narrowed**

- **Closed:** ← → turn the table's pages and Esc clears region and search (`key()`).
- **Still open:** the line drawing itself in, bars growing.

## Bugs hit (round 1)

1. Segmented `choice` taking the whole row — **fixed** in the new binary; the `box(width: 330)`
   workaround is removed and the search input fills what is left.
2. Rows clipping instead of wrapping — **fixed** (`wrap: true` now wraps).
3. "That click does nothing here." after a `set`-only click — not seen in this round.
4. A button spilling out of its box — gone with the hand-built header (and px boxes now clip).
5. A dot and a text not centred in a row — **still there**, see new bug 1.

## New bugs and traps (round 2)

1. **`align: center` does not centre a row's children across.** Repro:
   ```
   box(dir: "row", width: 300, height: 60, align: center, background: raised) {
     box(width: 40, height: 10, background: danger)
     text("x")
   }
   ```
   Both sit at the top of the 60 px row. (Hit hard in 09's chart; here only the 8 px region dot
   of round 1.)
2. **Esc while the search field has focus only leaves the field**: `key("escape")` fires on the
   second press. Repro: `click:bib-search;type:SO;key:Escape` leaves the search set; a second
   `key:Escape` clears it. Arguably right for a text field, but the artifact clears on the first.
3. **`fmt(812.4, currency: "USD", compact: true)` is `$812.4`**: currency's two decimals are lost
   under compact. Cosmetic; the doc only uses compact from 10,000.
4. **A plot's hover title is `xLabel` and the raw x** ("month (1 = Jan) 4"): reads oddly with a
   descriptive label. Repro: any `plot(xLabel: "month (1 = Jan)") { series(…) }`, hover it.

## Top 3 still missing, ranked

1. **Per-mark tooltips and clicks on charts** (`tip:` on `series` / `dots` / `bars`, `id:` on
   `bars` and `dots`): the scatter is the one view left that still needs a workaround.
2. **Category ticks** (`plot(xTicks: …)`, `slider(labels: …)`): months as names on the axis, in
   the hover and on the month sliders.
3. **`align: center` across a row** (bibmd layout): every row of mixed heights sits at the top.

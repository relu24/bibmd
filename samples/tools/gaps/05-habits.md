# Gaps: 05 habits (artifact vs bib clone)

Artifact: `artifacts/05-habits.html` (about 300 lines). Clone: `05-habits.bib.md` (140 lines,
4 blocks; `--check` ok, 122,710 steps of the 200,000 budget, 154,284 values).

Round 2 rewrite (with box events, `today()`, the date builtins, stacked `bars` and hex tones).
What the clone now does as the artifact does: four habits with a year of history, a 53 x 7
year per habit whose **day squares are clickable boxes** (click toggles the day; a future day
ignores the click), **a tooltip per square** ("Sat 11 April 2026 · Morning run: done"), a hover
outline, today outlined, month and weekday labels computed from the year (no calendar in the
front matter), current and longest streak and this month's completion counted back from
`today()`, the last 12 weeks as **stacked bars in each habit's hex colour** with a legend,
removing a habit (×), adding one with **round colour swatches** (clickable boxes with a ring on
the chosen one) or **any colour** typed as hex and read by `colourOf`, and a **primary** Add
button.

Proved headless with `/tmp/bibmd-tw-next/bibmd --script`:
- `click:bibbox-day-0-330;click:bibbox-day-0-329;click:bibbox-day-0-340;move:bibbox-day-1-100`:
  today and yesterday of "Read 20 pages" undone (current streak 2 → 0, November 67% → 59%),
  the future day 340 unchanged, and the tip over 11 April of "Morning run".
- `click:bib-remove1#0;click:bibbox-swatch-6;click:bib-name;type:Stretch;click:bib-addhabit#0;click:bibbox-day-3-330;move:bibbox-swatch-2`:
  "Morning run" removed, teal chosen, "Stretch" added with an empty year, its today marked
  (streak 1, November 4%), the weekly bars and legend gain a teal part, the swatch's tip shows.
- `click:bib-anycolour;type:#c03`: the preview and the text say `#cc0033`.

## Gaps

### 1. A day square cannot be clicked: **closed**
- **Artifact:** click any square of any heatmap to mark that day done or undone; the square pops.
- **Now:** each day is `box(id: "day-{hi}-{d}", …)`; `if c.clicked && d <= T { set(…flip…) }`.
  The "Mark a day" panel (week stepper and 28 day buttons) is gone. Closed by `box(id:)`
  events. The pop is gap 6.

### 2. No hover tooltip: **closed**
- **Now:** `tip: "{dayName[d]} · {hb.name}: done"` on each square, and `hover: {borderColor: ink}`
  for the outline. The per-week table that stood in for it is gone. Closed by `tip:` and `hover:`.

### 3. No dates: **closed**
- **Now:** `today()`, `date(y, 1, 1)`, `daysBetween`, `weekday` (for the first week's padding),
  `addDays`, `dateParts` and `fmtDate` ("ddd D MMMM YYYY", "MMM", "D MMM"). The front matter
  holds only `year: 2026`; `pad`, `mstart`, `months`, `weekdays`, `today` are gone, and the doc
  is right on any day of the year (and a leap year). The day strings are still indexed from
  1 January of `data.year` (a data format choice, not a gap).

### 4. Colour per item only where the drawing is hand-made: **closed**
- **Now:** `bars(…, stack: true, dir: "column")` with `parts: [{label, value, tone: hb.color}]`
  draws the weekly chart and its legend in one call (it replaced about 25 lines of layer).
  `box(background: hex)` does dots and swatches. Buttons still take no colour (not needed now
  that days are boxes).

### 5. No colour picker: **narrowed**
- **Now:** eight swatches as round clickable boxes with a ring on the chosen one and the hex as
  their tip, and an "Any colour" input whose text is accepted once `colourOf` reads it. What is
  missing is the system colour picker (`<input type=color>`): a hue/lightness surface to pick
  from rather than typing a hex.
- **Fix belongs:** bibui (a colour control) and bibdoc (a `colour(label, value:, swatches:)` widget).

### 6. No animation: **still open**
- The toggled square does not pop, bars do not ease, a new habit is not scrolled into view.
- **Proposed API (unchanged):** `animate: ms` on a keyed box; a `scrollTo(id)` effect for the new card.

### 7. The heatmap costs half the step budget: **narrowed** (moved)
- **Now:** the year is 1,460 boxes with events and tips; the whole doc runs 122,710 steps
  (about 30,000 per habit: ~9 for a bare `box(id:)`, the rest the tip text and the conditional
  props). A fifth habit runs 152,463; a sixth 182,216 and `--check` warns ("moving an input may
  run out"); a seventh would fail. `heatmap()` would be free but its cells answer nothing, and
  the artifact's point is the click, so it was not used.
- **Fix belongs:** bibdoc/bibmd Charts.cs.
- **Proposed API:** `heatmap(values, …, id: "heat", tips: [...])` returning `{clicked, hovered,
  cell}` (the index of the cell), so a clickable year is one free call.

### 8. Buttons cannot be styled: **closed**
- `style: primary` and `style: danger` are drawn (Add habit is the filled accent button,
  dimmed while disabled), and a click that only `set`s no longer says "That click does nothing
  here."

### 9. NEW: future days cannot have a dashed outline: **still open** (cosmetic)
- **Artifact:** future days are empty squares with a dashed border.
- **Bib:** `dash:` exists only on a layer's `line` and `path`; a box border is solid. The clone
  draws future days with a solid 1 px `border`.
- **Proposed API:** `borderDash: [2, 2]` on a box.

### 10. NEW: Enter in the name field does not add the habit: **still open**
- **Artifact:** the form submits on Enter.
- **Bib:** `button("Add habit", key: "enter")` does not fire while the input has focus (the
  input keeps the key, as the reference says the page keeps keys it is using), so the key was
  dropped. Repro: `let n = input("Name"); if button("Add", key: "enter").clicked { set("Name", "") }`
  then `--script "click:bib-name;type:Yoga;key:Enter"`: nothing happens.
- **Proposed API:** `input(…, submit: "Add")` (Enter clicks that button), or `changed("Name")`
  carrying `{submitted}` when Enter is pressed in it.

## Bugs hit

Round 1 bugs, retested on `/tmp/bibmd-tw-next/bibmd`:

1. **A loop variable `h` hidden inside a `layer`: fixed** (now a warning: "layer: inside it h is
   the box's height, so the h outside it is hidden").
2. **`style: primary | danger` drawn as the default button: fixed.**
3. **"That click does nothing here." after a click that only `set`s: fixed.**
4. **`row(fit: true)` of row boxes stacked them: changed, still wrong.** The repro
   `row(fit: true) { for n in ["alpha", "beta", "gamma"] { box(dir: "row", gap: 6) { box(width: 10, height: 10, background: accent); text(n) } } }`
   now draws alpha at the left, beta past the right edge of the content column (half clipped
   at 600 px wide), and gamma not at all. Badges in the same `row(fit: true)` sit side by side
   correctly. (The clone does not need it any more: `bars` draws its legend.)
5. **`align: center` in a row box does not centre a small square against text:** still so in
   the repro above (the 10 px square sits at the top of the text line). Cosmetic.

New, while rewriting:

6. **A button's script id keeps `#0` and drops the hyphen even with `id:`.**
   `button("×", id: "remove-{hi}")` is `click:bib-remove1#0`, not `bib-remove-1`; `click:bib-remove-1`
   says "no box". A box's `id: "day-0-330"` is `bibbox-day-0-330` as written. Tooling only, but
   the two id schemes differ.

## Top 3 still missing

1. A clickable, free heatmap (`heatmap(id:)` returning the clicked/hovered cell): the year as
   boxes is the doc's whole step cost and caps it at six habits.
2. Animation on a keyed box (the pop on toggle, bars easing, scrolling to the new habit).
3. A colour picker control (and Enter-to-submit for an input), the last two form gestures the
   artifact has.

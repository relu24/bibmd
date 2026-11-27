# Gaps: 09 project planner (Gantt)

Artifact: `artifacts/09-gantt.html` (359 lines). Clone: `09-gantt.bib.md` (285 lines, 4 blocks,
~25,300 steps a frame, `--check` clean, no warnings). Same invented project in both: a website
relaunch for "Larkspur Studio", 12 tasks, five role labels as owners, start Mon 16 Nov 2026,
four invented holidays.

**Round 2** (rewritten with the date builtins, `today()`, box events with `dx`, `key()`,
`button(key:)`, `path(arrow:, closed:, fill:)`, `line(dash:)`; checked with
`/tmp/bibmd-tw-next/bibmd`). The schedule is still the same to the day (Thu 7 Jan 2027,
35 working days, 9 of 12 critical). The 30-line hand calendar, the 52 Shorter / Longer /
Earlier / Later buttons, the 17-line diamonds, five-line arrows and 40-dash today line are gone:
bars are boxes that drag. Proved headless, each with a PNG:

- `drag:bibbox-bar-cms:90`: the CMS task moves to Mon 23 Nov, its slack drops from 10 to 7 days.
- `drag:bibbox-end-build:60`: the right end of *Build the templates* makes it 12 days; launch
  moves to 12 Jan, 37 working days.
- `drag:bibbox-bar-signoff:40`: the sign-off diamond moves to 11 Dec and the whole tail with it
  (38 working days; the tasks before it get slack).
- `move:bibbox-bar-cms@0.2,0.5;down;glide:420,576:300` (no `up`): mid-drag, the bar, its arrows
  and the dates already follow the pointer, as in the artifact.
- `click:bibbox-bar-copy;key:Right×3;key:Shift+Right`: *Write the copy* 3 Dec–15 Dec, 9 days;
  then `key:Ctrl+Z;key:Ctrl+Z;key:Escape`: 8 days from 2 Dec, nothing selected.
- `click:bibbox-bar-qa;click:bib-newtask;type:Accessibility review;click:bib-add#0`: a 13th task
  after the selected one, and the name field empties.

## 1. Dragging a bar — **closed**

Each bar is `box(id: "bar-{t.id}", tip:)`, its right end a nested `box(id: "end-{t.id}")`. On
the first `pressed` the doc remembers the start (`state("drag")`) and pushes an undo step; each
`pressed` after reads `dx`, turns it into calendar days and snaps to the next working day; the
plan reflows live; `released` ends it. Click selects (outline and a tinted row); ← → nudge,
Shift+← → resize. The editor rows of buttons are gone.

- **Left over:** the bars must be laid out as boxes in rows, not drawn in the layer, so the chart
  has a size in px (gap 9) and each row is a spacer box plus a bar box.

## 2. No dates — **closed**

`workdays(start, n, skip: holidays)`, `daysBetween`, `addDays`, `weekday`, `fmtDate(d, "ddd D
MMM YYYY")`. Working day n is `workdays(data.start, n, skip:)`; the list of 90 is cached as
calendar offsets so the chart can snap a drop. No six-month limit any more.

- **Left over (small):** there is no "how many working days between a and b"; the resize counts
  them with a `filter` over the cached list.

## 3. No today — **closed**

`today()`; `today:` is gone from the front matter. The line is dashed with `dash: [4, 3]`.

## 4. No Gantt/timeline chart kind — **narrowed**

Still a `box` + `layer` for the grid, arrows, whiskers and the today line (~45 lines), plus a box
per bar. Shorter, because of `path` and `dash`, and the bars do what the artifact's do. A
`timeline` chart kind would still save most of it. Proposed API unchanged from round 1.

## 5. No paths, polygons, arrowheads or dashes — **closed**

Arrows are `path([[x1, y1], [mx, y1], [mx, y2], [x2, y2]], arrow: "end")`, diamonds
`path(…, closed: true, fill:)`, the today line `line(…, dash: [4, 3])`. About 300 shapes became
about 40; the diamonds' edges are smooth.

## 6. A dropdown — **narrowed (worked around by design)**

`choice` still has no `style: dropdown`. The rewrite drops the 13-option "After" radio list: a
new task goes after the **selected** bar (or the last task), as the button says ("Add after
"Testing and fixes""). Still open for documents that need a long pick list.

## 7. Hover tooltips, selection highlight, keyboard — **closed**

`tip:` on a bar box gives name, days, dates and slack; the selected bar is outlined, its chart
row and table row tinted; ← → / Shift+← → / Esc as in the artifact.

## 8. Undo — **closed (in the document)**

A `history` state of `{edits, added}` (50 deep), pushed before every change;
`button("Undo", key: "ctrl+z")` pops it. Reset is three `set`s now, since edits live in one
`edits` list. An `undo()` built in would still save the bookkeeping.

## 9. A long plan does not scroll sideways — **still open**

Days are fitted into 800 px (`DW = 800 / days`), so a long plan still squeezes. Worse than round
1 in one way: the bars need a px width, so the chart no longer follows the page's width
(`layer`'s `w` is not available to the boxes in it), and at a narrow window it is clipped.
**Proposed API:** `box(scroll: "x")`, and a way to read a box's laid-out width outside its
layer (`let size = box(id:).width`).

## Bugs hit (round 1)

1. "That click does nothing here." after a `set`-only click — not seen this round.
2. `set("New task", "")` not emptying the field — **fixed**: proved by the add-task script.
3. `button(style: primary | danger)` drawn plain — **fixed** (Reset is red, Add is accent).
4. `value(key)` null for an input at its default — not hit (no slider per task any more).
5. A lambda returning `{…}` — unchanged, the reference now lists `t => ({…})`.
6. `index` in nested `for`s — still a trap; `rowOf(t.id)` as before.

## New bugs and traps (round 2)

1. **`align: center` does not centre a row's children across.** Repro:
   ```
   box(dir: "row", width: 300, height: 60, align: center, background: raised) {
     box(width: 40, height: 10, background: danger)
     text("x")
   }
   ```
   Both sit at the top. The bars sat 8 px above their arrows; the fix was to make each bar box
   as tall as the row and draw the bar in its middle in a layer.
2. **A box with a block cannot be assigned, only `let`.** Repro:
   ```
   let b = null
   b = box(id: "x", width: 40, height: 20) { text("x") }
   ```
   `error: unexpected '{'; one statement per line`. `let b = box(…) { … }` works, so a box
   whose events are wanted after an enclosing container cannot be read there.
3. **The artifact's own drag is wrong for a task that waits on another** (not a bib bug): it sets
   `delay = delay0 + (n - es0)`, which for *Design sign-off* (starts day 23, delay 0) gives 3,
   below its start, so dragging it right does nothing. The clone sets the earliest start to the
   working day it is dropped on.
4. **A click on a bar is also a press**, so it pushes an empty undo step, as the artifact's
   pointerdown does. Kept for parity; a `clicked`-only box would not.

## Top 3 still missing, ranked

1. **A box's laid-out size outside its layer, and `scroll: "x"`**: lets a chart of boxes follow
   the page and scroll when long (bibmd + bibdoc).
2. **`align: center` across a row** (bibmd layout): the bug behind the bar offset.
3. **A `timeline` chart kind** (bibmd Charts.cs) with draggable bars returning
   `{dropped: {id, start, end}}`: the ~45 lines of layer and ~40 of bar boxes become one call.

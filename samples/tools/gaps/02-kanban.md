# Gaps: 02 kanban

Artifact: `samples/tools/artifacts/02-kanban.html` (314 lines).
Bib clone: `samples/tools/02-kanban.bib.md` (1 block, about 200 lines of bib; `bibmd --check`:
`ok, 1 bib block(s), 4,785 steps, 2,151 values`).

Round 2 rewrite: cards are boxes with `id` that drag between columns (press remembers the card, the
column and card under the pointer are read from `hovered`, the release drops it before the hovered card
or at the column's end). While a card is held its column is outlined in the accent colour, a line shows
where it will land and the card itself is dimmed. A click on a card selects it; then ← → move it a
column, Delete deletes, Enter opens the editor, Escape lets go; a click on the selected card's title
renames it in place. "+ New card" (or **n**) opens a form only while it is used; the same form edits,
seeded with `set()`. Filters are clickable tag and owner pills that toggle independently, plus search.
Cards have hover outlines, tips and a priority stripe drawn in a layer.

WIP limits: moving never refuses (the artifact does not either); the limit is a warning, recomputed
from `cards` on every frame, so a drag, an arrow key and an edit that changes the column all update it.
Proven by script.

Proven headless with `/tmp/bibmd-tw-next/bibmd --png … --script`:
- `move:bibbox-card-1;down;glide:790,430:400` (held, not let go): Review outlined in accent, an insertion
  line above "Export to Markdown…", the held card dimmed.
- the same `;up`: the card lands in Review before that card; Review 4, "over WIP limit (2)".
- `card-4` to the end of Doing, `card-10` and `card-9` to Done: Doing 4 "over WIP limit (3)" in red,
  Review 1 with "limit 2" (no longer red), Done 4, summary "4 done · Doing over limit".
- `move:bibbox-title-2;down;glide:…;up`: a drag that starts on the title moves the card too.
- `click:bibbox-card-1;key:Right`: selected (accent border, Edit / Delete) and moved into Doing, which
  turns red.
- `click:bibbox-title-1;click:bibbox-title-1;click:bib-rename;key:End;type: (v2);key:Enter;key:Enter`:
  renamed in place to "Offline sync for shared notebooks (v2)".
- `click:bibbox-title-6;key:Enter`: the "Edit card" form, seeded with Backend / feature / P2 / Doing;
  `…;click:bib-fprio-P1;click:bib-fcol-Review;click:bib-save#0`: P1, in Review, Review 4.
- `…;key:Delete` on a selected card: 11 cards.
- `key:N;click:bib-ftitle;type:Write release notes;click:bib-fowner-PM;click:bib-add#0`: 13 cards;
  `click:bibbox-tag-bug;click:bibbox-tag-chore;click:bibbox-owner-QA;click:bibbox-owner-PM`: "3 shown",
  counts 0/5, 1/3, 1/3, 1/2, tip "Show PM's cards".
- `click:bib-search;type:export`: one card; typing an "n" into search does not open the form.

## Gaps

### 1. No drag and drop — **closed** (a preview **still open**)
- **Now:** `box(id: "card-…")` returns `pressed` / `released` / `hovered`; the drop is the reference's
  pattern plus a `moveTo(id, col, before)` that places the card before the hovered card, so order within
  a column changes too. The drop target and the insertion line are drawn from two states (`overCol`,
  `overCard`) set on each press-move, because a box's `hovered` is only known after it has drawn.
- **Still open (cosmetic):** the card does not follow the pointer: `at:` is accepted but not drawn, so
  there is no drag image; the source is dimmed with `opacity: 0.4` instead. Proposed: draw `at:`, or let
  bibmd draw a held box's ghost under the pointer by itself.

### 2. A box cannot be clicked — **closed** (double-click **still open**)
- **Now:** cards, titles and filter pills are boxes with `id` and `clicked`. No Edit button on every card:
  a click selects, and Edit / Delete appear on the selected card only.
- **Still open (cosmetic):** no `doubleClicked`; "double-click to edit" became click, then Enter.

### 3. No inline editing — **narrowed**
- **Now:** `set("rename", k.title)` seeds the field (bug 1 is fixed), so one `input(id: "rename")` serves
  every card, and one form (`ftitle`, `fowner`, …) serves both new and edit. The rename saves as it is
  typed (`if changed("rename") { set("cards", …) }`); Escape puts the old title back.
- **Still open (clumsy):** Enter in a focused field never reaches the document (new bug 2), so closing the
  rename takes Enter twice; there is no commit or blur event. Proposed as before: `committed("rename")`
  true on Enter or blur, and `input(…, inline: true)` drawn as text until clicked.

### 4. No modal dialog — **narrowed**
- **Now:** the form is drawn only while it is used (`state("form")`), above the board, so it no longer
  takes 170 px all the time; Escape cancels it (`button("Cancel", key: "escape")`).
- **Still open (cosmetic):** it pushes the board down instead of floating over a dimmed page.
  Proposed: `dialog(title, open:) { … }`.

### 5. Filters are single-choice — **closed**
- **Now:** a pill per tag and per owner, each a box with `id`, `tip:` and `hover:`, toggling membership
  in `state("tags")` / `state("owners")`. Several at once, as in the artifact. (A `checklist(style:
  chips)` widget would make it one line, but it is no longer needed.)

### 6. A row of inputs cannot wrap — **closed**
- **Now** (the newer binary): `box(dir: "row", wrap: true)` holds the search and nine pills, and the form's
  four segmented choices wrap onto lines of their own width. An input in a row fills what is left, so
  the pills go to the line under the search rather than beside it. Cosmetic; `input(…, width: 320)` would
  settle it.

### 7. Card decoration — **narrowed**
- **Now:** `hover: {borderColor: accent}` and `tip:` are drawn; the priority stripe is
  `layer { rect(0, 0, 4, h, fill: …) }`, which follows the card's height, so the editor and the renamed
  card keep it too.
- **Still open (cosmetic):** `shadow` is not drawn; columns are not of equal height (`height: fill` in a
  row); no hover-only tools (a box's `hovered` arrives after its children are drawn, so "show the icons
  while hovered" would need a state set from an event).

### 8. No keyboard — **closed** (focus **still open**)
- **Now:** `key("left")`, `key("right")`, `key("delete")`, `key("enter")`, `key("escape")` act on the
  selected card, and `button("+ New card", key: "n")` opens the form; a focused search field keeps its
  letters.
- **Still open (cosmetic):** "/" cannot focus the search: there is no way to focus an input from the
  document. Proposed: `focus("Search")` as an effect allowed in an event branch.

### 9. Buttons have no id — **closed**
- **Now:** `button("Edit", id: "edit-{k.id}")`. (The script names it `bib-edit-6#0`.)

## Bugs hit

Round 1 bugs: **1 (`set()` cannot change a typed input) — fixed**, the editor and the form are seeded with
`set`. **2 (`wrap: true` rows side by side, a choice as wide as the row) — fixed** in the newer binary.
**3 (set-only click says "does nothing") — fixed.** **4 (`style: primary` not drawn) — fixed.**

New in round 2:

1. **A box with an id inside another takes the press, and the outer box never hears it.** The card's title
   is a box with `id` (to rename on click); a drag that starts on the title did not move the card until the
   title box got its own `pressed` / `released` handling, duplicated from the card's. Repro:
   ```
   let p = state("p", 0)
   let o = box(id: "outer", padding: 20, background: raised) {
     box(id: "inner") { text("title") }
     text("body")
   }
   if o.pressed { set("p", p + 1) }
   text("outer pressed {p}")
   ```
   `--script "move:bibbox-inner;down;up"`: "outer pressed 0"; on "body": 1. The reference says "a box inside
   another answers first", so it is as documented, but for a press the outer one should hear it too
   (events that the inner box does not use could bubble, or `clicked` could stay with the inner box while
   `pressed` / `released` go to both).
2. **Enter in a focused input never reaches `key("enter")`.** The field takes the first Enter and lets go of
   focus; only the second reaches the document. There is no commit event either, so "Enter saves" cannot be
   written. Repro:
   ```
   let n = state("n", 0)
   let t = input("Name")
   if key("enter").pressed { set("n", n + 1) }
   text("enter pressed {n} times")
   ```
   `--script "click:bib-name;type:abc;key:Enter"`: 0 times; with a second `key:Enter`: 1. With
   `…;key:Enter;type:def` the field still says "abc": the first Enter blurred it.
3. **A tooltip runs off the right edge of the window.** A card in the last column with a long `tip:` has its
   tooltip cut by the window edge ("Drag to move. Click, then ← → Del E…"). Repro: a `box(id: "x", tip: "a
   long tip of forty characters or more")` at the right edge of a 1300 px page, `--script "move:bibbox-x"`.
   Proposed: keep the tooltip inside the window.
4. (Cosmetic, unconfirmed) In `box(dir: "row", align: center) { text(…, width: fill); button(…) }` the
   button sits some 8 px lower than the text's middle.

## Top 3 still missing (02 kanban)

1. **A commit event and focus for text fields:** Enter that reaches the document (or `committed(key)`), and
   `focus(key)`, for inline rename and "/" to search.
2. **Events that reach the outer box** (a press on a nested box with an id also presses its parent), so a
   card with clickable parts can still be dragged from anywhere without duplicated handlers.
3. **Overlays:** a drag image under the pointer (`at:` drawn), and `dialog(…)` for the new-card form.

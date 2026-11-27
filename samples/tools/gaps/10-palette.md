# Gaps: 10 palette builder and contrast checker

Artifact: `artifacts/10-palette.html` (300 lines). Clone: `10-palette.bib.md` (177 lines,
5 blocks, ~6,500 steps a frame, `--check` clean, no warnings). Same base colour (#2f6fd0, HSL
217/63/50), same four harmonies, same shade lightnesses, same invented preview text in both.

**Round 2 (rewritten with `colour()`, the colour builtins, box events, `random`, `key`, `copy()`/`download()`,
coloured table cells).** The 40 lines of hand-written colour maths are gone (`hsl`, `colourOf`,
`contrast` instead), and the frame fell from ~20,200 to ~6,500 steps. The clone now has the
artifact's picker with presets, sliders that follow it and that it follows, swatches that copy
their hex on a click and pick the shades row, a lock button in each swatch's corner, Space to
regenerate, shade steps that copy, a contrast matrix as a `table` with coloured cells, and Copy CSS
and Download palette.css.

Proved headless (`--png --script`, PNGs read):
- `click:bibbox-lock-Secondary;key:Space;key:Space;click:bib-harmony-Triadic;click:bib-basecolour;
  click:bib-basecolour-swatch-6;key:Escape;click:bibbox-sw-Accent;click:bibbox-step-300`: the preset
  #c2367a moves the sliders to 331/56/49, Secondary stays #c48f3b with a closed lock, "Regenerated 2
  times; 1 locked", the shades row is "of Accent", the clipboard holds step 300's #95c7e9;
- `click:bib-hue;…;click:bib-harmony-Split complementary;click:bib-downloadpalettecss#0;click:bib-copycss#0`:
  the hue slider at 180 turns the picker to #2fd0d0, `palette.css` is written and the same CSS is
  on the clipboard.

## 1. No colour picker — **closed**

- `colour("Base colour", value: "#2f6fd0", swatches: data.presets)`: a swatch opening a square, a
  hue strip, the eight presets and a hex field. The hex field, the Use buttons and the preset
  buttons of round 1 are gone.
- **Narrowed rather than gone:** the artifact's slider tracks show the gradient they move through;
  bib's sliders are plain (see 8).

## 2. No colour maths — **closed**

- `hsl(h, s, l)`, `colourOf(hex).h/.s/.l`, `contrast(a, b)`; the `inkOn` rule is one line over
  `contrast`. The hexes match round 1 and the artifact to the digit (primary #2f6dd0, secondary
  #c48f3b, accent #e6de37, scale 50 #f3f6fc … 900 #0f2343). The step-budget hazard is gone: the
  49-cell matrix calls `contrast` twice a cell and the frame is 6,500 steps.
- `readable(bg)` was not used: it picks black or white, and the artifact's rule picks white or
  its ink #111318. `readable(bg, among: [a, b])` would cover it.

## 3. Clicking a swatch to copy its hex; Copy CSS — **closed**

- `box(id: "sw-{r}", tip: "Copy {c}", hover: …)` with `if sw.clicked { copy(c) }`; the same on each
  of the ten shade steps; `button("Copy CSS")` → `copy(css)`, and a `download("palette.css", css)`
  beside it. bibmd shows "Copied." under the block, as the artifact's toast.

## 4. Inputs that follow each other — **closed**

- `if changed("Base colour") { set("Hue", round(p.h)) … }` and
  `if changed("Hue") || changed("Saturation") || changed("Lightness") { set("Base colour", hsl(h, s, l)) }`:
  the picker and the three sliders follow each other, with no Use button, and the two rules do
  not chase each other (a `set` is not a change by the reader).

## 5. Selecting a role by clicking its swatch — **closed**

- The swatch's click copies and sets `scaleRole`; the swatch picked has a 3 px ink border. The
  separate "Scale of" choice is gone.

## 6. Randomness — **closed**

- `random(seed, i * 3 + k)` replaces the hand-made sine hash; `button("Regenerate", key: "space")`
  shows the key and Space presses it.

## 7. Buttons in the preview cannot take the palette's colours — **narrowed**

- The preview's buttons are now boxes with ids and a hover outline, so they could answer a click,
  but a `button` still takes no `background:` or `color:`. Still a box dressed as a button.

## 8. Overlay, gradients, clipping, transitions — **narrowed**

- **Closed:** the lock sits in the swatch's top-right corner (a row with `justify: between`, a box
  with an id inside the swatch's box: the inner box answers first, so a lock click does not also
  copy); swatches outline on hover (`hover:`).
- **Still open:** gradient slider tracks, the 50–900 strip's rounded ends clipping its cells (the
  strip's `radius` does not clip), colour transitions.

## 9. A table with coloured cells — **closed, with a limit**

- `table(…)` with cells `{text: "Aa  4.98  AA", background: b.c, color: a.c, value: ratio}`: the
  matrix is one call, its heads line up, and each cell is written in its own pair.
- **Limit (new gap 10):** a cell has one text colour, so the ratio and the grade are in the pair's
  colour too, unreadable on a failing pair (the artifact writes "Aa" in the pair and the ratio and
  a grade badge in a readable colour). And a coloured cell is a strip the height of its text, not
  the row.

## New gaps (round 2)

10. **A table cell holds one text in one colour.** Proposed: a cell of parts,
    `{parts: [{text: "Aa", color: a.c, weight: bold}, {text: "4.98"}, {badge: "AA", tone: accent}], background: b.c}`,
    or a `cell:` slot so the cell is drawn by the document's own block.
11. **`readable()` chooses only black or white.** The artifact chooses between white and its own
    ink. Proposed: `readable(bg, among: ["#ffffff", INK])`.

## Bugs hit

1. **A headless `--png --script` run saves `download()` into the real `~/Downloads`** (see
   08's bug 1). Repro: `if button("S").clicked { download("x.css", "a") }`, `--script "click:bib-s#0"`.
2. **A segmented choice's script ids are not in the brief or the reference:** an option is
   `bib-<key>-<option text>` (`click:bib-harmony-Split complementary`), found in bibui's
   `InteractableMap.cs`.
3. A segmented choice still needs a box of a given width to share its row (round 1's bug); at
   430 px "Split complementary" was cut off without a sign. Given 490 px it fits.
4. Round 1's bugs 2–4 (`set` not reaching an input, "That click does nothing here.", `style:
   primary` drawn plain) are fixed: `set("Base colour", …)` moves the picker, the notices say what
   happened, and Copy CSS is drawn as a primary button.

## Top 3 still missing, ranked

1. **Table cells of several parts** (gap 10): the contrast matrix can show the pair or the numbers
   legibly, not both.
2. **Gradients and clipping** (`background: gradient([...])`, a radius that clips): slider tracks
   that show the colours they move through, a scale strip with round ends.
3. **Coloured buttons and `readable(bg, among:)`**: a preview that can be clicked, and the
   document's own ink in the readable-text rule.

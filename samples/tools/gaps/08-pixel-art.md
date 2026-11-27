# Gaps: 08 pixel art editor

Artifact: `artifacts/08-pixel-art.html` (317 lines): a `<canvas>` with pointer events.
Clone: `08-pixel-art.bib.md` (1 block, ~25,800 steps a frame at 16 × 16 and ~85,000 at 32 × 32,
`--check` clean). Both start from the same invented 16 × 16 heart, in the 16-colour Sweetie 16 palette.

**Round 2 (rewritten with events, `colour()`, `copy()`/`download()`, `range`).** The clone now
does what the artifact does: **drag to paint** (each move while held paints the line from the last
cell, so a fast drag leaves no gaps), right-click erases, Pencil/Eraser/Fill/Picker as buttons with
keys **B E G I**, **0–9** pick swatches, **M** mirrors, **Ctrl+Z / Ctrl+Y** (and Ctrl+Shift+Z)
undo and redo, a hover outline and an "x 4, y 6 #ef7d57" readout, 16 × 16 and 32 × 32 (undoable),
a `colour()` picker beside 16 clickable swatches with tips, 1× and 4× previews in a layer, **SVG
1× / 8×** as `download()` and **Copy the hexes** as `copy()`. The Line tool (a stand-in for
dragging) and the RGB sliders with hand-written hex maths are gone.

Proved headless (`--png --script`, PNGs read):
- drag: `key:5;move:bibbox-px-16@0.5,0.5;down;glide:600,330:150;glide:700,300:100;up`: an unbroken
  green line along row 1 that turns up into row 0;
- mirror and digit keys: `key:M;key:9;move:bibbox-px-225@0.5,0.5;down;glide:300,700:150;up`: the
  stroke and its mirror image, the axis drawn, the colour #3b5dc9;
- fill, picker, right-click, undo, export: `key:5;key:G;click:bibbox-px-0;key:I;click:bibbox-px-67;
  rclick:bibbox-px-100;rclick:bibbox-px-101;key:Ctrl+Z;click:bib-svg8#0;click:bib-copythehexes#0`:
  the outside of the heart green, the colour picked to #ef7d57 and the tool back to Pencil, one
  pixel erased and the second erase undone, `pixel-art-16x16@8x.svg` written (15 kB, one `<rect>`
  a pixel), the 16 lines of hexes on the clipboard;
- size and history: `click:bib-size-32 × 32;key:3;…drag…;key:Ctrl+Z;key:Ctrl+Z;key:Ctrl+Y`: back to
  the 32 × 32 blank with the Size choice following, 1 step to undo;
- 32 × 32 fills: two fills of the whole canvas and a mirrored stroke, 3 steps to undo.

## 1. Painting by dragging — **closed**

- **Artifact:** press and drag to paint, Bresenham between samples; right-drag erases; the cell
  under the pointer is outlined (both cells in mirror mode).
- **bib now:** `box(id:)` returns `pressed` on the press and every move while held, with `x`, `y`
  relative to the box pressed, and `released`. Each pixel is a `box(id: "px-{i}", hover: {borderColor: ink})`;
  the cell under the pointer is the pressed cell plus `floor(x / pitch)`, `floor(y / pitch)`, and a
  `lineCells` lambda (5 lines over `range`) fills between samples. A stroke is one undo step: the
  first press pushes history, `released` ends the stroke.
- **Still short:** *right-drag* (there is `rightClicked`, no right-button `pressed`, so right-click
  erases one pixel at a time), and the hover outline is one cell, not the cell and its mirror (a
  hover is not an event, so nothing can be set from it). **Narrowed** for those two.
- **Proposed API:** `pressed` with `button: "left" | "right"` (or `rightPressed`); and `x`, `y` while
  `hovered`, so one box could stand for the whole canvas (see 3).

## 2. A colour picker — **closed**

- `colour("Colour", value: "#b13e53", swatches: data.palette)`: a swatch that opens a square, hue
  strip, the 16 swatches and a hex field. `set("Colour", c)` moves it from the palette boxes, the
  picker tool and the digit keys. The hand-made `hex2` / `fromHex` are gone.

## 3. Many cells cost a widget each — **narrowed**

- **bib now:** a pixel is a `box` with an id (no buttons), which can be any size: 30 px at 16 × 16,
  15 px at 32 × 32, so **32 × 32 now works**. Cost: 25,800 steps a frame at 16 × 16, 85,000 at
  32 × 32 (1,024 boxes), and `--check` lists one line per kind of box event, not 256 lines.
- **Still open:** a box gives `x`, `y` only in its events, not while hovered, so a single
  canvas box with a `layer` could be painted but could not outline the cell under the pointer or
  show its position; the per-pixel boxes are there for the hover. 64 × 64 would be 4,096 boxes,
  past the step budget.
- **Proposed API:** `x`, `y` while `hovered`; then one `box(id: "canvas") { layer { … } }` of any size.

## 4. Hovering a ghost button hides the cell — **closed**

- `hover: {borderColor: ink}` on a box with an id draws a thin outline (seen in every PNG).

## 5. Flood fill without loops — **narrowed**

- `range(n)` replaced the 256-number `data.cells`. Still no `while`: the fill is a `reduce` over
  `range(n * n)` turns.
- **New:** the round-1 fill (one cell off a queue each turn, ~140 steps a cell) **runs out of steps
  at 32 × 32**: "That click fails: step budget exhausted at line 125:9" when filling the blank canvas
  (the event run also draws the 85,000-step frame). Rewritten as a scanline fill (each turn paints a
  run along a row with `findIndex` and queues seeds above and below): a whole 32 × 32 canvas costs
  ~65,000 steps, 20 lines of non-obvious code.
- **Proposed API:** `flood(grid, width, i, value)` among the list builtins, or a bounded
  `repeat n while cond { … }`.

## 6. Undo/redo with Ctrl+Z, tool keys — **closed**

- `button("↶ Undo", key: "ctrl+z")` (the key shows in the button), tools as `button(…, key: "b")`,
  and `key("0")`…`key("9")`, `key("m")`, `key("ctrl+shift+z")` for the rest. The artifact's `#` for
  the grid was left out (a shifted symbol depends on the keyboard layout).

## 7. Exporting a PNG — **narrowed**

- **bib now:** `download("pixel-art-16x16@8x.svg", svg)` writes an SVG of one `<rect>` per pixel
  with `shape-rendering='crispEdges'`, at 1× or 8×, and `copy()` puts the pixels on the clipboard
  as hex text. Both work; neither needs `effects:`.
- **Still open:** a PNG. bib has no bytes, so a raster image cannot be built.
- **Proposed API:** `download("heart.png", image(px, 16, 16, scale: 8))`, `image()` building a
  picture from a list of colours.

## 8. Live preview sizes — none (works)

- Unchanged: a `layer` of 1 × 1 and 4 × 4 `rect`s, now only for painted pixels.

## New gaps (round 2)

9. **Nothing can be drawn over a box's children.** A `layer` draws under them, so the mirror's
   dashed axis cannot cross the pixels. The clone puts a 2 px accent box between the halves of
   every row, which shifts the right half by 2 px (a drag that crosses it is 2 px off).
   Proposed: `layer(over: true) { … }`.

## Bugs hit

1. **A headless `--png --script` run saves a `download()` into the real `~/Downloads`** (it obeys
   `XDG_DOWNLOAD_DIR`, which is how the runs above were kept in `/tmp`). A test run should not write
   into the reader's folder. Repro: `if button("S").clicked { download("x.svg", "<svg/>") }`, then
   `bibmd FILE --png /tmp/a.png --script "click:bib-s#0"`; `~/Downloads/x.svg` appears.
   (`--check` reports "save x.svg" and does not write it, as it should.)
2. **`--check` does not find a click that runs out of steps under another tool.** It presses a box
   once with the tool the document starts with (Pencil), so the 32 × 32 fill failure above passed
   `--check` and showed only at run time. Not a bug in the strict sense, but `--check` "ok" read as
   "every click works".
3. **A local named after a builtin gives a puzzling error.** `let colour = ""` followed by
   `colour = colour("Colour")` fails with `'' is not a function at line 189:7`. Better: "`colour` is
   a text here (line 47 named it), not the function `colour`", or a warning when a `let` shadows a
   builtin.
4. Round 1's bugs 1–4 (segmented choice eating the row, "That click does nothing here." after a
   `set`, boxes taller than their height around a button, buttons without ids) are gone or no longer
   met: no buttons on the canvas, and the click notices now say what was done ("Copied.").

## Top 3 still missing, ranked

1. **`x`, `y` while hovered, and right-button press** on `box(id:)`: one canvas box of any size
   (instead of 1,024 boxes and 85,000 steps), a mirrored hover outline, right-drag erase.
2. **A bounded loop or a `flood()` builtin**: the scanline fill in a `reduce` is the hardest code in
   the document and still costs 65,000 steps at 32 × 32.
3. **An image value for `download()`** (`image(px, w, h, scale:)` to PNG), and a layer drawn over
   a box's children (the mirror axis).

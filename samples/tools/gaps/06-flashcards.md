# Gaps: 06 flashcards (artifact vs bib clone)

Artifact: `artifacts/06-flashcards.html` (about 310 lines). Clone: `06-flashcards.bib.md`
(189 lines, 2 blocks; `--check` ok, 2,918 steps, 1,900 values with Study showing; the Quiz tab
checked from a copy with `tabs("Mode", value: "Quiz")`: ok, 2,256 steps). The steps fell from
42,176 to under 3,000 once the hand-made hash sort gave way to `shuffle()` and `sample()`.

Round 2 rewrite (with box events, `key()`, `button(key:)`, `shuffle`/`sample`, `clock()`,
`today()`, button styles). What the clone now does as the artifact does: the **card is the
control** (click it, or press Space, to turn it over; it outlines on hover), **← / →** move,
**K or ↑** knew it, **J or ↓** didn't, **S** shuffles, **Enter** starts the next session; the
quiz answers with **1 to 4** or a click on an option box, which turns **green (right) or red
(wrong)**, and **Enter** goes on and starts a new quiz. Shuffle and New quiz take their seed from
`clock()`, so each is different; the first deal and first quiz are seeded by `today()`, so they
change daily. "Knew it" is a primary button, "Didn't know" a danger button, "Next" is
disabled until answered. A line of key caps under the card says the keys, as the artifact's.

Proved headless with `/tmp/bibmd-tw-next/bibmd --script`:
- `key:Space;key:K;click:bibbox-card;key:Down;key:Right;key:Up;key:S`: flip, knew (box 2: 1),
  flip by click, missed (back to box 1), next, knew by ↑ (box 2: 2), shuffle: "3 of 20 sorted",
  the unsorted rest re-dealt and the card at 4 / 20.
- `key:S` run six times a second apart: six different decks (seed shown as `clock()` values).
- `key:K` x 20 then `key:Enter`: box 2 holds 20, "Session 1 done", Enter starts session 2.
- Quiz: `glide:127,313:10;down;up` (the Quiz tab), `click:bibbox-opt-2;key:Enter`, then
  `key:NumberN;key:Enter` x 9: the score screen, 4 / 10, the mistakes table in red and green.
  `key:Number2` alone marks option 2 red, the answer green, "Not quite: Tungsten is W."

## Gaps

### 1. No keyboard shortcuts: **closed**
- **Now:** `button("Knew it", key: "k")`, `button("Didn't know", key: "j")`,
  `button("← Previous", key: "left")`, `button("Shuffle", key: "s")`, `button("Next", key: "enter")`,
  and `key("space")`, `key("up")`, `key("down")`, `key("{k + 1}")` for keys without a button of
  their own. A whole session runs without the mouse. Keys exist only in the tab that shows,
  since only its block runs, which is what the artifact does by hand with `mode`.
- **Narrowed part:** a button with `key:` draws no key hint; the artifact draws `<kbd>K</kbd>`
  inside the button. The clone draws its own key-cap legend (`fn kbd`). **Proposed:** draw the
  key small after the label, or `hint: true`.

### 2. No randomness: **closed** (one part narrowed)
- **Now:** `shuffle(ids, seed)`, `sample(others, 3, seed)` and seeds made by text
  (`"{qz.seed}-o{k}"`) to keep the questions, the distractors and the option order apart. The
  six-line sin hash is gone. Shuffle and New quiz set the seed from `clock()`.
- **Narrowed:** the first deal has no entropy: `state("seed", today())` changes it daily, but
  two readers on the same day get the same first deck and quiz. `set` cannot run outside an
  event, so there is no "seed once on open". **Proposed:** `state("seed", entropy())` with a
  value the host draws once per open.

### 3. The card does not turn: **closed** (the slide between cards **still open**)
- **Artifact:** rotates in 3D over 0.55 s, and slides out left / in right between cards.
- **Now:** `box(id: "card", …, turn: flipped, animate: {ms: 420, ease: "in-out"})`: when `flipped`
  changes the card narrows to edge on showing the side it was, and widens showing the other; its
  border and background fade. A wrong quiz pick shakes (`shake: tone == danger`). Proved headless:
  `click:bibbox-card;wait:150;png:…;wait:75;png:…` shows the front narrowed, then the back narrow.
- **Still open:** the slide out and in between cards (Next and Previous change the card at once).

### 4. The card itself cannot be clicked: **closed**
- **Now:** `let face = box(id: "card", …, hover: {borderColor: accent}) { … }` and
  `if face.clicked || key("space").pressed { set("flipped", !flipped) }`. The "Turn over"
  button is gone.

### 5. Many related values, one `set` each: **still open**
- "Knew it" is still four `set` calls and the boxes a list parallel to `data.cards`
  (`boxes.map((b, j) => j == i ? …)`). No computed record keys, no `set` of one entry.
- **Proposed API (unchanged):** `{...r, [k]: v}`; `set("boxes", i, v)`.

### 6. No colour or emphasis on a button: **closed**
- `style: primary` and `style: danger` are drawn. The quiz options are now clickable boxes
  coloured with `fade(success | danger, 0.16)` and a border of that tone, as the artifact's
  `.right` / `.wrong`; the mistakes table colours its cells (`{text, color: danger}`).
  The wrong option's shake is gap 3 (animation).

### 7. `--check` sees one tab: **still open**
- `--check` still runs only the tab that shows; the Quiz had to be checked from a copy with
  `tabs("Mode", value: "Quiz")`. **Proposed:** `--check` runs each tab once.

### 8. NEW: a tab cannot be named in `--script`: **still open** (tooling)
- `click:bib-mode`, `bib-quiz`, `bib-mode#1` all say "no box". The quiz was reached with
  `glide:127,313:10;down;up`, i.e. by the pixel. **Proposed:** a tab is `bib-<tabs key>-<n>`
  (or its title), as a button is.

### 9. NEW: `key:2` in a script does nothing, silently: **still open** (tooling)
- The document's `key("2")` is reached only by `--script "key:Number2"` (bibui's enum name);
  `key:2` runs without error and presses nothing, and `key:D2` / `key:Digit2` crash with a stack
  trace from `Program.cs:59`. **Proposed:** the script reads the same names `key()` does
  (`2`, `space`, `ctrl+z`), and an unknown name is an error, not a stack trace.

## Bugs hit

Round 1 bugs, retested on `/tmp/bibmd-tw-next/bibmd`:

1. **`style: primary | danger` drawn as the default button: fixed.**
2. **"That click does nothing here." after a click that only `set`s: fixed.**
3. **Raw HTML in Markdown prose shown as text: still so.** `Two to the 2<sup>n-1</sup> power.`
   draws the tags literally.
4. **No way to pin a header to the top of a box and centre the rest: still so**, worked around
   with `align: center` on the card box and a fixed spacer box under the header row (no
   `justify: between` across a column, no `height: fill` spacer).

New, while rewriting:

5. **`--script key:D2` (or any unknown key name) crashes the run** with a .NET stack trace at
   `src/Program.cs:59` instead of an error line. Repro: `bibmd any.bib.md --png /tmp/x.png --script "key:D2"`.

## Top 3 still missing

1. The slide between cards (the turn and the shake on a wrong pick are closed: `turn:`, `shake:`).
2. Per-open entropy (`entropy()` for a state's default), so the first deal differs per reader.
3. `--check` running every tab, and `--script` able to name tabs and digit keys as `key()` does.

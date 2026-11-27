# 03 Minesweeper: where the bib clone falls short

Artifact: `artifacts/03-minesweeper.html`. Clone: `03-minesweeper.bib.md` (two blocks, about 190
lines, no `locked:` any more). Round 2 rewrite: every square is a `box(id:)` with a hover style;
click opens, right click flags, a click on an opened number chords; mines laid with
`sample(…, seed)`; the seed of a new game comes from `clock()`; a real timer from
`clock(every: 1)`; the face shows 😮 while a square is held; arrows, space, F and N work; the
session's games are listed with Replay. Cost: 9,400 steps a frame (Beginner), 29,000 on 16×16;
the first click on 16×16 adds some 59,000 (opening 30 squares).

Proved headless with `/tmp/bibmd-tw-next/bibmd --script` and a PNG each: click (`click:bibbox-c40`
opens a field), right click (`rclick:bibbox-c0` plants a flag, mines left 9), hover
(`move:bibbox-c40` lightens a closed square), chord (flag c10, click the 1 at c2: c1 opens),
a loss (wrong flag drawn ✕, boom square red, 😵, history row "mine"), a win (every safe square
clicked from the seed's mines: "Cleared in 1 s", 😎, history row "cleared"), the timer
(`wait:1100`: Time 1 s), New game (seed 4242 became 64617, taken from the clock), the face
while held (`move;down`: 😮), keys (`key:Right/Down/Space/Left/F`: the ring moves, space opens,
F turns flag mode on), N and Replay (`key:N` then `click:bib-replay0#0` brings seed 4242 back),
and the Intermediate board.

## Gaps

### 1. No right click: flags take a mode toggle — **closed**
`box(id:)` returns `rightClicked`: `if cell.rightClicked { act(c, true) }`. Flag mode stays only
for touch, as in the artifact, and F switches it.

### 2. A clickable cell has to be a `button`, and a button cannot be styled — **closed**
Every square is a `box(id: "c{c}", background: …, hover: {background: …})` with its own colours:
closed, open, mine, the boom square, a wrong flag, an orange flag. One widget for every state.
(The pop animation is gap 9.)

### 3. No clock: no timer — **closed**
The first click stamps `clock()` into the game; while playing, `clock(every: 1)` reruns the
document each second and the Time stat counts; a win or a loss stamps the end. Only a game in
play calls `clock(every:)`, so a finished board does not tick.

### 4. No randomness: the seed is a slider, the generator is written by hand — **closed**
`sample(cells.filter(c => !safe.includes(c)), L.mines, g.seed)` lays the mines: the Park–Miller
reduce is gone. A new game's seed is `floor(clock() * 1000) % 999999 + 1`, read in the click's
run, so each game differs and still replays. (Typing a seed, as the artifact allows, was left
out: `set` on an input works now, so it could be added.)

### 5. Flood fill: no `while`, so the fill is a `reduce` over every cell — **still open**
Same `cells.reduce(… fillStep …)` of N rounds. Measured on 16×16: the first click (laying
mines, the neighbour table, the counts, a fill opening 30 squares) costs some 59,000 steps on
top of the 29,000 the board draws. A 30×16 board opening a large area would come near the
200,000 budget. `sample` made the mine laying cheap; the fill itself is what costs.
- **Proposed API (unchanged):** a bounded `repeat 1000 while queue.length > 0 { … }`, or
  `reach(start, next: j => …)` returning the set reachable from `start`.

### 6. A missed click on the board opens the block's source — **narrowed**
A click on a box with an id is the box's, so open squares no longer open the source, and an
id on the board box (`box(id: "board", …)`) takes the clicks between squares (proved: a click
in the board's padding leaves the board; without the id it opened the editor). `locked: true`
is gone. A click elsewhere in the block, beside the face say, still opens the source
(proved: `move:bibbox-face@3,0.5;down;up` puts bibmd in Edit mode).
- **Fix belongs:** bibmd (`App.cs`): a block that takes events (has a box with an id, a
  `key()`, a `clock(every:)`) should open its source only from its gutter or with Ctrl+E.

### 7. No chording — **closed**
An opened number is a box with an id too: a click on it with all its flags set opens the rest
of its neighbours through the same `reveal`, which now takes a list of starts.

### 8. No keyboard — **closed**
`key("left")` … `key("down")` move a ring (a 2 px accent border on the square), `key("space")`
and `key("enter")` open it, `key("f")` sets the Flag mode toggle, and `button("New game",
key: "n")`. The ring is the document's own state, not a focus the host knows of; that is fine here.

### 9. No feedback while pressing, no animation — **closed**
`cell.pressed` and `cell.released` set a `down` state, so the face is 😮 while a square is held,
as in the artifact. A square says `pulse: shown, animate: 160`: it pops (grows and settles) when it
opens, and its colour fades from closed to open; the board says `shake: g.status == "lost"`. Proved
headless: `click:bibbox-c40;png:…;wait:25;png:…` shows the opened squares grown over their
neighbours' edges and part way to their open colour.

### 10. History of games with Replay — **closed**
`fn play(next)` sets the game and, when it ended, prepends `{level, seed, result, time}` to a
`history` state (eight at most). Replay sets the Level choice (`set("Level", h.level)`) and a
fresh game on that seed.

## New gaps

### 11. A hex tone is the same in light and dark
The number colours 4 to 6 are hex (`"#5b3fb0"`, a purple), because there is no role for them;
on the dark theme the 4 is dim (seen on the 16×16 board). The artifact has a light and a dark
value for each.
- **Proposed API:** a pair, `tone: ["#5b3fb0", "#b59cff"]` (light, dark), or `readable`-like
  `onSurface(c)` that lightens a colour on a dark page.

## Bugs

Fixed since round 1: **`set()` on a text input** (it moves the field now) and **"That click does
nothing here" after a click that only `set()`s** (no note now).

1. **`row(fit: true)` stacks toggles in a column** (in `/tmp/bibmd-tw-next`, said to keep small
   boxes in one line). Repro:
   ```bib
   row(fit: true) {
     toggle("One", value: false)
     toggle("Two", value: false)
   }
   ```
   draws One above Two; `choice(…, style: segmented)` and a `toggle` in `row(fit: true)` stack
   the same way. A plain `row { }` lays them side by side, so the clone uses that. (A
   `row(fit: true)` of a `text` and two buttons does lie in one line.)

## Ranked: what is still missing here

1. A bounded loop (or a `reach` builtin): the flood fill is still an N-round reduce, the one
   part that grows with the board.
2. A click on an interactive block outside its widgets should not open the source.
3. Hex tones with a dark value (the pop of an opened square is closed: `pulse:`).

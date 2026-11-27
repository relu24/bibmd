---
bib: 1
title: Minesweeper
width: wide
---
###### A tool for a coffee break

# Minesweeper

Clear the field without hitting a mine. A number says how many of the eight neighbours hide
one. Your first click is always safe, and an empty square opens everything around it.

```bib
let level = ""
let flagMode = false
row {
  level = choice("Level", ["Beginner 9×9", "Intermediate 16×16"], style: segmented)
  flagMode = toggle("Flag mode", value: false)
}

let L = level.startsWith("Beginner") ? {w: 9, h: 9, mines: 10, size: 38} : {w: 16, h: 16, mines: 40, size: 30}
let w = L.w
let N = w * L.h
let cells = range(N)

// A cell's neighbours: the eight around it, less those off the board.
let nbrs = i => [i - w - 1, i - w, i - w + 1, i - 1, i + 1, i + w - 1, i + w, i + w + 1]
  .filter(j => j >= 0 && j < N && abs(j % w - i % w) <= 1)

// A game is a record in state. Another level starts a fresh game on the same seed.
let blank = {level, seed: 4242, status: "ready", mines: [], counts: [], open: [], flags: [], boom: -1, started: 0, ended: 0}
let saved = state("game", blank)
let g = saved.level == level ? saved : {...blank, seed: saved.seed}
let over = g.status == "won" || g.status == "lost"
let history = state("history", [])

// One step of the flood fill: open the head of the queue; a zero queues its closed neighbours.
let fillStep = (acc, counts, table) => {
  let j = acc.queue[0]
  let rest = acc.queue.slice(1)
  let opened = acc.open.includes(j) ? acc.open : [...acc.open, j]
  let more = counts[j] == 0 ? table[j].filter(k => !opened.includes(k) && !rest.includes(k) && !g.flags.includes(k)) : []
  return {open: opened, queue: [...rest, ...more]}
}

// What opening the squares `starts` makes of the game. The first click lays the mines
// (never on it or its neighbours) and starts the clock. No while loop: the fill is a reduce
// over every cell, and a cell enters the queue at most once, so N rounds are enough.
fn reveal(starts) {
  let table = cells.map(nbrs)
  let first = g.status == "ready"
  let safe = [starts[0], ...table[starts[0]]]
  let mines = first ? sample(cells.filter(c => !safe.includes(c)), L.mines, g.seed) : g.mines
  let counts = first ? table.map(ns => ns.filter(j => mines.includes(j)).length) : g.counts
  let started = first ? clock() : g.started
  let hit = starts.filter(i => mines.includes(i))
  if hit.length > 0 {
    return {...g, mines, counts, started, ended: clock(), status: "lost", boom: hit[0]}
  }
  let fill = cells.reduce((acc, c) => acc.queue.length == 0 ? acc : fillStep(acc, counts, table), {open: g.open, queue: starts})
  let won = fill.open.length == N - L.mines
  return {...g, mines, counts, started, ended: won ? clock() : 0, open: fill.open,
    status: won ? "won" : "playing", flags: won ? mines : g.flags}
}

// Keep the game, and a finished one in the list of this session's games.
fn play(next) {
  set("game", next)
  if next.status == "won" || next.status == "lost" {
    set("history", [{level, seed: next.seed, result: next.status, time: round(next.ended - next.started)}, ...history].slice(0, 8))
  }
}

fn flag(c) {
  if !over && !g.open.includes(c) {
    set("game", {...g, flags: g.flags.includes(c) ? g.flags.filter(f => f != c) : [...g.flags, c]})
  }
}

// Click an open number whose flags are all set: open the rest of its neighbours.
fn chord(c) {
  let ns = nbrs(c)
  let n = g.counts[c] ?? 0
  let rest = ns.filter(j => !g.open.includes(j) && !g.flags.includes(j))
  if n > 0 && ns.filter(j => g.flags.includes(j)).length == n && rest.length > 0 { play(reveal(rest)) }
}

fn act(c, asFlag) {
  if over { return null }
  if g.open.includes(c) { chord(c) }
  else if asFlag { flag(c) }
  else if !g.flags.includes(c) { play(reveal([c])) }
}

// A seed from the clock: a new field every game, still one that can be replayed.
fn newGame() { set("game", {...blank, seed: floor(clock() * 1000) % 999999 + 1}) }

let numberTone = [ink, accent, success, danger, "#5b3fb0", "#9a3a12", "#0f8a8a", ink, muted]
let elapsed = g.status == "playing" ? floor(clock(every: 1) - g.started) : over ? round(g.ended - g.started) : 0
let down = state("down", false)

// The bar: mines left, the face (a new game), the time.
box(dir: "row", gap: 12, align: center, justify: between) {
  box(width: 140) { stat("Mines left", L.mines - g.flags.length, tone: g.flags.length > L.mines ? danger : neutral) }
  let face = box(id: "face", padding: [4, 10], radius: 10, hover: {background: raised}, tip: "New game (N)") {
    text(g.status == "lost" ? "😵" : g.status == "won" ? "😎" : down ? "😮" : "🙂", size: xl)
  }
  if face.clicked { newGame() }
  box(width: 140) { stat("Time", "{Math.min(elapsed, 999)} s") }
}

// The board: every square a box with an id. Click opens, right click flags.
let cursor = state("cursor", -1)
box(align: center) {
  // An id on the board too, so a click between squares is the board's, not the block's.
  // and the board shakes when a mine goes off.
  box(id: "board", gap: 3, padding: 12, radius: 12, background: raised, shake: g.status == "lost") {
    for r in range(L.h) {
      box(dir: "row", gap: 3) {
        for c in range(r * w, r * w + w) {
          let isMine = g.mines.includes(c)
          let isFlag = g.flags.includes(c)
          let isOpen = g.open.includes(c)
          let n = g.counts[c] ?? 0
          let shown = isOpen || (over && (isMine || isFlag))
          let bg = c == g.boom ? danger
            : over && isMine && !isFlag ? fade(danger, 0.18)
            : over && isFlag && !isMine ? fade(muted, 0.3)
            : isOpen ? surface : fade(muted, 0.28)
          // A square pops as it opens (pulse: runs when its value changes) and its colour fades in.
          let cell = box(id: "c{c}", width: L.size, height: L.size, radius: 6, align: center, justify: center,
            background: bg, border: c == cursor ? 2 : 0, borderColor: accent,
            hover: shown ? {} : {background: fade(muted, 0.45)}, pulse: shown, animate: 160) {
            if over && isMine && !isFlag { text("●", weight: bold, color: c == g.boom ? surface : ink) }
            else if over && isFlag && !isMine { text("✕", weight: bold, color: danger) }
            else if isFlag { text("⚑", weight: bold, color: "#e05a1c") }
            else if isOpen && n > 0 { text(str(n), weight: bold, face: mono, size: lg, color: numberTone[n]) }
          }
          if cell.clicked { act(c, flagMode) }
          if cell.rightClicked { act(c, true) }
          if cell.pressed && !over && !down { set("down", true) }
          if cell.released { set("down", false) }
        }
      }
    }
  }
}

if g.status == "won" { text("Cleared in {elapsed} s. Seed {g.seed}.", weight: bold, color: success, align: center) }
if g.status == "lost" { text("Boom. The board is shown: Same field again replays it, New game lays another.", weight: bold, color: danger, align: center) }

row(fit: true) {
  text("Seed {g.seed}", face: mono, size: sm, color: muted)
  if button("Same field again", style: ghost).clicked { set("game", {...blank, seed: g.seed}) }
  if button("New game", style: primary, key: "n").clicked { newGame() }
}

// The keyboard: arrows move the ring, space or enter opens, F switches flag mode.
let at = cursor < 0 ? 0 : cursor
let moveTo = (dx, dy) => Math.max(0, Math.min(L.h - 1, floor(at / w) + dy)) * w + Math.max(0, Math.min(w - 1, at % w + dx))
if key("left").pressed { set("cursor", moveTo(-1, 0)) }
if key("right").pressed { set("cursor", moveTo(1, 0)) }
if key("up").pressed { set("cursor", moveTo(0, -1)) }
if key("down").pressed { set("cursor", moveTo(0, 1)) }
let space = key("space").pressed
let enter = key("enter").pressed
if (space || enter) && cursor >= 0 { act(cursor, flagMode) }
if key("f").pressed { set("Flag mode", !flagMode) }
```

## This session

```bib
if history.length == 0 {
  text("Finished games are listed here, with a button to replay the same field.", size: sm, color: muted)
}
for h in history {
  box(dir: "row", gap: 12, align: center) {
    text(h.result == "won" ? "😎" : "😵")
    box(width: 220) { text("{h.level}, seed {h.seed}", face: mono, size: sm) }
    box(width: 80) { badge(h.result == "won" ? "cleared" : "mine", tone: h.result == "won" ? success : danger) }
    box(width: 60) { text("{h.time} s", face: mono, size: sm, color: muted) }
    if button("Replay", style: ghost, id: "replay {index}").clicked {
      set("Level", h.level)
      set("game", {...blank, level: h.level, seed: h.seed})
    }
  }
}
```

**How to play.** Click a square to open it, right click to plant or lift a flag (or turn on
*Flag mode*, for a touch screen). Click an opened number whose flags are all set to open the
rest of its neighbours. Arrows move a ring over the board, space opens, F switches flag mode,
N starts a new game.

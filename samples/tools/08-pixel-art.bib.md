---
bib: 1
title: Pixel art editor
width: wide
data:
  palette: ["#1a1c2c", "#5d275d", "#b13e53", "#ef7d57", "#ffcd75", "#a7f070", "#38b764", "#257179", "#29366f", "#3b5dc9", "#41a6f6", "#73eff7", "#f4f4f4", "#94b0c2", "#566c86", "#333c57"]
  tools:
    - { name: Pencil, icon: "✎", key: b }
    - { name: Eraser, icon: "⌫", key: e }
    - { name: Fill, icon: "◧", key: g }
    - { name: Picker, icon: "⊙", key: i }
  key: { k: "#1a1c2c", r: "#b13e53", p: "#ef7d57", w: "#f4f4f4", s: "#5d275d" }
  heart:
    - "................"
    - "................"
    - "...kkk....kkk..."
    - "..krrrk..krrrk.."
    - ".krpprrkkrrrrrk."
    - ".krpwrrrrrrrrrk."
    - ".krpprrrrrrrrrk."
    - ".krrrrrrrrrrrsk."
    - "..krrrrrrrrrrsk."
    - "...krrrrrrrrsk.."
    - "....krrrrrrsk..."
    - ".....krrrrsk...."
    - "......krrsk....."
    - ".......kkk......"
    - "................"
    - "................"
---
# Pixel art editor

Drag to paint. Right-click erases a pixel. Pick a tool and a colour, and go.

```bib
// ── the picture: n × n colours, "" for an empty cell; its size is its length ──
let heart = data.heart.flatMap(r => r.split("").map(c => c == "." ? "" : data.key[c]))
let px = state("px", heart)
let n = px.length == 1024 ? 32 : 16
let hist = state("hist", [])
let redo = state("redo", [])
let last = state("last", -1)     // the cell a stroke is at; -1 between strokes, -2 after a fill or a pick
let tool = state("tool", "Pencil")

let mirror = false
let grid = true
// the colour input is drawn further down, in the palette card; its value is read here, before the canvas uses it
let brush = value("Colour", "#b13e53")

// ── what the tools do: each returns the new picture ──
let mirrorOf = i => floor(i / n) * n + n - 1 - i % n
let spots = i => mirror && mirrorOf(i) != i ? [i, mirrorOf(i)] : [i]
let paintAll = (g, is, c) => g.map((v, j) => is.includes(j) ? c : v)
// the cells from a to b, one per step along the longer side, so a fast drag leaves no gaps
let lineCells = (a, b) => {
  let dx = b % n - a % n
  let dy = floor(b / n) - floor(a / n)
  let k = max([abs(dx), abs(dy), 1])
  return range(k + 1).map(t => (floor(a / n) + round(dy * t / k)) * n + a % n + round(dx * t / k))
}
// flood fill without while: a reduce whose every turn paints one run of the target colour along a
// row and queues a seed for each run touching it above and below (a cell at a time is too dear at 32 × 32)
let flood = (g0, i, c) => {
  let target = g0[i]
  let span = (acc, s) => {
    let r = floor(s / n)
    let x = s % n
    let row = acc.g.slice(r * n, r * n + n)
    let fl = row.slice(0, x).reverse().findIndex(v => v != target)
    let fr = row.slice(x + 1).findIndex(v => v != target)
    let a = fl < 0 ? 0 : x - fl
    let b = fr < 0 ? n - 1 : x + fr
    let g = [...acc.g.slice(0, r * n + a), ...range(b - a + 1).map(k => c), ...acc.g.slice(r * n + b + 1)]
    let seeds = [r - 1, r + 1].filter(rr => rr >= 0 && rr < n).flatMap(rr =>
      range(a, b + 1).filter(k => g[rr * n + k] == target && (k == a || g[rr * n + k - 1] != target)).map(k => rr * n + k))
    return {g, q: [...acc.q.slice(1), ...seeds]}
  }
  let turn = (acc, t) => acc.q.length == 0 ? acc : acc.g[acc.q[0]] != target ? {g: acc.g, q: acc.q.slice(1)} : span(acc, acc.q[0])
  return target == c ? g0 : range(n * n).reduce(turn, {g: g0, q: [i]}).g
}

fn commit(next) {
  set("px", next)
  set("hist", [...hist.slice(-49), px])
  set("redo", [])
}
fn sizeOf(p) { set("Size", p.length == 1024 ? "32 × 32" : "16 × 16") }
fn undo() {
  if hist.length > 0 {
    let p = hist[hist.length - 1]
    set("px", p); set("hist", hist.slice(0, -1)); set("redo", [...redo, px]); sizeOf(p)
  }
}
fn redoIt() {
  if redo.length > 0 {
    let p = redo[redo.length - 1]
    set("px", p); set("redo", redo.slice(0, -1)); set("hist", [...hist, px]); sizeOf(p)
  }
}

let checker = i => (i % n + floor(i / n)) % 2 == 0 ? fade(muted, 0.10) : fade(muted, 0.18)
let painted = px.filter(c => c != "").length

// Three columns side by side on a wide page; narrower, they wrap: the canvas under the tools,
// the palette under the canvas. The pixels are as large as the room the canvas has (page.width).
box(dir: "row", wrap: true, gap: 16) {
  // ── left: tools, options, history ──
  box(width: 200, gap: 12) {
    card("Tools") {
      for t in data.tools {
        if button("{t.icon} {t.name}", key: t.key, id: "tool-{t.name}", style: t.name == tool ? primary : ghost).clicked { set("tool", t.name) }
      }
    }
    card("Options") {
      mirror = toggle("Mirror", value: false)
      grid = toggle("Grid", value: true)
      let size = choice("Size", ["16 × 16", "32 × 32"], style: segmented)
      if changed("Size") { commit(size == "32 × 32" ? range(1024).map(i => "") : heart) }
    }
    card("History") {
      if button("↶ Undo", key: "ctrl+z", disabled: hist.length == 0).clicked { undo() }
      if button("↷ Redo", key: "ctrl+y", disabled: redo.length == 0).clicked { redoIt() }
      if button("Clear", style: danger, disabled: painted == 0).clicked { commit(px.map(c => "")) }
      text("{hist.length} step{hist.length == 1 ? "" : "s"} to undo", size: sm, color: muted)
    }
  }

  // ── middle: the canvas, a box per pixel ──
  // beside the tools and the palette when the page has room for all three, else a line of its own
  let room = (page.width >= 960 ? page.width - 200 - 240 - 32 : page.width) - 30
  let cs = Math.min(n == 16 ? 30 : 15, floor((room - (grid ? n + 1 : 0) - (mirror ? 2 : 0)) / n))
  let pitch = cs + (grid ? 1 : 0)
  let canvasW = n * pitch + (grid ? 1 : 0) + (mirror ? 2 : 0)
  let hov = -1
  let down = null
  let up = false
  let erase = -1
  // a width of its own, the card's padding around the canvas, so it shares a line when there is room
  box(width: canvasW + 30, gap: 8) {
    card("") {
      box(width: canvasW, gap: grid ? 1 : 0, padding: grid ? 1 : 0, background: grid ? fade(muted, 0.35) : surface, radius: 4) {
        for r in range(n) {
          box(dir: "row", gap: grid ? 1 : 0) {
            for c in range(n) {
              let i = r * n + c
              // the mirror's axis: a thin accent line down the middle
              if mirror && c == n / 2 { box(width: 2, height: cs, background: accent) }
              let b = box(id: "px-{i}", width: cs, height: cs, background: px[i] == "" ? checker(i) : px[i], hover: {borderColor: ink})
              if b.hovered { hov = i }
              if b.pressed { down = {i, x: b.x, y: b.y} }
              if b.released { up = true }
              if b.rightClicked { erase = i }
            }
          }
        }
      }
      box(dir: "row", wrap: true, gap: 12) {
        text(hov < 0 ? "–" : "x {hov % n}, y {floor(hov / n)}  {px[hov] == "" ? "transparent" : px[hov]}", size: sm, face: mono, color: muted)
        text("{n} × {n} · {painted} pixels painted", size: sm, face: mono, color: muted)
      }
    }
  }

  // a press, and each move while it is held: the cell under the pointer, counted from the cell pressed
  if down != null {
    let col = down.i % n + floor(down.x / pitch)
    let row = floor(down.i / n) + floor(down.y / pitch)
    let at = col >= 0 && col < n && row >= 0 && row < n ? row * n + col : -1
    if at >= 0 && last == -1 {
      if tool == "Picker" {
        if px[at] != "" { set("Colour", px[at]) }
        set("tool", "Pencil"); set("last", -2)
      } else if tool == "Fill" {
        commit(flood(px, at, brush)); set("last", -2)
      } else {
        commit(paintAll(px, spots(at), tool == "Eraser" ? "" : brush)); set("last", at)
      }
    } else if at >= 0 && last >= 0 && at != last {
      set("px", paintAll(px, unique(lineCells(last, at).flatMap(spots)), tool == "Eraser" ? "" : brush)); set("last", at)
    }
  }
  if up { set("last", -1) }
  if erase >= 0 { commit(paintAll(px, spots(erase), "")) }

  // ── right: palette, preview, export ──
  box(width: 240, gap: 12) {
    card("Palette") {
      box(gap: 6) {
        for row4 in range(4) {
          box(dir: "row", gap: 6) {
            for k in range(4) {
              let c = data.palette[row4 * 4 + k]
              let on = c == brush
              let sw = box(id: "sw-{row4 * 4 + k}", width: 40, height: 40, radius: 7, background: c,
                border: on ? 3 : 1, borderColor: on ? ink : border, hover: {borderColor: ink},
                tip: "{c}{row4 * 4 + k < 10 ? "  ·  key {row4 * 4 + k}" : ""}")
              if sw.clicked { set("Colour", c) }
            }
          }
        }
      }
      colour("Colour", value: "#b13e53", swatches: data.palette)
    }
    card("Preview") {
      box(dir: "row", gap: 16, align: end) {
        for s in [1, 4] {
          box(gap: 4) {
            box(width: n * s, height: n * s, background: fade(muted, 0.12)) {
              layer { for i in range(n * n) where px[i] != "" { rect(i % n * s, floor(i / n) * s, s, s, fill: px[i]) } }
            }
            text("{s}×", size: sm, color: muted)
          }
        }
      }
    }
    card("Export") {
      box(dir: "row", gap: 6) {
        for s in [1, 8] {
          if button("SVG {s}×").clicked {
            let rects = range(n * n).filter(i => px[i] != "").map(i => "  <rect x='{i % n}' y='{floor(i / n)}' width='1' height='1' fill='{px[i]}'/>")
            download("pixel-art-{n}x{n}@{s}x.svg",
              "<svg xmlns='http://www.w3.org/2000/svg' width='{n * s}' height='{n * s}' viewBox='0 0 {n} {n}' shape-rendering='crispEdges'>\n" +
              rects.join("\n") + "\n</svg>\n")
          }
        }
      }
      if button("Copy the hexes").clicked {
        copy(range(n).map(r => px.slice(r * n, r * n + n).map(c => c == "" ? "......." : c).join(" ")).join("\n"))
      }
      text("One line a row, ....... for an empty pixel.", size: sm, color: muted)
    }
    markdown("**B** pencil · **E** eraser · **G** fill · **I** picker · **0–9** swatch · **M** mirror · **Ctrl Z** undo · **Ctrl Y** redo")
  }
}

// keys that are not a button's
for k in range(10) { if key(str(k)).pressed { set("Colour", data.palette[k]) } }
if key("m").pressed { set("Mirror", !mirror) }
if key("ctrl+shift+z").pressed { redoIt() }
```

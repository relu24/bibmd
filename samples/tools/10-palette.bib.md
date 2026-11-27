---
bib: 1
title: Palette builder and contrast checker
width: wide
data:
  presets: ["#2f6fd0", "#d9473a", "#2f8f5b", "#7b4fd6", "#e39a00", "#0f8a8a", "#c2367a", "#3a4452"]
  roles: [Primary, Secondary, Accent, Neutral, Surface]
  modes:
    - { name: Complementary, offsets: [0, 180, 200] }
    - { name: Analogous, offsets: [0, 30, -30] }
    - { name: Triadic, offsets: [0, 120, 240] }
    - { name: Split complementary, offsets: [0, 150, 210] }
  steps: [50, 100, 200, 300, 400, 500, 600, 700, 800, 900]
  lights: [97, 93, 85, 75, 63, 52, 43, 34, 25, 16]
---
###### Colour tool

# Palette builder and contrast checker

Pick a base colour, choose a harmony, lock what you like and regenerate the rest. Every
text and background pair is checked against WCAG 2.1: AA needs 4.5:1 for body text
(3:1 for large), AAA needs 7:1.

```bib
let clamp = (x, a, b) => min([b, max([a, x])])
let INK = "#111318"
// the artifact's rule: white text unless the near-black ink reads better
let inkOn = bg => contrast(bg, "#ffffff") >= contrast(bg, INK) ? "#ffffff" : INK

let seed = state("seed", 0)
let locks = state("locks", [])
let scaleRole = state("scaleRole", "Primary")
let lockOf = r => locks.find(x => x.role == r)?.hex

let h = 0
let s = 0
let l = 0
let pal = []
box(dir: "row", gap: 16) {
  // ---------- the base colour: a picker and three sliders that follow each other ----------
  box(width: 300) {
    card("Base colour") {
      let base = colour("Base colour", value: "#2f6fd0", swatches: data.presets)
      h = slider("Hue", 0, 360, value: 217, step: 1)
      s = slider("Saturation", 0, 100, value: 63, step: 1)
      l = slider("Lightness", 0, 100, value: 50, step: 1)
      if changed("Base colour") {
        let p = colourOf(base)
        set("Hue", round(p.h)); set("Saturation", round(p.s)); set("Lightness", round(p.l))
      }
      if changed("Hue") || changed("Saturation") || changed("Lightness") { set("Base colour", hsl(h, s, l)) }
    }
  }

  // ---------- the palette: a harmony of the base, nudged by the seed, locks kept ----------
  box(width: fill) {
    card("Palette") {
      let modeName = ""
      box(dir: "row", gap: 12, align: center) {
        box(width: 490) { modeName = choice("Harmony", data.modes.map(m => m.name), style: segmented) }
        if button("Regenerate", key: "space").clicked { set("seed", seed + 1) }
      }
      let off = data.modes.find(m => m.name == modeName).offsets
      let raw = [
        [h, s, l],
        [h + off[1], s * 0.85, clamp(l, 35, 60)],
        [h + off[2], clamp(s + 15, 0, 95), clamp(l + 6, 40, 65)],
        [h, s * 0.12, 42],
        [h, s * 0.25, 97],
      ]
      // the base is never nudged; the others move by up to ±15° hue, ±12 saturation, ±8 lightness
      let jit = (i, k, span) => i == 0 || seed == 0 ? 0 : round((random(seed, i * 3 + k) - 0.5) * span)
      pal = data.roles.map((r, i) => lockOf(r) ?? hsl(raw[i][0] + jit(i, 0, 30), clamp(raw[i][1] + jit(i, 1, 24), 0, 100), clamp(raw[i][2] + jit(i, 2, 16), 0, 100)))

      box(dir: "row", gap: 10) {
        for r in data.roles {
          let c = pal[index]
          let on = inkOn(c)
          let sw = box(id: "sw-{r}", width: fill, height: 150, radius: 14, background: c, padding: [8, 12, 10, 12],
            border: r == scaleRole ? 3 : 1, borderColor: r == scaleRole ? ink : border, hover: {borderColor: ink}, tip: "Copy {c}") {
            box(dir: "row", justify: between, height: 74) {
              box(width: 10)
              let lk = box(id: "lock-{r}", width: 30, height: 30, radius: 8, padding: [5, 6],
                background: lockOf(r) ? fade(INK, 0.45) : fade("#ffffff", 0.25), tip: lockOf(r) ? "Unlock" : "Lock") {
                text(lockOf(r) ? "🔒" : "🔓", size: sm)
              }
              if lk.clicked { set("locks", lockOf(r) ? locks.filter(x => x.role != r) : [...locks, {role: r, hex: c}]) }
            }
            text(r, size: sm, weight: bold, color: on)
            text(c, face: mono, weight: bold, color: on)
          }
          if sw.clicked { copy(c); set("scaleRole", r) }
        }
      }
      text("Click a swatch to copy its hex and see its shades. Space regenerates the unlocked ones. Regenerated {seed} time{seed == 1 ? "" : "s"}; {locks.length} locked.", size: sm, color: muted)
    }
  }
}
let colourFor = r => pal[data.roles.indexOf(r)]
```

## Shades and tints

```bib
let sp = colourOf(colourFor(scaleRole))
let scale = data.steps.map((st, i) => ({step: st, hex: hsl(sp.h, sp.s * (i < 2 ? 0.9 : 1), data.lights[i])}))

text("of {scaleRole}", weight: bold, color: muted)
box(dir: "row", gap: 0, radius: 12) {
  for c in scale {
    let st = box(id: "step-{c.step}", width: fill, height: 64, background: c.hex, padding: 6, gap: 14, tip: "Copy {c.hex}") {
      text(str(c.step), size: 11, face: mono, weight: bold, color: inkOn(c.hex))
      text(c.hex, size: 11, face: mono, color: inkOn(c.hex))
    }
    if st.clicked { copy(c.hex) }
  }
}
```

## Contrast matrix

Rows are the text colour, columns the background. Each cell is written in its pair: "Aa", the
ratio and the grade, so a failing pair is as hard to read here as it would be on a page.

```bib
let cols = [{n: "White", c: "#ffffff"}, ...data.roles.map((r, i) => ({n: r, c: pal[i]})), {n: "Ink", c: INK}]
let gradeOf = r => r >= 7 ? "AAA" : r >= 4.5 ? "AA" : r >= 3 ? "AA large" : "Fail"
let ratios = cols.flatMap(a => cols.filter(b => b.c != a.c).map(b => contrast(a.c, b.c)))
let passing = t => ratios.filter(r => r >= t).length

grid(columns: 5) {
  stat("Pass AAA", passing(7), tone: success)
  stat("Pass AA", passing(4.5), tone: accent)
  stat("Pass AA large", passing(3), tone: warning)
  stat("Fail", ratios.length - passing(3), tone: danger)
  stat("Pairs", ratios.length)
}

table(["Text \\ background", ...cols.map(b => b.n)],
  cols.map(a => [a.n, ...cols.map(b => a.c == b.c
    ? {text: "", background: fade(muted, 0.12)}
    : {text: "Aa  {contrast(a.c, b.c).toFixed(2)}  {gradeOf(contrast(a.c, b.c))}", background: b.c, color: a.c, value: contrast(a.c, b.c)})]),
  widths: [1.2, 1, 1, 1, 1, 1, 1, 1])
```

## Preview

```bib
box(width: fill, padding: 22, radius: 14, background: colourFor("Surface")) {
  box(width: 440, padding: 18, gap: 8, radius: 12, background: "#ffffff", border: 1, borderColor: "#e0e2e5") {
    box(dir: "row") {
      box(padding: [2, 8], radius: 9999, background: colourFor("Accent")) { text("New", size: 11, weight: bold, color: inkOn(colourFor("Accent"))) }
    }
    text("Quarterly report is ready", size: lg, weight: bold, color: scale[8].hex)
    text("Your team at Fernhill Outdoor closed 42 orders this week.", color: colourFor("Neutral"))
    text("See the details", weight: bold, color: scale[6].hex)
    box(dir: "row", gap: 8) {
      for b in [{t: "Open report", r: "Primary"}, {t: "Later", r: "Secondary"}] {
        box(id: "pv-{b.r}", padding: [8, 16], radius: 9, background: colourFor(b.r), hover: {borderColor: ink}) { text(b.t, weight: bold, color: inkOn(colourFor(b.r))) }
      }
    }
  }
}
```

## Export

```bib
let css = ":root \{\n" + data.roles.map((r, i) => "  --{r.toLowerCase()}: {pal[i]};").join("\n") + "\n" +
  scale.map(c => "  --{scaleRole.toLowerCase()}-{c.step}: {c.hex};").join("\n") + "\n}\n"
box(dir: "row", gap: 8) {
  if button("Copy CSS", style: primary).clicked { copy(css) }
  if button("Download palette.css").clicked { download("palette.css", css) }
}
markdown("```css\n" + css + "```")
```

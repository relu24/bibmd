---
bib: 1
title: Every wave is a sum of circles
width: wide
nav: true
data:
  pts: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 93, 94, 95, 96, 97, 98, 99, 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120]
  idx: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 93, 94, 95, 96, 97, 98, 99, 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120, 121, 122, 123, 124, 125, 126, 127, 128, 129, 130, 131, 132, 133, 134, 135, 136, 137, 138, 139, 140, 141, 142, 143, 144, 145, 146, 147, 148, 149, 150, 151, 152, 153, 154, 155, 156, 157, 158, 159, 160, 161, 162, 163, 164, 165, 166, 167, 168, 169, 170, 171, 172, 173, 174, 175, 176, 177, 178, 179, 180, 181, 182, 183, 184, 185, 186, 187, 188, 189, 190, 191, 192, 193, 194, 195, 196, 197, 198, 199]
  gibbsN: [1, 2, 3, 4, 5, 6, 8, 10, 13, 16, 20, 25, 32, 40, 50, 63, 80, 100, 125, 160, 200]
  # the first peak's overshoot, % of the jump, for each gibbsN: the same sum as `sq` in section 03,
  # computed ahead because 1,000+ terms a frame would eat the step budget
  gibbsOver: [13.662, 10.021, 9.418, 9.211, 9.116, 9.065, 9.014, 8.991, 8.974, 8.965, 8.959, 8.956, 8.953, 8.952, 8.951, 8.95, 8.95, 8.949, 8.949, 8.949, 8.949]
---
###### Explainer 01 · Signals

# Every wave is a sum of circles

A circle spinning at a steady rate traces a sine wave. Hang a smaller, faster circle off
its rim, and another off that one, and the tip can draw almost anything: a square wave with
sharp corners, a sawtooth, a triangle. That is Fourier's idea, and it runs your phone's
audio, your JPEGs and your radio.

```bib
// Shared pieces: each wave's sine coefficients (period 2π, height ±1).
let TAU = 2 * Math.PI
let coef = (wv, k) => wv == "square" ? (k % 2 == 1 ? 4 / (Math.PI * k) : 0)
  : wv == "sawtooth" ? (k % 2 == 1 ? 2 : -2) / (Math.PI * k)
  : (k % 2 == 1 ? (((k - 1) / 2) % 2 == 1 ? -1 : 1) * 8 / (Math.PI * Math.PI * k * k) : 0)
let termsOf = (wv, n) => data.idx.slice(0, n).map(j => {
  let k = wv == "sawtooth" ? j + 1 : 2 * j + 1
  return {k, b: coef(wv, k)}
})
let sumAt = (ts, x) => ts.reduce((s, t) => s + t.b * Math.sin(t.k * x), 0)
let target = (wv, x) => {
  let u = ((x + Math.PI) % TAU + TAU) % TAU - Math.PI
  let sg = u > 0 ? 1 : u < 0 ? -1 : 0
  return wv == "square" ? sg : wv == "sawtooth" ? u / Math.PI
    : abs(u) <= Math.PI / 2 ? 2 * u / Math.PI : sg * (2 - 2 * abs(u) / Math.PI)
}

// Epicycles: circles chained rim to centre, the tip's height traced to the right.
fn epicycles(wv, ts, t, height: 260) {
  box(width: fill, height: height, radius: 12, border: 1, background: "raised") {
    layer {
      // w and h are only known at draw time, so the circles use the box's fixed height
      let R = (height / 2 - 18) / 1.3
      let cx = height * 0.55
      let cy = height / 2
      let left = cx + R * 1.35
      let chain = ts.reduce((acc, tm) => {
        let p = acc[acc.length - 1]
        let ph = tm.b < 0 ? Math.PI : 0
        let r = abs(tm.b) * R
        return [...acc, {x: p.x + r * Math.cos(tm.k * t + ph), y: p.y - r * Math.sin(tm.k * t + ph), r}]
      }, [{x: cx, y: cy, r: 0}])
      line(0, cy, w, cy, color: border)
      for c in chain where index > 0 {
        let p = chain[index - 1]
        circle(p.x, p.y, c.r, color: fade(muted, 0.5), width: 1)
        line(p.x, p.y, c.x, c.y, color: ink, width: 1.4)
      }
      let tip = chain[chain.length - 1]
      let span = w - left - 10
      let ys = data.pts.slice(0, 91).map(i => cy - R * sumAt(ts, t - i / 90 * TAU * 1.5))
      for y in ys where index < 90 {
        line(left + index / 90 * span, y, left + (index + 1) / 90 * span, ys[index + 1], color: danger, width: 2.4)
      }
      line(tip.x, tip.y, left, tip.y, color: fade(danger, 0.5), width: 1)
      circle(tip.x, tip.y, 4.5, fill: danger)
      circle(left, tip.y, 3.5, fill: danger)
    }
  }
}

epicycles("square", termsOf("square", 5), 0.9)
text("Five circles, each turning a whole number of times for every turn of the first. The red curve is the height of the tip over time.", size: sm, color: muted)
```

## 01 · Stacking sines

Start with one sine wave, `sin x`. It is a poor square wave: round where the square is flat,
smooth where it jumps. Now add `sin 3x / 3`, a wave three times as fast and a third as tall.
The shoulders lift and the middle flattens. Keep adding odd harmonics, each `1/k` as tall,
and the sum hugs the square tighter and tighter.

```bib
let wave = ""
let n = 0
let parts = false
row {
  card("Wave") { wave = choice("Shape", ["square", "sawtooth", "triangle"]) }
  card("Terms") {
    n = slider("Terms", 1, 20, value: 3, step: 1)
    parts = toggle("Show each term", value: true)
  }
}

let ts = termsOf(wave, n)
// one period, x from −π to π (the artifact shows two; one keeps the step budget for more terms)
let X = x => (x + Math.PI) / TAU
let xs = data.pts.slice(0, 101).map(i => -Math.PI + i / 100 * TAU)
let sumY = xs.map(x => sumAt(ts, x))
let tgtY = xs.map(x => target(wave, x))

box(width: fill, height: 300, radius: 12, border: 1, background: "raised") {
  layer {
    let my = h / 2
    let sy = (h / 2 - 16) / 1.3
    line(0, my, w, my, color: border)
    if parts {
      for t in ts.slice(0, 5) {
        let py = xs.map(x => t.b * Math.sin(t.k * x))
        for y in py where index < 100 {
          line(w * X(xs[index]), my - sy * y, w * X(xs[index + 1]), my - sy * py[index + 1], color: fade(muted, 0.6), width: 1)
        }
      }
    }
    for y in tgtY where index < 100 && index % 2 == 0 {
      line(w * X(xs[index]), my - sy * y, w * X(xs[index + 1]), my - sy * tgtY[index + 1], color: fade(ink, 0.6), width: 1.5)
    }
    for y in sumY where index < 100 {
      line(w * X(xs[index]), my - sy * y, w * X(xs[index + 1]), my - sy * sumY[index + 1], color: accent, width: 2.6)
    }
  }
}

let pre = wave == "square" ? "4/π" : wave == "sawtooth" ? "2/π" : "8/π²"
let termText = t => {
  let den = t.k == 1 ? "" : wave == "triangle" ? " / {t.k * t.k}" : " / {t.k}"
  return "sin {t.k == 1 ? "" : t.k}x{den}"
}
let shown = ts.slice(0, 6).map((t, i) => (i == 0 ? (t.b < 0 ? "−" : "") : (t.b < 0 ? " − " : " + ")) + termText(t)).join("")
let more = ts.length > 6 ? " + … ({ts.length - 6} more)" : ""
// the average (root-mean-square) error, over the plotted samples of one period
let err = Math.sqrt(sumY.slice(0, 100).reduce((s, y, i) => s + (y - tgtY[i]) * (y - tgtY[i]), 0) / 100)

text("{pre} · ( {shown}{more} )", face: mono, color: muted)
row(fit: true) {
  badge("dashed: target   ·   blue: the sum   ·   grey: each term", tone: muted)
  badge("highest harmonic {ts[ts.length - 1].k}", tone: accent)
  badge("average error {err.toFixed(3)}", tone: accent)
}
```

The three shapes need different recipes. The square uses only odd harmonics at `1/k`. The
sawtooth uses every harmonic, with alternating signs. The triangle uses odd harmonics at
`1/k²`, so they die away fast: a triangle has no jumps, only corners, and smooth things need
little high-frequency help.

## 02 · Circles on circles

Every term `b·sin(kx)` is the height of a point on a circle of radius `b` turning `k` times
while the first circle turns once. Put each circle's centre on the previous circle's rim and
the tip's height is the sum. Ancient astronomers used the same trick, "epicycles", to fit
planetary orbits.

```bib
let deg = 0
let circles = 0
row {
  card("Time") { deg = slider("Angle of the first circle (°)", 0, 360, value: 50, step: 5) }
  card("Circles") { circles = slider("Circles", 1, 12, value: 4, step: 1) }
}
epicycles(wave, termsOf(wave, circles), deg / 360 * TAU, height: 300)
text("The wave is the shape picked in section 01. Slide the angle to turn the circles.", size: sm, color: muted)
```

## 03 · The overshoot that never goes away

Look closely at a jump. Next to it the sum overshoots, rings, and settles. Add more terms and
the ringing squeezes closer to the jump, but its first peak does not shrink. With a hundred
terms or a million, it rises about **9% of the jump** above the top. This is the **Gibbs
phenomenon**, first worked out by Henry Wilbraham in 1848 and later by J. Willard Gibbs.

```bib
let sq = (m, x) => 4 / Math.PI * data.idx.slice(0, m).reduce((s, j) => s + Math.sin((2 * j + 1) * x) / (2 * j + 1), 0)
let ng = slider("Terms (square wave)", 1, 30, value: 10, step: 1)
let px = Math.PI / (2 * ng)
let pk = sq(ng, px)
let over = (pk - 1) / 2 * 100
let gx = data.pts.slice(0, 81).map(i => -0.9 + i / 80 * 1.8)
let gy = gx.map(x => sq(ng, x))
let curve = data.gibbsOver

row {
  box(width: fill, gap: 6) {
    box(width: fill, height: 280, radius: 12, border: 1, background: "raised") {
      layer {
        let Y = v => (1.3 - v) / 2.6 * h
        let X = x => (x + 0.9) / 1.8 * w
        rect(0, Y(1.179), w, Y(1) - Y(1.179), fill: fade(danger, 0.12))
        line(0, Y(1), w, Y(1), color: border)
        line(0, Y(-1), w, Y(-1), color: border)
        line(0, Y(0), w, Y(0), color: border)
        line(0, Y(-1), X(0), Y(-1), color: fade(ink, 0.6), width: 1.5)
        line(X(0), Y(-1), X(0), Y(1), color: fade(ink, 0.6), width: 1.5)
        line(X(0), Y(1), w, Y(1), color: fade(ink, 0.6), width: 1.5)
        for y in gy where index < 80 {
          line(X(gx[index]), Y(y), X(gx[index + 1]), Y(gy[index + 1]), color: accent, width: 2.4)
        }
        circle(X(px), Y(pk), 5, fill: danger)
        label(6, Y(1) + 12, "+1", size: sm, color: muted)
        label(6, Y(-1) - 10, "−1", size: sm, color: muted)
        label(X(0) + 8, Y(0) + 14, "jump at x = 0", size: sm, color: muted)
      }
    }
  }
  box(width: fill, gap: 6) {
    box(width: fill, height: 280, radius: 12, border: 1, background: "raised") {
      layer {
        let lx = v => 34 + Math.log10(v) / Math.log10(200) * (w - 44)
        let ly = v => h - 28 - (v - 7) / 7 * (h - 44)
        for g in [8, 10, 12, 14] {
          line(34, ly(g), w, ly(g), color: border)
          label(4, ly(g), "{g}%", size: sm, color: muted)
        }
        for g in [1, 10, 100] { label(lx(g), h - 12, str(g), size: sm, color: muted, align: center) }
        line(34, ly(8.949), w, ly(8.949), color: fade(danger, 0.7), width: 1.5)
        label(w - 8, ly(8.949) - 10, "8.95%", size: sm, color: danger, align: end)
        for v in curve where index < curve.length - 1 {
          line(lx(data.gibbsN[index]), ly(v), lx(data.gibbsN[index + 1]), ly(curve[index + 1]), color: accent, width: 2)
        }
        circle(lx(ng), ly(over), 5, fill: danger)
      }
    }
    text("Overshoot (% of jump) against number of terms, log scale. It levels off at 8.95%, never zero.", size: sm, color: muted)
  }
}

row(fit: true) {
  badge("first peak {pk.toFixed(4)}", tone: danger)
  badge("overshoot {over.toFixed(2)}% of the jump", tone: danger)
  badge("peak sits at x = π/{2 * ng} = {px.toFixed(4)}", tone: accent)
}
```

> *Why?* Each finite sum is smooth, and a smooth curve cannot jump. It must climb from −1 to
> +1 in a sliver of width about π/N, and squeezing a fixed shape into a thinner sliver keeps
> its height. The error shrinks in area, not in peak: the sum converges "on average", not at
> every point at once.

## 04 · Which harmonics are in it?

The list of amplitudes is the wave's **spectrum**: a recipe instead of a picture.
Equalisers, MP3 and noise filters edit sound by editing this list. Switch a harmonic off
and on and watch what the wave loses.

```bib
let K = 20
let ws = choice("Wave to analyse", ["square", "sawtooth", "triangle"])
let off = state("off", [])
let present = data.idx.slice(0, K).map(j => j + 1).filter(k => coef(ws, k) != 0)
let active = present.filter(k => !off.includes(k))
let amp1 = abs(coef(ws, 1))

box(width: fill, height: 220, radius: 12, border: 1, background: "raised") {
  layer {
    let base = h - 26
    let bw = w / K
    label(8, 12, "amplitude |b_k| of harmonic k   (blue +, red −)", size: sm, color: muted)
    for j in data.idx.slice(0, K) {
      let k = j + 1
      let b = coef(ws, k)
      let bh = abs(b) / amp1 * (base - 28)
      if b != 0 {
        rect(j * bw + bw * 0.18, base - bh, bw * 0.64, bh,
             fill: off.includes(k) ? fade(muted, 0.18) : (b < 0 ? danger : accent))
      }
      label(j * bw + bw * 0.5, h - 13, str(k), size: sm, color: b != 0 ? ink : faint, align: center)
    }
    line(0, base, w, base, color: border)
  }
}

text("Switch a harmonic off and on:", size: sm, color: muted)
box(dir: "row", width: fill) {
  for k in present {
    box(width: fill, align: center) {
      if button(str(k), style: off.includes(k) ? ghost : primary).clicked {
        set("off", off.includes(k) ? off.filter(o => o != k) : [...off, k])
      }
    }
  }
}
row(fit: true) {
  if button("All on", style: ghost).clicked { set("off", []) }
  if button("Only the first 3", style: ghost).clicked { set("off", present.slice(3)) }
  if button("Remove the fundamental", style: ghost).clicked { set("off", [1]) }
}

let sts = active.map(k => ({k, b: coef(ws, k)}))
let sx = data.pts.slice(0, 81).map(i => -Math.PI + i / 80 * TAU)
let sY = sx.map(x => sumAt(sts, x))
let tY = sx.map(x => target(ws, x))
box(width: fill, height: 180, radius: 12, border: 1, background: "raised") {
  layer {
    let my = h / 2
    let sy = (h / 2 - 10) / 1.3
    line(0, my, w, my, color: border)
    for y in tY where index < 80 && index % 2 == 0 {
      line(w * index / 80, my - sy * y, w * (index + 1) / 80, my - sy * tY[index + 1], color: fade(ink, 0.6), width: 1.3)
    }
    for y in sY where index < 80 {
      line(w * index / 80, my - sy * y, w * (index + 1) / 80, my - sy * sY[index + 1], color: accent, width: 2.4)
    }
  }
}

let tot = present.reduce((s, k) => s + coef(ws, k) * coef(ws, k), 0)
let kept = active.reduce((s, k) => s + coef(ws, k) * coef(ws, k), 0)
row(fit: true) {
  badge("harmonics on {active.length} of {present.length}", tone: accent)
  badge("energy kept {(kept / tot * 100).toFixed(1)}%", tone: accent)
}
```

Removing the fundamental leaves a wave with the same jumps but a wobbling top: the edges live
in the high harmonics, the overall shape in the low ones. Keep only the first three and you
have a low-pass filter: the edges blur but the beat remains.

## What you just saw

1. A periodic wave can be written as a sum of sines whose frequencies are whole multiples of
   one base frequency.
2. Each sine is a spinning circle; chaining circles adds their heights, so the tip of the
   chain draws the wave.
3. The size of each term tells you how rough the wave is: jumps give amplitudes falling like
   1/k, corners like 1/k².
4. At a jump, any finite sum overshoots by about 9% of the jump, however many terms you take
   (Gibbs).
5. The list of amplitudes, the spectrum, is a complete description of the wave, and editing
   it is filtering.

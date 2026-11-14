---
bib: 1
title: "How a machine learns: rolling downhill"
width: wide
nav: true
data:
  xs: [0.3, 0.8, 1.2, 1.7, 2.1, 2.6, 3.0, 3.4, 3.9, 4.3, 4.8, 5.2]
  ys: [1.59, 1.24, 2.16, 2.21, 3.18, 2.78, 3.5, 3.27, 4.42, 4.49, 4.59, 5.56]
  idx: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 93, 94, 95, 96, 97, 98, 99, 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120, 121, 122, 123, 124, 125, 126, 127, 128, 129, 130, 131, 132, 133, 134, 135, 136, 137, 138, 139, 140, 141, 142, 143, 144, 145, 146, 147, 148, 149, 150, 151, 152, 153, 154, 155, 156, 157, 158, 159, 160, 161, 162, 163, 164, 165, 166, 167, 168, 169, 170, 171, 172, 173, 174, 175, 176, 177, 178, 179, 180, 181, 182, 183, 184, 185, 186, 187, 188, 189, 190, 191, 192, 193, 194, 195, 196, 197, 198, 199, 200]
---
###### Machine learning · an interactive explainer

# How a machine learns: rolling downhill

Strip away the jargon and most of machine learning is one move, repeated: measure how wrong
you are, work out which way is downhill, take a small step. Here is that move, on a problem
small enough to see whole.

> Twelve points, one straight line. You will fit it by hand, then watch a machine fit it by
> rolling down a landscape of its own mistakes, and then see the two ways that rolling goes wrong.

###### 1 · A guess and its error

## Fit a line by hand

The model is a line, `y = w·x + b`: a slope *w* and an intercept *b*. Move them. The thin
vertical strokes are the **errors**, how far each point is from the line. The machine's whole
notion of "wrong" is the average of their squares, the **mean squared error**:

`loss(w, b) = (1/12) · Σ (w·xᵢ + b − yᵢ)²`

```bib
// The loss is a quadratic in w and b, so a few averages of the data give it, and its gradient, exactly.
let N = data.xs.length
let mx = sum(data.xs) / N
let mxx = sum(data.xs.map(x => x * x)) / N
let my = sum(data.ys) / N
let mxy = sum(data.xs.map((x, i) => x * data.ys[i])) / N
let myy = sum(data.ys.map(y => y * y)) / N
let lossOf = (w, b) => w * w * mxx + 2 * w * b * mx + b * b - 2 * w * mxy - 2 * b * my + myy
let gradW = (w, b) => 2 * (w * mxx + b * mx - mxy)
let gradB = (w, b) => 2 * (w * mx + b - my)
let LMIN = 0.10015912
let W0 = -1.5
let W1 = 2.5
let B0 = -1.5
let B1 = 5

// The data and a line through it; resid draws the errors.
fn fitPlot(ww, bb, resid, height) {
  box(width: fill, height: height, radius: 12, border: 1, background: "raised") {
    layer {
      let T = 12
      let B = height - 26
      let px = x => (w - 50) * x / 5.6 + 36
      let py = y => B - (B - T) * (y + 0.5) / 7
      for yv in [0, 2, 4, 6] {
        line(36, py(yv), w - 14, py(yv), color: border, width: 1)
        label(30, py(yv), str(yv), size: 11, color: muted, align: end)
      }
      for xv in [0, 1, 2, 3, 4, 5] { label(px(xv), B + 14, str(xv), size: 11, color: muted, align: center) }
      if resid {
        for x in data.xs {
          line(px(x), py(data.ys[index]), px(x), py(min([max([ww * x + bb, -0.5]), 6.5])), color: fade(warning, 0.7), width: 1.5)
        }
      }
      // the line, clipped by hand to the plot's y range
      let ya = min([max([bb, -0.5]), 6.5])
      let yb = min([max([ww * 5.6 + bb, -0.5]), 6.5])
      let xa = ww == 0 ? 0 : min([max([(ya - bb) / ww, 0]), 5.6])
      let xb = ww == 0 ? 5.6 : min([max([(yb - bb) / ww, 0]), 5.6])
      line(px(xa), py(ya), px(xb), py(yb), color: accent, width: 2.5)
      for x in data.xs { circle(px(x), py(data.ys[index]), 4, fill: ink) }
    }
  }
}

let gw = 0
let gb = 0
row {
  gw = slider("Slope w", W0, W1, value: -1, step: 0.05)
  gb = slider("Intercept b", B0, B1, value: 4, step: 0.05)
}
fitPlot(gw, gb, true, 300)
grid(columns: 3) {
  stat("Your loss", lossOf(gw, gb).toFixed(3), tone: lossOf(gw, gb) < 0.11 ? success : warning)
  stat("Best possible", "0.100")
  stat("Best line", "y = 0.80x + 1.02")
}
```

###### 2 · The landscape

## Every guess is a place on a map

Each pair (*w*, *b*) has a loss. Draw all of them and you get a terrain: across is slope, up is
intercept, colour is depth. Your guess (the sliders above) is the dot. The best line, the cross,
sits at the bottom of the long, narrow valley. The arrow is the **negative gradient**: the
direction in which the loss falls fastest, worked out from the data by calculus:

`∂loss/∂w = (2/12)·Σ (w·xᵢ + b − yᵢ)·xᵢ      ∂loss/∂b = (2/12)·Σ (w·xᵢ + b − yᵢ)`

```bib
// The terrain: a 28 × 16 grid of cells, banded by loss. Each band is e^0.5 ≈ 1.65× higher than the one inside it.
let COLS = 28
let ROWS = 16
let cells = data.idx.slice(0, ROWS).flatMap(j => data.idx.slice(0, COLS).map(i => {
  let L = lossOf(W0 + (W1 - W0) * (i + 0.5) / COLS, B1 - (B1 - B0) * (j + 0.5) / ROWS)
  return {i, j, k: floor(Math.log(max([L, LMIN]) / LMIN) * 2)}
}))

// Drawn inside the caller's layer, so its dot or trail can share it; ww is the layer's w.
fn terrain(ww, height) {
    for c in cells {
      rect(ww * c.i / COLS, height * c.j / ROWS, ww / COLS, height / ROWS,
           fill: fade(success, max([0.04, 0.78 - c.k * 0.055]) + (c.k % 2 == 1 ? 0.04 : 0)))
    }
    let ox = (ww * (0.8037 - W0)) / (W1 - W0)
    let oy = height * (B1 - 1.0189) / (B1 - B0)
    line(ox - 6, oy - 6, ox + 6, oy + 6, color: ink, width: 1.5)
    line(ox - 6, oy + 6, ox + 6, oy - 6, color: ink, width: 1.5)
    for wv in [-1, 0, 1, 2] { label((ww * (wv - W0)) / (W1 - W0), height - 12, "w={wv}", size: 11, color: ink, align: center) }
    for bv in [0, 2, 4] { label(6, height * (B1 - bv) / (B1 - B0), "b={bv}", size: 11, color: ink) }
}

let H2 = 380
let g1 = gradW(gw, gb)
let g2 = gradB(gw, gb)
box(width: fill, height: H2, radius: 12, background: "raised") {
  layer {
    terrain(w, H2)
    // the arrow's direction on screen, for a map about 820 px wide
    let sx = -g1 * 820 / (W1 - W0)
    let sy = g2 * H2 / (B1 - B0)
    let len = max([Math.hypot(sx, sy), 0.000001])
    let ux = sx / len
    let uy = sy / len
    let x = (w * (gw - W0)) / (W1 - W0)
    let y = H2 * (B1 - gb) / (B1 - B0)
    line(x, y, x + 60 * ux, y + 60 * uy, color: warning, width: 2.5)
    line(x + 60 * ux, y + 60 * uy, x + 60 * ux - 10 * (ux * 0.92 - uy * 0.39), y + 60 * uy - 10 * (uy * 0.92 + ux * 0.39), color: warning, width: 2.5)
    line(x + 60 * ux, y + 60 * uy, x + 60 * ux - 10 * (ux * 0.92 + uy * 0.39), y + 60 * uy - 10 * (uy * 0.92 - ux * 0.39), color: warning, width: 2.5)
    circle(x, y, 8, fill: raised)
    circle(x, y, 5.5, fill: warning)
  }
}
text("Stronger green is lower. The valley is narrow because the slope matters far more than the intercept: x runs up to 5, so a change in w moves the line much more than the same change in b.", size: sm, color: muted)

grid(columns: 3) {
  stat("∂loss/∂w", g1.toFixed(2))
  stat("∂loss/∂b", g2.toFixed(2))
  stat("Steepness", Math.hypot(g1, g2).toFixed(2))
}
```

###### 3 · Rolling downhill

## Gradient descent

From your guess, step against the gradient, scaled by a **learning rate** η, and repeat:

`w ← w − η·∂loss/∂w      b ← b − η·∂loss/∂b`

Try η = 0.005 (it crawls), 0.05 (steady), 0.088 (it zig-zags across the valley but gets there)
and 0.095 (each zig is bigger than the last: it flies off the map). For this data the edge is
η = 2/λ = 0.0923, where λ = 21.66 is the curvature across the valley; past it, descent diverges.

```bib
let lr = slider("Learning rate η", 0.001, 0.1, value: 0.05, step: 0.001)
let n = state("steps", 30)
row(fit: true) {
  if button("Step", style: primary).clicked { set("steps", min([n + 1, 200])) }
  if button("Run 10 steps").clicked { set("steps", min([n + 10, 200])) }
  if button("Run 50 steps").clicked { set("steps", min([n + 50, 200])) }
  if button("Reset", style: ghost).clicked { set("steps", 0) }
}

// The whole trail from the guess, recomputed from the step count. Stepping n times would copy the
// trail n times (too slow at 200 steps), so use what the steps add up to: on a quadratic loss each
// step shrinks the distance to the best fit along the loss's two principal directions by a fixed
// factor (1 − η·λ), so after k steps it has shrunk by (1 − η·λ)^k. Same numbers, no loop.
let det = mxx - mx * mx
let wStar = (mxy - mx * my) / det
let bStar = my - wStar * mx
let ha = 2 * mxx
let hc = 2 * mx
let hd = 2
let disc = Math.sqrt((ha - hd) * (ha - hd) / 4 + hc * hc)
let lam1 = (ha + hd) / 2 + disc
let lam2 = (ha + hd) / 2 - disc
let vlen = Math.hypot(hc, lam1 - ha)
let v1 = {w: hc / vlen, b: (lam1 - ha) / vlen}
let c1 = (gw - wStar) * v1.w + (gb - bStar) * v1.b
let c2 = -(gw - wStar) * v1.b + (gb - bStar) * v1.w
let at = k => {
  let f1 = c1 * Math.pow(1 - lr * lam1, k)
  let f2 = c2 * Math.pow(1 - lr * lam2, k)
  return {w: wStar + f1 * v1.w - f2 * v1.b, b: bStar + f1 * v1.b + f2 * v1.w}
}
let trail = data.idx.slice(0, n + 1).map(at)
let losses = trail.map(p => lossOf(p.w, p.b))
let last = trail[trail.length - 1]
let Lnow = losses[losses.length - 1]

let H3 = 330
row {
  column {
    box(width: fill, height: H3, radius: 12, background: "raised") {
      layer {
        terrain(w, H3)
        // a diverging trail is clamped to just off the map, so it is drawn leaving it
        let X = v => (w * ((v < W0 - 1 ? W0 - 1 : v > W1 + 1 ? W1 + 1 : v) - W0)) / (W1 - W0)
        let Y = v => H3 * (B1 - (v < B0 - 1 ? B0 - 1 : v > B1 + 1 ? B1 + 1 : v)) / (B1 - B0)
        for p in trail where index > 0 {
          let q = trail[index - 1]
          line(X(q.w), Y(q.b), X(p.w), Y(p.b), color: warning, width: 2)
          circle(X(p.w), Y(p.b), 2.2, fill: warning)
        }
        circle(X(last.w), Y(last.b), 8, fill: raised)
        circle(X(last.w), Y(last.b), 5.5, fill: warning)
      }
    }
    text("The trail from your guess (section 1) after each step.", size: sm, color: muted)
  }
  column {
    let span = max([20, n])
    box(width: fill, height: 160, radius: 12, border: 1, background: "raised") {
      layer {
        let lx = i => (w - 50) * i / span + 40
        let ly = v => 138 - 128 * (Math.log10(max([v, 0.08])) + 1.1) / 3.1
        for v in [0.1, 1, 10, 100] {
          line(40, ly(v), w - 10, ly(v), color: border, width: 1)
          label(34, ly(v), str(v), size: 11, color: muted, align: end)
        }
        line(40, ly(LMIN), w - 10, ly(LMIN), color: fade(success, 0.8), width: 1)
        label(40, 150, "0", size: 11, color: muted, align: center)
        label(w - 10, 150, "{span} steps", size: 11, color: muted, align: end)
        for v in losses where index > 0 {
          line(lx(index - 1), ly(min([losses[index - 1], 1000])), lx(index), ly(min([v, 1000])), color: warning, width: 2)
        }
      }
    }
    text("Loss after each step (log scale; green: the best possible, 0.100).", size: sm, color: muted)
    fitPlot(last.w, last.b, false, 150)
    text("The current line on the data.", size: sm, color: muted)
  }
}

grid(columns: 3) {
  stat("Steps", n)
  stat("Loss", Lnow > 1000000 ? "{round(Lnow / Math.pow(10, floor(Math.log10(Lnow))), 1)}e{floor(Math.log10(Lnow))}" : Lnow.toFixed(Lnow < 10 ? 3 : 1))
  stat("w, b", abs(last.w) > 10000 ? "off the map" : "{last.w.toFixed(2)}, {last.b.toFixed(2)}")
}

if n == 0 { badge("Press Step: one step against the gradient.", tone: warning) }
else if lr > 0.0923 { badge("Diverging: each step overshoots the valley by more than the last.", tone: danger) }
else if Lnow < 0.101 { badge("Converged: within 1% of the best fit after {n} steps.", tone: success) }
else if lr < 0.01 { badge("Crawling: the steps are tiny, so progress is slow.", tone: warning) }
else if lr > 0.08 { badge("Zig-zagging across the steep walls of the valley, but heading down.", tone: warning) }
else { badge("Rolling down the valley.", tone: success) }
```

###### 4 · Bumps

## Stuck in a dip, and how momentum helps

The line-fitting loss is a perfect bowl, with one bottom. Real models have landscapes full of
bumps. Here is a one-dimensional one, `f(x) = 0.08x² + 0.55·sin(2.2x)`. Plain descent stops in
the first dip it reaches: a **local minimum**. **Momentum** lets the ball keep a share β of its
previous velocity, so it can roll through a small dip and over the next rise:

`v ← β·v − η·f′(x)      x ← x + v`

```bib
let f = x => 0.08 * x * x + 0.55 * Math.sin(2.2 * x)
let df = x => 0.16 * x + 1.21 * Math.cos(2.2 * x)

let x0 = 0
let eta = 0
let beta = 0
let steps = 0
row {
  x0 = slider("Start x₀", -5, 5, value: -4.5, step: 0.1)
  eta = slider("Learning rate η", 0.01, 0.2, value: 0.1, step: 0.01, id: "eta")
  beta = slider("Momentum β", 0, 0.95, value: 0.9, step: 0.05)
}
steps = slider("Steps", 0, 100, value: 100, step: 1)

// Each roll is a list of {x, v}, one per step.
let roll = mom => data.idx.slice(0, steps).reduce((acc, i) => {
  let p = acc[i]
  let v = mom * p.v - eta * df(p.x)
  return [...acc, {x: p.x + v, v}]
}, [{x: x0, v: 0}])
let plain = roll(0)
let withMom = roll(beta)
let pEnd = plain[plain.length - 1].x
let mEnd = withMom[withMom.length - 1].x

box(width: fill, height: 300, radius: 12, border: 1, background: "raised") {
  layer {
    let px = x => (w - 40) * (x + 5) / 10 + 20
    let py = y => 270 - 250 * (y + 0.8) / 3.6
    let curve = data.idx.slice(0, 121).map(i => py(f(-5 + 10 * i / 120)))
    for y in curve where index < 120 {
      rect(px(-5 + 10 * index / 120), y, (w - 40) / 120, 270 - y, fill: fade(warning, 0.12))
      line(px(-5 + 10 * index / 120), y, px(-5 + 10 * (index + 1) / 120), curve[index + 1], color: ink, width: 2)
    }
    for xv in [-4, -2, 0, 2, 4] { label(px(xv), 284, str(xv), size: 11, color: muted, align: center) }
    for p in plain { circle(px(p.x), py(f(p.x)), 2.5, fill: fade(accent, 0.3)) }
    for p in withMom { circle(px(p.x), py(f(p.x)), 2.5, fill: fade(warning, 0.3)) }
    circle(px(pEnd), py(f(pEnd)) - 8, 8, fill: accent)
    label(px(pEnd), py(f(pEnd)) - 26, "plain", size: 12, color: accent, align: center)
    circle(px(mEnd), py(f(mEnd)) - 10, 10, fill: warning)
    label(px(mEnd), py(f(mEnd)) - 30, "momentum", size: 12, color: warning, align: center)
  }
}

grid(columns: 3) {
  stat("Plain descent ends at", "x = {pEnd.toFixed(2)}", unit: "f = {f(pEnd).toFixed(2)}", tone: accent)
  stat("With momentum", "x = {mEnd.toFixed(2)}", unit: "f = {f(mEnd).toFixed(2)}", tone: warning)
  stat("Lowest point", "x = −0.68", unit: "f = −0.51")
}
```

Momentum is not magic: from some starts it overshoots the best valley too, and too much of it
oscillates for ages. Every serious optimiser (with names like Adam) is a refinement of these two
dials: how big a step, and how much to remember.

###### What you just saw

## What you just saw

- "Learning" here is choosing two numbers, *w* and *b*, to make one number, the loss, small.
- The loss over all choices is a landscape; its gradient points uphill, so the machine steps
  the other way.
- The learning rate is the stride: too small crawls, a bit large zig-zags, past 2/(steepest
  curvature) it diverges.
- Bumpy landscapes trap plain descent in local minima; momentum carries it over small rises.
- A network with a billion weights does exactly this, in a billion dimensions.

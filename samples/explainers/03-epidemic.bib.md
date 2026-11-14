---
bib: 1
title: Flattening the curve
width: wide
nav: true
data:
  days: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 93, 94, 95, 96, 97, 98, 99, 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120, 121, 122, 123, 124, 125, 126, 127, 128, 129, 130, 131, 132, 133, 134, 135, 136, 137, 138, 139, 140, 141, 142, 143, 144, 145, 146, 147, 148, 149, 150, 151, 152, 153, 154, 155, 156, 157, 158, 159, 160, 161, 162, 163, 164, 165, 166, 167, 168, 169, 170, 171, 172, 173, 174, 175, 176, 177, 178, 179, 180, 181, 182, 183, 184, 185, 186, 187, 188, 189, 190, 191, 192, 193, 194, 195, 196, 197, 198, 199, 200, 201, 202, 203, 204, 205, 206, 207, 208, 209, 210, 211, 212, 213, 214, 215, 216, 217, 218, 219, 220, 221, 222, 223, 224, 225, 226, 227, 228, 229, 230, 231, 232, 233, 234, 235, 236, 237, 238, 239, 240, 241, 242, 243, 244, 245, 246, 247, 248, 249, 250, 251, 252, 253, 254, 255, 256, 257, 258, 259, 260, 261, 262, 263, 264, 265, 266, 267, 268, 269, 270, 271, 272, 273, 274, 275, 276, 277, 278, 279, 280, 281, 282, 283, 284, 285, 286, 287, 288, 289, 290, 291, 292, 293, 294, 295, 296, 297, 298, 299]
  ks: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15]
  r0s: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50]
  fs1: [0, 0.17613, 0.3137, 0.42297, 0.51101, 0.58281, 0.64198, 0.69119, 0.73243, 0.76724, 0.79681, 0.82206, 0.84374, 0.86242, 0.8786, 0.89264, 0.90489, 0.91559, 0.92497, 0.93322, 0.94048, 0.94689, 0.95255, 0.95757, 0.96203, 0.96598, 0.96951, 0.97264, 0.97544, 0.97794, 0.98017, 0.98217, 0.98396, 0.98556, 0.987, 0.98829, 0.98945, 0.99049, 0.99142, 0.99227, 0.99302, 0.9937, 0.99432, 0.99487, 0.99537, 0.99582, 0.99622, 0.99659, 0.99692, 0.99722, 0.99748]
  pts: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75]
---
###### An interactive explainer

# Flattening the curve

One person catches a new virus. A few months later, either most of a city of a
million has had it, or almost nobody has, and the hospitals were either overrun or
quietly coped. The difference comes down to a handful of numbers you can move
below. Every chart on this page is computed live from the same small model.

> An epidemic is a chain reaction. Everything that matters is in how the chain grows,
> and in what makes it stop.

###### Part one

## One person's chain

**R₀** ("R nought") is the average number of people one infected person passes the
virus to, in a population where nobody is immune yet. Each case below infects a
random number of people with that average (a Poisson draw), and the chain is
followed for four generations.

```bib
// a plot: the frame, and inside its layer the plot area runs from x 52 to w − 14 and y 14 to h − 30
fn plot(H) {
  box(width: fill, height: H, radius: 12, border: 1, background: "raised") { slot(0) }
}
// gridlines with their numbers on the left, tick names along the bottom
fn axes(X, Y, ys, xs, right, bottom) {
  for t in ys {
    line(52, Y(t.v), right, Y(t.v), color: fade(muted, 0.15))
    label(44, Y(t.v), t.t, size: 11, color: muted, align: end)
  }
  for t in xs { label(X(t.v), bottom - 14, t.t, size: 11, color: muted, align: center) }
}
// a curve through points, as short straight segments
fn poly(xs, ys, color: accent, width: 2) {
  for x in xs where index > 0 { line(xs[index - 1], ys[index - 1], x, ys[index], color: color, width: width) }
}
let ks = data.ks
let R0 = 0
let seed = state("seed", 1)
row {
  card("The virus") {
    R0 = slider("R₀: people infected per case", 0.5, 5, value: 2.5, step: 0.1)
  }
  card("The dice", subtitle: "Outbreak #{seed}") {
    row(fit: true) {
      if button("Another outbreak", style: primary).clicked { set("seed", seed + 1) }
      if button("Back to #1", style: ghost, disabled: seed == 1).clicked { set("seed", 1) }
    }
  }
}

// a small deterministic generator: the same outbreak number always gives the same chain
let unif = (sd, g, j) => {
  let x0 = (sd * 7919 + g * 1013 + j * 7 + 1) % 233280
  let x1 = (x0 * 9301 + 49297) % 233280
  let x2 = (x1 * 9301 + 49297) % 233280
  let x3 = (x2 * 9301 + 49297) % 233280
  return x3 / 233280
}
// the Poisson ladder P(k or fewer) for this R₀, worked out once; a case's count is the first rung above its die
let ladder = ks.reduce((acc, m) => {
  let p = acc.p * R0 / m
  return {p: p, c: [...acc.c, acc.c[acc.c.length - 1] + p]}
}, {p: Math.exp(-R0), c: [Math.exp(-R0)]}).c
let pois = u => {
  let k = ladder.findIndex(c => c >= u)
  return k < 0 ? 15 : k
}
let CAP = 60
let gens = [1, 2, 3, 4].reduce((acc, g) => {
  let prev = acc[acc.length - 1]
  return [...acc, prev.slice(0, CAP).flatMap((n, j) => ks.slice(0, pois(unif(seed + 2, g, j))).map(_ => ({p: j})))]
}, [[{p: 0}]])
let cut = gens.map((gn, g) => g > 0 && gens.slice(0, g).some(x => x.length > CAP))
let fizzle = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29,
  30, 31, 32, 33, 34, 35, 36, 37, 38, 39].reduce((q, i) => Math.exp(R0 * (q - 1)), 0)

box(width: fill, height: 320, radius: 12, border: 1, background: "raised") {
  layer {
    let gy = g => 30 + g * 65
    let nx = (j, n) => 150 + (w - 170) * (j + 0.5) / min([n, CAP])
    for g in [0, 1, 2, 3, 4] {
      label(16, gy(g) - 8, g == 0 ? "patient zero" : "generation {g}", size: 12, color: muted)
      label(16, gy(g) + 9, "{gens[g].length}{cut[g] || gens[g].length > CAP ? "+" : ""}  (avg {round(Math.pow(R0, g), 1)})", size: 12, color: g == 0 ? danger : ink)
    }
    for g in [1, 2, 3, 4] {
      let prev = gens[g - 1]
      for n in gens[g].slice(0, CAP) {
        line(nx(n.p, prev.length), gy(g - 1), nx(index, gens[g].length), gy(g), color: fade(danger, 0.35), width: 1)
      }
    }
    for g in [0, 1, 2, 3, 4] {
      for n in gens[g].slice(0, CAP) {
        circle(nx(index, gens[g].length), gy(g), g == 0 ? 9 : 5.5, fill: g == 0 ? danger : fade(danger, 1 - g * 0.15))
      }
    }
    if gens[4].length == 0 { label(150 + (w - 170) / 2, 300, "This chain died out by itself.", size: 14, color: success, align: center) }
  }
}

grid(columns: 3) {
  stat("Cases in generation 4", gens[4].length > CAP || cut[4] ? "{gens[4].length}+" : str(gens[4].length), tone: gens[4].length == 0 ? success : danger)
  stat("Expected on average", round(Math.pow(R0, 4), 1))
  stat("Chance one case fizzles out", round(fizzle * 100), unit: "%", tone: fizzle > 0.5 ? success : neutral)
}
```

Press **Another outbreak** a few times. Above R₀ = 1 the average chain explodes,
1, R₀, R₀², R₀³…, yet many single introductions still die out by luck: the chance is
the *q* that solves *q* = e^(R₀(q − 1)). Below 1 every chain dies out eventually. Only
the drawn cases are followed, at most 60 per row, so a "+" marks a row that is really
bigger.

###### Part two

## From chains to curves: the SIR model

A chain cannot grow forever: the people it reaches are increasingly people who have
already had it. The SIR model splits a population into **S**usceptible,
**I**nfectious and **R**ecovered (immune) and moves people between them each day:

- new infections per day: β·S·I, where β = R₀ / D
- recoveries per day: I / D, where D is how long someone stays infectious

The model starts with 100 infectious people in a city of a million, and steps forward
one day at a time (Heun's method, a careful version of "rate × one day").

```bib
let D = slider("Infectious period D (days)", 3, 14, value: 7, step: 0.5)
let days = data.days
let simulate = (R, Dd, v, c, t0, len) => days.reduce((acc, d) => {
  let p = acc[acc.length - 1]
  let beta = R / Dd * (d >= t0 && d < t0 + len ? 1 - c : 1)
  let n1 = beta * p.s * p.i
  let m1 = p.i / Dd
  let s2 = p.s - n1
  let i2 = p.i + n1 - m1
  let n2 = beta * s2 * i2
  let q = {s: p.s - (n1 + n2) / 2, i: p.i + (n1 + n2 - m1 - i2 / Dd) / 2}
  return [...acc, {s: q.s, i: q.i, r: 1 - q.s - q.i}]
}, [{s: 1 - v - 0.0001, i: 0.0001, r: v}])

let base = simulate(R0, D, 0, 0, 0, 0)
let peakOf = sim => max(sim.map(p => p.i))
let basePeak = peakOf(base)
let basePeakDay = base.findIndex(p => p.i == basePeak)
let turnDay = base.findIndex(p => p.s < 1 / R0)

let pct = [0, 0.25, 0.5, 0.75, 1].map(g => ({v: g, t: "{round(g * 100)}%"}))
let dayTicks = [0, 50, 100, 150, 200, 250, 300].map(d => ({v: d, t: d == 0 ? "day 0" : str(d)}))
plot(300) {
  layer {
    let X = d => 52 + (w - 66) * d / 300
    let Y = v => 14 + (h - 44) * (1 - v)
    axes(X, Y, pct, dayTicks, w - 14, h)
    if R0 > 1 { line(52, Y(1 / R0), w - 14, Y(1 / R0), color: fade(accent, 0.45), width: 1) }
    if turnDay > 0 {
      line(X(turnDay), 14, X(turnDay), h - 30, color: fade(danger, 0.4), width: 1)
      label(X(turnDay) + 6, 22, "peak, day {basePeakDay}", size: 12, color: danger)
    }
    let xs = data.pts.map(k => X(k * 4))
    let pts = data.pts.map(k => base[k * 4])
    poly(xs, pts.map(p => Y(p.s)), color: accent, width: 2.5)
    poly(xs, pts.map(p => Y(p.r)), color: success, width: 2.5)
    poly(xs, pts.map(p => Y(p.i)), color: danger, width: 3)
  }
}
row(fit: true) {
  badge("S susceptible", tone: accent)
  badge("I infectious", tone: danger)
  badge("R recovered", tone: success)
  badge("S = 1/R₀", tone: muted)
}

grid(columns: 4) {
  stat("Peak day", basePeak < 0.001 ? "none" : str(basePeakDay), tone: danger)
  stat("Infectious at the peak", round(basePeak * 100, 1), unit: "%")
  stat("Ever infected by day 300", round(base[300].r * 100, 1), unit: "%")
  stat("Early doubling time", R0 > 1 ? round(Math.log(2) * D / (R0 - 1), 1) : "never", unit: R0 > 1 ? "days" : "")
}
```

Look at where the red curve turns. The epidemic peaks exactly when the susceptible
share S falls below **1/R₀**: from then on each case infects fewer than one new
person, because most of the people it meets are already immune. A longer infectious
period with the same R₀ does not make the epidemic bigger; it stretches it out.

###### Part three

## Flattening the curve

Suppose 5% of infectious people need a hospital bed, and the city has 2,000 beds.
At the peak above, that is far more than the hospitals have. Cutting contacts (working
from home, smaller gatherings, masks) lowers β for as long as it lasts. The grey curve
is the epidemic left alone.

```bib
let c = 0
let t0 = 0
let len = 0
row {
  card("Contact reduction") {
    c = slider("Contacts cut by (%)", 0, 80, value: 40, step: 5) / 100
  }
  card("Timing") {
    t0 = slider("Starts on day", 0, 150, value: 25, step: 1)
    len = slider("Lasts (days, 300 = to the end)", 10, 300, value: 300, step: 5)
  }
}
let N = 1000000
let BEDS = 2000
let flat = simulate(R0, D, 0, c, t0, len)
let flatPeak = peakOf(flat)
let flatPeakDay = flat.findIndex(p => p.i == flatPeak)
let cap = BEDS / (N * 0.05)
let over = flat.filter(p => p.i > cap).length
let top = ceil(max([basePeak, cap * 1.3]) * N * 0.05 / 1000) * 1000 / (N * 0.05)   // a round number of beds

let commas = n => n >= 1000 ? str(floor(n / 1000)) + "," + str(1000 + n % 1000).slice(1) : str(n)
let bedTicks = [0, 0.25, 0.5, 0.75, 1].map(g => ({v: top * g, t: commas(round(top * g * N * 0.05))}))
plot(300) {
  layer {
    let X = d => 52 + (w - 66) * d / 300
    let Y = v => 14 + (h - 44) * (1 - v / top)
    axes(X, Y, bedTicks, dayTicks, w - 14, h)
    rect(X(t0), 14, X(min([t0 + len, 300])) - X(t0), h - 44, fill: fade(accent, 0.09))
    label(X(t0) + 8, 24, "contacts −{round(c * 100)}%", size: 12, color: accent)
    let xs = data.pts.map(k => X(k * 4))
    let ys = data.pts.map(k => Y(min([flat[k * 4].i, top])))
    let y0 = Y(0)
    let dx = xs[1] - xs[0] + 0.5
    for x in xs { rect(x, ys[index], dx, y0 - ys[index], fill: fade(danger, 0.14)) }
    poly(xs, data.pts.map(k => Y(min([base[k * 4].i, top]))), color: fade(muted, 0.6), width: 2)
    poly(xs, ys, color: danger, width: 3)
    line(52, Y(cap), w - 14, Y(cap), color: warning, width: 2)
    label(w - 18, Y(cap) - 10, "2,000 beds", size: 12, color: warning, align: end)
  }
}
row(fit: true) {
  badge("beds needed, with measures", tone: danger)
  badge("left alone", tone: muted)
  badge("2,000 beds", tone: warning)
  badge("contacts cut", tone: accent)
}
grid(columns: 4) {
  stat("Peak beds needed", round(flatPeak * N * 0.05), tone: flatPeak > cap ? danger : success)
  stat("Peak day", flatPeak < 0.001 ? "none" : str(flatPeakDay))
  stat("Days over capacity", over, tone: over > 0 ? danger : success)
  stat("Ever infected by day 300", round(flat[300].r * 100, 1), unit: "%")
}
row(fit: true) {
  text("Left alone: peak {commas(round(basePeak * N * 0.05))} beds on day {basePeakDay}, {round(base[300].r * 100)}% infected.  ", size: sm, color: muted)
  if len < 300 && t0 + len < 300 && flatPeakDay > t0 + len { badge("second wave after the measures end", tone: warning) }
}
```

Flattening buys time: the peak is lower and later, so the same beds can treat
everyone. Notice what it does *not* do on its own: if the measures are lifted while
most people are still susceptible (try *Lasts* = 60), the curve simply comes back.
Contact reduction holds an epidemic down; only immunity ends it.

###### Part four

## Herd immunity and the final size

If a share **v** of people are immune before the virus arrives (say, vaccinated),
each case meets fewer susceptible people, and the effective reproduction number is
R = R₀·(1 − v). The epidemic cannot take off when that is below one, that is when

> v ≥ 1 − 1/R₀, the **herd immunity threshold**.

And when it does take off, how many people does it reach in the end? The SIR model
answers without simulating: the share *z* of the population ever infected solves the
**final-size relation** z = s₀·(1 − e^(−R₀·z)), where s₀ = 1 − v is the share that was
susceptible to begin with.

```bib
let v = slider("Immune before the outbreak, v (%)", 0, 90, value: 0, step: 1) / 100
let finalSize = (R, s0) => R * s0 <= 1 ? 0 : [0, 1, 2, 3, 4, 5, 6].reduce(
  (z, k) => z - (z - s0 * (1 - Math.exp(-R * z))) / (1 - s0 * R * Math.exp(-R * z)), s0)
let herd = R0 > 1 ? 1 - 1 / R0 : 0
let z = finalSize(R0, 1 - v)
let both = simulate(R0, D, v, c, t0, len)
let fs1 = data.fs1   // the same curve for v = 0, worked out once
let fsv = data.r0s.map(k => finalSize(1 + k / 10, 1 - v))

plot(300) {
  layer {
    let X = R => 52 + (w - 66) * (R - 1) / 5
    let Y = f => 14 + (h - 44) * (1 - f)
    axes(X, Y, pct, [1, 2, 3, 4, 5, 6].map(r => ({v: r, t: r == 1 ? "R₀ = 1" : str(r)})), w - 14, h)
    line(52, Y(v), w - 14, Y(v), color: fade(success, 0.7), width: 2)
    let xs = data.r0s.map(k => X(1 + k / 10))
    poly(xs, data.r0s.map(k => Y(1 - 1 / (1 + k / 10))), color: warning, width: 2)
    poly(xs, fs1.map(Y), color: fade(muted, 0.5), width: 2)
    poly(xs, fsv.map(Y), color: danger, width: 3)
    if R0 >= 1 {
      line(X(R0), 14, X(R0), h - 30, color: fade(ink, 0.2), width: 1)
      if z > 0 { line(X(R0), Y(herd), X(R0), Y(z), color: fade(danger, 0.18), width: 6) }
      circle(X(R0), Y(herd), 5, fill: warning)
      circle(X(R0), Y(z), 7, fill: danger)
    }
  }
}
row(fit: true) {
  badge("herd immunity threshold 1 − 1/R₀", tone: warning)
  badge("final size, nobody immune", tone: muted)
  badge("final size with v immune", tone: danger)
  badge("already immune: v", tone: success)
}

grid(columns: 4) {
  stat("Herd immunity threshold", round(herd * 100, 1), unit: "%", tone: warning)
  stat("Effective R", round(R0 * (1 - v), 2), tone: R0 * (1 - v) < 1 ? success : danger)
  stat("Final size (theory)", round(z * 100, 1), unit: "%", tone: z > 0 ? danger : success)
  stat("Immune in the end", round((v + z) * 100, 1), unit: "%")
}
kv([
  {label: "Overshoot", value: z > 0 ? "{round((v + z - herd) * 100, 1)} points past the threshold: infections that happen after the peak" : "none: the outbreak never takes off"},
  {label: "Everything together", value: "with v = {round(v * 100)}% and contacts cut {round(c * 100)}% from day {t0}: peak {commas(round(peakOf(both) * N * 0.05))} beds, {round((both[300].r - v) * 100, 1)}% infected by day 300"},
])
```

Two things are worth seeing here. First, the red curve is always *above* the orange
one: an unchecked epidemic does not stop at the herd immunity threshold, it overshoots
it, because at the peak there are still many infectious people and each infects nearly
one more on the way down. At R₀ = 2.5 the threshold is 60% but the epidemic reaches
about 89%. Second, drag **v** up to the threshold and the red curve drops to zero at
your R₀: immunity given in advance prevents the overshoot entirely.

---

## What you just saw

- **R₀ is an average of a random process.** Chains above R₀ = 1 tend to explode, but a
  single introduction can still fizzle out by luck.
- **The SIR model turns chains into curves.** The epidemic peaks when the susceptible
  share falls to 1/R₀; the infectious period sets the pace, not the size.
- **Flattening** lowers and delays the peak so the hospitals cope. Lifted too early,
  it only postpones the wave.
- **Herd immunity** at 1 − 1/R₀ is the point where the epidemic turns, not where it
  ends: left alone it overshoots to the final size z = s₀(1 − e^(−R₀z)). Immunity in
  advance, above the threshold, means no wave at all.

*The model is deliberately simple: everyone mixes with everyone, immunity lasts, and
nobody is born or moves. Real epidemics add age, places, seasons and chance, but this
skeleton is what they are built on.*

---
bib: 1
title: Moving clocks run slow
width: wide
nav: true
data:
  idx: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 93, 94, 95, 96, 97, 98, 99, 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120]
---
###### Special relativity · an interactive explainer

# Moving clocks run slow

Not because their gears get tired, and not as an illusion. It falls straight out of one
stubborn fact: every observer measures the same speed of light. All you need is a triangle.

> High in the atmosphere, cosmic rays make muons: particles that live, on average, 2.2
> millionths of a second. Even at the speed of light that is only 660 metres of travel. They
> are born 15 km up. **Yet detectors on the ground catch them by the thousand.** By the end
> of this page you will know why.

###### 01 — The light clock

## A clock made of light

Take the simplest clock imaginable: a flash of light bouncing between two mirrors a distance
*L* apart. Each trip from one mirror to the other is one **tick**, and takes *L/c*. Now put an
identical clock on a ship moving to the right at speed *v*. Watch both from the ground: slide
the time to run the clocks.

```bib
// Shared: the Lorentz factor, and the photon's height in a clock whose one-way trip takes g.
let gammaOf = b => 1 / Math.sqrt(1 - b * b)
let photonY = (t, g) => {
  let p = t / g
  let k = floor(p)
  return k % 2 == 0 ? p - k : 1 - (p - k)
}
let TMAX = 6

let beta = 0
let t = 0
row {
  beta = slider("Ship speed (fraction of c)", 0, 0.99, value: 0.6, step: 0.01)
  t = slider("Time on the ground (L/c)", 0, TMAX, value: 2.5, step: 0.05)
}
let G = gammaOf(beta)

fn lane(title, v, g, tone) {
  box(width: fill, height: 170, radius: 12, border: 1, background: "raised") {
    layer {
      // vertical unit: a fixed 120 px; horizontal unit: the width over the run, so the clock fits
      let s = 120
      let u = (w - 60) / (TMAX + 1.4)
      let top = 34
      let bot = top + s
      let x0 = u * 0.35 + 30
      let cx = x0 + u * v * t
      let n = floor(t / g)
      label(12, 16, title, size: 11, color: tone)
      label(w - 12, 16, "{n} ticks", size: 12, color: ink, align: end)
      // the photon's path so far: straight up and down at rest, a zig-zag when moving
      for k in data.idx where k < n {
        let xa = x0 + u * v * k * g
        let xb = x0 + u * v * (k + 1) * g
        line(xa, k % 2 == 0 ? bot : top, xb, k % 2 == 0 ? top : bot, color: fade(accent, 0.45), width: 1.5)
      }
      let py = bot - photonY(t, g) * s
      line(x0 + u * v * n * g, n % 2 == 0 ? bot : top, cx, py, color: fade(accent, 0.45), width: 1.5)
      // ghosts of the clock at each tick
      if v > 0 {
        for k in data.idx where k >= 1 && k <= n {
          let xk = x0 + u * v * k * g
          line(xk - u * 0.3 / g, top, xk + u * 0.3 / g, top, color: faint, width: 1)
          line(xk - u * 0.3 / g, bot, xk + u * 0.3 / g, bot, color: faint, width: 1)
        }
      }
      // the mirrors, length-contracted by 1/γ when moving
      let half = u * 0.3 / g
      line(cx - half, top, cx - half, bot, color: faint, width: 1)
      line(cx + half, top, cx + half, bot, color: faint, width: 1)
      line(cx - half, top, cx + half, top, color: ink, width: 4)
      line(cx - half, bot, cx + half, bot, color: ink, width: 4)
      circle(cx, py, 11, fill: fade(accent, 0.25))
      circle(cx, py, 5, fill: accent)
    }
  }
}

lane("AT REST", 0, 1, muted)
lane("MOVING AT {beta.toFixed(2)} c", beta, G, accent)

grid(columns: 3) {
  stat("Ticks, clock at rest", floor(t))
  stat("Ticks, moving clock", floor(t / G), tone: accent)
  stat("One moving tick lasts", G.toFixed(2), unit: "L/c")
}
```

From the ground, the moving photon cannot go straight up: the top mirror has moved on by the
time the light arrives, so the light runs along a **diagonal**. A diagonal is longer than the
vertical. And light, for everybody, goes at exactly *c*. Longer path, same speed: each tick of
the moving clock takes longer. The ship's clock runs slow.

###### 02 — The triangle

## Pythagoras finds γ

Freeze one tick of the moving clock. In the time *t* the ground measures, the light covers the
hypotenuse *c·t* while the ship moves *v·t* sideways. The vertical leg is still the mirror gap,
*L = c·t₀*, where *t₀* is a tick on a clock at rest. (The speed is the one you set above.)

```bib
row {
  box(width: fill, height: 300, radius: 12, border: 1, background: "raised") {
    layer {
      let side = beta * G
      let s = min([180, 300 / max([side, 0.001])])
      let ox = 40
      let oy = 260
      let vx = ox + side * s
      let ty = oy - s
      line(ox, oy, ox, ty, color: muted, width: 3)
      line(ox, oy, vx, oy, color: success, width: 3)
      line(ox, oy, vx, ty, color: accent, width: 3)
      line(vx, oy, vx, ty, color: faint, width: 1)
      rect(ox, oy - 12, 12, 12, color: muted, width: 1)
      label(ox + 6, ty - 14, "c·t₀ = L", size: 12, color: muted)
      label((ox + vx) / 2, oy + 16, "v·t = {side.toFixed(2)}", size: 12, color: success, align: center)
      label((ox + vx) / 2 + 14, (oy + ty) / 2 + 4, "c·t = {G.toFixed(2)}", size: 12, color: accent)
      circle(vx, ty, 5, fill: accent)
    }
  }
  card("The algebra") {
    markdown("1. Pythagoras: (c·t)² = (c·t₀)² + (v·t)²\n2. Collect the *t*: t²(c² − v²) = c²·t₀²\n3. Divide by c²: t²(1 − v²/c²) = t₀²\n4. Take the root:")
    box(padding: [10, 0], align: center) {
      text("t = γ · t₀,   γ = 1 / √(1 − v²/c²)", size: lg, weight: italic, color: accent)
    }
    text("At v = {beta.toFixed(2)} c, in units of L: vertical 1, sideways {(beta * G).toFixed(3)}, diagonal {G.toFixed(3)}. Check: {G.toFixed(3)}² = 1 + {(beta * G).toFixed(3)}² = {(G * G).toFixed(3)}.", size: sm, color: muted)
  }
}
```

γ, the **Lorentz factor**, is the stretch. Here it is against speed. It barely leaves 1 at
everyday speeds (a jet at 250 m/s has γ − 1 ≈ 3·10⁻¹³) and then shoots to infinity as *v*
approaches *c*.

```bib
let GMAX = 8
box(width: fill, height: 260, radius: 12, border: 1, background: "raised") {
  layer {
    let gx = b => 44 + (w - 60) * b
    let gy = g => 230 - 210 * (min([g, GMAX]) - 1) / (GMAX - 1)
    for k in [1, 2, 3, 4, 5, 6, 7, 8] {
      line(44, gy(k), w - 16, gy(k), color: border, width: 1)
      label(36, gy(k), str(k), size: 11, color: muted, align: end)
    }
    for b in [0, 0.2, 0.4, 0.6, 0.8, 1] {
      label(gx(b), 246, b == 1 ? "c" : b.toFixed(1), size: 11, color: muted, align: center)
    }
    line(gx(1), 14, gx(1), 230, color: faint, width: 1)
    label(8, 12, "γ", size: 12, color: muted)
    for i in data.idx where i < 120 {
      let b1 = i / 120 * 0.9922
      let b2 = (i + 1) / 120 * 0.9922
      line(gx(b1), gy(gammaOf(b1)), gx(b2), gy(gammaOf(b2)), color: accent, width: 2.5)
    }
    let x = gx(beta)
    let y = gy(G)
    line(x, 230, x, y, color: faint, width: 1)
    line(44, y, x, y, color: faint, width: 1)
    circle(x, y, 12, fill: fade(accent, 0.25))
    circle(x, y, 6, fill: accent)
    label(beta > 0.85 ? x - 120 : x + 12, max([y - 16, 12]), "γ = {G.toFixed(3)}", size: 12, color: ink)
  }
}

grid(columns: 3) {
  stat("γ", G.toFixed(3), tone: accent)
  stat("1 hour on board =", round(60 * G), unit: "min on the ground")
  stat("A 100 m ship measures", round(100 / G), unit: "m from the ground", tone: success)
}
```

The same γ shrinks lengths along the motion: a moving ruler is **1/γ** as long as at rest (look
at the moving clock above: it is squeezed). Time stretches by γ, length shrinks by γ, and the two
together keep the speed of light the same for everyone.

###### 03 — The twins

## The trip calculator

Two twins. One stays home, one flies to a star and back at a steady speed. Choose the trip.

```bib
let d = 0
let tb = 0
let age = 0
row {
  d = slider("Distance to the star (light-years)", 1, 40, value: 10, step: 0.5)
  tb = slider("Trip speed (fraction of c)", 0.1, 0.999, value: 0.9, step: 0.001)
  age = slider("Age at departure", 18, 60, value: 30, step: 1)
}
let TG = gammaOf(tb)
let tEarth = 2 * d / tb
let tShip = tEarth / TG
let every = [1, 2, 5, 10, 20, 50, 100, 200, 500].find(s => tEarth / s <= 24) ?? 1000

grid(columns: 4) {
  stat("Twin at home, on return", (age + tEarth).toFixed(1), unit: "years old", tone: success)
  stat("Traveller, on return", (age + tShip).toFixed(1), unit: "years old", tone: accent)
  stat("Age gap", (tEarth - tShip).toFixed(1), unit: "years")
  stat("γ", TG.toFixed(3))
}

box(width: fill, height: 380, radius: 12, border: 1, background: "raised") {
  layer {
    let L = 56
    let R = w - 24
    let T = 20
    let B = 344
    let X = x => L + (R - L) * x / (d * 1.08)
    let Y = tt => B - (B - T) * tt / tEarth
    line(L, T, L, B, color: border, width: 1)
    line(L, B, R, B, color: border, width: 1)
    label(X(0), B + 16, "0", size: 11, color: muted, align: center)
    label(X(d), B + 16, "{d} ly", size: 11, color: muted, align: center)
    label(L - 8, B, "0", size: 11, color: muted, align: end)
    label(L - 8, T, "{tEarth.toFixed(1)} y", size: 11, color: muted, align: end)
    // a light ray from the departure
    let lc = min([d * 1.08, tEarth])
    line(X(0), Y(0), X(lc), Y(lc), color: faint, width: 1)
    label(X(lc * 0.45) + 6, Y(lc * 0.45) + 6, "light", size: 11, color: faint)
    // the star
    line(X(d), T, X(d), B, color: faint, width: 1)
    label(X(d) - 8, T, "★ star", size: 11, color: muted, align: end)
    // worldlines
    line(X(0), Y(0), X(0), Y(tEarth), color: success, width: 3)
    line(X(0), Y(0), X(d), Y(tEarth / 2), color: accent, width: 3)
    line(X(d), Y(tEarth / 2), X(0), Y(tEarth), color: accent, width: 3)
    // birthdays on each twin's own clock
    for k in data.idx where k >= 1 && k * every < tEarth - 0.000001 {
      circle(X(0), Y(k * every), 3.5, fill: success)
    }
    for k in data.idx where k >= 1 && k * every < tShip - 0.000001 {
      let te = k * every * TG
      let x = te <= tEarth / 2 ? tb * te : d - tb * (te - tEarth / 2)
      circle(X(x), Y(te), 3.5, fill: accent)
    }
    label(X(0) + 12, Y(tEarth * 0.5), "home: {tEarth.toFixed(1)} y", size: 12, color: success)
    label(X(d * 0.75), Y(tEarth * 0.625) - 16, "traveller: {tShip.toFixed(1)} y", size: 12, color: accent, align: center)
  }
}
text("Spacetime diagram: up is time on Earth, across is distance. Dots on each worldline mark one birthday (every {every == 1 ? "1 year" : "{every} years"}) on that twin's own clock.", size: sm, color: muted)

fn bar(title, frac, value, tone) {
  box(dir: "row", gap: 12, align: center) {
    box(width: 170) { text(title, size: sm, color: muted) }
    box(width: fill, height: 14, radius: 7, background: fade(success, 0.14)) {
      layer { rect(0, 0, w * frac, h, fill: tone, radius: 7) }
    }
    box(width: 80) { text(value, size: sm, face: mono, align: end) }
  }
}
box(gap: 10, padding: [6, 0]) {
  bar("Distance, Earth's view", 1, "{d.toFixed(1)} ly", success)
  bar("Distance, ship's view", 1 / TG, "{(d / TG).toFixed(2)} ly", accent)
}
```

Two stories, one answer. On Earth: the traveller's clock runs slow by γ, so she ages
*2d / (v·γ)*. On the ship: nothing is wrong with her clock, but the trip is shorter: the distance
to the star is **length-contracted** to *d/γ*. Both give the same age.

Why isn't it symmetric, each twin seeing the other's clock as slow? Because only the traveller
turns around. The kink in her worldline is a real, felt acceleration, and it is the
stay-at-home twin whose path through spacetime is the long one between the two meetings.

###### 04 — The muons

## Back to the sky

A muon's mean life at rest is τ = 2.197 μs. Out of a million made at altitude *h*, the number
still alive after flying for a time *t* is 1,000,000 · e^(−t/τ). The question is just: whose *t*?

```bib
let TAU = 0.000002197
let CMS = 299792458
let comma = n => {
  let r = round(n)
  let pad = u => (u < 10 ? "00" : u < 100 ? "0" : "") + str(u)
  let m = floor(r / 1000000)
  let th = floor(r / 1000) % 1000
  let u = r % 1000
  return m > 0 ? str(m) + "," + pad(th) + "," + pad(u) : th > 0 ? str(th) + "," + pad(u) : str(u)
}
let sci = n => {
  let e = floor(Math.log10(n))
  return (n / Math.pow(10, e)).toFixed(1) + "e" + str(e)
}
let fmt = n => n >= 1 ? comma(n) : n >= 0.001 ? n.toFixed(3) : sci(n)

let hk = 0
let mb = 0
row {
  hk = slider("Altitude of birth (km)", 2, 20, value: 15, step: 0.5)
  mb = slider("Muon speed (fraction of c)", 0.95, 0.9999, value: 0.998, step: 0.0001)
}
let MG = gammaOf(mb)
let v = mb * CMS
let tf = hk * 1000 / v
let no = 1000000 * Math.exp(-tf / TAU)
let yes = 1000000 * Math.exp(-tf / (MG * TAU))

grid(columns: 4) {
  stat("Flight time, ground clock", (tf * 1000000).toFixed(1), unit: "μs = {(tf / TAU).toFixed(1)} τ")
  stat("Survivors, no relativity", fmt(no))
  stat("Survivors, with γ", fmt(yes), tone: accent)
  stat("Atmosphere, muon's view", round(hk * 1000 / MG), unit: "m thick (γ = {MG.toFixed(1)})", tone: success)
}

box(width: fill, height: 300, radius: 12, border: 1, background: "raised") {
  layer {
    let L = 70
    let R = w - 20
    let T = 16
    let B = 266
    let X = km => L + (R - L) * (1 - km / hk)
    let Y = n => B - (B - T) * n / 1000000
    for f in [0, 0.25, 0.5, 0.75, 1] {
      line(L, Y(f * 1000000), R, Y(f * 1000000), color: border, width: 1)
      label(L - 6, Y(f * 1000000), comma(f * 1000000), size: 11, color: muted, align: end)
    }
    label(X(hk), B + 18, "{hk} km", size: 11, color: muted, align: center)
    label(X(hk / 2), B + 18, "{hk / 2} km", size: 11, color: muted, align: center)
    label(X(0) + 16, B + 18, "ground", size: 11, color: muted, align: end)
    for i in data.idx where i < 100 {
      let k1 = hk * (1 - i / 100)
      let k2 = hk * (1 - (i + 1) / 100)
      let t1 = (hk - k1) * 1000 / v
      let t2 = (hk - k2) * 1000 / v
      line(X(k1), Y(1000000 * Math.exp(-t1 / TAU)), X(k2), Y(1000000 * Math.exp(-t2 / TAU)), color: faint, width: 2.5)
      line(X(k1), Y(1000000 * Math.exp(-t1 / (MG * TAU))), X(k2), Y(1000000 * Math.exp(-t2 / (MG * TAU))), color: accent, width: 2.5)
    }
    circle(X(0), Y(yes), 6, fill: accent)
    label(X(0) - 10, max([Y(yes) - 16, T + 4]), fmt(yes), size: 12, color: accent, align: end)
  }
}
text("Muons still alive (of 1,000,000) as they fall from the birth altitude to the ground. Faint: if their clocks ran at the ground's rate. Bright: with time dilation.", size: sm, color: muted)
```

From the ground, the muon's internal clock is slowed by γ, so it has γ lifetimes to spend on the
way down. From the muon's side its clock is fine, but the atmosphere rushes at it contracted to
*h/γ*, a few hundred metres. Same survivors either way. This was measured: in the 1940s and,
precisely, in 1963 on a mountain and at sea level.

###### What you just saw

## What you just saw

- A light clock in motion traces a diagonal; with light's speed fixed, the tick gets longer.
- Pythagoras on that diagonal gives **γ = 1/√(1 − v²/c²)**: about 1 for anything we build,
  unbounded near *c*.
- Lengths along the motion shrink by the same γ, so each observer's account agrees on what
  actually happens.
- The twin who turns around ages less: a trip of 2·10 ly at 0.9 c costs her 9.7 years, not 22.2.
- Cosmic-ray muons reach the ground because their clocks, seen from here, run γ times slow.

*Units: c = 1 light-year per year in the trip calculator; c = 299,792 km/s for the muons.*

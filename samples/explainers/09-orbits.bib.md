---
bib: 1
title: To catch up, slow down
width: wide
nav: true
---
###### Orbital mechanics, by hand

# To catch up, slow down

Two spacecraft share an orbit. The one behind wants to catch the one ahead. On a road
you would press the accelerator. In orbit, that sends you *backwards*: the push lifts you
into a higher, slower orbit and the gap grows. To catch up you brake.

This page builds that strange rule from one equation, then uses it to fly to Mars.
Every number is computed live from the sliders; nothing is a picture.

> In orbit, speed and height are one thing. Change one and you have changed the other.

```bib
let I = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 93, 94, 95, 96, 97, 98, 99, 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120]
let TAU = 2 * Math.PI
let DEG = 180 / Math.PI

// Kepler's equation M = E - e sin E, solved by Newton's method.
fn kep(M, e) {
  let E = M + e * Math.sin(M)
  E = E - (E - e * Math.sin(E) - M) / (1 - e * Math.cos(E))
  E = E - (E - e * Math.sin(E) - M) / (1 - e * Math.cos(E))
  E = E - (E - e * Math.sin(E) - M) / (1 - e * Math.cos(E))
  E = E - (E - e * Math.sin(E) - M) / (1 - e * Math.cos(E))
  return E
}
// The true anomaly from the eccentric anomaly, continuous past one turn.
fn trueAnom(E, e) {
  let b = e / (1 + Math.sqrt(1 - e * e))
  return E + 2 * Math.atan2(b * Math.sin(E), 1 - b * Math.cos(E))
}
fn hm(hours) {
  let hh = floor(hours)
  let mm = round((hours - hh) * 60)
  return "{hh} h {mm < 10 ? "0" : ""}{mm} min"
}
```

###### One

## Newton's cannon

Put a cannon on a tower 6,371 km above the ground, twice the planet's radius from its
centre, and fire sideways. Too slow and the shot falls in. At one exact speed it falls
*around* the planet forever: a circle. Faster still and the circle stretches into an
ellipse, until at √2 times the circular speed it never comes back.

The orbit follows from just the position and the velocity at launch. With the sideways
launch here the eccentricity is simply `e = |v²/v_c² − 1|`.

```bib
let MU = 398600
let R0 = 12742
let RP = 6371
let VC = Math.sqrt(MU / R0)
let VE = VC * Math.sqrt(2)
let T0 = TAU * Math.sqrt(R0 * R0 * R0 / MU) / 3600

let vk = 0
let f = 0
row {
  card("The cannon", subtitle: "circular {round(VC, 2)} km/s · escape {round(VE, 2)} km/s") {
    vk = slider("Launch speed (km/s)", 3.5, 9, value: 6.6, step: 0.05)
  }
  card("Follow the shot", subtitle: "equal times, unequal distances") {
    f = slider("Time flown (fraction of one orbit)", 0, 1, value: 0.35, step: 0.01)
  }
}

let v = vk / VC
let es = v * v - 1
let e = abs(es)
let bound = v * v < 1.9999
let a = bound ? 1 / (2 - v * v) : 0
let off = es < 0 ? Math.PI : 0
let peri = es < 0 ? v * v / (1 + e) : 1
let apo = bound ? (es < 0 ? 1 : v * v / (1 - e)) : Infinity
let crash = peri < RP / R0
let period = bound ? T0 * Math.pow(a, 1.5) : 0

// Where the shot is after a fraction f of its period.
let Ef = kep(off + TAU * f, e)
let rf = bound ? a * (1 - e * Math.cos(Ef)) : 1
let thf = bound ? trueAnom(Ef, e) - off : 0
let vf = bound ? VC * Math.sqrt(2 / rf - 1 / a) : vk
let Eg = kep(off + TAU * (f + 0.004), e)
let rg = a * (1 - e * Math.cos(Eg))
let thg = trueAnom(Eg, e) - off
let S1 = 380 * 0.24
let orbitPts = I.map(i => {
  let t = TAU * i / 120
  let d = 1 + es * Math.cos(t)
  let r = d > 0.02 ? v * v / d : 99
  return {ok: r < 40, x: S1 * r * Math.cos(t), y: S1 * r * Math.sin(t)}
})

box(width: fill, height: 380, radius: 12, border: 1, background: "raised") {
  layer {
    let cx = w * 0.6
    let cy = 190
    let S = 380 * 0.24
    let X = (r, t) => cx + S * r * Math.cos(t)
    let Y = (r, t) => cy - S * r * Math.sin(t)
    // the circular orbit, for reference
    circle(cx, cy, S, color: fade(muted, 0.5), width: 1)
    // the orbit (or the escape path)
    for i in I where i < 120 {
      let p = orbitPts[i]
      let q = orbitPts[i + 1]
      if p.ok && q.ok {
        line(cx + p.x, cy - p.y, cx + q.x, cy - q.y, color: crash ? danger : bound ? accent : warning, width: 2.5)
      }
    }
    // the planet
    circle(cx, cy, S * RP / R0, fill: fade(success, 0.35))
    circle(cx, cy, S * RP / R0, color: success, width: 1.5)
    // the tower
    line(cx + S * RP / R0, cy, cx + S, cy, color: muted, width: 3)
    circle(cx + S, cy, 4, fill: ink)
    // the shot and its velocity
    if bound && !crash {
      let px = X(rf, thf)
      let py = Y(rf, thf)
      let dx = S * (rg * Math.cos(thg) - rf * Math.cos(thf))
      let dy = -S * (rg * Math.sin(thg) - rf * Math.sin(thf))
      let dl = Math.sqrt(dx * dx + dy * dy)
      let L = 14 * vf
      line(px, py, px + dx / dl * L, py + dy / dl * L, color: warning, width: 2.5)
      circle(px + dx / dl * L, py + dy / dl * L, 3, fill: warning)
      line(cx, cy, px, py, color: fade(ink, 0.25), width: 1)
      circle(px, py, 7, fill: ink)
      label(px + 12, py - 20, "{round(vf, 2)} km/s", size: 12)
    }
    label(cx, cy, "planet", size: 12, align: center)
    label(16, 22, crash ? "falls back in" : !bound ? "escapes: an open curve" : e < 0.01 ? "a circle" : "an ellipse, e = {round(e, 2)}", size: 15, color: crash ? danger : !bound ? warning : accent)
    label(16, h - 18, "faint circle: the circular orbit · arrow: velocity", size: 11, color: muted)
  }
}

grid(columns: 4) {
  stat("Eccentricity", round(e, 3), tone: !bound ? warning : neutral)
  stat("Lowest point", crash ? "underground" : "{round((peri * R0 - RP) / 1000, 1)}k km", tone: crash ? danger : neutral)
  stat("Highest point", bound ? "{round((apo * R0 - RP) / 1000, 1)}k km" : "never", tone: bound ? neutral : warning)
  stat("Period", bound ? hm(period) : "none")
}
```

The dot moves by **time**, not by angle: drag the second slider and it lingers far out
and whips round close in. The speed comes from one line, the **vis-viva equation**:

`v² = GM (2/r − 1/a)`

Here `r` is the distance from the planet's centre now and `a` the semi-major axis, half the
long width of the ellipse. A circle is the case `r = a` at every point, so `v² = GM/r`. The
curve below is that speed over one orbit.

```bib
box(width: fill, height: 170, radius: 12, border: 1, background: "raised") {
  layer {
    let top = 10
    let sp = bound && !crash ? I.filter(i => i <= 60).map(i => {
      let E = kep(off + TAU * i / 60, e)
      let r = a * (1 - e * Math.cos(E))
      return VC * Math.sqrt(2 / r - 1 / a)
    }) : []
    let yv = s => h - 24 - (h - 40) * s / top
    for g in [0, 2, 4, 6, 8, 10] {
      line(40, yv(g), w - 10, yv(g), color: fade(muted, 0.18))
      label(30, yv(g), "{g}", size: 11, color: muted, align: end)
    }
    label(46, 10, "km/s", size: 11, color: muted)
    label(w - 12, yv(VC) - 9, "circular speed", size: 11, color: accent, align: end)
    line(40, yv(VC), w - 10, yv(VC), color: fade(accent, 0.5), width: 1)
    if bound && !crash {
      for i in I where i < 60 {
        line(40 + (w - 50) * i / 60, yv(sp[i]), 40 + (w - 50) * (i + 1) / 60, yv(sp[i + 1]), color: warning, width: 2.5)
      }
      circle(40 + (w - 50) * f, yv(vf), 6, fill: ink)
    } else {
      label(w / 2, h / 2, crash ? "the shot hits the ground before it completes an orbit" : "an open path has no period to plot", size: 13, color: muted, align: center)
    }
    label(40, h - 10, "launch", size: 11, color: muted)
    label(w - 10, h - 10, "one full orbit later", size: 11, color: muted, align: end)
  }
}

kv([
  {label: "Distance from centre now", value: "{round(rf * R0)} km"},
  {label: "Semi-major axis a", value: bound ? "{round(a * R0)} km" : "infinite or negative: unbound"},
  {label: "v² = GM (2/r − 1/a)", value: bound ? "{MU} × (2/{round(rf * R0)} − 1/{round(a * R0)}) = {round(vf * vf, 2)} km²/s²" : "-"},
  {label: "Speed", value: "{round(vf, 2)} km/s"},
])
```

###### Two

## Lower is faster

For a circle, `v = √(GM/r)`: the closer in, the faster. And the period grows as `r^1.5`
(Kepler's third law), so a low orbit is both faster *and* shorter. Here are four circular
orbits of the same planet:

```bib
let circ = alt => {
  let r = alt + RP
  let vv = Math.sqrt(MU / r)
  return {r, v: vv, T: TAU * r / vv / 3600}
}
let orbits = [
  {name: "Low orbit", alt: 400},
  {name: "Navigation satellites", alt: 20200},
  {name: "Geostationary", alt: 35786},
  {name: "The Moon's distance", alt: 378029},
]
table(["Circular orbit", "Altitude", "Speed", "Once round"],
  orbits.map(o => [o.name, "{o.alt} km", "{round(circ(o.alt).v, 2)} km/s",
    circ(o.alt).T < 48 ? hm(circ(o.alt).T) : "{round(circ(o.alt).T / 24, 1)} days"]),
  widths: [3, 2, 2, 2])
```

Now the chase. Two craft fly a circular orbit 400 km up; the chaser is behind. It fires its
engine once, along or against its motion. Fire *forward* and the extra speed lifts the far
side of its orbit: a bigger ellipse, a longer period, and it falls further behind on every
lap. Fire *backward* and it drops into a smaller, quicker ellipse and gains on every lap.

```bib
let r0 = RP + 400
let v0 = Math.sqrt(MU / r0)
let Tt = TAU * Math.sqrt(r0 * r0 * r0 / MU) / 3600

let dv = 0
let gap0 = 0
let th = 0
row {
  card("The burn") {
    dv = slider("Engine burn (m/s, minus = brake)", -60, 60, value: -20, step: 5)
    gap0 = slider("Chaser starts behind by (degrees)", 2, 30, value: 10, step: 1)
  }
  card("The clock") {
    th = slider("Hours since the burn", 0, 24, value: 4, step: 0.1)
  }
}

let v1 = v0 + dv / 1000
let ac = 1 / (2 / r0 - v1 * v1 / MU)
let ec = abs(r0 / ac - 1)
let offc = dv < 0 ? Math.PI : 0
let Tc = TAU * Math.sqrt(ac * ac * ac / MU) / 3600
let gainRev = 360 * (1 - Tc / Tt)
let chaserAt = t => trueAnom(kep(offc + TAU * t / Tc, ec), ec) - offc
let gapAt = t => gap0 + 360 * t / Tt - chaserAt(t) * DEG
let catchH = gainRev > 0 ? gap0 / gainRev * Tc : 0
let gaps = I.filter(i => i <= 60).map(i => gapAt(24 * i / 60))
let gapNow = gapAt(th)
let chPts = I.filter(i => i <= 60).map(i => {
  let E = TAU * i / 60
  return {r: ac * (1 - ec * Math.cos(E)), t: trueAnom(E, ec) - offc}
})

box(width: fill, height: 340, radius: 12, border: 1, background: "raised") {
  layer {
    let cx = w * 0.3
    let cy = 170
    let R = 340 * 0.4
    let X10 = (r, t) => cx + R * (1 + (r - r0) / r0 * 10) * Math.cos(t)
    let Y10 = (r, t) => cy - R * (1 + (r - r0) / r0 * 10) * Math.sin(t)
    circle(cx, cy, R * RP / r0 * 0.6, fill: fade(success, 0.3))
    circle(cx, cy, R, color: fade(muted, 0.6), width: 1.5)
    for i in I where i < 60 {
      let p = chPts[i]
      let q = chPts[i + 1]
      line(X10(p.r, p.t), Y10(p.r, p.t), X10(q.r, q.t), Y10(q.r, q.t), color: dv < 0 ? accent : warning, width: 2)
    }
    let tt = (gap0 + 360 * th / Tt) / DEG
    let Ec = kep(offc + TAU * th / Tc, ec)
    let rc = ac * (1 - ec * Math.cos(Ec))
    let tc = trueAnom(Ec, ec) - offc
    circle(X10(r0, tt), Y10(r0, tt), 7, fill: ink)
    circle(X10(rc, tc), Y10(rc, tc), 7, fill: dv < 0 ? accent : warning)
    label(X10(r0, tt) + 10, Y10(r0, tt) - 12, "target", size: 12)
    label(X10(rc, tc) - 12, Y10(rc, tc) + 14, "chaser", size: 12, color: dv < 0 ? accent : warning, align: end)
    label(14, h - 16, "height differences drawn 10× larger", size: 11, color: muted)

    // the gap over the day
    let gx = w * 0.62
    let gw = w * 0.35
    let lo = min([-30, ...gaps])
    let hi = max([gap0 + 5, ...gaps])
    let gy = g => 30 + (h - 60) * (hi - g) / (hi - lo)
    line(gx, gy(0), gx + gw, gy(0), color: fade(ink, 0.4), width: 1)
    for i in I where i < 60 {
      line(gx + gw * i / 60, gy(gaps[i]), gx + gw * (i + 1) / 60, gy(gaps[i + 1]), color: dv < 0 ? accent : warning, width: 2)
    }
    line(gx + gw * th / 24, 20, gx + gw * th / 24, h - 30, color: fade(ink, 0.3), width: 1)
    circle(gx + gw * th / 24, gy(gapNow), 5, fill: ink)
    label(gx - 6, gy(0), "0°", size: 11, color: muted, align: end)
    label(gx - 6, gy(gap0), "{gap0}°", size: 11, color: muted, align: end)
    label(gx, 12, "how far behind the chaser is (degrees)", size: 11, color: muted)
    label(gx, h - 16, "0 h", size: 11, color: muted)
    label(gx + gw, h - 16, "24 h", size: 11, color: muted, align: end)
  }
}

grid(columns: 4) {
  stat("Chaser's period", "{round(Tc * 60, 1)} min", tone: dv < 0 ? accent : dv > 0 ? warning : neutral)
  stat("Target's period", "{round(Tt * 60, 1)} min")
  stat("Gained per lap", "{round(gainRev, 2)}°", tone: gainRev > 0 ? success : gainRev < 0 ? danger : neutral)
  stat("Gap now", "{round(gapNow, 1)}°", tone: gapNow <= 0 ? success : neutral)
}

row(fit: true) {
  if dv == 0 { badge("no burn: the gap never changes", tone: muted) }
  else if gainRev > 0 { badge("braking: caught up after about {hm(catchH)}, {round(gap0 / gainRev, 1)} laps", tone: success) }
  else { badge("speeding up made it slower: it loses {round(-gainRev, 2)}° every lap", tone: danger) }
}
```

Braking lowered the chaser's orbit, and the lower orbit is the faster one. To actually
dock it would then burn *forward* to climb back up, once it has caught up.

###### Three

## The Hohmann transfer to Mars

The same trick, on the scale of the Sun. Earth circles at 1 AU, Mars at 1.524 AU. The
cheapest two-burn route is an ellipse that just touches both orbits: leave Earth's
orbit with a forward burn at the ellipse's closest point to the Sun, coast half an
ellipse, and burn forward again at Mars to match its orbit. (These are speeds relative to
each planet's orbit; escaping Earth's own gravity is a separate cost.)

```bib
let MUS = 132712440018
let AU = 149597871
let r1 = 1
let r2 = 1.524
let at = (r1 + r2) / 2
let et = (r2 - r1) / (r2 + r1)
let vE = Math.sqrt(MUS / (r1 * AU))
let vM = Math.sqrt(MUS / (r2 * AU))
let dv1 = vE * (Math.sqrt(2 * r2 / (r1 + r2)) - 1)
let dv2 = vM * (1 - Math.sqrt(2 * r1 / (r1 + r2)))
let YEAR = 365.25
let TE = YEAR
let TM = YEAR * Math.pow(r2, 1.5)
let ttr = YEAR / 2 * Math.pow(at, 1.5)
let lead = 180 - 360 * ttr / TM

let day = slider("Days since leaving Earth", 0, 259, value: 120, step: 1)
let tday = min(day, ttr)

let Et = kep(Math.PI * tday / ttr, et)
let rt = at * (1 - et * Math.cos(Et))
let nut = trueAnom(Et, et)
let thE = TAU * day / TE
let thM = lead / DEG + TAU * day / TM
let tPts = I.filter(i => i <= 40).map(i => {
  let E = Math.PI * i / 40
  return {E, r: at * (1 - et * Math.cos(E)), t: trueAnom(E, et)}
})

box(width: fill, height: 420, radius: 12, border: 1, background: "raised") {
  layer {
    let cx = w / 2
    let cy = 210
    let S = (210 - 26) / r2
    let X = (r, t) => cx + S * r * Math.cos(t)
    let Y = (r, t) => cy - S * r * Math.sin(t)
    circle(cx, cy, S * r1, color: fade(accent, 0.45), width: 1.5)
    circle(cx, cy, S * r2, color: fade(danger, 0.45), width: 1.5)
    for i in I where i < 40 {
      let p = tPts[i]
      let q = tPts[i + 1]
      line(X(p.r, p.t), Y(p.r, p.t), X(q.r, q.t), Y(q.r, q.t),
           color: q.E <= Et + 0.0001 ? warning : fade(warning, 0.35), width: q.E <= Et + 0.0001 ? 3 : 1.5)
    }
    circle(cx, cy, 13, fill: warning)
    // the phase angle at launch
    line(cx, cy, X(r2, lead / DEG), Y(r2, lead / DEG), color: fade(danger, 0.3), width: 1)
    line(cx, cy, X(r2, 0), Y(r2, 0), color: fade(accent, 0.3), width: 1)
    circle(X(r2, lead / DEG), Y(r2, lead / DEG), 4, color: fade(danger, 0.5), width: 1.5)
    // the planets and the craft now
    circle(X(r1, thE), Y(r1, thE), 8, fill: accent)
    circle(X(r2, thM), Y(r2, thM), 7, fill: danger)
    circle(X(rt, nut), Y(rt, nut), 5, fill: ink)
    label(cx, cy + 26, "Sun", size: 12, color: muted, align: center)
    label(X(0.6, lead / 2 / DEG), Y(0.6, lead / 2 / DEG), "{round(lead, 1)}°", size: 12, color: muted, align: center)
    label(X(r1, thE) + 12, Y(r1, thE), "Earth", size: 12, color: accent)
    label(X(r2, thM) + 12, Y(r2, thM), "Mars", size: 12, color: danger)
    label(X(rt, nut) - 10, Y(rt, nut) - 12, "craft", size: 12, align: end)
    label(16, 22, "day {day} of {round(ttr)}", size: 15)
    label(16, h - 18, "open circle: where Mars was at launch", size: 11, color: muted)
  }
}

grid(columns: 5) {
  stat("Burn 1, at Earth", round(dv1, 2), unit: "km/s", tone: accent)
  stat("Burn 2, at Mars", round(dv2, 2), unit: "km/s", tone: danger)
  stat("Total", round(dv1 + dv2, 2), unit: "km/s")
  stat("Transfer time", round(ttr), unit: "days")
  stat("Mars ahead at launch", "{round(lead, 1)}°")
}

kv([
  {label: "Transfer ellipse", value: "a = {round(at, 3)} AU, e = {round(et, 3)}"},
  {label: "Earth's orbital speed", value: "{round(vE, 2)} km/s; at perihelion of the transfer {round(vE + dv1, 2)} km/s"},
  {label: "Craft's distance from the Sun now", value: "{round(rt, 3)} AU, moving {round(Math.sqrt(MUS / AU * (2 / rt - 1 / at)), 2)} km/s"},
  {label: "Mars's orbital speed", value: "{round(vM, 2)} km/s; craft arrives at {round(vM - dv2, 2)} km/s, so it is overtaken"},
])
```

Look at the arrival: the craft reaches Mars's orbit *slower* than Mars, because it has
climbed uphill against the Sun the whole way. The second burn is Mars catching up and
the craft speeding up to match.

###### Four

## The launch window

The transfer takes 259 days, and in that time Mars moves 136° round its orbit. So Mars
must be **44° ahead** of Earth on launch day, or the craft arrives at an empty point in
space. Earth laps Mars every 780 days (the synodic period), so the angle comes round
again only then. Slide the launch date and watch the angle drift.

```bib
let phi0 = 120
let rate = 360 / TM - 360 / TE
let wrap = x => ((x % 360) + 360) % 360
let phaseAt = d => wrap(phi0 + rate * d)
let synodic = 360 / -rate
let first = wrap(phi0 - lead) / -rate
let windows = [0, 1, 2].map(k => first + k * synodic)

let L = slider("Launch day (days from today)", 0, 1600, value: 400, step: 5)
let ph = phaseAt(L)
let miss = ((ph - lead + 540) % 360) - 180
let next = windows.find(d => d >= L - 2.5)

box(width: fill, height: 230, radius: 12, border: 1, background: "raised") {
  layer {
    let x = d => 44 + (w - 60) * d / 1600
    let y = p => 16 + (h - 44) * (1 - p / 360)
    for g in [0, 90, 180, 270, 360] {
      line(44, y(g), w - 16, y(g), color: fade(muted, 0.15))
      label(36, y(g), "{g}°", size: 11, color: muted, align: end)
    }
    for d in windows where d <= 1600 {
      rect(x(d) - 3, 16, 6, h - 44, fill: fade(success, 0.25))
    }
    line(44, y(lead), w - 16, y(lead), color: success, width: 1.5)
    for i in I where i < 120 {
      let a1 = phaseAt(1600 * i / 120)
      let a2 = phaseAt(1600 * (i + 1) / 120)
      if abs(a1 - a2) < 180 { line(x(1600 * i / 120), y(a1), x(1600 * (i + 1) / 120), y(a2), color: danger, width: 2.5) }
    }
    line(x(L), 16, x(L), h - 28, color: fade(ink, 0.35), width: 1)
    circle(x(L), y(ph), 6, fill: ink)
    label(w - 18, y(lead) - 10, "44° ahead: go", size: 11, color: success, align: end)
    label(44, h - 12, "today", size: 11, color: muted)
    label(w - 16, h - 12, "day 1600", size: 11, color: muted, align: end)
    label(50, 26, "how far Mars is ahead of Earth", size: 11, color: muted)
  }
}

let arriveM = (ph + 360 * ttr / TM) / DEG
box(width: fill, height: 260, radius: 12, border: 1, background: "raised") {
  layer {
    let cx = w / 2
    let cy = 130
    let S = (130 - 20) / r2
    let X = (r, t) => cx + S * r * Math.cos(t)
    let Y = (r, t) => cy - S * r * Math.sin(t)
    circle(cx, cy, S * r1, color: fade(accent, 0.4), width: 1)
    circle(cx, cy, S * r2, color: fade(danger, 0.4), width: 1)
    for i in I where i < 40 {
      let p = tPts[i]
      let q = tPts[i + 1]
      line(X(p.r, p.t), Y(p.r, p.t), X(q.r, q.t), Y(q.r, q.t), color: warning, width: 2)
    }
    circle(cx, cy, 9, fill: warning)
    circle(X(r1, 0), Y(r1, 0), 6, fill: accent)
    circle(X(r2, ph / DEG), Y(r2, ph / DEG), 5, color: danger, width: 1.5)
    circle(X(r2, Math.PI), Y(r2, Math.PI), 6, color: ink, width: 1.5)
    circle(X(r2, arriveM), Y(r2, arriveM), 7, fill: danger)
    label(X(r2, arriveM) + 10, Y(r2, arriveM) - 12, "Mars on arrival", size: 11, color: danger)
    label(X(r2, Math.PI) + 10, Y(r2, Math.PI) + 16, "craft arrives here", size: 11)
    label(16, 20, "launch on day {L}", size: 13)
  }
}

grid(columns: 3) {
  stat("Mars ahead at launch", "{round(ph)}°")
  stat("Miss on arrival", abs(miss) < 3 ? "on target" : "{round(abs(miss))}° {miss > 0 ? "ahead" : "behind"}", tone: abs(miss) < 3 ? success : abs(miss) < 15 ? warning : danger)
  stat("Next window", next == null ? "after day 1600" : "day {round(next)}", tone: abs(miss) < 3 ? success : neutral)
}
text("Windows open every {round(synodic)} days: on day {windows.map(d => round(d)).join(", day ")}.", size: sm, color: muted)
```

###### What you just saw

## The one idea underneath

- **Position and velocity fix the orbit.** One speed gives a circle, more an ellipse,
  √2 times as much an escape. The shot slows as it climbs and speeds up as it falls.
- **Vis-viva**, `v² = GM(2/r − 1/a)`, ties speed to height and to the orbit's size.
- **Lower is faster**, so a brake makes you gain and a push makes you lose. Rendezvous
  works backwards from intuition.
- **Hohmann**: two forward burns, 2.95 and 2.65 km/s, half an ellipse, 259 days, and a
  launch only when Mars is 44° ahead, which happens once every 780 days.

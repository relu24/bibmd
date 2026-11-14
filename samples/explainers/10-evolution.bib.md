---
bib: 1
title: Chance and choice in evolution
width: wide
nav: true
---
###### Population genetics, by hand

# Chance and choice in evolution

A new mutation appears in one individual. It makes its carriers a little better at
surviving: one percent more offspring, say. Will it spread through the species?

Usually, no. Most beneficial mutations vanish within a few generations, lost by plain bad
luck before selection ever gets a grip. Evolution is a contest between **choice**
(natural selection, which is systematic) and **chance** (genetic drift, which is not).
This page builds both from the simplest possible model: one gene, two versions of it,
**A** and **a**.

> Selection is a nudge. Drift is a coin toss. Which wins depends on the size of the nudge
> and the size of the population.

```bib
let G = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 93, 94, 95, 96, 97, 98, 99, 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120, 121, 122, 123, 124, 125, 126, 127, 128, 129, 130, 131, 132, 133, 134, 135, 136, 137, 138, 139, 140, 141, 142, 143, 144, 145, 146, 147, 148, 149, 150, 151, 152, 153, 154, 155, 156, 157, 158, 159, 160, 161, 162, 163, 164, 165, 166, 167, 168, 169, 170, 171, 172, 173, 174, 175, 176, 177, 178, 179, 180, 181, 182, 183, 184, 185, 186, 187, 188, 189, 190, 191, 192, 193, 194, 195, 196, 197, 198, 199, 200]
let cAA = accent
let cAa = warning
let caa = danger
fn key(color, s) {
  box(dir: "row", gap: 6, align: center) {
    box(width: 10, height: 10, radius: 5, background: color)
    text(s, size: sm, color: muted)
  }
}
fn pct(x, d: 1) { return "{round(x * 100, d)}%" }
```

###### One

## No change without a cause: Hardy–Weinberg

Every individual carries two copies of the gene, one from each parent. If a fraction `p`
of all copies in the population are **A** and `q = 1 − p` are **a**, and mates pair up at
random, then an offspring draws its two copies independently: **AA** with chance `p²`,
**Aa** with `2pq` (A from mother and a from father, or the other way round) and **aa**
with `q²`. Those three always add up to `(p + q)² = 1`.

The surprise is what happens next: nothing. The offspring pass on A at the same rate `p`,
so the next generation has the same proportions, and the next. Without selection, drift,
migration or mutation, allele frequencies stay put forever.

```bib
let p = slider("p, the share of A copies", 0, 1, value: 0.7, step: 0.01)
let q = 1 - p
let nAA = round(100 * p * p)
let naa = round(100 * q * q)
let nAa = 100 - nAA - naa
let pNext = (2 * nAA + nAa) / 200

box(dir: "row", gap: 16) {
  box(width: 280, height: 280, radius: 12, border: 1, background: "raised") {
    layer {
      for k in G where k < 100 {
        let col = k % 10
        let rowi = floor(k / 10)
        let x = 14 + col * 25.5
        let y = 14 + rowi * 25.5
        let g = k < nAA ? 2 : k < nAA + nAa ? 1 : 0
        rect(x, y, 22, 22, fill: fade(g == 2 ? cAA : g == 1 ? cAa : caa, 0.16), radius: 5)
        circle(x + 6.5, y + 11, 4.5, fill: g >= 1 ? cAA : caa)
        circle(x + 15.5, y + 11, 4.5, fill: g == 2 ? cAA : caa)
      }
    }
  }
  box(width: fill, height: 280, radius: 12, border: 1, background: "raised") {
    layer {
      let X = x => 20 + (w - 40) * x
      let Y = v => 260 - 240 * v
      for v in [0, 0.25, 0.5, 0.75, 1] {
        line(X(0), Y(v), X(1), Y(v), color: fade(muted, 0.15))
        label(X(0) + 2, Y(v) - 8, "{v * 100}%", size: 11, color: muted)
      }
      for i in G where i < 60 {
        let a = i / 60
        let b = (i + 1) / 60
        line(X(a), Y(a * a), X(b), Y(b * b), color: cAA, width: 2.5)
        line(X(a), Y(2 * a * (1 - a)), X(b), Y(2 * b * (1 - b)), color: cAa, width: 2.5)
        line(X(a), Y((1 - a) * (1 - a)), X(b), Y((1 - b) * (1 - b)), color: caa, width: 2.5)
      }
      line(X(p), Y(0), X(p), Y(1), color: fade(ink, 0.35), width: 1)
      circle(X(p), Y(p * p), 6, fill: cAA)
      circle(X(p), Y(2 * p * q), 6, fill: cAa)
      circle(X(p), Y(q * q), 6, fill: caa)
      label(X(p) + 10, Y(p * p), "AA", size: 11, color: cAA)
      label(X(p) + 10, Y(2 * p * q), "Aa", size: 11, color: cAa)
      label(X(p) + 10, Y(q * q), "aa", size: 11, color: caa)
    }
  }
}
box(dir: "row", gap: 18, wrap: true) {
  key(cAA, "AA: two A copies")
  key(cAa, "Aa: one of each")
  key(caa, "aa: two a copies")
  key(muted, "left: 100 individuals, two dots each · right: each genotype's share as p runs from 0 to 1")
}

grid(columns: 4) {
  stat("AA = p²", pct(p * p), tone: accent)
  stat("Aa = 2pq", pct(2 * p * q), tone: warning)
  stat("aa = q²", pct(q * q), tone: danger)
  stat("A in the next generation", pct(pNext), tone: success)
}
text("Count the A dots: {2 * nAA} in the AA squares plus {nAa} in the Aa squares, out of 200 dots, is {pct(pNext)}: the same p you started with (up to rounding to 100 individuals). Heterozygotes are most common at p = 50%, where they are half of everyone.", size: sm, color: muted)
```

###### Two

## Choice: natural selection

Now let the genotypes differ in **fitness**: the average number of offspring an
individual leaves, relative to the others. After selection, the share of A copies becomes

`p′ = (p²·w_AA + pq·w_Aa) / W`, with `W = p²·w_AA + 2pq·w_Aa + q²·w_aa`

That is all of it: one line, applied generation after generation. Small differences in
fitness compound like interest.

```bib
let preset = choice("Fitness from", ["The sliders below", "A is dominant and helps", "A is recessive and helps", "Heterozygote advantage", "Heterozygote disadvantage", "Neutral"])
let sAA = 0
let sAa = 0
let saa = 0
let p0 = 0
row {
  card("Fitness of each genotype", subtitle: preset == "The sliders below" ? "used as set" : "overridden by the preset above") {
    sAA = slider("w(AA)", 0.5, 1.2, value: 1.05, step: 0.01, id: "fit-AA")
    sAa = slider("w(Aa)", 0.5, 1.2, value: 1.025, step: 0.005, id: "fit-het")
    saa = slider("w(aa)", 0.5, 1.2, value: 1, step: 0.01, id: "fit-aa-recessive")
  }
  card("Start") {
    p0 = slider("Starting share of A", 0.01, 0.99, value: 0.05, step: 0.01)
  }
}
let fit = preset == "A is dominant and helps" ? [1.05, 1.05, 1]
  : preset == "A is recessive and helps" ? [1.05, 1, 1]
  : preset == "Heterozygote advantage" ? [0.9, 1, 0.8]
  : preset == "Heterozygote disadvantage" ? [1, 0.9, 1]
  : preset == "Neutral" ? [1, 1, 1]
  : [sAA, sAa, saa]
let wAA = fit[0]
let wAa = fit[1]
let waa = fit[2]

let traj = G.slice(1).reduce((acc, g) => {
  let pp = acc[acc.length - 1]
  let qq = 1 - pp
  let wbar = pp * pp * wAA + 2 * pp * qq * wAa + qq * qq * waa
  return [...acc, (pp * pp * wAA + pp * qq * wAa) / wbar]
}, [p0])
let reach = traj.findIndex(x => x >= 0.99)
let denom = 2 * wAa - wAA - waa
let pStar = denom != 0 ? (wAa - waa) / denom : -1
let poly = pStar > 0 && pStar < 1 && wAa > wAA && wAa > waa

box(width: fill, height: 260, radius: 12, border: 1, background: "raised") {
  layer {
    let X = g => 40 + (w - 56) * g / 200
    let Y = v => 236 - 216 * v
    for v in [0, 0.25, 0.5, 0.75, 1] {
      line(X(0), Y(v), X(200), Y(v), color: fade(muted, 0.15))
      label(34, Y(v), "{v * 100}%", size: 11, color: muted, align: end)
    }
    for g in [50, 100, 150, 200] {
      line(X(g), Y(0), X(g), Y(1), color: fade(muted, 0.1))
      label(X(g), 249, "{g}", size: 11, color: muted, align: center)
    }
    if poly { line(X(0), Y(pStar), X(200), Y(pStar), color: fade(success, 0.7), width: 1.5) }
    for i in G where i < 200 {
      line(X(i), Y(traj[i]), X(i + 1), Y(traj[i + 1]), color: accent, width: 2.5)
    }
    circle(X(0), Y(p0), 5, fill: accent)
    circle(X(200), Y(traj[200]), 5, fill: accent)
  }
}
box(dir: "row", gap: 18, wrap: true) {
  key(accent, "share of A, generation 0 → 200")
  if poly { key(success, "the balance point p* = {pct(pStar)}") }
}

grid(columns: 4) {
  stat("Fitness used", "{wAA} · {wAa} · {waa}")
  stat("After 50 generations", pct(traj[50]))
  stat("After 200", pct(traj[200]), tone: accent)
  stat("Reaches 99%", reach < 0 ? "not in 200" : "generation {reach}", tone: reach < 0 ? neutral : success)
}
row(fit: true) {
  if poly { badge("heterozygote advantage: both alleles are kept, near {pct(pStar)} A", tone: success) }
  else if wAA == wAa && wAa == waa { badge("equal fitness: Hardy–Weinberg, nothing moves", tone: muted) }
  else if traj[200] > p0 { badge("selection favours A", tone: accent) }
  else { badge("selection favours a", tone: danger) }
}
```

Try *A is recessive and helps*: the same 5% advantage as the dominant case, but while A is
rare it hides in heterozygotes, where it does nothing, so the curve crawls for a hundred
generations before it takes off. Try *heterozygote advantage*: neither allele wins, and the
population settles where `p* = (w_Aa − w_aa) / (2w_Aa − w_AA − w_aa)`.

###### Three

## Chance: genetic drift

Selection treated the population as infinite. Real populations are finite: each generation
is a *sample* of `2N` gene copies drawn from the last, and samples wobble. Here are eight
populations, all starting at 50% A, with **no selection at all**. Each generation draws its
`2N` copies at random (the Wright–Fisher model).

```bib
let N = 0
let seed = 0
row {
  card("Population") { N = slider("N, individuals", 5, 500, value: 50, step: 5) }
  card("Dice") { seed = slider("Random seed", 1, 60, value: 7, step: 1) }
}
let M = 2147483647
let gens = G.slice(0, 100)
let reps = [0, 1, 2, 3, 4, 5, 6, 7]
let runs = reps.map(r => gens.reduce((acc, g) => {
  // two uniform draws from a Park–Miller generator, turned into a normal draw (Box–Muller)
  let s1 = (acc.s * 48271) % M
  let s2 = (s1 * 48271) % M
  let z = Math.sqrt(-2 * Math.log(s1 / M)) * Math.cos(2 * Math.PI * s2 / M)
  // the binomial count of A among 2N copies, approximated by a normal draw and clamped
  let k = Math.max(0, Math.min(2 * N, Math.round(2 * N * acc.p + z * Math.sqrt(2 * N * acc.p * (1 - acc.p)))))
  return {s: s2, p: k / (2 * N), path: [...acc.path, k / (2 * N)]}
}, {s: seed * 7919 + r * 104729 + 1, p: 0.5, path: [0.5]}))
let fixed = runs.filter(x => x.p == 1).length
let lost = runs.filter(x => x.p == 0).length
let hetSim = sum(runs.map(x => 2 * x.p * (1 - x.p))) / 8
let hetTheory = 0.5 * Math.pow(1 - 1 / (2 * N), 100)
let tones = [accent, warning, danger, success, "#8B6FD6", "#3BA7A0", "#C2588F", muted]

box(width: fill, height: 280, radius: 12, border: 1, background: "raised") {
  layer {
    let X = g => 40 + (w - 56) * g / 100
    let Y = v => 256 - 236 * v
    for v in [0, 0.25, 0.5, 0.75, 1] {
      line(X(0), Y(v), X(100), Y(v), color: fade(muted, v == 0.5 ? 0.35 : 0.15))
      label(34, Y(v), "{v * 100}%", size: 11, color: muted, align: end)
    }
    label(X(100), 270, "generation 100", size: 11, color: muted, align: end)
    for r in reps {
      let path = runs[r].path
      for i in gens {
        line(X(i), Y(path[i]), X(i + 1), Y(path[i + 1]), color: tones[r], width: 2)
      }
      circle(X(100), Y(path[100]), 4, fill: tones[r])
    }
  }
}
box(dir: "row", gap: 18, wrap: true) {
  text("share of A in eight populations, generation 0 → 100 · gridlines at 0, 25, 50, 75, 100%", size: sm, color: muted)
}

grid(columns: 4) {
  stat("Fixed at 100% A", fixed, tone: fixed > 0 ? accent : neutral)
  stat("Lost (0% A)", lost, tone: lost > 0 ? danger : neutral)
  stat("Heterozygosity left", pct(hetSim), unit: "simulated")
  stat("Expected", pct(hetTheory), unit: "0.5 × (1 − 1/2N)¹⁰⁰")
}
text("Each generation's binomial draw of 2N copies is approximated by a normal draw (Box–Muller from a seeded Park–Miller generator) with the binomial's mean and variance, clamped to 0 and 2N; a count that reaches 0 or 2N stays there.", size: sm, color: muted)
```

With five individuals, most lines hit 0% or 100% within a few dozen generations: one allele
is gone for good, by chance alone. With five hundred they barely leave the middle. Drift
eats variation at a rate of `1/(2N)` per generation, so its strength is set by population
size.

###### Four

## The race: selection against drift

Back to the new mutation. It starts as a single copy, frequency `1/(2N)`. A neutral
mutation fixes with exactly that chance: every copy in the population is equally likely to
become the ancestor of all future copies. A beneficial one with advantage `s` fixes with
the chance worked out by Motoo Kimura,

`u = (1 − e^(−2s)) / (1 − e^(−4Ns))`

which is about **2s** once `4Ns` is well above 1, regardless of N. A 1% advantage gives a
2% chance: the mutation is lost 98 times in 100. When `4Ns` is below 1, `u` is about
`1/(2N)`: selection is invisible and the mutation behaves as if neutral.

```bib
let sp = 0
let lg = 0
row {
  card("Advantage") { sp = slider("s, the advantage (%)", 0.1, 10, value: 1, step: 0.1) }
  card("Population") { lg = slider("Population size N (log scale)", 1, 4, value: 2, step: 0.05) }
}
let s = sp / 100
let NN = round(Math.pow(10, lg))
let kim = (n, ss) => (1 - Math.exp(-2 * ss)) / (1 - Math.exp(-4 * n * ss))
let u = kim(NN, s)
let neutral = 1 / (2 * NN)

box(width: fill, height: 260, radius: 12, border: 1, background: "raised") {
  layer {
    // x: log10 N from 1 to 4; y: log10 of the probability from -5 to 0
    let X = l => 50 + (w - 66) * (l - 1) / 3
    let Y = pr => 20 + 216 * (-Math.log10(pr)) / 5
    let ticks = ["100%", "10%", "1%", "0.1%", "0.01%", "0.001%"]
    for d in [0, 1, 2, 3, 4, 5] {
      line(X(1), 20 + 216 * d / 5, X(4), 20 + 216 * d / 5, color: fade(muted, 0.15))
      label(44, 20 + 216 * d / 5, ticks[d], size: 10, color: muted, align: end)
    }
    let nlab = ["N = 10", "N = 100", "N = 1,000", "N = 10,000"]
    for l in [1, 2, 3, 4] {
      line(X(l), 20, X(l), 236, color: fade(muted, 0.1))
      label(X(l), 248, nlab[l - 1], size: 11, color: muted, align: l == 1 ? start : l == 4 ? end : center)
    }
    line(X(1), Y(2 * s), X(4), Y(2 * s), color: fade(success, 0.8), width: 1.5)
    label(X(4) - 4, Y(2 * s) - 9, "2s", size: 11, color: success, align: end)
    for i in G where i < 60 {
      let l1 = 1 + 3 * i / 60
      let l2 = 1 + 3 * (i + 1) / 60
      line(X(l1), Y(1 / (2 * Math.pow(10, l1))), X(l2), Y(1 / (2 * Math.pow(10, l2))), color: muted, width: 2)
      line(X(l1), Y(kim(Math.pow(10, l1), s)), X(l2), Y(kim(Math.pow(10, l2), s)), color: accent, width: 3)
    }
    line(X(lg), 20, X(lg), 236, color: fade(ink, 0.3), width: 1)
    circle(X(lg), Y(u), 6, fill: accent)
    circle(X(lg), Y(neutral), 5, fill: muted)
  }
}
box(dir: "row", gap: 18, wrap: true) {
  key(accent, "chance a single beneficial copy fixes")
  key(muted, "a neutral copy: 1/2N")
  key(success, "2s")
}

grid(columns: 4) {
  stat("N", NN)
  stat("4Ns", round(4 * NN * s, 2), tone: 4 * NN * s < 1 ? warning : success)
  stat("Fixation chance u", pct(u, d: 2), tone: accent)
  stat("Neutral 1/2N", pct(neutral, d: 3))
}
row(fit: true) {
  if 4 * NN * s < 1 { badge("drift wins: selection this weak is invisible in a population this small", tone: warning) }
  else { badge("selection wins on average, yet {round(1000 * (1 - u))} of 1000 such mutations are still lost", tone: success) }
}
text("Advantage over a neutral mutation: {round(u / neutral, 1)} times as likely to fix. With 2s = {pct(2 * s, d: 2)}, the approximation is off by {pct(abs(u - 2 * s) / u)} here.", size: sm, color: muted)
```

###### What you just saw

## Chance and choice

- **Hardy–Weinberg:** random mating turns allele shares into genotype shares `p²`, `2pq`,
  `q²`, and on its own changes nothing.
- **Selection** is deterministic: one line, `p′ = (p²w_AA + pqw_Aa)/W`, repeated. Dominance
  decides how fast; heterozygote advantage keeps both alleles.
- **Drift** is sampling error, of size `1/(2N)` per generation. Small populations lose
  variation fast, whatever its worth.
- **The race:** a beneficial mutation fixes with chance about `2s`, a neutral one with
  `1/(2N)`. Selection can only steer what drift lets survive, and only when `4Ns` is
  well above 1.

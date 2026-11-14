---
bib: 1
title: The same votes, four different winners
width: wide
nav: true
data:
  cands:
    - { id: A, name: Amber, color: "#D38A12" }
    - { id: B, name: Blue, color: "#3F78DA" }
    - { id: C, name: Coral, color: "#D8506A" }
    - { id: D, name: Dusk, color: "#8462CF" }
    - { id: E, name: Emerald, color: "#2A9A70" }
  presets:
    - key: five
      label: Five rules, five winners
      blocs:
        - { size: 31, rank: [E, B, D, A, C] }
        - { size: 21, rank: [A, D, B, C, E] }
        - { size: 18, rank: [D, B, C, A, E] }
        - { size: 16, rank: [C, E, A, D, B] }
        - { size: 14, rank: [B, C, D, A, E] }
    - key: spoiler
      label: The spoiler town
      blocs:
        - { size: 38, rank: [A, D, B, C, E] }
        - { size: 30, rank: [B, C, D, A, E] }
        - { size: 20, rank: [C, B, D, A, E] }
        - { size: 7, rank: [D, A, B, C, E] }
        - { size: 5, rank: [E, B, C, D, A] }
    - key: cycle
      label: A Condorcet cycle
      blocs:
        - { size: 30, rank: [A, B, C, D, E] }
        - { size: 26, rank: [B, C, A, E, D] }
        - { size: 22, rank: [C, A, B, E, D] }
        - { size: 12, rank: [D, C, A, B, E] }
        - { size: 10, rank: [E, B, A, C, D] }
---
###### An explainer about counting

# The same votes, four different winners

Ninety-nine percent of an election is the voters. The last percent is the rule that turns their
ballots into one name, and it is not neutral. Below is one town of 100 voters in five blocs, each
ranking five candidates. Count their ballots five reasonable ways and you can get five different
mayors.

```bib
// ---------- the counting, shared by every block below ----------
let ids = ["A", "B", "C", "D", "E"]
let cand = id => data.cands.find(c => c.id == id)
let nameOf = id => cand(id).name
let colorOf = id => cand(id).color
let ord = id => ids.indexOf(id)

let ballot = (rank, cs) => rank.filter(c => cs.includes(c))
let totalOf = bs => bs.sum(b => b.size)
// first choices, biggest pile first; a tie goes to the earlier letter
let firstsOf = (bs, cs) => cs
  .map(c => ({ c, v: bs.filter(b => ballot(b.rank, cs)[0] == c).sum(b => b.size) }))
  .sort((x, y) => y.v - x.v != 0 ? y.v - x.v : ord(x.c) - ord(y.c))
// voters ranking x above y
let preferOf = (bs, x, y) => bs.filter(b => b.rank.indexOf(x) < b.rank.indexOf(y)).sum(b => b.size)

let pluralityOf = (bs, cs) => firstsOf(bs, cs)[0].c

let runoffOf = (bs, cs) => {
  let t = firstsOf(bs, cs)
  let x = t[0].c
  let y = cs.length > 1 ? t[1].c : null
  let outright = t[0].v * 2 > totalOf(bs) || cs.length == 1
  let vx = outright ? t[0].v : preferOf(bs, x, y)
  let vy = outright ? 0 : preferOf(bs, y, x)
  return { outright, x, y, vx, vy, winner: outright || vx > vy || (vx == vy && ord(x) < ord(y)) ? x : y, t }
}

// one round of instant runoff; the later letter goes first on a tie for last
let irvStep = (bs, st) => {
  let t = st.done ? [] : firstsOf(bs, st.left)
  let wins = !st.done && (t[0].v * 2 > totalOf(bs) || st.left.length == 1)
  let out = st.done || wins ? null : t[t.length - 1].c
  return st.done ? st
    : wins ? { ...st, done: true, winner: t[0].c, rounds: [...st.rounds, { t, out: null }] }
    : { ...st, left: st.left.filter(c => c != out), rounds: [...st.rounds, { t, out }] }
}
let irvOf = (bs, cs) => [0, 1, 2, 3, 4].reduce((st, k) => irvStep(bs, st), { left: cs, rounds: [], done: false, winner: null })

let bordaOf = (bs, cs) => cs
  .map(c => ({ c, v: bs.sum(b => b.size * (cs.length - 1 - ballot(b.rank, cs).indexOf(c))) }))
  .sort((x, y) => y.v - x.v != 0 ? y.v - x.v : ord(x.c) - ord(y.c))

let beatsOf = (bs, x, y) => preferOf(bs, x, y) > preferOf(bs, y, x)
let condorcetOf = (bs, cs) => cs.find(x => cs.every(y => y == x || beatsOf(bs, x, y))) ?? null
let cycleOf = (bs, cs) => {
  let found = cs.flatMap(x => cs.flatMap(y => cs
    .filter(z => ord(x) < ord(y) && ord(x) < ord(z) && y != z && beatsOf(bs, x, y) && beatsOf(bs, y, z) && beatsOf(bs, z, x))
    .map(z => [x, y, z])))
  return found.length > 0 ? found[0] : null
}

// ---------- pieces ----------
fn chip(id, out: false) {
  box(width: 28, height: 26, radius: 7, align: center, justify: center, background: colorOf(id), opacity: out ? 0.25 : 1) {
    text(id, size: sm, weight: bold, color: "#FFFFFF")
  }
}

fn hbar(id, v, top, half: 0) {
  box(dir: "row", gap: 10, align: center) {
    box(dir: "row", gap: 6, width: 110, align: center) { chip(id); text(nameOf(id), size: sm) }
    box(width: fill, height: 20, radius: 6, background: fade(muted, 0.14)) {
      layer {
        rect(0, 0, top > 0 ? w * v / top : 0, h, fill: colorOf(id), radius: 6)
        if half > 0 { line(w * half / top, 0, w * half / top, h, color: ink, width: 2) }
      }
    }
    box(width: 44) { text(str(v), weight: bold, face: mono) }
  }
}

fn tile(method, winner) {
  box(width: fill, padding: [8, 10], radius: 10, background: winner == null ? fade(muted, 0.18) : colorOf(winner)) {
    text(method, size: sm, caps: true, color: winner == null ? muted : "#FFFFFF")
    text(winner == null ? "nobody" : nameOf(winner), size: lg, weight: bold, color: winner == null ? ink : "#FFFFFF")
  }
}
```

###### 1 · The electorate

## Five blocs, one ranked ballot each

Every voter in a bloc casts the same ranking, best first. Move a slider to grow or shrink a bloc;
untick a candidate to take them off the ballot (their name is then skipped on every ranking).
Picking a town resets its blocs.

```bib
let town = choice("Town", data.presets.map(p => p.label))
let preset = data.presets.find(p => p.label == town)

let allNames = data.cands.map(c => c.name)
let sizes = []
let onBallot = value("On the ballot") ?? allNames
box(gap: 6, padding: 18, radius: 12, border: 1, background: "raised") {
  for b in preset.blocs {
    box(dir: "row", gap: 14, align: center) {
      box(width: 64) { text("Bloc {index + 1}", size: sm, weight: bold) }
      box(dir: "row", gap: 4, align: center, width: 200) {
        for c in b.rank { chip(c, out: !onBallot.includes(nameOf(c))) }
      }
      box(width: fill) {
        sizes = [...sizes, slider("Bloc {index + 1} voters", 0, 50, value: b.size, step: 1, id: "{preset.key}-{index}")]
      }
    }
  }
  divider()
  onBallot = checklist("On the ballot", allNames, value: allNames)
}

let blocs = preset.blocs.map((b, i) => ({ ...b, size: sizes[i] }))
let cs = ids.filter(c => onBallot.includes(nameOf(c)))
let n = totalOf(blocs)
let ok = cs.length >= 2 && n > 0

let P = ok ? pluralityOf(blocs, cs) : null
let R = ok ? runoffOf(blocs, cs) : null
let I = ok ? irvOf(blocs, cs) : null
let B = ok ? bordaOf(blocs, cs) : null
let K = ok ? condorcetOf(blocs, cs) : null
let cyc = ok && K == null ? cycleOf(blocs, cs) : null
let winners = ok ? [P, R.winner, I.winner, B[0].c, K] : []
let distinct = unique(winners.filter(x => x != null)).length

if !ok { badge("Keep at least two candidates and some voters", tone: warning) }
else {
  text("{n} voters · a majority is more than {n / 2}", size: sm, color: muted)
  box(dir: "row", gap: 8) {
    tile("Plurality", P)
    tile("Runoff", R.winner)
    tile("Instant runoff", I.winner)
    tile("Borda", B[0].c)
    tile("Condorcet", K)
  }
  text("Same ballots, {distinct} different winner{distinct == 1 ? "" : "s"}{K == null ? " · no Condorcet winner" : ""}", weight: bold, color: accent)
}
```

###### 2 · Plurality and the runoff

## Count first choices, then maybe count again

**Plurality** looks only at first choices: the biggest pile wins, majority or not. A
**two-round runoff** keeps the two biggest piles and asks everyone again, so each ballot goes to
whichever of the two finalists it ranks higher. A candidate with a majority in round one wins
outright. The thin line marks half the voters.

```bib
if ok {
  row {
    card("Round one: first choices") {
      for t in R.t { hbar(t.c, t.v, n, half: n / 2) }
      let top = R.t[0].v
      markdown(top * 2 > n
        ? "**{nameOf(P)}** wins plurality with {top} of {n}, a true majority."
        : "**{nameOf(P)}** wins plurality with {top} of {n} ({round(100 * top / n)}%): most voters, {n - top}, put someone else first.")
    }
    card("Round two: the top two") {
      if R.outright {
        hbar(R.x, R.vx, n, half: n / 2)
        markdown("**{nameOf(R.x)}** has a majority in round one: no second round.")
      } else {
        hbar(R.x, R.vx, n, half: n / 2)
        hbar(R.y, R.vy, n, half: n / 2)
        markdown("**{nameOf(R.x)}** and **{nameOf(R.y)}** go through. The {n - R.t[0].v - R.t[1].v} voters whose favourite is out pick between them: **{nameOf(R.winner)}** wins {max([R.vx, R.vy])}–{min([R.vx, R.vy])}.")
      }
    }
  }
}
```

###### 3 · Instant runoff

## Eliminate the weakest, one at a time

**Instant-runoff voting** runs the runoff many times without asking again. Each round, the
candidate with the fewest first choices is eliminated, and their ballots move to the next name
still standing. Repeat until someone holds a majority. The two-round runoff throws away everyone
but two at once; instant runoff does it gently, and the order of elimination matters.

```bib
if ok {
  box(dir: "row", gap: 10) {
    for r in I.rounds {
      box(width: fill, gap: 6, padding: 12, radius: 10, border: 1, background: "raised") {
        text("Round {index + 1}", size: sm, caps: true, weight: bold, color: muted)
        for t in r.t {
          box(dir: "row", gap: 6, align: center, opacity: t.c == r.out ? 0.35 : 1) {
            chip(t.c)
            box(width: fill, height: 10, radius: 5) {
              layer { rect(0, 0, w * t.v / n, h, fill: colorOf(t.c), radius: 5) }
            }
            box(width: 26) { text(str(t.v), size: sm, face: mono, weight: bold) }
          }
        }
        text(r.out != null ? "{nameOf(r.out)} has the fewest ({r.t[r.t.length - 1].v}): out, ballots move on."
                           : "{nameOf(r.t[0].c)} has {r.t[0].v} of {n}: a majority.", size: sm, color: muted)
      }
    }
  }
  let outs = I.rounds.filter(r => r.out != null).map(r => nameOf(r.out))
  markdown("**{nameOf(I.winner)}** wins instant runoff after {outs.length} elimination{outs.length == 1 ? "" : "s"}: {outs.join(", then ")}.")
}
```

###### 4 · Borda count

## Points for every place, not just first

The **Borda count** reads the whole ballot. With *n* candidates, a first place earns *n*−1
points, second *n*−2, down to zero for last. A candidate nobody loves but nobody hates (many
second places) can outscore a polarising favourite.

```bib
if ok {
  box(gap: 6, padding: 18, radius: 12, border: 1, background: "raised") {
    for t in B { hbar(t.c, t.v, B[0].v) }
    text("{cs.length} candidates: {cs.map((c, i) => str(cs.length - 1 - i)).join(", ")} points for 1st to last place. Most possible: {n * (cs.length - 1)}.", size: sm, color: muted)
    let firstsW = R.t.find(t => t.c == B[0].c).v
    markdown("**{nameOf(B[0].c)}** wins Borda with {B[0].v} points{B[0].c != P ? ", though only {firstsW} voters put them first." : "."}")
  }
}
```

###### 5 · Condorcet

## Everyone against everyone

The Marquis de Condorcet proposed the most demanding test: hold a head-to-head race between every
pair. A candidate who beats each rival one-on-one is the **Condorcet winner**, and arguably the
people's choice. The catch, which he found himself: majorities need not be transitive. A can beat
B, B beat C, and C beat A, a rock-paper-scissors with no winner at all.

```bib
let pos = i => [0.5 + 0.36 * Math.cos(-Math.PI / 2 + i * 2 * Math.PI / 5), 0.5 + 0.40 * Math.sin(-Math.PI / 2 + i * 2 * Math.PI / 5)]
let gw = 340
let gh = 280
let inCycle = (x, y) => cyc != null && ((cyc[0] == x && cyc[1] == y) || (cyc[1] == x && cyc[2] == y) || (cyc[2] == x && cyc[0] == y))

if ok {
  row {
    card("Row beats column by …") {
      table(["", ...cs],
        cs.map(x => [x, ...cs.map(y => x == y ? "—"
          : { badge: str(preferOf(blocs, x, y)), tone: beatsOf(blocs, x, y) ? success : beatsOf(blocs, y, x) ? danger : muted })]))
    }
    card("Who beats whom", subtitle: "an arrow points from each duel's winner to its loser") {
      box(width: gw, height: gh) {
        layer {
          for x in cs {
            for y in cs where x != y && beatsOf(blocs, x, y) {
              let a = pos(ord(x))
              let b = pos(ord(y))
              let x1 = a[0] * gw
              let y1 = a[1] * gh
              let x2 = b[0] * gw
              let y2 = b[1] * gh
              let L = Math.hypot(x2 - x1, y2 - y1)
              let ux = (x2 - x1) / L
              let uy = (y2 - y1) / L
              let hot = inCycle(x, y)
              let col = cyc != null && !hot ? fade(colorOf(x), 0.3) : colorOf(x)
              let tx = x2 - ux * 24
              let ty = y2 - uy * 24
              line(x1 + ux * 22, y1 + uy * 22, tx, ty, color: col, width: hot ? 4 : 1.8)
              line(tx, ty, tx - ux * 10 - uy * 6, ty - uy * 10 + ux * 6, color: col, width: hot ? 4 : 2)
              line(tx, ty, tx - ux * 10 + uy * 6, ty - uy * 10 - ux * 6, color: col, width: hot ? 4 : 2)
            }
          }
          for c in ids {
            let p = pos(ord(c))
            let on = cs.includes(c)
            if K == c { circle(p[0] * gw, p[1] * gh, 29, fill: fade(colorOf(c), 0.3)) }
            circle(p[0] * gw, p[1] * gh, 21, fill: on ? colorOf(c) : fade(colorOf(c), 0.15))
            label(p[0] * gw, p[1] * gh, c, size: 15, color: on ? "#FFFFFF" : colorOf(c), align: center)
          }
        }
      }
    }
  }
  if K != null {
    markdown("**{nameOf(K)}** beats every rival head to head: the Condorcet winner. " +
      cs.filter(y => y != K).map(y => "{preferOf(blocs, K, y)}–{preferOf(blocs, y, K)} over {nameOf(y)}").join(", ") + ".")
  } else if cyc != null {
    let m = (x, y) => "{preferOf(blocs, x, y)}–{preferOf(blocs, y, x)}"
    markdown("No Condorcet winner. **{nameOf(cyc[0])}** beats **{nameOf(cyc[1])}** ({m(cyc[0], cyc[1])}), **{nameOf(cyc[1])}** beats **{nameOf(cyc[2])}** ({m(cyc[1], cyc[2])}), and **{nameOf(cyc[2])}** beats **{nameOf(cyc[0])}** ({m(cyc[2], cyc[0])}). The majority goes round in a circle.")
  } else {
    markdown("No Condorcet winner: some head-to-head races are tied.")
  }
}
```

###### 6 · The spoiler

## A candidate who loses can still decide who wins

In the spoiler town, Amber leads on first choices with 38, but Blue and Coral share a camp: 55
voters prefer Blue to Amber. Under plurality, Coral's 20 votes split that camp and Amber wins.
Take Coral off the ballot, a candidate who was never going to win, and the winner changes. Here
are both ballots side by side, at the spoiler town's starting sizes (pick it above and untick
Coral to see the same thing in every rule).

```bib
let sp = data.presets.find(p => p.key == "spoiler").blocs
let withC = ["A", "B", "C", "D", "E"]
let withoutC = ["A", "B", "D", "E"]
let spN = totalOf(sp)

row {
  card("Coral runs", badge: "plurality: {nameOf(pluralityOf(sp, withC))}", badgeTone: accent) {
    for t in firstsOf(sp, withC) { hbar(t.c, t.v, spN, half: spN / 2) }
  }
  card("Coral withdraws", badge: "plurality: {nameOf(pluralityOf(sp, withoutC))}", badgeTone: accent) {
    for t in firstsOf(sp, withoutC) { hbar(t.c, t.v, spN, half: spN / 2) }
  }
}
text("Coral's 20 voters moved to their second choice, Blue. A losing candidate decided the race.", weight: bold)
```

In the first town every candidate wins under some rule, so any withdrawal is felt. Take Amber off
the ballot (the runoff is the only rule it wins) and Dusk wins four rules out of five. Methods that
read the whole ranking feel it too: Borda's points depend on how many candidates there are to rank.

###### What you just saw

## There is no perfect rule

The voters never changed. Plurality rewards the biggest bloc, the runoff the strongest finalist,
instant runoff the survivor of a particular elimination order, Borda the broadly acceptable,
Condorcet the one who wins every duel, when such a candidate exists. Each is a defensible reading
of "what the town wants".

> In 1951 Kenneth Arrow proved that this is not a failure of imagination. With three or more
> candidates, no rule that turns ranked ballots into a ranking of society can satisfy all of these
> at once: **unanimity** (if every voter prefers X to Y, so does the result), **independence of
> irrelevant alternatives** (whether X beats Y depends only on how voters rank X against Y, so no
> spoilers), and **no dictator** (the result is not simply one voter's ballot). Something has to
> give. Choosing a voting rule is choosing which flaw you will live with.

Ties on this page are broken alphabetically: the earlier letter wins, and in instant runoff the
later letter is eliminated first.

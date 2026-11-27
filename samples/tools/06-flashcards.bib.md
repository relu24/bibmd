---
bib: 1
title: The elements
width: wide
data:
  # General knowledge: twenty elements, their symbol, atomic number, family and one fact.
  cards:
    - { sym: H, name: Hydrogen, z: 1, cat: nonmetal, fact: "The lightest element: about three quarters of the ordinary matter in the universe by mass." }
    - { sym: He, name: Helium, z: 2, cat: noble gas, fact: "Found in the Sun's spectrum before it was found on Earth." }
    - { sym: Li, name: Lithium, z: 3, cat: alkali metal, fact: "The lightest metal; it carries the charge in most rechargeable batteries." }
    - { sym: C, name: Carbon, z: 6, cat: nonmetal, fact: "Diamond and graphite are both pure carbon; it is the basis of organic chemistry." }
    - { sym: N, name: Nitrogen, z: 7, cat: nonmetal, fact: "About 78% of the air you breathe." }
    - { sym: O, name: Oxygen, z: 8, cat: nonmetal, fact: "The most abundant element in Earth's crust by mass." }
    - { sym: Na, name: Sodium, z: 11, cat: alkali metal, fact: "Its symbol comes from the Latin natrium; it reacts violently with water." }
    - { sym: Al, name: Aluminium, z: 13, cat: post-transition metal, fact: "The most abundant metal in Earth's crust." }
    - { sym: Si, name: Silicon, z: 14, cat: metalloid, fact: "The semiconductor most computer chips are made of." }
    - { sym: S, name: Sulfur, z: 16, cat: nonmetal, fact: "A yellow solid, called brimstone in older texts." }
    - { sym: Cl, name: Chlorine, z: 17, cat: halogen, fact: "A yellow-green gas, used to disinfect drinking water." }
    - { sym: K, name: Potassium, z: 19, cat: alkali metal, fact: "Its symbol comes from the Latin kalium." }
    - { sym: Ca, name: Calcium, z: 20, cat: alkaline earth metal, fact: "Bones and teeth are built largely from calcium compounds." }
    - { sym: Fe, name: Iron, z: 26, cat: transition metal, fact: "Its symbol comes from the Latin ferrum; most of Earth's core is iron." }
    - { sym: Cu, name: Copper, z: 29, cat: transition metal, fact: "Its symbol comes from the Latin cuprum; most electrical wiring is copper." }
    - { sym: Ag, name: Silver, z: 47, cat: transition metal, fact: "Its symbol comes from the Latin argentum; the best electrical conductor of all." }
    - { sym: W, name: Tungsten, z: 74, cat: transition metal, fact: "Its symbol comes from its other name, wolfram; the highest melting point of any metal." }
    - { sym: Au, name: Gold, z: 79, cat: transition metal, fact: "Its symbol comes from the Latin aurum; it does not tarnish in air." }
    - { sym: Hg, name: Mercury, z: 80, cat: transition metal, fact: "The only metal that is liquid at room temperature." }
    - { sym: Pb, name: Lead, z: 82, cat: post-transition metal, fact: "Its symbol comes from the Latin plumbum, as does the word plumbing." }
  intervals: [1, 2, 4, 8, 16]
---
# The elements

Twenty elements as flashcards, sorted into five Leitner boxes: a card you knew moves up a
box, a card you missed goes back to box 1. Box 1 comes up every session, box 2 every second, box 3 every fourth, and so on,
so what you know well comes back less often. Then test yourself in the quiz.

```bib
// ── shared: the cards, the boxes, the session's deck
let cards = data.cards
let ids = range(cards.length)

let boxes = state("boxes", cards.map(c => 1))
let session = state("session", 1)
let seed = state("seed", today())                // a new first deal each day; Shuffle takes the clock
let dueIn = (n, i) => (n - 1) % data.intervals[boxes[i] - 1] == 0
// the session's cards are dealt when it starts, so sorting a card does not reshuffle the rest
let queue = state("deck", null) ?? shuffle(ids.filter(i => dueIn(session, i)), seed)
let sorted = state("sorted", [])
let pos = state("pos", 0)
let flipped = state("flipped", false)
let open = queue.filter(i => !sorted.includes(i))
let nextOpen = queue.slice(pos + 1).find(i => !sorted.includes(i)) ?? open[0]

// the quiz: ten questions from a seed, four options each
let qz = state("quiz", {seed: today(), i: 0, picked: -1, score: 0, mistakes: []})
let qs = shuffle(ids, "{qz.seed}").slice(0, 10).map((c, k) => {
  let give = k % 2 == 0 ? "name" : "sym"
  let wrong = sample(ids.filter(j => j != c), 3, "{qz.seed}-{k}")
  return {c, ask: k % 2 == 0 ? "sym" : "name", give, options: shuffle([c, ...wrong], "{qz.seed}-o{k}").map(j => cards[j][give])}
})

// a small key cap, as the artifact's <kbd>
fn kbd(k) { box(padding: [0, 5], radius: 4, border: 1, borderColor: border) { text(k, size: 11, face: mono, color: muted) } }
```

```bib
tabs("Mode") {
  tab("Study") {
    // ── the five boxes
    grid(columns: 5) {
      for iv in data.intervals {
        card("Box {index + 1}", subtitle: iv == 1 ? "every session" : "every {iv} sessions", tone: (session - 1) % iv == 0 ? accent : neutral) {
          stat("Cards", boxes.filter(b => b == index + 1).length)
        }
      }
    }
    progress("Session {session}: {sorted.length} of {queue.length} sorted", sorted.length, max: max([1, queue.length]))

    if open.length == 0 {
      card("Session {session} done", tone: success) {
        text("Every card due this session is sorted.", color: muted)
        if button("Start session {session + 1}", style: primary, key: "enter").clicked {
          set("deck", shuffle(ids.filter(j => dueIn(session + 1, j)), "{seed}-{session + 1}"))
          set("session", session + 1); set("sorted", []); set("pos", 0); set("flipped", false)
        }
      }
    } else {
      let c = cards[queue[pos]]
      let i = queue[pos]
      let done = sorted.includes(i)
      // the card: click it (or Space) to turn it over. turn: makes it turn over, through edge on,
      // whenever flipped changes; the side it turns from shows until it is half way.
      let face = box(id: "card", width: fill, height: 290, radius: 18, border: 1, padding: [16, 20],
        borderColor: flipped ? accent : border, background: flipped ? fade(accent, 0.06) : raised,
        hover: {borderColor: accent}, gap: 6, align: center, turn: flipped, animate: {ms: 420, ease: "in-out"}) {
        box(dir: "row", width: fill, align: center) {
          box(width: fill) { text(flipped ? "{c.z}" : "{pos + 1} / {queue.length}", color: muted, face: mono) }
          badge(flipped ? c.cat : "box {boxes[i]}", tone: accent)
        }
        box(height: flipped ? 50 : 24)
        if flipped {
          text(c.name, size: 40, weight: bold, align: center)
          text(c.fact, color: muted, align: center)
        } else {
          text(c.sym, size: 96, weight: bold, align: center)
          text(done ? "already sorted this session" : "Click or press Space to turn over", size: sm, color: muted, align: center)
        }
      }
      if face.clicked || key("space").pressed { set("flipped", !flipped) }

      let knew = false
      let missed = false
      box(dir: "row", width: fill, align: center, gap: 8) {
        if button("← Previous", disabled: pos == 0, key: "left").clicked { set("pos", pos - 1); set("flipped", false) }
        if button("Next →", disabled: pos == queue.length - 1, key: "right").clicked { set("pos", pos + 1); set("flipped", false) }
        if button("Shuffle", key: "s").clicked {
          // the sorted cards keep their places at the front; the rest are dealt again, from the clock
          set("seed", clock())
          set("deck", [...queue.filter(j => sorted.includes(j)), ...shuffle(open, clock())])
          set("pos", sorted.length); set("flipped", false)
        }
        box(width: fill)
        missed = button("Didn't know", style: danger, disabled: done, key: "j").clicked
        knew = button("Knew it", style: primary, disabled: done, key: "k").clicked
      }
      // ↑ and ↓ answer as well as K and J
      if !done && (knew || key("up").pressed) {
        set("boxes", boxes.map((b, j) => j == i ? min([b + 1, 5]) : b)); set("sorted", [...sorted, i])
        set("pos", queue.indexOf(nextOpen ?? i)); set("flipped", false)
      }
      if !done && (missed || key("down").pressed) {
        set("boxes", boxes.map((b, j) => j == i ? 1 : b)); set("sorted", [...sorted, i])
        set("pos", queue.indexOf(nextOpen ?? i)); set("flipped", false)
      }
    }
    box(dir: "row", gap: 6, align: center) {
      kbd("Space"); text("flip ·", size: sm, color: muted); kbd("←"); kbd("→"); text("previous / next ·", size: sm, color: muted)
      kbd("K"); text("knew it ·", size: sm, color: muted); kbd("J"); text("didn't ·", size: sm, color: muted); kbd("S"); text("shuffle", size: sm, color: muted)
    }
  }

  tab("Quiz") {
    if qz.i >= qs.length {
      row {
        stat("Your score", "{qz.score} / {qs.length}", tone: qz.score >= 7 ? success : warning)
        text(qz.score == qs.length ? "Perfect." : qz.score >= 7 ? "Well done." : "Worth another round in Study.", color: muted)
      }
      if qz.mistakes.length > 0 {
        table(["Question", "You said", "Answer"], qz.mistakes.map(m => [m.q, {text: m.said, color: danger}, {text: m.answer, color: success}]))
      }
      if button("New quiz", style: primary, key: "enter").clicked { set("quiz", {seed: clock(), i: 0, picked: -1, score: 0, mistakes: []}) }
    } else {
      let q = qs[qz.i]
      let c = cards[q.c]
      let answer = c[q.give]
      let answered = qz.picked >= 0
      box(dir: "row", width: fill) {
        box(width: fill) { text("Question {qz.i + 1} of {qs.length}", color: muted, size: sm) }
        text("Score {qz.score}", color: muted, size: sm)
      }
      progress("", qz.i, max: qs.length)
      card(q.ask == "sym" ? "Symbol → name" : "Name → symbol") {
        text(q.ask == "sym" ? "Which element is {c.sym}?" : "What is the symbol for {c.name}?", size: xl, weight: bold)
        let pick = -1
        grid(columns: 2) {
          for o in q.options {
            let k = index
            let tone = !answered ? null : o == answer ? success : k == qz.picked ? danger : null
            // a wrong pick shakes (shake: runs when its value changes), and the colours fade in
            let b = box(id: "opt-{k}", dir: "row", gap: 8, padding: [12, 14], radius: 12, border: 1.5,
              borderColor: tone ?? border, background: tone == null ? null : fade(tone, 0.16),
              hover: answered ? null : {borderColor: accent}, shake: tone == danger, animate: 200) {
              text("{k + 1}", size: sm, color: muted); text(o)
            }
            if b.clicked || key("{k + 1}").pressed { pick = k }
          }
        }
        if pick >= 0 && !answered {
          let o = q.options[pick]
          set("quiz", {...qz, picked: pick, score: qz.score + (o == answer ? 1 : 0),
            mistakes: o == answer ? qz.mistakes : [...qz.mistakes, {q: q.ask == "sym" ? c.sym : c.name, said: o, answer}]})
        }
        if answered {
          let right = q.options[qz.picked] == answer
          text(right ? "Right: {c.name} is {c.sym}." : "Not quite: {c.name} is {c.sym}.", weight: bold, color: right ? success : danger)
        }
        if button("Next", style: primary, disabled: !answered, key: "enter").clicked { set("quiz", {...qz, i: qz.i + 1, picked: -1}) }
      }
    }
  }
}
```

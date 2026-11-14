---
bib: 1
title: A lock anyone can close but only you can open
width: wide
nav: true
data:
  primes: [11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53, 59, 61, 67, 71, 73, 79, 83, 89, 97]
  eCandidates: [3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53, 59, 61, 67, 71, 73, 79, 83, 89, 97]
  # bit positions, highest first: every exponent here is below 2^14 = 16384
  bitpos: [13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0]
  # enough rounds for Euclid on numbers below 10,000 (it needs at most about 20)
  rounds: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21]
  # trial divisors: 2, then the odd numbers up to 97 (√n is at most 97 here)
  divisors: [2, 3, 5, 7, 9, 11, 13, 15, 17, 19, 21, 23, 25, 27, 29, 31, 33, 35, 37, 39, 41, 43, 45, 47, 49, 51, 53, 55, 57, 59, 61, 63, 65, 67, 69, 71, 73, 75, 77, 79, 81, 83, 85, 87, 89, 91, 93, 95, 97]
  alphabet: [A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z]
  digits: [4, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 120, 140, 160, 180, 200, 220, 240, 260, 280, 300, 320, 340, 360, 380, 400, 420, 440, 460, 480, 500, 520, 540, 560, 580, 600, 620]
---
###### Explainer 02 · Cryptography

# A lock anyone can close but only you can open

Imagine handing out open padlocks to the whole world. Anyone can snap one shut on a box and
send it to you; only your key opens it. That is public-key cryptography, and RSA builds the
padlock out of nothing but two prime numbers and remainders.

```bib
// The arithmetic every section uses. bib numbers are doubles, exact below 2^53, so every
// product is reduced mod n at once: nothing here ever exceeds n² < 10^8.
let bitsOf = x => data.bitpos.filter(i => Math.pow(2, i) <= x).map(i => floor(x / Math.pow(2, i)) % 2)
// Square and multiply: walk the exponent's bits from the left; square, and on a 1 also multiply.
let powSteps = (m, ex, n) => bitsOf(ex).reduce((acc, b) => {
  let r = acc.length == 0 ? 1 : acc[acc.length - 1].r
  let sq = (r * r) % n
  let mul = b == 1 ? (sq * m) % n : null
  return [...acc, {b, sq, mul, r: b == 1 ? mul : sq}]
}, [])
let modpow = (m, ex, n) => {
  let s = powSteps(m, ex, n)
  return s[s.length - 1].r
}
let gcd = (a, b) => data.rounds.reduce((g, i) => g.b == 0 ? g : {a: g.b, b: g.a % g.b}, {a, b}).a
// Extended Euclid on (φ, e): each new row is r = r₂ − q·r₁ and t = t₂ − q·t₁.
let euclid = (phi, e) => data.rounds.reduce((rows, i) => {
  let a = rows[rows.length - 2]
  let b = rows[rows.length - 1]
  let q = b.r == 0 ? 0 : floor(a.r / b.r)
  return b.r == 0 ? rows : [...rows.slice(0, rows.length - 1), {...b, q}, {r: a.r - q * b.r, q: null, t: a.t - q * b.t}]
}, [{r: phi, q: null, t: 0}, {r: e, q: null, t: 1}])
let codeOf = ch => ch == " " ? 32 : 65 + data.alphabet.indexOf(ch)
let shown = m => m == 32 ? "␣" : m >= 65 && m <= 90 ? data.alphabet[m - 65] : "·"
let lettersOf = s => s.split("")

// The padlock: n = 3233 = 61 × 53, e = 17, d = 2753.
let lockState = state("lock", "new")   // new → closed → opened → closed …
let locked = lockState == "closed"
let heroWord = lettersOf("HELLO")
let face = locked ? heroWord.map(ch => str(modpow(codeOf(ch), 17, 3233))).join(" ") : "HELLO"

box(dir: "row", gap: 26, padding: 20, radius: 14, border: 1, background: "raised", align: center) {
  box(width: 180, height: 200) {
    layer {
      let drop = locked ? 26 : 0
      rect(45, 14 + drop, 90, 120, color: muted, width: 16, radius: 45)
      rect(20, 90, 140, 108, fill: "#C99A2E", radius: 20)
      circle(90, 132, 12, fill: surface)
      rect(85, 136, 10, 32, fill: surface, radius: 4)
    }
  }
  box(width: fill, gap: 10) {
    text("The box holds", size: sm, face: mono, caps: true, color: muted)
    box(width: fill, padding: [10, 14], radius: 10, border: 1, background: surface) {
      text(face, size: lg, face: mono)
    }
    row(fit: true) {
      if button("Close it with the public key (n, e)", style: primary, disabled: locked).clicked { set("lock", "closed") }
      if button("Open it with the private key d", style: danger, disabled: !locked).clicked { set("lock", "opened") }
    }
    text(locked ? "Shut with n = 3233 and e = 17: the code m of each letter became mᵉ mod n, a number nobody can turn back without d."
      : lockState == "opened" ? "Opened with d = 2753: each number raised to d mod 3233 is its letter again."
      : "Anyone may close it. Watch what the box holds once it is shut.", size: sm, color: muted)
  }
}
```

## 01 · Two primes make a key

Pick two primes, `p` and `q`, and keep them secret. Their product `n = p·q` goes public. So
does a small number `e`, the "closing" exponent. The private "opening" exponent `d` is the
number that undoes `e`, and computing it needs `φ = (p−1)(q−1)`, which you can only get if
you know `p` and `q`.

```bib
let p = state("p", 61)
let q = state("q", 53)
let n = p * q
let phi = (p - 1) * (q - 1)
let valid = data.eCandidates.filter(x => x < phi && gcd(x, phi) == 1)
let ePicked = state("e", 17)
let e = valid.includes(ePicked) ? ePicked : valid.includes(17) ? 17 : valid[0]
let rows = euclid(phi, e)
let one = rows.find(r => r.r == 1)
let d = ((one.t % phi) + phi) % phi

fn primeChips(title, key, current, other) {
  box(width: fill, gap: 8) {
    text(title, size: sm, face: mono, caps: true, color: muted)
    grid(columns: 7) {
      for v in data.primes {
        if button(str(v), style: v == current ? primary : ghost, disabled: v == other).clicked { set(key, v) }
      }
    }
  }
}

box(gap: 16, padding: 20, radius: 14, border: 1, background: "raised") {
  row {
    primeChips("Secret prime p", "p", p, q)
    primeChips("Secret prime q", "q", q, p)
  }
  text("Public exponent e (must share no factor with φ)", size: sm, face: mono, caps: true, color: muted)
  grid(columns: 7) {
    for v in valid.slice(0, 14) {
      if button(str(v), style: v == e ? primary : ghost).clicked { set("e", v) }
    }
  }
  grid(columns: 4) {
    card("n = p · q", badge: "public", badgeTone: accent, footer: "{p} × {q}, {str(n).length} digits") { stat("", n, tone: accent) }
    card("φ = (p−1)(q−1)", badge: "secret", badgeTone: muted, footer: "{p - 1} × {q - 1}") { stat("", phi) }
    card("e", badge: "public", badgeTone: accent, footer: "closing exponent") { stat("", e, tone: accent) }
    card("d", badge: "private", badgeTone: danger, footer: "{e} × {d} = {e * d} = {floor(e * d / phi)}·φ + 1") { stat("", d, tone: danger) }
  }
}
```

### Finding d: the extended Euclidean algorithm

We need `d` with `e·d ≡ 1 (mod φ)`. Euclid's algorithm divides repeatedly, keeping
remainders; the extended version also carries a coefficient `t` so that every remainder equals
`t·e` modulo φ. When the remainder reaches 1, its `t` is the inverse.

```bib
table(["Step", "Remainder r", "Quotient", "t (r ≡ t·e mod φ)"],
  rows.map((r, i) => [
    i == 0 ? "φ" : i == 1 ? "e" : str(i),
    r.r == 1 ? {badge: str(r.r), tone: warning} : str(r.r),
    r.q == null ? "" : str(r.q),
    r.r == 1 ? {badge: str(r.t), tone: warning} : str(r.t),
  ]),
  widths: [1, 2, 2, 2])

markdown("Each row: r = r₂ − quotient·r₁ and t = t₂ − quotient·t₁, using the two rows above. Remainder 1 has t = {one.t}" +
  (one.t < 0 ? ", negative, so add φ: d = {one.t} + {phi} = **{d}**." : ", so d = **{d}**.") +
  " Check: {e} × {d} mod {phi} = {(e * d) % phi}.")
```

## 02 · Closing the lock

To encrypt, turn each letter into a number `m` (here its character code: A = 65 … Z = 90,
space = 32) and compute `c = mᵉ mod n`. Anyone can do it: it uses only the public `n` and `e`.
To decrypt, compute `m = cᵈ mod n`. Press a letter to see the arithmetic.

```bib
let msg = input("Message (A–Z and spaces)", value: "MEET AT NOON")
let wrong = toggle("Open with a wrong key (d + 2)", value: false)
let clean = lettersOf(msg.toUpperCase()).filter(ch => ch == " " || data.alphabet.includes(ch)).slice(0, 24)
let chars = clean.length == 0 ? ["A"] : clean
let dk = wrong ? d + 2 : d
let letters = chars.map(ch => {
  let m = codeOf(ch)
  let c = modpow(m, e, n)
  return {ch, m, c, back: modpow(c, dk, n)}
})
let selState = state("sel", 0)
let sel = selState < letters.length ? selState : 0

box(dir: "row", gap: 8, wrap: true) {
  for L in letters {
    box(width: 76, gap: 2, padding: [6, 6], radius: 10, border: index == sel ? 2 : 1,
        borderColor: index == sel ? warning : border, background: "raised") {
      if button(L.ch == " " ? "␣" : L.ch, style: index == sel ? primary : ghost).clicked { set("sel", index) }
      text("m={L.m}", size: sm, face: mono, color: muted)
      text("↓ ^e", size: sm, color: faint)
      text(str(L.c), face: mono, weight: bold, color: accent)
      text("↓ ^d", size: sm, color: faint)
      text(shown(L.back), face: mono, weight: bold, color: danger)
    }
  }
}

let repeats = unique(letters.map(l => l.c)).length < letters.length
text((wrong ? "With d + 2 = {dk} the numbers come back wrong: {letters.map(l => l.back).join(" ")}. " : "Each cipher number decrypts to its letter. ") +
  (repeats ? "Notice repeated letters give repeated ciphers: one reason real RSA adds random padding." : ""),
  size: sm, color: wrong ? danger : muted)
```

### Square and multiply

Computing `mᵉ` directly would produce enormous numbers; instead, write the exponent in binary
and walk its bits from the left. For each bit, square the running value; if the bit is 1,
also multiply by `m`. Reduce modulo `n` after *every* step, so nothing ever exceeds `n²`.
Fewer than 2·(number of bits) multiplications, even for exponents with hundreds of digits.

```bib
let L = letters[sel]

fn ladder(title, base, ex) {
  let steps = powSteps(base, ex, n)
  box(width: fill, gap: 10, padding: 16, radius: 14, border: 1, background: "raised") {
    text("{title}: {base}^{ex} mod {n} = {steps[steps.length - 1].r}", size: sm, face: mono, caps: true, color: muted)
    box(dir: "row", gap: 4, wrap: true) {
      for s in steps {
        box(width: 26, padding: [4, 0], radius: 6, border: 1, borderColor: s.b == 1 ? warning : border,
            background: s.b == 1 ? warning : "raised", align: center) {
          text(str(s.b), face: mono, weight: bold, color: s.b == 1 ? surface : ink)
        }
      }
    }
    table(["Bit", "Square", "× {base}", "Value"],
      steps.map((s, i) => [str(s.b), i == 0 ? "1²" : str(s.sq), s.mul == null ? "—" : str(s.mul), str(s.r)]))
  }
}

row {
  ladder("Close \"{L.ch == " " ? "␣" : L.ch}\"", L.m, e)
  ladder("Open it", L.c, dk)
}
```

## 03 · Why nobody else can open it

An attacker sees `n` and `e`. To get `d` they need φ, and to get φ they need `p` and `q`:
they must **factor n**. For our tiny `n` that is instant. The simplest attack is trial
division: try 2, 3, 5, 7, … up to √n.

```bib
let lim = floor(Math.sqrt(n))
let cands = data.divisors.filter(c => c <= lim)
let small = min([p, q])
let at = cands.indexOf(small)
let big = n / small
let phi2 = (small - 1) * (big - 1)
let d2 = ((euclid(phi2, e).find(r => r.r == 1).t % phi2) + phi2) % phi2

box(gap: 10, padding: 18, radius: 14, border: 1, background: "raised") {
  text("Trial division of n = {n}: candidates 2, 3, 5, … up to √n ≈ {lim}", size: sm, face: mono, caps: true, color: muted)
  box(dir: "row", gap: 3, wrap: true) {
    for c in cands {
      box(padding: [2, 5], radius: 4, border: 1, borderColor: c == small ? danger : border,
          background: c == small ? danger : surface) {
        text(str(c), size: sm, face: mono, color: c == small ? surface : index < at ? ink : faint)
      }
    }
  }
  markdown("Found after **{at + 1}** tries: {n} = {small} × {big}. Now φ = {phi2}, and the same Euclid steps give d = **{d2}**. The lock is open to anyone who can factor n.")
}
```

But √n has half as many digits as `n`, so every two extra digits of `n` make trial division
ten times longer. Multiplying `p·q` takes about digits² steps; undoing it takes about
10^(digits/2). Slide the size of `n`:

```bib
let D = slider("Digits in n", 4, 620, value: 40, step: 1)
// odd candidates up to √n: √n ≈ 10^((D − 0.5)/2), half of them odd; at 10⁹ tries a second
let lgTrial = x => (x - 1) / 2 - Math.log10(2) + 0.25
let lgSec = lgTrial(D) - 9
let lgYear = Math.log10(3.156e7)
let lgUniverse = Math.log10(4.35e17)
let commas = x => {
  let s = str(round(x))
  return s.length <= 3 ? s : s.length <= 6 ? s.slice(0, s.length - 3) + "," + s.slice(s.length - 3, s.length)
    : s.slice(0, s.length - 6) + "," + s.slice(s.length - 6, s.length - 3) + "," + s.slice(s.length - 3, s.length)
}
let bigNum = lg => lg < 6 ? commas(Math.pow(10, lg)) : "10^" + str(round(lg))
let secs = Math.pow(10, lgSec)
let lgY = lgSec - lgYear
let lgU = lgSec - lgUniverse
let timeText = lgSec < -3 ? "< 1 ms" : secs < 60 ? "{secs < 10 ? secs.toFixed(2) : round(secs)} s"
  : secs < 3600 ? "{(secs / 60).toFixed(1)} min" : secs < 86400 ? "{(secs / 3600).toFixed(1)} h"
  : secs < 3.156e7 ? "{(secs / 86400).toFixed(1)} days" : lgY < 6 ? "{commas(Math.pow(10, lgY))} yr" : "10^{round(lgY)} yr"
let timeNote = lgSec < -3 ? "instant" : secs < 60 ? "seconds" : secs < 3600 ? "minutes" : secs < 86400 ? "hours"
  : secs < 3.156e7 ? "days" : lgU > 0 ? "{lgU < 3 ? round(Math.pow(10, lgU)) : "10^" + str(round(lgU))} × the age of the universe" : "years"

box(width: fill, height: 260, radius: 12, border: 1, background: "raised") {
  layer {
    // x: digits 4 → 620; y: log10(seconds) from −9 to 320, as in the artifact
    let X = x => 52 + (x - 4) / 616 * (w - 62)
    let Y = v => 10 + (320 - v) / 329 * 224
    for m in [[0, "1 second"], [100, "10¹⁰⁰ s"], [200, "10²⁰⁰ s"], [300, "10³⁰⁰ s"]] {
      line(52, Y(m[0]), w, Y(m[0]), color: border)
      label(56, Y(m[0]) - 9, m[1], size: sm, color: muted)
    }
    line(52, Y(lgUniverse), w, Y(lgUniverse), color: fade(warning, 0.5))
    label(w - 8, Y(lgUniverse) - 9, "age of the universe", size: sm, color: warning, align: end)
    for x in [100, 200, 300, 400, 500, 600] { label(X(x), h - 12, str(x), size: sm, color: muted, align: center) }
    label(6, h - 12, "digits", size: sm, color: muted)
    label(6, 14, "time", size: sm, color: muted)
    for x in data.digits where index < data.digits.length - 1 {
      let x2 = data.digits[index + 1]
      line(X(x), Y(lgTrial(x) - 9), X(x2), Y(lgTrial(x2) - 9), color: danger, width: 2.4)
      line(X(x), Y(2 * Math.log10(x) - 9), X(x2), Y(2 * Math.log10(x2) - 9), color: accent, width: 2.4)
    }
    label(X(300), Y(-4) - 22, "multiplying p·q", size: sm, color: accent)
    label(X(200), Y(lgTrial(200) - 9) - 22, "trial division", size: sm, color: danger)
    line(X(D), 10, X(D), h - 26, color: fade(warning, 0.7), width: 1)
    circle(X(D), Y(lgSec), 5.5, fill: danger)
  }
}

grid(columns: 4) {
  stat("Multiply p·q (digit operations)", commas(D * D), tone: accent)
  stat("Trial divisions (odd numbers up to √n)", bigNum(lgTrial(D)))
  stat("At 10⁹ per second", timeText, tone: danger)
  stat("Today's keys", 617, unit: "digits (2048 bits)")
}
text(timeNote, size: sm, color: danger)
```

Cleverer methods exist. The best known, the number field sieve, grows more slowly than trial
division, but still faster than any power of the number of digits: the largest RSA challenge
number factored in public had 250 digits and took about 2,700 core-years of computing.
Doubling the digits does not double the attacker's work; it multiplies it beyond reach.

## What you just saw

1. RSA's public key is a product `n = p·q` and an exponent `e`; the private key `d` is the
   inverse of `e` modulo `(p−1)(q−1)`, found with the extended Euclidean algorithm.
2. Encrypting is `mᵉ mod n`, decrypting is `cᵈ mod n`; because `e·d = 1 + k·φ`, raising to
   `e` then `d` lands back on `m` (Euler's theorem).
3. Square-and-multiply makes huge powers cheap, and reducing mod `n` at each step keeps every
   number small.
4. Security rests on an asymmetry: multiplying two primes is easy, factoring their product is
   not.
5. Caveat: encrypting letter by letter, as here, is insecure (equal letters give equal
   ciphers). Real RSA uses 2048-bit or larger `n` and pads each message with randomness first.

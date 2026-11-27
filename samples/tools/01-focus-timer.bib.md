---
bib: 1
title: Focus timer
width: wide
data:
  hours: [7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20]
  # bib's clock is in UTC and has no time zone: the reader's offset, in hours, for the times of day
  utcOffset: 1
  # an invented morning, start in seconds since midnight
  morning:
    - { mode: Focus, start: 29100, mins: 25 }
    - { mode: Short break, start: 30600, mins: 5 }
    - { mode: Focus, start: 30960, mins: 25 }
    - { mode: Short break, start: 32460, mins: 5 }
    - { mode: Focus, start: 33000, mins: 18, skipped: true }
    - { mode: Focus, start: 38400, mins: 25 }
    - { mode: Short break, start: 39900, mins: 5 }
    - { mode: Focus, start: 40320, mins: 25 }
---
# Focus timer

Work in focused blocks with short breaks between them; after four focus sessions, take a long
one. **Space** starts and pauses, **R** resets, **S** skips, **1** **2** **3** pick the mode.

```bib
// ---- state -----------------------------------------------------------------
let modes = ["Focus", "Short break", "Long break"]
let toneOf = m => m == "Focus" ? danger : m == "Short break" ? success : accent
// The sliders draw in the card beside the timer, after it: read them by name, with their defaults.
let lengths = {
  "Focus": value("Focus length", 25),
  "Short break": value("Short break length", 5),
  "Long break": value("Long break length", 15),
}
let auto = value("Start the next session on its own", false)
let mode = value("mode", "Focus")
let log = state("log", data.morning)
let done = state("round", 3)
let flash = state("flash", null)                 // {text, until}
// A session runs while endsAt is set; paused, `left` holds what is left (null: all of it).
let sess = state("session", {endsAt: null, left: null, started: null})
let running = sess.endsAt != null
let now = clock(every: running ? 0.1 : 1)
let total = lengths[mode] * 60
let left = running ? Math.max(0, sess.endsAt - now) : sess.left ?? total
let frac = left / total
let tone = toneOf(mode)

let two = n => n < 10 ? "0{n}" : "{n}"
let mmss = s => "{two(floor(ceil(s) / 60))}:{two(ceil(s) % 60)}"
let ofDay = t => floor(t + data.utcOffset * 3600) % 86400        // seconds since local midnight
let hhmm = s => "{two(floor(s / 3600))}:{two(floor(s % 3600 / 60))}"

// ---- what the events do ------------------------------------------------------
let fresh = {endsAt: null, left: null, started: null}
fn pick(m) {
  set("mode", m)
  set("session", fresh)
}
fn startStop() {
  if running { set("session", {...sess, endsAt: null, left}) }
  else { set("session", {...sess, endsAt: now + (left > 0 ? left : total), left: null, started: sess.started ?? now}) }
}
fn finish(skipped) {
  let el = total - left
  if sess.started != null || !skipped {
    set("log", [...log, {mode, start: ofDay(sess.started ?? now - el), mins: round(el / 60), skipped}])
  }
  let r = mode == "Focus" && !skipped ? done + 1 : mode == "Long break" ? 0 : done
  let next = mode != "Focus" ? "Focus" : r >= 4 ? "Long break" : "Short break"
  let keep = !skipped && running && auto
  set("round", r)
  set("mode", next)
  set("session", keep ? {endsAt: now + lengths[next] * 60, left: null, started: now} : fresh)
  if !skipped {
    set("flash", {text: mode == "Focus" ? "Focus done. {next} next." : "Break over. Back to it.", until: now + 4})
  }
}

if changed("mode") { set("session", fresh) }
if key("1").pressed { pick("Focus") }
if key("2").pressed { pick("Short break") }
if key("3").pressed { pick("Long break") }
if alarm(sess.endsAt).rang { finish(false) }

// ---- the timer ---------------------------------------------------------------
text(fmtDate(today(), "dddd D MMMM"), color: muted)
// The mode above the timer; the timer and the lengths side by side when the column has room.
choice("Mode", modes, id: "mode", style: segmented)
row {
  box(gap: 14, padding: 16, radius: 16, border: 1, background: raised, align: center) {
    // the ring at full size on a page, smaller in a note's column or on a phone (page.width)
    let S = page.width >= 700 ? 300 : 240
    let c = S / 2
    let r = S * 0.41
    box(width: S, height: S, align: center, justify: center) {
      layer {
        let a = 2 * Math.PI * frac
        circle(c, c, r, color: fade(muted, 0.18), width: S / 21)
        arc(c, c, r, 0, frac, color: tone, width: S / 21, cap: "round")
        circle(c + r * Math.sin(a), c - r * Math.cos(a), (running ? 9 : 7) * S / 300, fill: tone)
      }
      text(mmss(left), size: S >= 300 ? 64 : 52, weight: bold, face: mono)
      text(mode, caps: true, color: muted, size: sm)
    }

    // Four dots: the focus sessions of this round.
    box(dir: "row", wrap: true, gap: 8, align: center) {
      for i in range(4) {
        box(width: 14, height: 14, radius: 7, border: 2, borderColor: danger,
            background: i < done ? danger : mode == "Focus" && i == done ? fade(danger, 0.2) : surface)
      }
      text("{done} of 4 before the long break", size: sm, color: muted)
    }

    row(fit: true) {
      if button("Reset", style: ghost, key: "r", disabled: left == total && !running).clicked {
        set("session", fresh)
      }
      if button(running ? "Pause" : left < total ? "Resume" : "Start", id: "go", style: primary, key: "space").clicked { startStop() }
      if button("Skip", style: ghost, key: "s").clicked { finish(true) }
    }
    if flash != null && now < flash.until { badge(flash.text, tone: tone) }
    else { text("Space start/pause · R reset · S skip · 1 2 3 mode", size: sm, color: faint) }
  }

  column {
    card("Lengths", subtitle: "minutes") {
      slider("Focus length", 5, 60, value: 25, step: 5)
      slider("Short break length", 1, 15, value: 5, step: 1)
      slider("Long break length", 5, 40, value: 15, step: 5)
      toggle("Start the next session on its own", value: false)
    }
    // ---- today --------------------------------------------------------------
    let focus = log.filter(s => s.mode == "Focus")
    let streak = focus.reverse().findIndex(s => s.skipped)
    card("Today") {
      grid(columns: 3) {
        stat("Focus minutes", sum(focus, s => s.mins))
        stat("Sessions done", focus.count(s => !s.skipped))
        stat("In a row", streak < 0 ? focus.length : streak)
      }
      // Focus minutes per hour: a column a bar, its value as a tip, a dashed line at one hour.
      let perHour = data.hours.map(hr => sum(focus.filter(s => floor(s.start / 3600) == hr), s => s.mins))
      let top = Math.max(60, max(perHour))
      let nowH = floor(ofDay(now) / 3600)
      let y60 = 132 - 120 * 60 / top
      box(dir: "row", gap: 4, height: 150) {
        layer { line(0, y60, w, y60, color: border, dash: [3, 3]) }
        for hr in data.hours {
          let v = perHour[index]
          let bh = 120 * v / top
          box(id: "hour-{hr}", width: fill, height: 150, tip: "{hr}:00 · {v} min", hover: {background: fade(danger, 0.06)}) {
            layer {
              rect(2, 132 - bh, w - 4, bh, fill: hr == nowH ? fade(danger, 0.55) : danger, radius: 3)
              if v > 0 { label(w / 2, 124 - bh, "{v}", size: 11, color: faint, align: "center") }
              if hr % 2 == 1 { label(w / 2, 142, "{hr}", size: 11, color: faint, align: "center") }
            }
          }
        }
      }
    }
  }
}
```

## Session log

```bib
table(["Started", "Session", "Length"],
  log.reverse().map(s => [hhmm(s.start), {badge: s.skipped ? "{s.mode} (skipped)" : s.mode, tone: s.skipped ? muted : toneOf(s.mode)}, "{s.mins} min"]),
  widths: [1, 3, 1], height: 230)
```

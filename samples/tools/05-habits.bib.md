---
bib: 1
title: Habits
width: wide
data:
  # Invented: one character per day of the year, "1" = done.
  year: 2026
  habits:
    - name: Read 20 pages
      color: "#2f9e6e"
      days: "11111111111101110000100111011111111111111111110000111111111000000000011100000111111111011100000000111111111110000010000001111111111111001111100001111011001111000001111110000000001111100000000111000001100001110000001110010000011000000000000001000000000011111111111111111111100000000000001100111111110111100100000111111111111111000110000000000000000000000000000000000"
    - name: Morning run
      color: "#e07a2f"
      days: "01110011000110010001010011011111111110111111100110100001100100001111011100011111110001011111111111111111100001101001000001110000000111110000001000001011101000110000111011111000111110100110001101111010000100111111110100100011100111110101111100000000100001101111001011111101100111111111001111111011001111100011001111000011110000011100000000000000000000000000000000000"
    - name: Piano practice
      color: "#7a5cd6"
      days: "00011111111111111000011000111111011111100010001101001111011101111000000100111111111010110001110110110011101000100100110000111000111010000000001101111100011110011110001011101111000000100011000111100000010011111111111111111001111111111100000000101100000011110111111101100110001010001011001111111001101011111111111111100011111111111000000000000000000000000000000000000"
    - name: No screens after 22
      color: "#2f7fd6"
      days: "11101000010000100110100011011111110001011110000100000000001110000000111111011111111111111101111000011111100101111101101111101111000100000111111100000000001011110000010000000000000111011100111111111100010001111000000110111111111000011100001010111001110111010111111100001110101110010111000111000110011011110111111110010110000111111100000000000000000000000000000000000"
  palette: ["#2f9e6e", "#e07a2f", "#7a5cd6", "#2f7fd6", "#d64569", "#c9a227", "#1fa6a6", "#8a8f98"]
---
# Habits

A year at a glance: one square a day, coloured when the habit was done. Click a day to mark it
done or undone; hover it for the date. Streaks count back from today, and a today not yet done
does not break one.

```bib
// ── dates: day d of the year counts from 1 January; weeks start on Monday
let start = date(data.year, 1, 1)
let ndays = daysBetween(start, date(data.year + 1, 1, 1))
let T = min([max([daysBetween(start, today()), -1]), ndays - 1])   // today, as a day of the year
let pad = weekday(start) - 1                     // empty cells before 1 January in the first week
let weekOf = d => floor((d + pad) / 7)
let dayName = range(ndays).map(d => fmtDate(addDays(start, d), "ddd D MMMM YYYY"))
let monthDay = m => daysBetween(start, date(data.year, m + 1, 1))

// ── the numbers, from a habit's string of 0s and 1s
let upto = s => s.slice(0, T + 1)
let current = s => {
  let u = s.slice(T, T + 1) == "1" ? upto(s) : s.slice(0, T)
  let runs = u.split("0")
  return runs[runs.length - 1].length
}
let longest = s => max(upto(s).split("0").map(r => r.length))
let m0 = T < 0 ? 0 : monthDay(dateParts(addDays(start, T)).month - 1)
let monthPct = s => T < 0 ? 0 : round(100 * s.slice(m0, T + 1).split("0").join("").length / (T - m0 + 1))

let habits = state("habits", data.habits)
let flip = (s, d) => s.slice(0, d) + (s.slice(d, d + 1) == "1" ? "0" : "1") + s.slice(d + 1)

text("Today: {fmtDate(today(), "dddd D MMMM YYYY")}", color: muted)
```

```bib
// ── a year per habit: 53 weeks of 7 clickable days, months above, Mon/Wed/Fri beside
let weeks = range(weekOf(ndays - 1) + 1)
for hb in habits {
  let hi = index
  let done = hb.days.split("")
  card(null) {
    box(dir: "row", gap: 28, align: center) {
      box(width: 12, height: 12, radius: 6, background: hb.color)
      box(width: fill) { text(hb.name, weight: bold, size: lg) }
      stat("Current streak", current(hb.days), unit: "days")
      stat("Longest", longest(hb.days), unit: "days")
      stat(T < 0 ? "This month" : fmtDate(addDays(start, T), "MMMM"), monthPct(hb.days), unit: "%")
      if button("×", style: ghost, id: "remove-{hi}").clicked { set("habits", habits.filter((x, j) => j != hi)) }
    }
    box(width: 30 + weeks.length * 16, height: 14) {
      layer { for m in range(12) { label(30 + weekOf(monthDay(m)) * 16, 7, fmtDate(date(data.year, m + 1, 1), "MMM"), size: 11, color: muted) } }
    }
    box(dir: "row", gap: 3) {
      box(width: 27, gap: 3) {
        for wd in ["Mon", "", "Wed", "", "Fri", "", ""] { box(height: 13) { text(wd, size: 10, color: muted) } }
      }
      for wk in weeks {
        box(gap: 3) {
          for r in range(7) {
            let d = wk * 7 + r - pad
            if d < 0 || d >= ndays { box(width: 13, height: 13) }
            else {
              let on = done[d] == "1"
              let c = box(id: "day-{hi}-{d}", width: 13, height: 13, radius: 3,
                background: d > T ? null : on ? hb.color : fade(muted, 0.16),
                border: d > T || d == T ? 1 : 0, borderColor: d == T ? ink : border,
                hover: {borderColor: ink},
                tip: "{dayName[d]} · {hb.name}: {d > T ? "in the future" : on ? "done" : "not done"}")
              if c.clicked && d <= T { set("habits", habits.map((x, j) => j == hi ? {...x, days: flip(x.days, d)} : x)) }
            }
          }
        }
      }
    }
  }
}
```

### Days done per week, last 12 weeks

```bib
let last = weekOf(T < 0 ? 0 : T)
let doneIn = (s, wk) => {
  let a = wk * 7 - pad
  return s.slice(a < 0 ? 0 : a, a + 7 > T + 1 ? T + 1 : a + 7).split("0").join("").length
}
bars(range(last - 11, last + 1).filter(wk => wk >= 0).map(wk => ({
  label: fmtDate(addDays(start, max([0, wk * 7 - pad])), "D MMM"),
  parts: habits.map(hb => ({label: hb.name, value: doneIn(hb.days, wk), tone: hb.color})),
})), stack: true, dir: "column", height: 240, max: 7 * max([1, habits.length]))
```

### Add a habit

```bib
let name = input("Name", placeholder: "e.g. Stretch for 10 minutes")
let chosen = state("colour", data.palette[4])
text("Colour", size: sm, color: muted)
box(dir: "row", gap: 8, align: center) {
  for c in data.palette {
    let s = box(id: "swatch-{index}", width: 26, height: 26, radius: 13, background: c,
      border: c == chosen ? 2 : 0, borderColor: ink, hover: {borderColor: muted}, tip: c)
    if s.clicked { set("colour", c) }
  }
  box(width: 12)
  box(width: 140) {
    let any = input("Any colour", placeholder: "#rrggbb")
    if changed("Any colour") && colourOf(any) != null { set("colour", colourOf(any).hex) }
  }
}
box(dir: "row", gap: 10, align: center) {
  box(width: 12, height: 12, radius: 6, background: chosen)
  text("{name.trim() == "" ? "New habit" : name.trim()}, in {chosen}. A new habit starts with an empty year.", color: muted, size: sm)
}
if button("Add habit", style: primary, disabled: name.trim() == "").clicked {
  set("habits", [...habits, {name: name.trim(), color: chosen, days: range(ndays).map(d => "0").join("")}])
  set("Name", "")
}
```

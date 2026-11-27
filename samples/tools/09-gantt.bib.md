---
bib: 1
title: Website relaunch for Larkspur Studio
width: wide
data:
  start: "2026-11-16"
  holidays: ["2026-12-24", "2026-12-25", "2026-12-31", "2027-01-01"]
  owners: [Project lead, Designer, Developer, Writer, Tester]
  tasks:
    - { id: kick, name: Kick-off and scope, owner: Project lead, dur: 2, deps: [] }
    - { id: res, name: User research, owner: Designer, dur: 5, deps: [kick] }
    - { id: ia, name: Information architecture, owner: Designer, dur: 3, deps: [res] }
    - { id: cms, name: Choose and set up the CMS, owner: Developer, dur: 4, deps: [kick] }
    - { id: vis, name: Visual design, owner: Designer, dur: 6, deps: [ia] }
    - { id: copy, name: Write the copy, owner: Writer, dur: 8, deps: [ia] }
    - { id: signoff, name: Design sign-off, owner: Project lead, dur: 0, deps: [vis] }
    - { id: build, name: Build the templates, owner: Developer, dur: 10, deps: [signoff, cms] }
    - { id: content, name: Load the content, owner: Writer, dur: 4, deps: [build, copy] }
    - { id: qa, name: Testing and fixes, owner: Tester, dur: 5, deps: [content] }
    - { id: train, name: Train the editors, owner: Project lead, dur: 2, deps: [content] }
    - { id: launch, name: Launch, owner: Project lead, dur: 0, deps: [qa, train] }
effects: [respond]
---
###### Project planner

# Website relaunch for Larkspur Studio

Twelve tasks, five roles, one launch date. **Drag a bar** to hold a task back, or drag its
right end to change how long it takes; everything that depends on it moves. Click a bar to
select it: **← →** nudge it a working day, **Shift+← →** make it shorter or longer, **Esc**
lets go, **Ctrl+Z** undoes. Weekends and the holidays are skipped. Red is the critical path:
a day lost there is a day lost at launch.

```bib
// ---------- the calendar: working day n is the date workdays(start, n) ----------
let skip = data.holidays
let workDate = n => workdays(data.start, n, skip: skip)
let wdOff = range(90).map(n => daysBetween(data.start, workDate(n)))   // working day n as calendar days from the start
let calOf = n => wdOff[n]
let firstOnOrAfter = o => wdOff.findIndex(x => x >= o)            // the working day a calendar day snaps to
let dateAt = o => addDays(data.start, o)
let short = o => fmtDate(dateAt(o), "D MMM")
let now = today()
let todayOff = daysBetween(data.start, now)

// ---------- the plan: the front matter's tasks, the reader's edits, the added ones ----------
let added = state("added", [])
let edits = state("edits", [])        // [{id, dur, delay}], the reader's changes
let history = state("history", [])    // what edits and added were before each change, for undo
let sel = state("sel", null)
let all = [...data.tasks, ...added]
let editOf = t => edits.find(e => e.id == t.id)
let durOf = t => editOf(t)?.dur ?? t.dur
let delayOf = t => editOf(t)?.delay ?? 0

fn remember() { set("history", [...history, {edits, added}].slice(max([0, history.length - 49]))) }
fn change(id, dur, delay) { set("edits", [...edits.filter(e => e.id != id), {id, dur, delay}]) }

// forward pass: tasks come after what they depend on, so one pass does
let fwd = all.reduce((acc, t) => {
  let es = max([delayOf(t), ...t.deps.map(d => acc.find(a => a.id == d).ef)])
  return [...acc, {...t, dur: durOf(t), delay: delayOf(t), es, ef: es + durOf(t)}]
}, [])
let end = max(fwd.map(t => t.ef))
// backward pass: the latest each can finish without moving the end
let bwd = fwd.reverse().reduce((acc, t) => {
  let succ = acc.filter(s => s.deps.includes(t.id))
  let lf = succ.length == 0 ? end : min(succ.map(s => s.ls))
  return [...acc, {...t, lf, ls: lf - t.dur, slack: lf - t.ef}]
}, [])
let plan = fwd.map(t => bwd.find(b => b.id == t.id))
let crit = plan.filter(t => t.slack == 0)
let finishOff = calOf(max([0, end - 1]))

grid(columns: 4) {
  stat("Work finishes", fmtDate(dateAt(finishOff), "ddd D MMM YYYY"))
  stat("Working days", end)
  stat("Critical tasks", "{crit.length} of {plan.length}", tone: danger)
  stat("Today", fmtDate(now, "ddd D MMM"))
}
```

```bib
// ---------- the chart: weeks across, a row per task; the bars are boxes, so they can be dragged ----------
let weeks = ceil((finishOff + 4) / 7) + 1
let days = weeks * 7
// The bars are boxes, so the chart has a size in px: as wide as the block is drawn (page.width),
// the task names narrower in a narrow column.
let LEFT = page.width >= 900 ? 210 : page.width >= 520 ? 150 : 110
let ROW = 34
let TOP = 46
let DW = round((page.width - LEFT - 12) / days, 2)   // px a calendar day
let showDate = DW * 7 >= 52                    // a week wide enough to write its date in
let X = o => LEFT + DW * o
let rowOf = id => plan.findIndex(t => t.id == id)
let tipOf = t => "{t.name} · {t.owner} · {t.dur} d · {short(calOf(t.es))}{t.dur == 0 ? "" : "–" + short(calOf(t.ef - 1))} · slack {t.slack} d"

row(fit: true) {
  badge("task", tone: accent)
  badge("critical path", tone: danger)
  badge("◆ milestone", tone: danger)
  badge("today", tone: warning)
  badge("holiday", tone: warning)
  text("  the thin line after a bar is its slack", size: sm, color: muted)
}

let drag = state("drag", null)      // {id, es0, dur0, resize} while a bar is held
fn moved(t, dx, resize) {
  let dd = round(dx / DW)           // calendar days moved
  if resize {
    let endCal = calOf(drag.es0 + drag.dur0 - 1) + dd
    change(t.id, max([1, wdOff.filter(o => o >= calOf(drag.es0) && o <= endCal).length]), t.delay)
  } else {
    // the working day it was dropped on is its earliest start; what it waits for still comes first
    change(t.id, t.dur, max([0, firstOnOrAfter(calOf(drag.es0) + dd)]))
  }
}
fn grab(t, resize) {
  if drag?.id != t.id {
    remember()
    set("drag", {id: t.id, es0: t.es, dur0: t.dur, resize})
    set("sel", t.id)
  }
}

box(width: LEFT + DW * days + 10, radius: 12, border: 1, background: raised) {
  layer {
    let H = TOP + plan.length * ROW + 4
    // weekends and holidays
    for o in range(days) where weekday(dateAt(o)) >= 6 || skip.includes(dateAt(o)) {
      rect(X(o), TOP - 6, DW, H - TOP, fill: skip.includes(dateAt(o)) ? fade(warning, 0.16) : fade(muted, 0.08))
    }
    // weeks across
    for wk in range(weeks) {
      line(X(wk * 7), 8, X(wk * 7), H, color: border, width: 1)
      label(X(wk * 7) + 6, 16, "W{wk + 1}", size: 11, color: muted)
      if showDate || wk % 2 == 0 { label(X(wk * 7) + 6, 31, short(wk * 7), size: 11, color: muted) }
    }
    // the selected row
    if sel != null && rowOf(sel) >= 0 { rect(0, TOP + rowOf(sel) * ROW, w, ROW, fill: fade(accent, 0.08)) }
    // arrows: the end of a dependency to the start of what waits for it, as an elbow
    for t in plan {
      for d in t.deps {
        let a = plan[rowOf(d)]
        let isCrit = a.slack == 0 && t.slack == 0 && a.ef == t.es
        let x1 = a.dur == 0 ? X(calOf(a.es) + 0.5) + 8 : X(calOf(a.ef - 1) + 1)
        let y1 = TOP + rowOf(d) * ROW + ROW / 2
        let x2 = t.dur == 0 ? X(calOf(t.es) + 0.5) - 9 : X(calOf(t.es))
        let y2 = TOP + rowOf(t.id) * ROW + ROW / 2
        path([[x1, y1], [x1 + 6, y1], [x1 + 6, y2], [x2, y2]], color: isCrit ? danger : fade(muted, 0.7),
          width: isCrit ? 1.8 : 1.2, arrow: "end")
      }
    }
    // slack whiskers and milestone dates
    for t in plan {
      let y = TOP + index * ROW
      if t.dur > 0 && t.slack > 0 {
        rect(X(calOf(t.ef - 1) + 1), y + ROW / 2 - 1, X(calOf(t.lf - 1) + 1) - X(calOf(t.ef - 1) + 1), 2, fill: fade(muted, 0.5))
      }
      if t.dur == 0 { label(X(calOf(t.es) + 0.5) + 14, y + ROW / 2, short(calOf(t.es)), size: 11, color: muted) }
    }
    // today, dashed
    line(X(todayOff + 0.5), TOP - 10, X(todayOff + 0.5), H, color: warning, width: 2, dash: [4, 3])
    label(X(todayOff + 0.5) + 5, TOP - 6, "today", size: 11, color: warning)
  }
  box(height: TOP)
  for t in plan {
    let tone = t.slack == 0 ? danger : accent
    // the bars are as tall as the row and drawn in its middle: align: center does not centre a row's children across
    box(dir: "row", height: ROW) {
      box(width: LEFT, padding: [1, LEFT < 150 ? 8 : 12]) {
        // in a phone's column the name has two lines of its own and the owner is in its tip
        if LEFT < 150 { text(t.name, size: 11, weight: t.slack == 0 ? bold : regular, color: ink) }
        else {
          text(t.name, size: sm, weight: t.slack == 0 ? bold : regular, color: ink, wrap: false)
          text(t.owner, size: 11, color: muted, wrap: false)
        }
      }
      if t.dur == 0 {
        // a milestone: a diamond on its day
        box(width: X(calOf(t.es) + 0.5) - LEFT - 9)
        let m = box(id: "bar-{t.id}", width: 18, height: ROW, tip: tipOf(t)) {
          layer { path([[9, 8], [18, 17], [9, 26], [0, 17]], closed: true, fill: tone, color: sel == t.id ? ink : tone, width: 1.5) }
        }
        if m.pressed { grab(t, false) }
        if m.pressed && drag != null && m.dx != null { moved(t, m.dx, false) }
        if m.released { set("drag", null) }
      } else {
        let s = calOf(t.es)
        let e = calOf(t.ef - 1) + 1
        box(width: X(s) - LEFT)
        let g = box(id: "bar-{t.id}", dir: "row", width: X(e) - X(s), height: ROW, tip: tipOf(t)) {
          layer {
            rect(0, 8, w, ROW - 16, fill: fade(tone, 0.22), radius: 5)
            // a piece per run of working days, so weekends and holidays show as gaps in the bar
            for n in range(t.es, t.ef) where n == t.es || calOf(n) != calOf(n - 1) + 1 {
              let runEnd = range(n, t.ef).find(k => k == t.ef - 1 || calOf(k + 1) != calOf(k) + 1)
              rect(DW * (calOf(n) - s) + 1, 9, DW * (calOf(runEnd) + 1 - calOf(n)) - 2, ROW - 18, fill: tone, radius: 4)
            }
            if sel == t.id { rect(0, 7, w, ROW - 14, color: ink, width: 2, radius: 6) }
          }
          box(width: fill)
          // the right end: drag it to change the length
          let r = box(id: "end-{t.id}", width: 8, height: ROW, hover: {background: fade(ink, 0.2)})
          if r.pressed { grab(t, true) }
          if r.pressed && drag != null && r.dx != null { moved(t, r.dx, true) }
          if r.released { set("drag", null) }
        }
        if g.pressed && drag?.resize != true { grab(t, false) }
        if g.pressed && drag != null && drag.resize != true && g.dx != null { moved(t, g.dx, false) }
        if g.released { set("drag", null) }
      }
    }
  }
  box(height: 10)
}

// keys: nudge or resize the selected task, let go, undo
let picked = plan.find(t => t.id == sel)
if picked != null && key("right").pressed { remember(); change(picked.id, picked.dur, max([picked.delay, picked.es]) + 1) }
if picked != null && key("left").pressed { remember(); change(picked.id, picked.dur, max([0, min([picked.delay, picked.es]) - 1])) }
if picked != null && picked.dur > 0 && key("shift+right").pressed { remember(); change(picked.id, picked.dur + 1, picked.delay) }
if picked != null && picked.dur > 1 && key("shift+left").pressed { remember(); change(picked.id, picked.dur - 1, picked.delay) }
if key("escape").pressed { set("sel", null) }

row(fit: true) {
  if button("Undo", key: "ctrl+z", disabled: history.length == 0).clicked {
    let last = history[history.length - 1]
    set("edits", last.edits)
    set("added", last.added)
    set("history", history.slice(0, history.length - 1))
  }
  if button("Reset the plan", style: danger, disabled: edits.length == 0 && added.length == 0).clicked {
    remember()
    set("edits", [])
    set("added", [])
    set("sel", null)
  }
  text(picked == null ? "Click a bar to select it." : "Selected: {tipOf(picked)}", size: sm, color: muted)
}
```

## Add a task

It goes after the selected task, or after the last one when none is selected.

```bib
let newName = ""
let newOwner = ""
let newDays = 0
let after = plan.find(t => t.id == sel) ?? plan[plan.length - 1]
box(dir: "row", gap: 16, align: end, wrap: true) {
  newName = input("New task", placeholder: "e.g. Accessibility review")
  newOwner = choice("Owner", data.owners, style: segmented)
  box(width: 200) { newDays = slider("Working days", 0, 20, value: 3, step: 1) }
}
row(fit: true) {
  if button("Add after “{after.name}”", id: "add", style: primary, disabled: newName.trim() == "").clicked {
    remember()
    set("added", [...added, {id: "x{added.length + 1}", name: newName.trim(), owner: newOwner, dur: newDays, deps: [after.id]}])
    set("New task", "")
  }
  if button("Remove the last added", style: ghost, disabled: added.length == 0).clicked {
    remember()
    set("added", added.slice(0, added.length - 1))
  }
}
```

## Summary

```bib
let rowBg = t => t.id == sel ? fade(accent, 0.14) : null
// On a phone the row numbers give their room to the rest.
let numbered = page.width >= 480
let rows = plan.map((t, i) => [
  {text: str(i + 1), background: rowBg(t)},
  {text: t.dur == 0 ? "◆ {t.name}" : t.name, background: rowBg(t)},
  t.owner, t.dur,
  {text: fmtDate(workDate(t.es), "ddd D MMM"), value: t.es},
  t.dur == 0 ? "–" : fmtDate(workDate(t.ef - 1), "ddd D MMM"),
  {badge: t.slack == 0 ? "critical" : "{t.slack} d", tone: t.slack == 0 ? danger : success, value: t.slack}])
let from = numbered ? 0 : 1
table(["#", "Task", "Owner", "Days", "Start", "Finish", "Slack"].slice(from), rows.map(r => r.slice(from)),
  widths: [0.4, 3, 1.6, 0.7, 1.2, 1.2, 1.2].slice(from), align: ["end", "start", "start", "end", "start", "start", "start"].slice(from))

text("Critical path", size: sm, weight: bold, color: muted)
chips(crit.map(t => t.name), tone: danger)

if button("Send the plan").clicked {
  respond(finish: fmtDate(dateAt(finishOff), "YYYY-MM-DD"), workingDays: end,
    tasks: plan.map(t => ({id: t.id, name: t.name, start: workDate(t.es), days: t.dur, slack: t.slack})))
}
```

---
bib: 1
title: "Kanban: Lumen Notes"
width: wide
data:
  columns:
    - { id: todo, name: To do }
    - { id: doing, name: Doing, limit: 3 }
    - { id: review, name: Review, limit: 2 }
    - { id: done, name: Done }
  owners: [Designer, Frontend, Backend, PM, QA]
  tags: [feature, bug, chore, research]
  prios: [P1, P2, P3]
  cards:
    - { id: 1, col: todo, title: Offline sync for shared notebooks, owner: Backend, tag: feature, prio: P1 }
    - { id: 2, col: todo, title: Empty state for the search page, owner: Designer, tag: feature, prio: P3 }
    - { id: 3, col: todo, title: Interview five teachers about tagging, owner: PM, tag: research, prio: P2 }
    - { id: 4, col: todo, title: Upgrade the build toolchain, owner: Frontend, tag: chore, prio: P3 }
    - { id: 5, col: doing, title: Note list jumps when a note is pinned, owner: Frontend, tag: bug, prio: P1 }
    - { id: 6, col: doing, title: Rate-limit the share-link endpoint, owner: Backend, tag: feature, prio: P2 }
    - { id: 7, col: doing, title: Onboarding checklist copy, owner: PM, tag: chore, prio: P3 }
    - { id: 8, col: review, title: Dark mode for the editor toolbar, owner: Designer, tag: feature, prio: P2 }
    - { id: 9, col: review, title: Export to Markdown loses checkboxes, owner: Backend, tag: bug, prio: P1 }
    - { id: 10, col: review, title: Regression suite for attachments, owner: QA, tag: chore, prio: P2 }
    - { id: 11, col: done, title: Keyboard shortcut sheet, owner: Frontend, tag: feature, prio: P3 }
    - { id: 12, col: done, title: Crash on empty notebook title, owner: QA, tag: bug, prio: P1 }
---
# Lumen Notes · Sprint 14

```bib
// ---- state -----------------------------------------------------------------
let cards = state("cards", data.cards)
let nextId = state("next", 13)
let sel = state("sel", null)          // the card clicked last: the keys act on it
let renaming = state("renaming", null)
let form = state("form", null)        // null, "new", or the id of the card being edited
let moving = state("moving", null)    // the card being dragged
let tagsOn = state("tags", [])
let ownersOn = state("owners", [])
let cols = data.columns
let colIndex = id => cols.findIndex(c => c.id == id)
let tagTone = t => t == "bug" ? danger : t == "feature" ? accent : t == "research" ? success : muted
let prioTone = p => p == "P1" ? danger : p == "P2" ? warning : faint
let inCol = (list, id) => list.filter(c => c.col == id)
let over = c => c.limit != null && inCol(cards, c.id).length > c.limit
let toggled = (xs, x) => xs.includes(x) ? xs.filter(y => y != x) : [...xs, x]

// Move card `id` into column `col`, before card `before` (or at the column's end).
fn moveTo(id, col, before) {
  let card = {...cards.find(c => c.id == id), col}
  let rest = cards.filter(c => c.id != id)
  let at = before == null ? -1 : rest.findIndex(c => c.id == before)
  let last = max([-1, ...rest.map((c, i) => c.col == col ? i : -1)])
  let pos = at >= 0 ? at : last >= 0 ? last + 1 : rest.length
  return [...rest.slice(0, pos), card, ...rest.slice(pos)]
}
fn openForm(c) {
  set("form", c == null ? "new" : c.id)
  set("ftitle", c?.title ?? "")
  set("fowner", c?.owner ?? data.owners[0])
  set("ftag", c?.tag ?? data.tags[0])
  set("fprio", c?.prio ?? "P2")
  set("fcol", cols.find(x => x.id == (c?.col ?? "todo")).name)
}
fn remove(id) {
  set("cards", cards.filter(x => x.id != id))
  set("sel", null)
}

fn pill(label, tone: muted, on: true) {
  box(padding: [1, 8], radius: 9999, background: on ? fade(tone, 0.16) : surface, border: on ? 0 : 1, borderColor: border) {
    text(label, size: 11, color: on ? tone : muted, weight: bold)
  }
}

// ---- keys: on the clicked card, and n for a new one ----------------------------
let selCard = cards.find(c => c.id == sel)
if selCard != null && renaming == null && form == null {
  let i = colIndex(selCard.col)
  if key("right").pressed && i < cols.length - 1 { set("cards", moveTo(sel, cols[i + 1].id, null)) }
  if key("left").pressed && i > 0 { set("cards", moveTo(sel, cols[i - 1].id, null)) }
  if key("delete").pressed { remove(sel) }
  if key("enter").pressed { openForm(selCard) }
  if key("escape").pressed { set("sel", null) }
}
// Renaming saves as it is typed. The field keeps the first Enter (it lets go of the keys), so the
// Enter or Escape after that closes it; Escape puts the old title back.
if renaming != null {
  let t = value("rename", "").trim()
  if changed("rename") && t != "" { set("cards", cards.map(x => x.id == renaming.id ? {...x, title: t} : x)) }
  if key("enter").pressed { set("renaming", null) }
  if key("escape").pressed {
    set("cards", cards.map(x => x.id == renaming.id ? {...x, title: renaming.was} : x))
    set("renaming", null)
  }
}

// ---- header, filters -------------------------------------------------------------
let q = value("Search", "").trim().toLowerCase()
let visible = c => (tagsOn.length == 0 || tagsOn.includes(c.tag)) && (ownersOn.length == 0 || ownersOn.includes(c.owner)) &&
  (q == "" || "{c.title} {c.owner} {c.tag}".toLowerCase().includes(q))
let shownN = cards.count(visible)
let overCols = cols.filter(over).map(c => c.name)

box(dir: "row", align: center, gap: 12) {
  text("{cards.length} cards · {cards.count(c => c.col == "done")} done{shownN != cards.length ? " · {shownN} shown" : ""}{overCols.length > 0 ? " · " + overCols.join(", ") + " over limit" : ""}",
    color: muted, width: fill)
  if button("+ New card", style: primary, key: "n", disabled: form != null).clicked { openForm(null) }
}

box(dir: "row", wrap: true, gap: 8, align: center) {
  input("Search", placeholder: "title, owner or tag")
  for t in data.tags {
    let b = box(id: "tag-{t}", radius: 9999, hover: {borderColor: tagTone(t)}, tip: "Show {t} cards") { pill(t, tone: tagTone(t), on: tagsOn.includes(t)) }
    if b.clicked { set("tags", toggled(tagsOn, t)) }
  }
  text("·", color: faint)
  for o in data.owners {
    let b = box(id: "owner-{o}", radius: 9999, hover: {borderColor: accent}, tip: "Show {o}'s cards") { pill(o, tone: accent, on: ownersOn.includes(o)) }
    if b.clicked { set("owners", toggled(ownersOn, o)) }
  }
}

// ---- the new-card / edit form, open only while it is used ------------------------------
if form != null {
  let editing = cards.find(c => c.id == form)
  card(editing == null ? "New card" : "Edit card", tone: accent) {
    input("Title", id: "ftitle", placeholder: "What needs doing?")
    box(dir: "row", wrap: true, gap: 16) {
      choice("Owner", data.owners, id: "fowner", style: segmented)
      choice("Tag", data.tags, id: "ftag", style: segmented)
      choice("Priority", data.prios, id: "fprio", style: segmented)
      choice("Column", cols.map(c => c.name), id: "fcol", style: segmented)
    }
    let t = value("ftitle", "").trim()
    let fields = {title: t, owner: value("fowner"), tag: value("ftag"), prio: value("fprio"), col: cols.find(c => c.name == value("fcol")).id}
    row(fit: true) {
      if button(editing == null ? "Add" : "Save", style: primary, disabled: t == "").clicked {
        if editing == null {
          set("cards", [...cards, {id: nextId, ...fields}])
          set("next", nextId + 1)
          set("sel", nextId)
        } else {
          set("cards", cards.map(x => x.id == form ? {...x, ...fields} : x))
        }
        set("form", null)
      }
      if button("Cancel", style: ghost, key: "escape").clicked { set("form", null) }
      if editing != null {
        if button("Delete", style: danger).clicked { remove(form); set("form", null) }
      }
    }
  }
}

// ---- the board -----------------------------------------------------------------
let dropped = false
let held = false
let overCol = null
let overCard = null
row {
  for col in cols {
    let all = inCol(cards, col.id)
    let shown = all.filter(visible)
    let hot = over(col)
    let target = moving != null && state("overCol", null) == col.id
    let c = box(id: "col-{col.id}", gap: 8, padding: 10, radius: 14,
        background: target ? fade(accent, 0.10) : hot ? fade(danger, 0.08) : fade(muted, 0.08),
        border: 2, borderColor: target ? accent : hot ? fade(danger, 0.55) : fade(muted, 0.02)) {
      box(dir: "row", gap: 8, align: center) {
        text(col.name, weight: bold)
        pill(shown.length == all.length ? "{all.length}" : "{shown.length}/{all.length}", tone: hot ? danger : muted)
        if hot { text("over WIP limit ({col.limit})", size: sm, color: danger, weight: bold) }
        else if col.limit != null { text("limit {col.limit}", size: sm, color: faint) }
      }
      if shown.length == 0 {
        box(padding: [18, 8], radius: 10, border: 1, borderColor: border, align: center) {
          text(all.length > 0 ? "No cards match" : "Drop cards here", size: sm, color: faint)
        }
      }
      for k in shown {
        let picked = sel == k.id
        // where a dragged card would land: a line above the card it is over
        if moving != null && moving != k.id && state("overCard", null) == k.id {
          box(height: 3, radius: 2, background: accent)
        }
        let b = box(id: "card-{k.id}", padding: [8, 10, 8, 14], gap: 6, radius: 10, background: surface,
            border: picked ? 2 : 1, borderColor: picked ? accent : border, opacity: moving == k.id ? 0.4 : 1,
            hover: {borderColor: accent}, tip: "Drag to move. Click, then ← → Del Enter") {
          layer { rect(0, 0, 4, h, fill: prioTone(k.prio)) }     // the priority stripe
          if renaming?.id == k.id {
            input("Rename", id: "rename")
            text("Saved as you type. Enter twice closes, Escape undoes", size: 11, color: faint)
          } else {
            let t = box(id: "title-{k.id}", hover: {background: fade(accent, 0.08)}, tip: picked ? "Click to rename" : null) {
              text(k.title, weight: bold, size: sm)
            }
            // a box inside another answers first: the title selects the card, or renames it once selected
            if t.pressed && moving == null { set("moving", k.id) }   // and it carries the card, as the card does
            if t.pressed { held = true }
            if t.released { dropped = true }
            if t.clicked { if picked { set("renaming", {id: k.id, was: k.title}); set("rename", k.title) } else { set("sel", k.id) } }
          }
          box(dir: "row", gap: 6, align: center) {
            pill(k.tag, tone: tagTone(k.tag))
            text(k.owner, size: sm, color: muted, width: fill)
            text(k.prio, size: sm, face: mono, weight: bold, color: prioTone(k.prio))
          }
          if picked && renaming?.id != k.id {
            row(fit: true) {
              if button("Edit", id: "edit-{k.id}", style: ghost).clicked { openForm(k) }
              if button("Delete", id: "del-{k.id}", style: ghost).clicked { remove(k.id) }
            }
          }
        }
        if b.pressed && renaming?.id != k.id {
          held = true
          if moving == null { set("moving", k.id) }
        }
        if b.clicked { set("sel", k.id) }
        if b.released { dropped = true }
        if b.hovered { overCard = k.id }
      }
    }
    if c.hovered { overCol = col.id }
  }
}

// Each move while a card is held is a press: remember what is under the pointer, to draw the
// target. On release, drop the card there: before the card it is over, or at the column's end.
if held { set("overCol", overCol); set("overCard", overCard) }
if dropped {
  if overCol != null && moving != null && overCard != moving { set("cards", moveTo(moving, overCol, overCard)) }
  set("moving", null)
  set("overCol", null)
  set("overCard", null)
}
```

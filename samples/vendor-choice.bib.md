---
bib: 1
title: Which supplier for the pilot run
data:
  vendors:
    - { name: Northwind, price: 41000, lead: 6, quality: 8, service: 6, fit: 7 }
    - { name: Contoso, price: 38500, lead: 10, quality: 9, service: 5, fit: 5 }
    - { name: Fabrikam, price: 45250, lead: 4, quality: 6, service: 9, fit: 8 }
  steps: [Quote in hand, Demo booked, Reference call, Budget signed]
effects: [respond, send]
---
# Which supplier for the pilot run

Three vendors are left. Move the weights to say what matters, and the
scores follow. The prose around the blocks is ordinary Markdown.

```bib
let wq = slider("Quality weight", 0, 10, value:0, step:0.1)
let ws = slider("Service weight", 0, 10, value: 0.1, step:1.0)
let wf = slider("Fit weight", 0, 10, value: 2)
let score = v => {
  let total = wq + ws + wf
  return total == 0 ? 0 : round((v.quality * wq + v.service * ws + v.fit * wf) / total, 1)
}
let best = max(data.vendors.map(score))
```

The cards below read the weights from the block above: the blocks of one
document are one program.

```bib
row {
  for v in data.vendors {
    card(v.name, subtitle: "{v.lead} weeks", tone: score(v) == best ? accent : neutral) {
      stat("Score", score(v))
      stat("Price", v.price, unit: "EUR")
      if score(v) == best { badge("best", tone: accent) }
    }
  }
}
```

## Deciding

```bib
let done = checklist("Before we choose", data.steps)
progress("Ready", done.length, max: data.steps.length)

let pick = choice("Pick", data.vendors.map(v => v.name))
let note = input("Note", placeholder: "Why this one")
let urgent = toggle("Urgent")

if button("Choose", disabled: done.length < data.steps.length).clicked {
  respond(vendor: pick, score: score(data.vendors.find(v => v.name == pick)), urgent: urgent, note: note)
}
if button("Ask the team", style: ghost).clicked {
  send("Which supplier? I lean to {pick}{urgent ? " (urgent)" : ""}. {note}")
}
```

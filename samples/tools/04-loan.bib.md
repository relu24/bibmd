---
bib: 1
title: What does the mortgage really cost?
width: wide
effects: [download]
---
###### A calculator

# What does the mortgage really cost?

Set up two loans side by side: the same house on a 30-year and a 15-year term, or the same
loan with and without an extra payment each month. Every number, chart and row below follows
as you type.

```bib
// Money as people read it: whole dollars, or cents.
let usd = x => fmt(x, 0, currency: "USD")
let usd2 = x => fmt(x, currency: "USD")
// A typed number, with its thousands separators; anything else (empty, a typo) is `d`.
let typed = (s, d) => { let v = num(s.split(",").join("").trim()); return v >= 0 ? v : d }

// One scenario's fields: typed, each with its own id. The first payment is a month, "2027-01".
fn inputs(tag, extra) {
  let f = {}
  grid(columns: 2) {
    f = {...f, amount: input("Loan amount (USD)", value: "420000", id: "amount {tag}")}
    f = {...f, rate: input("Interest rate (% a year)", value: "6.5", id: "rate {tag}")}
    f = {...f, years: input("Term (years)", value: "30", id: "years {tag}")}
    f = {...f, extra: input("Extra each month (USD)", value: str(extra), id: "extra {tag}")}
    f = {...f, start: input("First payment (YYYY-MM)", value: "2027-01", id: "start {tag}")}
  }
  let bad = ["amount", "rate", "years", "extra"].filter(k => typed(f[k], -1) < 0)
  if bad.length > 0 { text("Not a number: {bad.join(", ")}. It counts as 0 until it is one.", size: sm, color: danger) }
  let ym = f.start.split("-")
  let y = typed(ym[0], 2027)
  let m = typed(ym[1] ?? "", 1)
  return {amount: typed(f.amount, 0), rate: typed(f.rate, 0), years: Math.max(1, round(typed(f.years, 30))),
    extra: typed(f.extra, 0), start: date(y >= 1900 && y <= 2200 ? y : 2027, m >= 1 && m <= 12 ? m : 1, 1)}
}

// The schedule: a reduce over the months, as there is no loop to run until paid.
fn schedule(s) {
  let r = s.rate / 100 / 12
  let n = s.years * 12
  let pay = r == 0 ? s.amount / n : s.amount * r / (1 - Math.pow(1 + r, -n))
  let rows = range(n).reduce((acc, k) => {
    let last = acc.length == 0 ? {balance: s.amount, cumP: 0, cumI: 0} : acc[acc.length - 1]
    let interest = last.balance * r
    let principal = Math.min(last.balance, pay - interest + s.extra)
    return last.balance <= 0.005 ? acc : [...acc, {k, payment: principal + interest, principal, interest,
      balance: Math.max(last.balance - principal, 0), cumP: last.cumP + principal, cumI: last.cumI + interest}]
  }, [])
  let interest = rows.length == 0 ? 0 : rows[rows.length - 1].cumI
  // Without the extra the loan runs its whole term: n payments of pay.
  return {pay, rows, interest, months: rows.length, saved: s.extra > 0 ? n - rows.length : 0,
    savedInterest: s.extra > 0 ? pay * n - s.amount - interest : 0,
    payoff: rows.length == 0 ? "" : fmtDate(addMonths(s.start, rows.length - 1), "MMM YYYY")}
}

fn summary(s, R) {
  grid(columns: 2) {
    stat("Monthly payment", usd2(R.pay + s.extra))
    stat("Total interest", usd(R.interest))
    stat("Paid off", R.payoff)
    stat("Months saved by the extra", R.saved, tone: R.saved > 0 ? success : neutral)
  }
  let share = R.interest / Math.max(s.amount + R.interest, 1)
  pie([{label: "Principal", value: round(s.amount), tone: accent}, {label: "Interest", value: round(R.interest), tone: warning}],
    hole: 0.6, label: "{round(share * 100)}%\ninterest", height: 150)
  kv([
    {label: "Total paid", value: usd(s.amount + R.interest)},
    {label: "Interest saved by the extra", value: usd(R.savedInterest)},
  ])
}

let A = null
let B = null
let RA = null
let RB = null
row {
  card("Scenario A", badge: "A", badgeTone: accent) {
    A = inputs("A", 0)
    RA = schedule(A)
    summary(A, RA)
  }
  card("Scenario B", badge: "B", badgeTone: danger) {
    B = inputs("B", 300)
    RB = schedule(B)
    summary(B, RB)
  }
}

// Presets: B starts from A and changes one thing.
fn copyAtoB(change) {
  let b = {...A, ...change}
  set("amount B", str(b.amount)); set("rate B", str(b.rate)); set("years B", str(b.years))
  set("extra B", str(b.extra)); set("start B", fmtDate(b.start, "YYYY-MM"))
}
card("B against A", subtitle: "Start Scenario B from A, then change one thing") {
  row(fit: true) {
    if button("B = A").clicked { copyAtoB({}) }
    if button("B = A + $300 a month").clicked { copyAtoB({extra: A.extra + 300}) }
    if button("B = A on 15 years").clicked { copyAtoB({years: 15}) }
    if button("B = A at 1 point less").clicked { copyAtoB({rate: Math.max(0, round(A.rate - 1, 2))}) }
  }
  let dI = RA.interest - RB.interest
  let dM = RA.months - RB.months
  row {
    stat("Interest, B less A", (dI >= 0 ? "−" : "+") + usd(abs(dI)), tone: dI >= 0 ? success : danger)
    stat("Paid off", "{abs(dM)} months {dM >= 0 ? "sooner" : "later"}")
    stat("Monthly outlay", usd2(RB.pay + B.extra - RA.pay - A.extra) + " more")
  }
}
```

## Where the money goes

Each month's total is everything the loan has cost or still owes: the interest and the
principal paid so far, and the balance left. Move over the chart for a month.

```bib
let pick = choice("Chart", ["Scenario A", "Scenario B"], style: segmented)
let R = pick == "Scenario A" ? RA : RB
let top = Math.max(A.amount + RA.interest, B.amount + RB.interest)
let ks = R.rows.map(r => (r.k + 1) / 12)

plot(y: [0, top], height: 300, xLabel: "years from the first payment", yLabel: "USD") {
  series("Interest paid", ks, R.rows.map(r => r.cumI), tone: warning, stack: "money")
  series("Principal paid", ks, R.rows.map(r => r.cumP), tone: accent, stack: "money")
  series("Balance left", ks, R.rows.map(r => r.balance), tone: muted, stack: "money")
}
```

## Amortization schedule

```bib
let which = ""
let grain = ""
row(fit: true) {
  which = choice("Schedule of", ["Scenario A", "Scenario B"], style: segmented, id: "table of")
  grain = choice("By", ["Monthly", "Yearly"], style: segmented)
}
let T = which == "Scenario A" ? RA : RB
let TS = which == "Scenario A" ? A : B
let monthly = grain == "Monthly"

// Calendar years: the first holds the months left in the start year, then twelve each.
let y0 = dateParts(TS.start).year
let m0 = dateParts(TS.start).month - 1
let first = 12 - m0
let yearCount = T.months <= first ? 1 : 1 + ceil((T.months - first) / 12)
let years = monthly ? [] : range(yearCount).map(i => {
  let part = T.rows.slice(i == 0 ? 0 : first + (i - 1) * 12, first + i * 12)
  return [y0 + i, part.sum(r => r.payment), part.sum(r => r.principal), part.sum(r => r.interest), part[part.length - 1].balance]
})
let raw = monthly
  ? T.rows.map(r => [r.k + 1, fmtDate(addMonths(TS.start, r.k), "MMM YYYY"), r.payment, r.principal, r.interest, r.balance])
  : years
let head = monthly ? ["#", "Month", "Payment", "Principal", "Interest", "Balance"] : ["Year", "Payments", "Principal", "Interest", "Balance at year end"]
let lead = monthly ? 2 : 1

// Money as text that sorts by its number; December closes a year, so its row is shaded.
let money = (c, bg) => ({text: usd2(c), value: c, background: bg})
let cellsOf = r => {
  let bg = monthly && (m0 + r[0]) % 12 == 0 ? fade(accent, 0.08) : null
  return monthly
    ? [{text: str(r[0]), value: r[0], background: bg}, {text: r[1], value: r[0], background: bg},
       money(r[2], bg), money(r[3], bg), money(r[4], bg), money(r[5], bg)]
    : [{text: str(r[0]), value: r[0]}, money(r[1], null), money(r[2], null), money(r[3], null), money(r[4], null)]
}

let name = "amortization-{which == "Scenario A" ? "a" : "b"}-{monthly ? "monthly" : "yearly"}.csv"
let csv = () => [head.join(","), ...raw.map(r => r.map((c, j) => j == 1 && monthly ? "\"{c}\"" : j < lead ? str(c) : c.toFixed(2)).join(","))].join("\n") + "\n"
row(fit: true) {
  text("{raw.length} rows", size: sm, color: muted)
  if button("Download CSV", style: primary).clicked { download(name, csv()) }
}
table(head, raw.map(cellsOf), id: "schedule", sort: true, height: 440,
  align: monthly ? ["end", "start", "end", "end", "end", "end"] : ["start", "end", "end", "end", "end"],
  widths: monthly ? [1, 2, 2, 2, 2, 2] : [1, 2, 2, 2, 2])
```

# Gaps: 01 focus timer

Artifact: `samples/tools/artifacts/01-focus-timer.html` (352 lines).
Bib clone: `samples/tools/01-focus-timer.bib.md` (2 blocks, about 140 lines of bib; `bibmd --check`:
`ok, 2 bib block(s), 2,707 steps, 1,436 values`, down from 13,145 steps).

Round 2 rewrite: the clone is now a real timer. `clock(every:)` drives it (0.1 s while running, 1 s
otherwise), `alarm(sess.endsAt)` ends a session, logs it, moves the round on and, with "Start the next
session on its own", starts the next one. The ring is one `arc(…, cap: "round")` plus a tip `circle`;
Space / R / S are `button(…, key:)`, 1 / 2 / 3 are `key("1")…`; picking a mode by click runs
`changed("mode")`, which stops and resets the session. The hour chart is hand-built from boxes so
each bar has a `tip:` and a hover, with a dashed one-hour line; the log is a `table(height: 230)`; the
date comes from `today()`.

Proven headless with `/tmp/bibmd-tw-next/bibmd --png … --script`:
- `key:Space;wait:2500`: counts down to 24:57, the button says Pause, the ring's tip moves.
- `click:bib-go#0;wait:2200;key:Space`: Pause, then Resume.
- `key:Number3`: Long break, 15:00 (stopped).
- `key:Space;wait:2000;click:bib-mode-Short break`: the click on the choice runs `changed`, the session
  stops and shows 05:00 / Start.
- `key:Space;wait:1500;key:S`: logs "Focus (skipped) 0 min" at the local time, Short break next.
- `drag:bib-shortbreaklength:-400;key:Number2;key:Space;wait:61500`: the alarm rings after one minute,
  logs "Short break 1 min", mode back to Focus, toast "Break over. Back to it." (seen with a longer
  toast; the 4 s one had expired by the time the harness drew).
- `move:bibbox-hour-9;wait:900`: the tooltip "9:00 · 18 min" over the hovered bar.

## Gaps

### 1. No clock: time does not pass — **closed**
- **Was:** a simulated clock in state and four "let time pass" buttons.
- **Now:** `let now = clock(every: running ? 0.1 : 1)`, the session stored as `{endsAt, left, started}`,
  and `if alarm(sess.endsAt).rang { finish(false) }`. 25 lines of workaround gone.
- **Left over (new, see gap 9):** the clock is UTC seconds with no time zone, so the log's times of day
  need the reader's offset written into `data:`.

### 2. No animation: the ring jumps — **narrowed**
- **Now:** with `clock(every: 0.1)` the arc drains smoothly while running, which was the clumsy part.
- **Still open (cosmetic):** no eased values or transitions: the time does not pulse when a session ends,
  the tip does not glow, colours switch at once on a mode change, the toast does not slide. Proposed as
  before: `ease(value, ms:)` and `box(…, fade: ms)`.

### 3. No arc: the ring is 120 straight lines — **closed**
- **Now:** `arc(150, 150, 124, 0, frac, color: tone, width: 14, cap: "round")`; `data.ring` and the
  120-segment loop are gone, and with them some 10,000 steps a frame. A `ring(…)` widget would still be
  shorter, but the layer is three lines and reads well.

### 4. Layout order is evaluation order — **narrowed**
- **Now:** `value("Focus length", 25)` reads the slider drawn later, with a default; no more `?? 25`.
- **Still:** the default is written twice (in `value(…, 25)` and in `slider(…, value: 25)`); if they
  disagree the timer starts wrong until the slider is touched. Cosmetic-to-clumsy. Proposed: `value(key)`
  without a default returns the input's declared `value:` once the input has drawn in an earlier frame.

### 5. No change event on an input — **closed**
- **Now:** `if changed("mode") { set("session", fresh) }`. Proven by script (see above). The session no
  longer has to carry its mode to fake a reset.

### 6. No keyboard shortcuts — **closed**
- **Now:** `button("Reset", key: "r")`, `button(…, id: "go", key: "space")`, `button("Skip", key: "s")`,
  and `key("1")`, `key("2")`, `key("3")` branches that `set` mode and session.

### 7. No sound, no notification, no title — **still open**
- Nothing new: the end of a session is a toast on the page only, and the window title does not show the
  countdown. Severity: clumsy (a timer you have to watch). Proposed as before: a `notify("Focus done",
  sound: true)` effect, allowed in an alarm branch, and `page(title: …)`.

### 8. Smaller things
- **Tooltips on the hour bars — narrowed.** `bars` itself still has no tip, so the chart is built from 14
  boxes with `id`, `tip:` and `hover:` and a `rect` each (about 15 lines instead of one `bars` call).
  Proposed: `bars` shows each item's value (or an item's `tip`) on hover, as `plot` already does.
- **A reference line at 60 min — narrowed:** a `line(…, dash: [3, 3])` in the hand-built chart; `bars`
  takes no `rule`.
- **Log in a 230 px scroll box — closed:** `table(…, height: 230)`.
- **Per-mode slider colours — still open:** sliders have no `tone:`.
- **The time in tabular figures — still open (cosmetic):** the mono face sets the colon wide ("25 : 00").
- **Toast — narrowed:** a `badge` shown while `now < flash.until` (4 s), so it now goes away by itself;
  it is not an overlay and does not slide.

### 9. New: no time of day — **still open**
- **Artifact:** `toTimeString().slice(0, 5)` for the log, `getHours()` for the current-hour bar.
- **Bib doc:** `clock()` is UTC seconds and `today()` is the local date, but nothing gives the local
  time of day or the offset. The doc puts `utcOffset: 1` in `data:` and computes
  `floor(t + data.utcOffset * 3600) % 86400`; it is wrong for a reader in another zone and wrong for half
  the year (summer time).
- **Severity:** clumsy.
- **Fix belongs:** bibdoc.
- **Proposed API:** `fmtTime(t, "HH:mm")` in the reader's zone, and/or `timeOfDay(t)` returning
  `{hour, minute, second, seconds}` local; or `today()` alongside a `now()` giving `{date, hour, minute}`.

## Bugs hit

Round 1 bugs: **1 (set-only click says "does nothing") — fixed**: no notice is drawn, and `--check`
lists each click's sets. **2 (`style: primary` not drawn) — fixed**: Start is drawn as primary.
**3 (script click below the fold) — not retested.**

New in round 2:

1. **A `fn` named like a widget replaces it, and the error lands on the widget call.** Writing
   `fn toggle() { … }` and later `toggle("Start the next session on its own", value: false)` fails with
   `toggle() has no parameter 'value'` at the widget's line. `--check` should warn where the `fn` shadows
   a builtin. Repro:
   ```
   fn toggle() { text("x") }
   toggle("On", value: false)
   ```
2. **A layer's `h` silently shadows a variable named `h`.** In
   `for h in hours { box(…) { layer { rect(…, fill: h == nowH ? a : b) } } }` the `h` is the box's height,
   and the error is `w and h are only known when the box is drawn: they cannot be compared`, which does not
   say that the loop's `h` is hidden. Repro:
   ```
   for h in [1, 2] { box(width: 20, height: 20) { layer { rect(0, 0, w, h, fill: h == 1 ? accent : muted) } } }
   ```
   Proposed: a warning when a layer's `w`/`h` hides an outer name.
3. **(Tooling) `--script key:3` presses some other key and says nothing.** `Enum.Parse<Key>("3")` accepts
   the number as the enum's value 3. The digit key is `Number3`. Proposed: reject numeric names, or map
   `"3"` to `Number3`.
4. **(Tooling) ids the script needs are not where the brief says.** A button with `id: "go"` is
   `bib-go#0`, not `bib-go`; a segmented choice's option is `bib-<key>-<Option>` (`bib-mode-Short break`).
   Worth a line in `--help` or the reference.

## Top 3 still missing (01 focus timer)

1. **Local time of day** (`fmtTime` / `timeOfDay`): the log and the "now" bar need a hand-written UTC offset.
2. **A notification effect and a window title** (`notify(…, sound: true)`, `page(title:)`): the timer still
   has to be watched.
3. **Hover values on `bars`** (and a `rule` on bars): the hour chart is 15 hand-built lines instead of one call.

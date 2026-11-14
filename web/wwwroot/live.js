// A shared document's client, in the viewer (hosted bibmd). The server keeps the one order of changes;
// this keeps three texts: the server's as last heard (shadow, at revision seq), the one change sent and
// not yet acknowledged (inflight), and the reader's own (text), which may hold more besides. A change
// from someone else is moved past what is in flight and unsent before it is shown; what is in flight is
// moved past it, as the server will move it. Positions are UTF-16 code units, as in C#.
//
// diff, apply and transform are Hosted/Splices.cs line for line: the two must agree.

export const apply = (s, op) => s.slice(0, op.start) + op.text + s.slice(op.end);
const delta = (op) => op.text.length - (op.end - op.start);
const empty = (op) => op.start === op.end && op.text.length === 0;

export function diff(a, b) {
  let p = 0;
  const max = Math.min(a.length, b.length);
  while (p < max && a[p] === b[p]) p++;
  let s = 0;
  while (s < max - p && a[a.length - 1 - s] === b[b.length - 1 - s]) s++;
  return { start: p, end: a.length - s, text: b.slice(p, b.length - s) };
}

export function transform(a, b, aFirst) {
  const shift = (op, by) => ({ ...op, start: op.start + by, end: op.end + by });
  if (a.start === a.end && b.start === b.end && a.start === b.start) return aFirst ? a : shift(a, delta(b));
  if (b.end <= a.start) return shift(a, delta(b));
  if (b.start >= a.end) return a;
  return null;
}

const map = (p, op) => p < op.start ? p : p >= op.end ? p + delta(op) : op.start;
const unmap = (p, op) => { const e = op.start + op.text.length; return p <= op.start ? p : p >= e ? p - delta(op) : op.start; };

// viewer: the .NET exports (ApplyRemote, SetRemotes, SetLive, DropEdit); send: a message to the server.
export function createLive(viewer, send, flash) {
  let on = false, seq = 0, shadow = '', inflight = null, text = '', nextId = 0;
  let you = '', people = [], claim = null, typingTimer = 0, typingNext = null;

  const pendingOps = () => {
    const ops = [];
    if (inflight) ops.push(inflight);
    const u = diff(inflight ? apply(shadow, inflight) : shadow, text);
    if (!empty(u)) ops.push(u);
    return ops;
  };

  function pump() {
    if (!on || inflight || text === shadow) return;
    const d = diff(shadow, text);
    inflight = { ...d, id: ++nextId };
    send({ t: 'splice', id: inflight.id, base: seq, start: d.start, end: d.end, text: d.text });
  }

  // The others, in this reader's positions: their held block and what they type in it.
  function showPeople() {
    const ops = pendingOps();
    const others = people.filter(p => p.conn !== you && p.claim).map(p => {
      let { start, end } = p.claim;
      for (const op of ops) { start = map(start, op); end = Math.max(start, map(end, op)); }
      return { name: p.name, color: p.color, start, end, isNew: p.claim.isNew, typing: p.typing ?? null, caret: p.caret ?? 0 };
    });
    viewer.SetRemotes(JSON.stringify(others));
  }

  // Show text as the reader's own, as one change from what the viewer has.
  function become(next) {
    const fix = diff(text, next);
    text = next;
    if (!empty(fix)) viewer.ApplyRemote(fix.start, fix.end, fix.text);
  }

  function resync() { send({ t: 'sync' }); }

  return {
    get on() { return on; },
    // The document as the page opened it, at revision rev: the base until the server's state arrives.
    opened(initial, rev) { shadow = text = initial; seq = rev; inflight = null; },
    changed(now) { text = now; pump(); },
    editing(start, end, isNew) {
      if (start < 0) { claim = null; send({ t: 'claim', start: -1 }); return; }
      // From this reader's positions to the server's: unsent changes, then what is in flight, taken back.
      for (const op of pendingOps().reverse()) { start = unmap(start, op); end = Math.max(start, unmap(end, op)); }
      claim = { start, end, isNew };
      send({ t: 'claim', ...claim });
    },
    typing(t, caret) {
      typingNext = { t: 'typing', text: t, caret };
      if (!typingTimer) typingTimer = setTimeout(() => { typingTimer = 0; if (typingNext) send(typingNext); typingNext = null; }, 100);
    },
    receive(m) {
      switch (m.t) {
        case 'state': {
          // Join, or join again: what this reader changed meanwhile is moved onto the server's text.
          const mine = diff(shadow, text), theirs = diff(shadow, m.text);
          let next = m.text;
          if (!empty(mine)) {
            const moved = empty(theirs) ? mine : transform(mine, theirs, false);
            if (moved) next = apply(m.text, moved);
            else flash('Your last change clashed with someone else’s and was not kept');
          }
          on = true; you = m.you; people = m.people; seq = m.seq; shadow = m.text; inflight = null;
          viewer.SetLive(true);
          become(next);
          if (claim) send({ t: 'claim', ...claim });
          showPeople();
          pump();
          break;
        }
        case 'splice': {
          const r = { start: m.start, end: m.end, text: m.text };
          const before = inflight ? apply(shadow, inflight) : shadow;
          const unsent = diff(before, text);
          shadow = apply(shadow, r);
          seq = m.seq;
          let shown = r;
          if (inflight) {
            const r1 = transform(shown, inflight, true), i1 = transform(inflight, r, false);
            if (!r1 || !i1) { resync(); return; }
            shown = r1;
            inflight = { ...i1, id: inflight.id };
          }
          if (!empty(unsent)) {
            const r2 = transform(shown, unsent, true);
            if (!r2) { resync(); return; }
            shown = r2;
          }
          text = apply(text, shown);
          viewer.ApplyRemote(shown.start, shown.end, shown.text);
          showPeople();
          break;
        }
        case 'ack':
          if (!inflight || m.id !== inflight.id) return;
          shadow = apply(shadow, { start: m.start, end: m.end, text: m.text });
          seq = m.seq;
          inflight = null;
          pump();
          break;
        case 'reject':
          inflight = null;
          resync();
          break;
        case 'people':
          people = m.people;
          showPeople();
          break;
        case 'typing': {
          const p = people.find(p => p.conn === m.conn);
          if (p) { p.typing = m.text; p.caret = m.caret; showPeople(); }
          break;
        }
        case 'claimed':
          if (!m.ok) { claim = null; viewer.DropEdit(m.by); }
          break;
      }
    },
  };
}

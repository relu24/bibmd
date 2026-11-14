// Everything on the page but the document: the bar, the section strip, the menu on the left (contents,
// history, keys), the insert and block menu, the overflow menu and the notes. The canvas draws only the
// document; each frame it says what this shows (Viewer's state, App.HostState) when that changes, and
// what is pressed here goes back to it as a command (Viewer.Command, GoTo, ChooseMenu…).
//
// Hosted, the bar is the app's page's, outside the frame: the state goes out to it as well, and its
// buttons come back as commands (main.js relays both).

const $ = (id) => document.getElementById(id);
const el = (tag, props = {}, ...children) => {
  const e = Object.assign(document.createElement(tag), props);
  e.append(...children);
  return e;
};
const SIZES = ['S', 'M', 'L', 'XL'];

export function createChrome({ viewer, canvas, hosted, onAct, onState }) {
  const body = document.body;
  let state = {};
  let outlineKey = '', historyKey = '', keysKey = '';
  let menu = null;        // the insert menu while open: { items, pick }
  let toastTimer = 0;

  const command = (name) => { viewer().Command(name); };
  const back = () => canvas.focus({ preventScroll: true });

  // Buttons on the bar and in the menus keep the canvas focused: the keys go on working after a press.
  const keepFocus = (root) => root.addEventListener('pointerdown', (e) => {
    if (e.target.closest('button') && !e.target.closest('#panel-list, #palette')) e.preventDefault();
  });
  keepFocus($('bar')); keepFocus($('more')); keepFocus($('strip'));

  document.addEventListener('click', (e) => {
    const b = e.target.closest('[data-cmd]');
    if (b) { command(b.dataset.cmd); if (b.closest('#more') && !b.closest('.size')) closeMore(); return; }
    const m = e.target.closest('[data-mode]');
    if (m) { if ((m.dataset.mode === 'write') === state.locked) command('lock'); return; }
    const a = e.target.closest('[data-act]');
    if (a) { closeMore(); onAct(a.dataset.act); }
  });

  // ── The overflow menu ──
  const more = $('more'), moreButton = $('t-more');
  if (hosted) for (const x of more.querySelectorAll('.standalone')) x.remove();
  function openMore() {
    const r = moreButton.getBoundingClientRect();
    more.classList.add('on');
    more.style.top = `${r.bottom + 6}px`;
    more.style.left = `${Math.max(8, r.right - more.offsetWidth)}px`;
    moreButton.setAttribute('aria-expanded', 'true');
  }
  function closeMore() { more.classList.remove('on'); moreButton.setAttribute('aria-expanded', 'false'); }
  moreButton.addEventListener('click', () => more.classList.contains('on') ? closeMore() : openMore());
  document.addEventListener('pointerdown', (e) => {
    if (!e.target.closest('#more, #t-more')) closeMore();
    if (menu && !e.target.closest('#palette, #t-insert')) closeMenu(false);
  });

  // ── The menu on the left ──
  const list = $('panel-list');
  for (const t of document.querySelectorAll('.tab')) t.addEventListener('click', () => {
    if (t.dataset.tab !== state.tab) command(t.dataset.tab);
  });
  $('scrim').addEventListener('click', () => command('close-panel'));

  function renderPanel(force) {
    const tab = state.tab;
    for (const t of document.querySelectorAll('.tab')) t.setAttribute('aria-selected', String(t.dataset.tab === tab));
    if (tab === 'contents') {
      const key = `${state.rev}|${state.current}`;
      if (!force && key === outlineKey) return;
      outlineKey = key; historyKey = keysKey = '';
      const heads = JSON.parse(viewer().Outline());
      if (!heads.length) { list.replaceChildren(el('p', { className: 'note', textContent: 'No headings yet. Lines that start with # show here.' })); return; }
      list.replaceChildren(...heads.map(h => {
        const b = el('button', { className: 'row' + (h.level === 0 ? ' top' : ''), textContent: h.text || '(untitled)' });
        b.style.paddingLeft = `${10 + 14 * h.level}px`;
        if (h.heading === state.current) b.setAttribute('aria-current', 'true');
        b.addEventListener('click', () => { viewer().GoTo(h.block); if (matchMedia('(max-width: 720px)').matches) command('close-panel'); back(); });
        return b;
      }));
      list.querySelector('[aria-current]')?.scrollIntoView({ block: 'nearest' });
    } else if (tab === 'history') {
      const key = `${state.versions}|${state.at}`;
      if (!force && key === historyKey) return;
      historyKey = key; outlineKey = keysKey = '';
      const versions = JSON.parse(viewer().History());
      const rows = [];
      for (let i = versions.length - 1; i >= 0; i--) {
        const v = versions[i], current = i === state.at;
        const b = el('button', { className: 'row' + (i > state.at ? ' undone' : '') },
          el('span', { textContent: v.summary }),
          el('span', { className: 'when', textContent: (v.added || v.removed ? `+${v.added} −${v.removed}  ` : '') + v.at }));
        if (current) b.setAttribute('aria-current', 'true');
        b.addEventListener('click', () => viewer().GoToVersion(i));
        rows.push(b);
      }
      list.replaceChildren(...rows);
    } else {
      const key = String(state.editing);
      if (!force && key === keysKey) return;
      keysKey = key; outlineKey = historyKey = '';
      const groups = JSON.parse(viewer().Keys());
      list.replaceChildren(...groups.flatMap(g => [
        el('div', { className: 'group', textContent: g.title }),
        ...g.keys.map(([k, what]) => el('div', { className: 'key' }, el('kbd', { textContent: k }), el('span', { textContent: what }))),
      ]));
    }
  }

  // The keys walk the list: the arrows, Home and End; Escape gives them back to the page.
  list.addEventListener('keydown', (e) => {
    const rows = [...list.querySelectorAll('.row')];
    const i = rows.indexOf(document.activeElement);
    const go = (n) => { e.preventDefault(); rows[Math.max(0, Math.min(rows.length - 1, n))]?.focus(); };
    if (e.key === 'ArrowDown' || e.key === 'j') go(i + 1);
    else if (e.key === 'ArrowUp' || e.key === 'k') go(i - 1);
    else if (e.key === 'Home') go(0);
    else if (e.key === 'End') go(rows.length - 1);
    else if (e.key === 'Escape') { e.preventDefault(); back(); }
  });
  $('panel').addEventListener('keydown', (e) => {
    if (e.key === 'Tab' && document.activeElement?.closest('#panel-list')) {
      e.preventDefault();
      const tabs = ['contents', 'history', 'keys'];
      command(tabs[(tabs.indexOf(state.tab) + (e.shiftKey ? 2 : 1)) % 3]);
      requestAnimationFrame(() => focusList());
    }
    if ((e.ctrlKey || e.metaKey) && e.code === 'KeyB') { e.preventDefault(); command('close-panel'); back(); }
  });
  const focusList = () => (list.querySelector('[aria-current]') ?? list.querySelector('.row') ?? list).focus();
  list.tabIndex = -1;

  // ── The section strip ──
  const strip = $('strip');
  let stripKey = '';
  function renderStrip() {
    const nav = state.source ? [] : state.nav ?? [];
    let active = -1;
    for (const n of nav) if (n.block <= state.current) active = n.block;
    const key = JSON.stringify(nav) + active;
    if (key === stripKey) return;
    stripKey = key;
    strip.classList.toggle('on', nav.length > 0);
    strip.replaceChildren(...nav.map(n => {
      const b = el('button', { className: 'pill', textContent: n.label });
      if (n.block === active) b.setAttribute('aria-current', 'true');
      b.addEventListener('click', () => viewer().GoTo(n.block));
      return b;
    }));
    strip.querySelector('[aria-current]')?.scrollIntoView({ inline: 'nearest', block: 'nearest' });
  }

  // ── The insert menu, and the block menu ──
  const palette = $('palette'), q = $('pal-q'), options = $('pal-list');
  function openMenu(m) {
    menu = { pick: 0, items: [] };
    $('pal-kind').textContent = m.block ? 'Block' : 'Insert';
    q.placeholder = m.block ? 'move, copy, turn into…' : 'heading, table 4x3, code…';
    q.value = m.query ?? '';
    palette.classList.add('on');
    place(m);
    fill();
    q.focus({ preventScroll: true });
  }
  function place(m) {
    if (matchMedia('(max-width: 720px)').matches) return;
    const c = canvas.getBoundingClientRect();
    const w = palette.offsetWidth, h = palette.offsetHeight || 320;
    let top = m.y != null ? c.top + m.y + 6 : c.top + 56;
    if (top + h > innerHeight - 12) top = Math.max(c.top + 12, innerHeight - 12 - h);
    palette.style.top = `${top}px`;
    palette.style.left = `${Math.max(12, c.left + (c.width - w) / 2)}px`;
  }
  function fill() {
    menu.items = JSON.parse(viewer().MenuItems(q.value));
    menu.pick = Math.min(menu.pick, Math.max(0, menu.items.length - 1));
    const rows = [];
    menu.items.forEach((it, i) => {
      if (i > 0 && it.name.startsWith('Turn into') && !menu.items[i - 1].name.startsWith('Turn into')) rows.push(el('div', { className: 'opt-sep' }));
      const b = el('button', { className: 'opt' + (it.name === 'Delete' ? ' danger' : ''), role: 'option', tabIndex: -1 },
        el('span', { textContent: it.name }), el('span', { className: 'hint', textContent: it.hint }));
      b.setAttribute('aria-selected', String(i === menu.pick));
      b.addEventListener('pointerdown', (e) => e.preventDefault());
      b.addEventListener('click', () => choose(i));
      b.addEventListener('pointermove', () => { if (menu.pick !== i) { menu.pick = i; mark(); } });
      rows.push(b);
    });
    if (!menu.items.length) rows.push(el('p', { className: 'note', textContent: 'Nothing called that. Try “table 4x3”.' }));
    options.replaceChildren(...rows);
  }
  function mark() {
    options.querySelectorAll('.opt').forEach((b, i) => b.setAttribute('aria-selected', String(i === menu.pick)));
    options.querySelectorAll('.opt')[menu.pick]?.scrollIntoView({ block: 'nearest' });
  }
  function hideMenu() { menu = null; palette.classList.remove('on'); }
  function choose(i) { const query = q.value; hideMenu(); viewer().ChooseMenu(query, i); back(); }
  function closeMenu(takeBack) { if (!menu) return; const query = q.value; hideMenu(); viewer().CloseMenu(query, takeBack); back(); }
  q.addEventListener('input', () => { menu.pick = 0; fill(); });
  q.addEventListener('keydown', (e) => {
    if (!menu) return;
    const n = menu.items.length;
    const step = (by) => { e.preventDefault(); if (n) { menu.pick = (menu.pick + by + n) % n; mark(); } };
    if (e.key === 'ArrowDown' || (e.ctrlKey && e.key === 'n')) step(1);
    else if (e.key === 'ArrowUp' || (e.ctrlKey && e.key === 'p')) step(-1);
    else if (e.key === 'Tab') step(e.shiftKey ? -1 : 1);
    else if (e.key === 'Enter') { e.preventDefault(); if (n) choose(menu.pick); }
    else if (e.key === 'Escape') { e.preventDefault(); closeMenu(false); }
    else if (e.key === 'Backspace' && q.value === '') { e.preventDefault(); closeMenu(true); }
  });

  // ── Notes ──
  const toast = $('toast');
  function say(text, error) {
    toast.textContent = text;
    toast.classList.toggle('error', !!error);
    toast.classList.add('on');
    clearTimeout(toastTimer);
    if (!error) toastTimer = setTimeout(() => toast.classList.remove('on'), 2200);
  }

  // ── The state, each time the canvas says it changed ──
  function update(json) {
    const was = state;
    state = JSON.parse(json);
    onState?.(state, json);
    document.documentElement.dataset.theme = state.dark ? 'dark' : 'light';
    body.classList.toggle('locked', state.locked);
    body.classList.toggle('panel', state.panel);
    $('t-panel').setAttribute('aria-pressed', String(state.panel));
    $('t-source').setAttribute('aria-pressed', String(state.source));
    $('t-read').setAttribute('aria-pressed', String(state.locked));
    $('t-write').setAttribute('aria-pressed', String(!state.locked));
    $('size-out').textContent = SIZES[state.size] ?? state.size;
    const theme = $('m-theme');
    theme.querySelector('use').setAttribute('href', state.dark ? '#i-sun' : '#i-moon');
    theme.querySelector('span').textContent = state.dark ? 'Light' : 'Dark';
    if (state.panel) renderPanel(!was.panel || was.tab !== state.tab);
    if (state.panelFocus !== was.panelFocus && was.panelFocus !== undefined) requestAnimationFrame(focusList);
    renderStrip();
    if (state.menu && !menu) openMenu(state.menu);
    else if (!state.menu && menu) hideMenu();
    if (state.flashes !== was.flashes && state.flash) say(state.flash);
    if (state.error !== was.error) state.error ? say(state.error, true) : toast.classList.remove('on');
  }

  return { update, say, command, get state() { return state; } };
}

/** Sets the bar's title and path (the standalone page; hosted, the app's page shows its own). */
export function showTitle(title, path) {
  $('title').textContent = title;
  $('path').textContent = path;
}

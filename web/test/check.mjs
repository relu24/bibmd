// Checks a built web/dist in headless Chromium: serves it, opens the page with no document
// (the sourdough notebook) and with ?doc= naming the other sample, and checks that each
// starts, draws on its canvas and says nothing on the console's error stream, and that Ctrl+T
// changes the theme (the frame differs); that a finger dragged up scrolls the page; that a key, an IME's composition and a phone keyboard's text
// type into an open block (a finger's tap opens one), the keyboard asked for only while it is open, the IME's place at its caret and
// its composition drawn in the block before it is committed; that a JPEG and a PNG beside a document draw (test/fixture,
// served at /test/) and its link to Markdown opens in place when tapped; that emoji draw in colour; and that a
// missing document says so; and that the page's bar and menus drive it (Ctrl+B, /, Read).
//
//   node web/test/check.mjs [dist] [--shots DIR]
//
// Node 22 or later; CHROME names the browser, or chromium is on PATH (none: SKIP).
import { spawn, spawnSync } from 'node:child_process';
import { createServer } from 'node:http';
import { readFile, writeFile, mkdir, mkdtemp, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, extname, resolve, sep, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);
const shotsAt = args.indexOf('--shots');
const shots = shotsAt >= 0 ? args.splice(shotsAt, 2)[1] : null;
const dist = resolve(args[0] ?? join(here, '..', 'dist'));
const sleep = ms => new Promise(r => setTimeout(r, ms));
const chrome = [process.env.CHROME, 'chromium', 'chromium-browser', 'google-chrome']
  .filter(Boolean).find(b => spawnSync(b, ['--version'], { stdio: 'ignore' }).status === 0);
if (!chrome) { console.log('SKIP: no Chromium (set CHROME)'); process.exit(0); }

const types = { '.html': 'text/html', '.js': 'text/javascript', '.wasm': 'application/wasm', '.json': 'application/json',
  '.md': 'text/markdown', '.png': 'image/png', '.jpg': 'image/jpeg', '.dat': 'application/octet-stream', '.ttf': 'font/ttf' };
const server = createServer(async (req, res) => {
  let path = decodeURIComponent(new URL(req.url, 'http://x').pathname);
  if (path.endsWith('/')) path += 'index.html';
  const root = path.startsWith('/test/') ? join(here, 'fixture') : dist;
  const file = resolve(join(root, path.replace(/^\/test\//, '/')));
  if (!file.startsWith(root + sep)) { res.writeHead(403).end(); return; }
  try {
    const body = await readFile(file);
    res.writeHead(200, { 'content-type': types[extname(file)] ?? 'application/octet-stream' }).end(body);
  } catch { res.writeHead(404).end(); }
});
await new Promise(r => server.listen(0, '127.0.0.1', r));
const origin = `http://127.0.0.1:${server.address().port}`;

const profile = await mkdtemp(join(tmpdir(), 'bibmd-web-'));
const browser = spawn(chrome, ['--headless=new', '--no-sandbox', '--disable-gpu', '--remote-debugging-port=0',
  `--user-data-dir=${profile}`, '--window-size=1100,800', 'about:blank'], { stdio: ['ignore', 'ignore', 'pipe'] });
const wsUrl = await new Promise((ok, no) => {
  let err = '';
  browser.stderr.on('data', d => { err += d; const m = /DevTools listening on (ws:\S+)/.exec(err); if (m) ok(m[1]); });
  setTimeout(() => no(new Error('Chromium did not start: ' + err)), 30000);
});

const ws = new WebSocket(wsUrl);
await new Promise(r => ws.addEventListener('open', r));
let id = 0;
const pending = new Map(), listeners = [];
ws.addEventListener('message', e => {
  const m = JSON.parse(e.data);
  if (m.id && pending.has(m.id)) { const p = pending.get(m.id); pending.delete(m.id); m.error ? p.no(new Error(m.error.message)) : p.ok(m.result); }
  else listeners.forEach(l => l(m));
});
const send = (method, params = {}, sessionId) => new Promise((ok, no) => {
  const n = ++id; pending.set(n, { ok, no }); ws.send(JSON.stringify({ id: n, method, params, sessionId }));
});

// Where the typing fixture's paragraph is drawn, at the window size above.
const CLICK = { x: 205, y: 153 };
const KEYCODES = { End: 35, Escape: 27, s: 83 };

let failures = 0;
const check = (ok, what) => { console.log(`${ok ? 'ok  ' : 'FAIL'} ${what}`); if (!ok) failures++; };

async function page(query, name, then) {
  const { targetId } = await send('Target.createTarget', { url: 'about:blank' });
  const { sessionId } = await send('Target.attachToTarget', { targetId, flatten: true });
  const errors = [];
  listeners.push(m => {
    if (m.sessionId !== sessionId) return;
    if (m.method === 'Runtime.consoleAPICalled' && m.params.type === 'error') errors.push(m.params.args.map(a => a.value ?? a.description).join(' '));
    if (m.method === 'Runtime.exceptionThrown') errors.push(m.params.exceptionDetails.exception?.description ?? m.params.exceptionDetails.text);
  });
  await send('Runtime.enable', {}, sessionId);
  await send('Page.enable', {}, sessionId);
  await send('Page.navigate', { url: `${origin}/${query}` }, sessionId);
  const evaluate = async expr => (await send('Runtime.evaluate', { expression: expr, awaitPromise: true, returnByValue: true }, sessionId)).result.value;
  let started = false;
  for (let i = 0; i < 120 && !started; i++) { await sleep(250); started = await evaluate(`document.getElementById('veil').classList.contains('gone')`); }
  check(started, `${name}: starts and opens its document (${await evaluate('document.title')})`);
  await sleep(1500);
  // How much of the canvas is not the ground: a page drawn has text on it.
  const frame = () => evaluate(`(async () => {
    const c = document.getElementById('bibmd'), x = new OffscreenCanvas(c.width, c.height).getContext('2d');
    x.drawImage(c, 0, 0); const d = x.getImageData(0, 0, c.width, c.height).data;
    let ink = 0, sum = 0; for (let i = 0; i < d.length; i += 4) { sum += d[i] + d[i+1] + d[i+2]; if (Math.abs(d[i] - d[0]) > 40) ink++; }
    return { w: c.width, h: c.height, ink, sum };
  })()`);
  const first = await frame();
  check(first.w > 0 && first.ink > 2000, `${name}: draws (${first.w}×${first.h}, ${first.ink} px of ink)`);
  const shoot = async label => {
    if (!shots) return;
    await mkdir(shots, { recursive: true });
    const { data } = await send('Page.captureScreenshot', { format: 'png' }, sessionId);
    await writeFile(join(shots, `${label}.png`), Buffer.from(data, 'base64'));
  };
  await shoot(name);
  const key = async (key, code, text, modifiers = 0) => {
    await send('Input.dispatchKeyEvent', { type: 'keyDown', key, code, text, modifiers }, sessionId);
    await send('Input.dispatchKeyEvent', { type: 'keyUp', key, code, modifiers }, sessionId);
  };
  await key('t', 'KeyT', '', 2);
  await sleep(800);
  const themed = await frame();
  check(themed.sum !== first.sum, `${name}: Ctrl+T changes the theme`);
  await shoot(`${name}-t`);
  if (then) await then({ evaluate, sessionId, shoot, frame, key });
  check(errors.length === 0, `${name}: no errors on the console` + (errors.length ? ':\n  ' + errors.join('\n  ') : ''));
  await send('Target.closeTarget', { targetId });
}

// A document that is not there: the page says so, and offers dropping one instead.
async function missing() {
  const { targetId } = await send('Target.createTarget', { url: `${origin}/?doc=samples/missing.md` });
  const { sessionId } = await send('Target.attachToTarget', { targetId, flatten: true });
  let note = '';
  for (let i = 0; i < 120 && !note; i++) {
    await sleep(250);
    note = (await send('Runtime.evaluate', { expression: `document.querySelector('#veil.failed #note')?.textContent ?? ''`, returnByValue: true }, sessionId)).result.value;
  }
  check(/missing\.md: not found/.test(note), `missing: says so (${note})`);
  await send('Target.closeTarget', { targetId });
}

try {
  await page('', 'default', async ({ sessionId, shoot, frame }) => {
    // A finger dragged up the page, as on a phone: the page scrolls with it, and flings on.
    await send('Emulation.setTouchEmulationEnabled', { enabled: true, maxTouchPoints: 1 }, sessionId);
    const before = await frame();
    const touch = (type, y) => send('Input.dispatchTouchEvent', { type, touchPoints: type === 'touchEnd' ? [] : [{ x: 550, y }] }, sessionId);
    await touch('touchStart', 650);
    for (let y = 630; y >= 250; y -= 20) { await touch('touchMove', y); await sleep(16); }
    await touch('touchEnd');
    await sleep(1500);
    const scrolled = await frame();
    check(scrolled.sum !== before.sum, 'default: a finger dragged up scrolls the page');
    await shoot('default-touch');
  });
  await page('?doc=samples/vendor-choice.bib.md', 'vendor-choice');
  await page('?doc=test/doc.md', 'images', async ({ evaluate, sessionId }) => {
    // The images' rows: the JPEG's red and the PNG's green, where the column draws them.
    const at = (y) => evaluate(`(() => { const c = document.getElementById('bibmd'), x = new OffscreenCanvas(c.width, c.height).getContext('2d');
      x.drawImage(c, 0, 0); return [...x.getImageData(Math.round(c.width / 2) - 300, ${y}, 1, 1).data]; })()`);
    const [jpeg, png] = [await at(150), await at(320)];
    check(jpeg[0] > 150 && jpeg[2] < 150, `images: the JPEG draws (${jpeg})`);
    check(png[1] > 150 && png[0] < 150, `images: the PNG draws (${png})`);
    // Tapped with a finger, as on a phone (in the page: the canvas is under the page's 48 px bar).
    await send('Emulation.setTouchEmulationEnabled', { enabled: true, maxTouchPoints: 1 }, sessionId);
    await send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x: 215, y: 497 }] }, sessionId);
    await sleep(60);
    await send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] }, sessionId);
    await sleep(1500);
    check(/printer/.test(await evaluate('document.title')) && /vendor-choice/.test(await evaluate('location.search')),
      `images: the link opens in place when tapped (${await evaluate('location.search')})`);
  });
  // The page around the canvas: Ctrl+B in the canvas opens the page's contents, / its insert menu (its field takes
  // the typing, Enter puts the block in), and the bar's Read locks the document.
  await page('?edit&doc=test/typing.md', 'chrome', async ({ evaluate, sessionId, key, shoot }) => {
    await evaluate(`document.getElementById('bibmd').focus()`);
    await key('b', 'KeyB', '', 2);
    await sleep(400);
    const contents = await evaluate(`document.body.classList.contains('panel') && [...document.querySelectorAll('#panel-list .row')].map(r => r.textContent).join('|')`);
    check(contents === 'Typing', `chrome: Ctrl+B opens the page's contents (${contents})`);
    await shoot('chrome-contents');
    await evaluate(`document.getElementById('bibmd').focus()`);
    await key('/', 'Slash', '/');
    await sleep(400);
    check(await evaluate(`document.getElementById('palette').classList.contains('on') && document.activeElement.id`) === 'pal-q',
      'chrome: / opens the insert menu, its field focused');
    await send('Input.insertText', { text: 'table 3x2' }, sessionId);
    await sleep(200);
    const first = await evaluate(`document.querySelector('#pal-list .opt')?.textContent`);
    check(first === 'Table 3 × 2| |', `chrome: the menu follows what is typed (${first})`);
    await shoot('chrome-insert');
    await key('Enter', 'Enter', '\r');
    await sleep(500);
    await key('Escape', 'Escape');
    await sleep(500);
    const rows = (await evaluate('bibmd.text()')).split('\n').filter(l => l.startsWith('|') && l.split('|').length === 5).length;
    check(rows === 3 && !(await evaluate(`document.getElementById('palette').classList.contains('on')`)),
      `chrome: Enter puts a 3 × 2 table in, and the menu closes (${rows} rows)`);
    await evaluate(`document.getElementById('t-read').click()`);
    await sleep(400);
    check(await evaluate(`bibmd.state().locked && document.body.classList.contains('locked')`), 'chrome: Read locks the document');
  });
  await page('?doc=test/emoji.md', 'emoji', async ({ evaluate }) => {
    // The theme is grey, so a pixel of colour is an emoji's: without the emoji face they are missing glyphs.
    const colour = await evaluate(`(() => { const c = document.getElementById('bibmd'), x = new OffscreenCanvas(c.width, c.height).getContext('2d');
      x.drawImage(c, 0, 0); const d = x.getImageData(0, 0, c.width, c.height).data;
      let n = 0; for (let i = 0; i < d.length; i += 4) if (Math.max(d[i], d[i+1], d[i+2]) - Math.min(d[i], d[i+1], d[i+2]) > 80) n++;
      return n; })()`);
    check(colour > 500, `emoji: draw in colour (${colour} px)`);
  });
  await page('?edit&doc=test/typing.md', 'typing', async ({ evaluate, sessionId, shoot }) => {
    // What Ctrl+S would download: the document as it is now.
    const saved = async () => {
      await evaluate(`window.__saved = null; (() => { const make = URL.createObjectURL;
        URL.createObjectURL = b => { b.text().then(t => window.__saved = t); return make(b); }; })()`);
      await send('Input.dispatchKeyEvent', { type: 'keyDown', key: 's', code: 'KeyS', modifiers: 2 }, sessionId);
      await send('Input.dispatchKeyEvent', { type: 'keyUp', key: 's', code: 'KeyS', modifiers: 2 }, sessionId);
      for (let i = 0; i < 20; i++) { await sleep(100); const t = await evaluate('window.__saved'); if (t != null) return t; }
      return null;
    };
    // The textarea that takes the canvas's keyboard: its inputmode, where it sits, whether it shows.
    const sink = () => evaluate(`(() => { const t = document.querySelector('#bibmd ~ textarea'), r = t.getBoundingClientRect();
      return { mode: t.inputMode, x: r.left, y: r.top, h: r.height, opacity: getComputedStyle(t).opacity }; })()`);
    // How much ink a strip of the page has: from x, a line of height h at y, w wide (CSS px).
    const ink = (x, y, w, h) => evaluate(`(() => { const c = document.getElementById('bibmd'), d = devicePixelRatio, r = c.getBoundingClientRect();
      const g = new OffscreenCanvas(c.width, c.height).getContext('2d'); g.drawImage(c, 0, 0);
      const px = g.getImageData(Math.round((${x} - r.left) * d), Math.round((${y} - r.top) * d), Math.round(${w} * d), Math.round(${h} * d)).data;
      let n = 0; for (let i = 0; i < px.length; i += 4) if (Math.abs(px[i] - px[0]) > 40) n++; return n; })()`);
    const closed = await sink();
    check(closed.mode === 'none', `typing: no phone keyboard before a block opens (inputmode ${closed.mode})`);
    // A finger's tap, whose press and release come in one frame: it opens the block too, and Escape puts it back.
    await send('Emulation.setTouchEmulationEnabled', { enabled: true, maxTouchPoints: 1 }, sessionId);
    await send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [CLICK] }, sessionId);
    await sleep(60);
    await send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] }, sessionId);
    await sleep(500);
    const tapped = await sink();
    check(tapped.mode === 'text', `typing: a finger's tap opens the block (inputmode ${tapped.mode})`);
    await send('Emulation.setTouchEmulationEnabled', { enabled: false }, sessionId);
    for (const type of ['rawKeyDown', 'keyUp'])
      await send('Input.dispatchKeyEvent', { type, key: 'Escape', code: 'Escape', windowsVirtualKeyCode: 27 }, sessionId);
    await sleep(400);
    // A press and its release a few frames apart, as a hand's are: the block opens for editing.
    for (const type of ['mouseMoved', 'mousePressed', 'mouseReleased']) {
      await send('Input.dispatchMouseEvent', { type, x: CLICK.x, y: CLICK.y, button: 'left', clickCount: 1 }, sessionId);
      await sleep(100);
    }
    await sleep(400);
    const keyTap = async (key, code, text) => {
      await send('Input.dispatchKeyEvent', { type: 'keyDown', key, code, text, windowsVirtualKeyCode: KEYCODES[key] ?? 0 }, sessionId);
      await send('Input.dispatchKeyEvent', { type: 'keyUp', key, code, windowsVirtualKeyCode: KEYCODES[key] ?? 0 }, sessionId);
    };
    await keyTap('End', 'End');
    await sleep(300);
    check(await evaluate(`document.activeElement?.tagName === 'TEXTAREA'`), 'typing: the canvas\'s keyboard is the textarea beside it');
    const open = await sink();
    check(open.mode === 'text', `typing: a phone keyboard while the block edits (inputmode ${open.mode})`);
    check(Math.abs(open.y + open.h / 2 - CLICK.y) < 20 && open.x > 120 && open.x < 400 && open.h > 8,
      `typing: the textarea sits at the caret, after "word" (${Math.round(open.x)}, ${Math.round(open.y)}, ${Math.round(open.h)} px tall)`);
    // A key that types, as before.
    await keyTap('s', 'KeyS', 's');
    await sleep(300);
    const typed = await sink();
    check(typed.x > open.x + 3 && typed.x < open.x + 20, `typing: and follows it as it types (${Math.round(open.x)} → ${Math.round(typed.x)})`);
    // An IME: a composition drawn in the block at the caret, marked, then committed whole.
    const line = [typed.x + 2, typed.y, 60, typed.h];
    const bare = await ink(...line);
    await send('Input.imeSetComposition', { text: 'に', selectionStart: 1, selectionEnd: 1 }, sessionId);
    await send('Input.imeSetComposition', { text: 'にほん', selectionStart: 3, selectionEnd: 3 }, sessionId);
    await sleep(400);
    const composing = await ink(...line), during = await sink();
    await shoot('typing-composing');
    await send('Input.insertText', { text: '日本' }, sessionId);
    // A phone keyboard's text, which comes with no key that types it.
    await send('Input.insertText', { text: ' é' }, sessionId);
    // A phone keyboard's backspace: no key, only the edit it asks for.
    await evaluate(`document.activeElement.dispatchEvent(new InputEvent('beforeinput', { inputType: 'deleteContentBackward', cancelable: true }))`);
    await send('Input.insertText', { text: 'ü' }, sessionId);
    await sleep(500);
    await keyTap('Escape', 'Escape');   // the edit done: the document has it
    await sleep(300);
    const text = await saved();
    check(composing > bare + 40, `typing: a composition draws in the block after the caret before it is committed (${bare} → ${composing} px of ink)`);
    check(during.opacity === '0' && during.x > typed.x + 20, `typing: not in a box of its own; the caret after it (opacity ${during.opacity}, ${Math.round(typed.x)} → ${Math.round(during.x)})`);
    const after = await sink();
    check(after.mode === 'none', `typing: no phone keyboard once the block is put back (inputmode ${after.mode})`);
    check(/^words日本 ü$/m.test(text ?? ''), `typing: keys, an IME and a phone keyboard (its backspace too) type into the block (${JSON.stringify(text)})`);
  });
  await missing();
} finally {
  browser.kill();
  await new Promise(r => browser.once('exit', r));
  server.close();
  await rm(profile, { recursive: true, force: true, maxRetries: 10 }).catch(() => {});   // Chromium's helpers may still be writing
}
process.exit(failures ? 1 : 0);

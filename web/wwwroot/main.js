// The page around bibmd's canvas: it starts .NET (whose Main opens the window and returns),
// fetches the document the URL names (?doc=path/or/url.md; the sourdough notebook without one),
// and hands it to Viewer.Open. Images are fetched and decoded here, by the browser, and handed
// back as PNG. A dropped file (Ctrl+O picks one) is read in place of the URL's; images dropped
// with it are found by name. Ctrl+S downloads the document as it is now (Ctrl+L unlocks editing).
//
// Hosted (?host=<the app's origin>, in a sandboxed iframe of hosted bibmd): the parent page is the only
// way out. It sends the document ({t:'open'}), fetches images ({t:'png'}), is told each change to the
// text ({t:'changed'}) to save it, and follows links ({t:'link'}); messages from anywhere else are ignored.
// Its bar is the parent's: the chrome's state goes out to it ({t:'state'}) and its buttons come back ({t:'cmd'}).
//
// The canvas draws only the document; the bar, the menus and the notes around it are chrome.js.
import { dotnet } from './_framework/dotnet.js';
import { createLive } from './live.js';
import { createChrome, showTitle } from './chrome.js';

const DEFAULT_DOC = 'samples/sourdough.bib.md';
const params = new URLSearchParams(location.search);
const host = params.get('host');
const hosted = !!host && window.parent !== window;
let locked = !params.has('edit');
const prefersDark = matchMedia('(prefers-color-scheme: dark)').matches;
const veil = document.getElementById('veil'), note = document.getElementById('note');
const canvas = document.getElementById('bibmd');
if (hosted) document.body.classList.add('hosted');

let viewer;                 // the app's exports, once .NET is up
let current = { name: 'untitled.md', dir: '', base: null };   // the document shown: its file name, its folder in the
const dropped = new Map();  // site's paths (the reader's), and the URL that folder is at; files dropped, by name

const fail = (message) => { veil.classList.remove('gone'); veil.classList.add('failed'); note.textContent = message; };

// --- What the app calls --------------------------------------------------------------------
let ready;
const started = new Promise(r => ready = r);
const imports = {
  ready: () => ready(),
  png: async (source) => base64(await toPng(await (hosted ? askImage(source) : fetchImage(source)))),
  navigate: (link) => hosted ? toParent({ t: 'link', href: link }) : navigate(link),
  changed: (text) => {
    retitle(text);
    if (live) live.changed(text); else if (hosted) toParent({ t: 'changed', text });
  },
  state: (json) => chrome.update(json),
  editing: (start, end, isNew) => live?.editing(start, end, isNew),
  typing: (text, caret) => live?.typing(text, caret),
};
// A shared document: its changes go to the server through the parent page, which holds the socket.
let live = null;
// For the hosted service's browser tests: the text as this viewer has it.
// And for the viewer's own: the chrome's state, as the canvas last said it.
globalThis.bibmd = { text: () => viewer?.Text(), state: () => chrome.state };

const chrome = createChrome({
  viewer: () => viewer, canvas, hosted,
  onAct: (act) => act === 'open' ? pick.click() : act === 'download' ? download() : null,
  onState: (_, json) => { if (hosted) toParent({ t: 'state', s: json }); },
});

// --- Hosted: the parent page -------------------------------------------------------------------
const toParent = (message) => window.parent.postMessage(message, host);
const asked = new Map();
let askedId = 0;

function askImage(source) {
  if (/^[a-z][a-z0-9+.-]*:/i.test(source)) return Promise.reject(new Error('images from other sites are not shown here'));
  return new Promise((ok, no) => {
    const id = ++askedId;
    asked.set(id, { ok, no });
    toParent({ t: 'png', id, path: source });
  });
}

if (hosted) addEventListener('message', (e) => {
  if (e.source !== window.parent || e.origin !== host) return;
  const m = e.data ?? {};
  if (m.t === 'open') {
    locked = !!m.locked;
    show(m.path, m.text, null, m.dark);
    if (m.live) {
      live = createLive(viewer, (msg) => toParent({ t: 'send', m: msg }), (note) => toParent({ t: 'note', text: note }));
      live.opened(viewer.Text(), m.rev);
    }
  }
  else if (m.t === 'live') live?.receive(m.m);
  else if (m.t === 'cmd' && viewer) chrome.command(m.name);
  else if (m.t === 'png' && asked.has(m.id)) {
    const { ok, no } = asked.get(m.id);
    asked.delete(m.id);
    m.error ? no(new Error(m.error)) : ok(new Blob([m.bytes]));
  }
});

async function fetchImage(source) {
  if (/^[a-z][a-z0-9+.-]*:/i.test(source)) return (await get(source)).blob();
  const name = source.split('/').pop();
  if (dropped.has(source)) return dropped.get(source);
  if (dropped.has(name)) return dropped.get(name);
  // A path in the site: under the document's folder it is beside the document, wherever that is.
  const url = current.base && source.startsWith(current.dir) ? new URL(source.slice(current.dir.length), current.base)
                                                                : new URL(source, location.href);
  return (await get(url)).blob();
}

async function get(url) {
  const response = await fetch(url);
  if (!response.ok) throw new Error(response.status === 404 ? 'not found' : `${response.status} ${response.statusText}`);
  return response;
}

// PNG as it is; anything else the browser can show (JPEG, WebP, GIF, AVIF, SVG) drawn to a canvas and encoded.
async function toPng(blob) {
  const bytes = new Uint8Array(await blob.arrayBuffer());
  if (bytes[0] === 0x89 && bytes[1] === 0x50 && bytes[2] === 0x4e && bytes[3] === 0x47) return bytes;
  const url = URL.createObjectURL(blob);
  try {
    const img = new Image();
    img.src = url;
    await img.decode();
    const w = img.naturalWidth || 1200, h = img.naturalHeight || Math.round(w * 0.75);
    const surface = new OffscreenCanvas(w, h);
    surface.getContext('2d').drawImage(img, 0, 0, w, h);
    return new Uint8Array(await (await surface.convertToBlob({ type: 'image/png' })).arrayBuffer());
  } catch {
    throw new Error('unsupported image format');
  } finally {
    URL.revokeObjectURL(url);
  }
}

function base64(bytes) {
  let s = '';
  for (let i = 0; i < bytes.length; i += 0x8000) s += String.fromCharCode.apply(null, bytes.subarray(i, i + 0x8000));
  return btoa(s);
}

// A link to Markdown opens here, in place; anything else in a new tab, once the app has asked
// about any that is not a web page or a mail. A link that would run in this page is never followed.
function navigate(link) {
  const base = current.base ?? location.href;
  let url;
  try { url = new URL(link, base); } catch { return; }
  if (/\.(md|markdown)$/i.test(url.pathname)) {
    const doc = url.origin === location.origin ? relative(url) : url.href;
    history.pushState(null, '', `?doc=${encodeURIComponent(doc)}${url.hash}`);
    openUrl(doc);
  } else if (!['javascript:', 'data:', 'vbscript:', 'blob:'].includes(url.protocol)) {
    window.open(url.href, '_blank', 'noopener');
  }
}

// --- Documents -----------------------------------------------------------------------------
// A same-origin URL as a path from the page's folder ("notes/a.md", "../x.md" stays a URL).
function relative(url) {
  const here = new URL('.', location.href).pathname;
  return url.pathname.startsWith(here) ? decodeURIComponent(url.pathname.slice(here.length)) : url.href;
}

async function openUrl(doc) {
  try {
    const url = new URL(doc, location.href);
    const text = await (await get(url)).text();
    // The path the reader sees: the site's own, or for another site's file, its host and path.
    const path = url.origin === location.origin ? relative(url) : `${url.host}${url.pathname}`;
    show(path, text, new URL('.', url).href);
  } catch (e) {
    fail(`Could not read ${doc}: ${e.message}. Drop a Markdown file here to read it instead.`);
  }
}

function show(path, text, base, dark = prefersDark) {
  const name = path.split('/').pop() || 'untitled.md';
  current = { name, dir: path.slice(0, path.length - name.length), base };
  viewer.Open(path, text, dark, locked);
  showTitle(title(text) ?? name, path === name ? '' : path);
  document.title = `${title(text) ?? name} — bibmd`;
  veil.classList.remove('failed');
  veil.classList.add('gone');
  canvas.focus();
}

// The title follows the text as it is written (its front matter's title, or its first heading).
function retitle(text) {
  const t = title(text) ?? current.name;
  if (document.title === `${t} — bibmd`) return;
  document.title = `${t} — bibmd`;
  showTitle(t, current.dir ? current.dir + current.name : '');
}

function title(text) {
  const front = /^---\n([\s\S]*?)\n---/.exec(text.replace(/\r\n/g, '\n'));
  const t = front && /^title:\s*(.+)$/m.exec(front[1]);
  if (t) return t[1].replace(/^["']|["']$/g, '');
  const h = /^#\s+(.+)$/m.exec(text);
  return h ? h[1].trim() : null;
}

async function openFiles(files) {
  files = [...files];
  const doc = files.find(f => /\.(md|markdown|txt)$/i.test(f.name));
  for (const f of files) if (f !== doc) dropped.set(f.webkitRelativePath || f.name, f);
  if (!doc) return;
  history.pushState(null, '', location.pathname);
  show(doc.name, await doc.text(), null);
}

// --- The page ------------------------------------------------------------------------------
let depth = 0;
addEventListener('dragenter', e => { e.preventDefault(); if (depth++ === 0) document.body.classList.add('dragging'); });
addEventListener('dragleave', () => { if (--depth === 0) document.body.classList.remove('dragging'); });
addEventListener('dragover', e => e.preventDefault());
addEventListener('drop', e => {
  e.preventDefault();
  depth = 0;
  document.body.classList.remove('dragging');
  if (viewer && !hosted) openFiles(e.dataTransfer.files);
});

const pick = document.getElementById('pick');
pick.addEventListener('change', () => { openFiles(pick.files); pick.value = ''; });
addEventListener('keydown', e => {
  if (!(e.ctrlKey || e.metaKey) || e.altKey || !viewer) return;
  if (hosted) {
    // Saving is the page's: Ctrl+S saves now rather than after the pause.
    if (e.code === 'KeyS') { e.preventDefault(); toParent({ t: 'changed', text: viewer.Text(), now: true }); }
    return;
  }
  if (e.code === 'KeyO') { e.preventDefault(); pick.click(); }
  if (e.code === 'KeyS') { e.preventDefault(); download(); }
}, true);

function download() {
  const a = Object.assign(document.createElement('a'), {
    href: URL.createObjectURL(new Blob([viewer.Text()], { type: 'text/markdown' })), download: current.name,
  });
  a.click();
  setTimeout(() => URL.revokeObjectURL(a.href), 1000);
}
addEventListener('popstate', () => { if (!hosted) openUrl(new URLSearchParams(location.search).get('doc') ?? DEFAULT_DOC); });

// A phone's keyboard covers the page without making it shorter (iOS), and Safari slides the page
// up under it to show the field: the page keeps to what is left in sight, so bibmd lays the document
// out above the keyboard and keeps the caret there. Not while pinched into (scale above 1).
const sight = window.visualViewport;
if (sight) {
  const app = document.getElementById('app');
  const fit = () => {
    const zoomed = sight.scale > 1.01;
    Object.assign(app.style, zoomed ? { top: '', height: '' } : { top: `${sight.offsetTop}px`, height: `${sight.height}px` });
  };
  sight.addEventListener('resize', fit);
  sight.addEventListener('scroll', fit);
  fit();
}

// --- Start ---------------------------------------------------------------------------------
// The colour emoji face and the fallbacks for symbols Inter lacks (build.sh puts them beside the
// page), fetched while .NET starts. Without one a character is drawn as a missing glyph, which is no
// reason not to show the document.
const font = (name) => fetch(`fonts/${name}`).then(r => r.ok ? r.arrayBuffer() : null).catch(() => null);
const emoji = font('Twemoji.Mozilla.ttf');
const fallbacks = ['NotoSansMath-Symbols.ttf', 'NotoSansSymbols2-Symbols.ttf'].map(font);
try {
  const runtime = await dotnet.create();
  runtime.setModuleImports('bibmd', imports);
  await runtime.runMain();
  await started;
  viewer = (await runtime.getAssemblyExports('bibmd-web.dll')).BibMd.Viewer;
  const face = await emoji;
  if (face) viewer.UseEmoji(new Uint8Array(face));
  for (const f of await Promise.all(fallbacks)) if (f) viewer.UseFallback(new Uint8Array(f));
} catch (e) {
  fail(`bibmd did not start: ${e.message ?? e}`);
  throw e;
}
if (hosted) toParent({ t: 'ready' });
else await openUrl(params.get('doc') ?? DEFAULT_DOC);

// ?perf: a corner readout of the frames drawn each second and what one took, on average, to build
// and lay out, to rasterize, and to hand to the canvas. For measuring on a phone.
if (params.has('perf')) {
  const readout = Object.assign(document.createElement('div'), { id: 'perf' });
  readout.style.cssText = 'position:fixed;right:6px;top:6px;z-index:10;padding:4px 7px;border-radius:6px;pointer-events:none;'
    + 'font:11px/1.35 ui-monospace,monospace;white-space:pre;color:#fff;background:rgb(0 0 0 / .65)';
  document.body.append(readout);
  let last = performance.now();
  setInterval(() => {
    const now = performance.now(), p = JSON.parse(viewer.Perf()), fps = p.frames * 1000 / (now - last);
    last = now;
    const each = p.build + p.raster + p.present;
    readout.textContent = `${fps.toFixed(0)} fps  ${p.width}×${p.height}\n${each.toFixed(1)} ms: build ${p.build.toFixed(1)}`
      + ` · raster ${p.raster.toFixed(1)} · present ${p.present.toFixed(1)}`;
  }, 1000);
}

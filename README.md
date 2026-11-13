### bibmd

A zen Markdown reader that edits in place, built on [bibui](https://navisurgvision.com/bibui/)
(Wayland + managed rasterizer, Native AOT: one ~9 MB binary, no runtime).

- md notes.md        # opens (or creates) the file

The document reads as rendered Markdown in a single quiet column. Each top-level
block (heading, paragraph, list item, quote, code block, table, rule, image)
turns into its Markdown source when opened, and back when you leave it. Every
change is written to the file at once (and while typing, after a short pause),
and every version of this session is on the undo history.

Fenced `bib` blocks are small programs drawn live: sliders, choices, stats, tables,
charts and shapes that recompute as you move them. On Omarchy the window takes its
colours from the desktop's theme, and fades to the new one when the theme changes.

## Using it

Click a block to edit it: it turns into its Markdown, and back when you press `Esc`
or click somewhere else. Click below the end to add a block. Type `/` (on the page,
or in an empty block) or press the + in the status line for a menu of headings,
tables, lists, images and the rest, so you need not remember their Markdown.

Right-click a block for the **block menu**: copy, cut, duplicate, move up or down,
delete, or turn it into another kind (a paragraph into a heading, lines into a list).

| Key                                 |                                                              |
| ----------------------------------- | ------------------------------------------------------------ |
| `↓` `↑`                             | select the next / previous block (the first press shows it)  |
| `Enter`                             | edit the selected block                                      |
| `Alt+↑` `Alt+↓`                     | move the selected block up / down                            |
| `Ctrl+C` `Ctrl+X` `Ctrl+V` `Ctrl+D` | copy / cut / paste after / duplicate the selected block      |
| `Delete`                            | delete the selected block                                    |
| `Esc`                               | done editing (the block stays selected); clear the selection |
| `Tab` `Shift+Tab` while editing     | indent / outdent the line; in a table, the next cell         |
| `Ctrl+Z` `Ctrl+Y` (`Ctrl+Shift+Z`)  | undo / redo                                                  |
| `Space` `PageDown` `PageUp`         | scroll                                                       |
| `Home` `End`                        | first / last block                                           |
| `Ctrl+T`                            | Omarchy theme / built-in (light / dark without Omarchy)      |
| `Ctrl+=` `Ctrl+-`                   | text size                                                    |
| `Ctrl+E`                            | Markdown source view                                         |
| `Ctrl+L`                            | lock / unlock: read only, so a click never opens a block     |
| `F1` `Ctrl+/`                       | the keys                                                     |
| `Ctrl+Q`                            | quit                                                         |

Every change is written to the file at once; `Ctrl+S` is there for the habit.

**The menu** on the left is hidden until asked for. `Ctrl+B` or the ≡ icon in the
status line shows or hides it; `Tab` / `Shift+Tab` on the page open it and step
through its three panels, the arrows and `Enter` walk it, and `Esc` goes back to the page.

- **Contents** — the headings, indented by level; the one you are in is lit. Click
  one to go to it.
- **History** — every version of the file this session, newest first: what changed,
  when, and how many characters went in and out. Click one to go back (or forward)
  to it; the later ones stay, marked *undone*, until the next change.
- **Keys** — every shortcut, the ones for what you are doing first.
- hey

Images: PNG is decoded natively; JPEG, WebP, GIF, SVG… go through ImageMagick
(`magick`). Paths are relative to the Markdown file; `http(s)` images are fetched.
Fenced `mermaid` blocks (flowchart/graph, sequenceDiagram and stateDiagram) are drawn as diagrams; one it cannot read stays code.
Links open with `xdg-open`, and a link to another `.md` file opens it in a new window.

## Live blocks

Fenced `bib` blocks are small programs, drawn live: sliders, choices, toggles,
checklists, text inputs, buttons, cards, stats, tables and lists. The language is
the bib document language (bibdoc's `docs/proposal.md`): each block runs again
every frame, and a widget draws where it is called and returns its value.

````markdown
---
data:
  vendors: [{ name: Northwind, price: 41000 }, { name: Fabrikam, price: 45250 }]
---
```bib
let units = slider("Units", 1, 10, value: 2)
for v in data.vendors { stat(v.name, v.price * units, unit: "EUR") }
```
````

- All the `bib` blocks of a file are one program, in order: a `let` in one is seen by the next.
- The front matter's `data:` is the document's input, as `data`.
- What a click would do (`call`, `send`, `respond`) is shown under the block, and not
  done: bibmd is a viewer. The values you give the inputs are not saved to the file.
- A block that fails says why, and on which line, and the others keep drawing. Clicking
  a block outside its widgets opens it for editing, as any block does.
- In a text input the keys are typed, not commands; `Esc` or `Enter` gives them back.
- A slider moves in floating point: a hundredth of its range (0.1 on 0–10), unless it
  says `step:` (`step: 1` for whole numbers).
- The front matter can widen the page: `width: wide` (the window, up to 1200 px),
  `width: full`, or a number of pixels. Prose stays at the reading column's width; bib
  blocks, code and tables take the rest. `nav: true` (or a label per `##` heading) puts a
  strip of section pills above the page. A `######` line is a small label above the heading
  after it, and a `bib data` block (YAML) adds to the document's data without being drawn.
- `locked: true` in the front matter opens the document read only, as a page is read: a
  click opens nothing, and the keys that edit say so. `Ctrl+L` or the lock in the status
  line unlocks it.
- Text can be selected: press and drag over it (a double click takes a word), `Ctrl+A`
  selects a block's text, `Ctrl+C` copies. A click let go where it was pressed still opens
  the block when the document is not locked.

`samples/vendor-choice.bib.md` uses most of it. The language itself (interpreter,
specification, conformance suite) is the **bibdoc** repository, which bibmd builds
against as a checkout beside this one (`../bibdoc`; `-p:BibDocRoot=<checkout>` for
another).

## Build

With bibdoc (github.com/relu24/bibdoc) checked out beside this folder, and bibui's
integration branch (github.com/relu24/bibui) checked out beside it as `../bibui-bib-agent`
(bibui 1.2 with changes not yet released, among them selectable Markdown text and input
methods). `-p:BibuiSource=<checkout>` builds against another checkout.

    dotnet publish -c Release -o out
    ln -sf "$PWD/out/bibmd" ~/.local/bin/md

Check a document's bib blocks (no window): `md notes.bib.md --check` prints each error at
its line in the file, and what each button's click would do; exit 1 on an error. The
language is written down for writers, and agents, in bibdoc's `docs/reference.md`.

Headless picture (no window): `md notes.md --png out.png --script "type:jj;key:Enter"`
— see `src/Program.cs` for the script steps.

## Install (Arch)

From the pacman repository `[bibmd]` (AUR sign-ups are closed for now): add to
`/etc/pacman.conf`

    [bibmd]
    SigLevel = Optional TrustAll
    Server = https://github.com/relu24/bibmd-releases/releases/download/repo

then `sudo pacman -Sy bibmd-bin` (or `yay -S bibmd-bin`); `-Syu` updates it.

A notebook to try it on, the sourdough planner (copy it first: opening a block edits the file):

    cp /usr/share/doc/bibmd/examples/sourdough.bib.md ~ && md ~/sourdough.bib.md

The package is a prebuilt binary from github.com/relu24/bibmd-releases. A release:
`packaging/release.sh <version>` builds the tarball for release v<version> there, the
PKGBUILD (`packaging/aur/bibmd-bin`) takes its pkgver and sha256, and
`packaging/publish-repo.sh` builds the package and puts it in the repository.

## In the browser

`web/` is bibmd as a web page: the same sources (without the Wayland `Program.cs`) built as a
.NET WebAssembly app on bibui's browser platform, drawn by the managed rasterizer on a
`<canvas>`. It reads; Ctrl+L unlocks editing in the page, Ctrl+S downloads the file as it is.

    web/build.sh                                  # publishes web/dist, static files
    python3 -m http.server 8000 -d web/dist       # http://localhost:8000/?doc=samples/vendor-choice.bib.md
    node web/test/check.mjs                       # headless Chromium: starts, draws, images, links

- `?doc=path/or/url.md` names the document (a path beside the page, or a URL that allows
  CORS); without one it shows the sourdough notebook. `?edit` starts it unlocked.
- Drop a Markdown file on the page to read it, with its images dropped beside it, or Ctrl+O.
- Images are fetched beside the document and decoded by the browser (PNG, JPEG, WebP, GIF,
  SVG…); a link to another `.md` opens in place, Back returns, others open in a new tab.
- It builds against bibui's integration branch, checked out as `../bibui-bib-agent`, whose
  `bibui-platform-browser/native/build.sh` must have run, with a .NET 10 SDK that has the
  wasm-tools workload (`DOTNET_ROOT`, by default `~/.dotnet-wasm`). About 14 MB, 2.5 MB with Brotli.

## License

MIT (`LICENSE`). The embedded iA Writer Mono S font is under the SIL Open Font
License (`assets/fonts/OFL.txt`).

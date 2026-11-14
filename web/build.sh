#!/usr/bin/env bash
# Publishes bibmd's web viewer to web/dist, a folder of static files any server can serve:
#
#   web/build.sh                     then  python3 -m http.server 8000 -d web/dist
#
# Needs a .NET 10 SDK with the wasm-tools workload (DOTNET_ROOT, by default ~/.dotnet-wasm, else
# the dotnet on PATH), and the browser's FreeType and HarfBuzz built in the bibui checkout
# (bibui-platform-browser/native/build.sh). It is compiled ahead of time, which takes a minute or two.
# The emoji font is downloaded (once, checked against its SHA-256) and put beside the page, with the
# symbol fonts in web/fonts.
# Extra arguments go to dotnet publish.
set -euo pipefail
here=$(cd "$(dirname "$0")" && pwd)
if [[ -z "${DOTNET_ROOT:-}" && -x $HOME/.dotnet-wasm/dotnet ]]; then export DOTNET_ROOT=$HOME/.dotnet-wasm; fi
dotnet=${DOTNET_ROOT:+$DOTNET_ROOT/}dotnet
"$dotnet" publish "$here/bibmd-web.csproj" -c Release -o "$here/obj/publish" "$@"
rm -rf "$here/dist"
cp -r "$here/obj/publish/wwwroot" "$here/dist"
# The documents it can show without being told one: the samples, beside the page.
mkdir -p "$here/dist/samples"
cp "$here/../samples/"*.md "$here/dist/samples/"
# The emoji: the browser has no system fonts to find a colour emoji in, so the page brings Mozilla's
# Twemoji. It is COLRv0, layers FreeType blends itself; a CBDT font (Noto Color Emoji) is PNGs, and the
# browser's FreeType is built without libpng. Art CC-BY 4.0 (Twitter), font Apache 2.0 (Mozilla).
emoji_url=https://github.com/mozilla/twemoji-colr/releases/download/v0.7.0/Twemoji.Mozilla.ttf
emoji_sha256=6d90152ee0d29e82fe2a87793af5aa4b7ad13e6538360889e141e81ed299ee8e
emoji="$here/obj/download/Twemoji.Mozilla.ttf"
mkdir -p "$here/obj/download"
if [[ ! -f $emoji ]] || ! echo "$emoji_sha256  $emoji" | sha256sum -c --status; then
  curl -fsSL --retry 3 -o "$emoji.part" "$emoji_url"
  if ! echo "$emoji_sha256  $emoji.part" | sha256sum -c --status; then
    echo "SHA-256 of $emoji_url is $(sha256sum "$emoji.part" | cut -d' ' -f1), not $emoji_sha256" >&2
    rm -f "$emoji.part"; exit 1
  fi
  mv "$emoji.part" "$emoji"
fi
mkdir -p "$here/dist/fonts"
cp "$emoji" "$here/fonts/"*.ttf "$here/fonts/OFL.txt" "$here/dist/fonts/"
# The bar's and the menus' faces: the monospaced one the canvas sets code in, and
cp "$here/../assets/fonts/"iAWriterMonoS-*.ttf "$here/dist/fonts/"
cp "$here/../assets/fonts/OFL.txt" "$here/dist/fonts/iAWriterMonoS-OFL.txt"
# Inter, which the canvas sets the document in, for the page around it (a Latin subset, from bibui's).
cp "$here/fonts/"Inter-*.woff "$here/fonts/Inter-LICENSE.txt" "$here/dist/fonts/"
cat > "$here/dist/fonts/Twemoji.Mozilla.txt" <<'NOTICE'
Twemoji.Mozilla.ttf: https://github.com/mozilla/twemoji-colr (v0.7.0).
Emoji art: Twemoji, Copyright 2019 Twitter, Inc and other contributors, CC-BY 4.0
(https://creativecommons.org/licenses/by/4.0/). Font build: Copyright Mozilla Foundation, Apache 2.0.
NOTICE
echo "web/dist: python3 -m http.server 8000 -d $here/dist"

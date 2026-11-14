#!/usr/bin/env bash
# Makes the fallback fonts the web viewer brings for symbols its Inter lacks (an arrow such as ⇄, a
# math operator, a dingbat): the browser has no system fonts to fall back on, as fontconfig does on
# the desktop. Each is a Noto font from google/fonts at a pinned commit, checked against its
# SHA-256, cut down to the blocks it is here for. The results are committed; run this to change them.
#
#   web/fonts/subset.sh          needs curl and fontTools (pyftsubset)
set -euo pipefail
here=$(cd "$(dirname "$0")" && pwd)
commit=cc39c4c2c304bc368c9b7c311ea2bcc6013299fe
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

subset() { # path in google/fonts, sha256, output, unicode ranges
  curl -fsSL --retry 3 -o "$work/font.ttf" "https://raw.githubusercontent.com/google/fonts/$commit/ofl/$1"
  echo "$2  $work/font.ttf" | sha256sum -c --status || { echo "SHA-256 of $1 is not $2" >&2; exit 1; }
  pyftsubset "$work/font.ttf" --unicodes="$4" --layout-features='*' --output-file="$here/$3"
}
# Letterlike, arrows, math operators, technical, box and geometric shapes, math arrows and operators.
subset notosansmath/NotoSansMath-Regular.ttf 3f495fe933c06786e4d5f6d86b8ee70b6753a68ee3b9d87528726de0f6e2c47d \
  NotoSansMath-Symbols.ttf "2100-214F,2190-21FF,2200-22FF,2300-23FF,2500-25FF,27C0-27FF,2900-297F,2980-2AFF"
# What Math lacks of arrows and technical, and the miscellaneous symbols, dingbats and their arrows.
subset notosanssymbols2/NotoSansSymbols2-Regular.ttf 7d5fb73b7ca67a6798101741f5d280a3d016a56a197afcd4199dbb57b4b82a21 \
  NotoSansSymbols2-Symbols.ttf "2190-21FF,2300-23FF,25A0-27BF,2B00-2BFF"
ls -l "$here"/*.ttf

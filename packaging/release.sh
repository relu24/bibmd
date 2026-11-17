#!/usr/bin/env bash
# Builds the release tarball the AUR package bibmd-bin downloads, from a clean worktree at a
# commit (HEAD by default), against the bibdoc and bibui-bibmd (bibui's bibmd branch) checkouts beside this one:
#   packaging/release.sh 0.1.0 [commit]
# then attach dist/bibmd-<version>-x86_64.tar.gz to release v<version> of relu24/bibmd-releases, and set
# pkgver and sha256sums in packaging/aur/bibmd-bin/PKGBUILD to what this prints.
set -euo pipefail
version=${1:?version, e.g. 0.1.0}
commit=${2:-HEAD}
root=$(cd "$(dirname "$0")/.." && pwd)
name=bibmd-$version-x86_64
work=$(mktemp -d)
trap 'git -C "$root" worktree remove --force "$work/src" >/dev/null 2>&1 || true; rm -rf "$work"' EXIT

git -C "$root" worktree add --detach "$work/src" "$commit" >/dev/null
dotnet publish "$work/src/bibmd.csproj" -c Release -o "$work/out" \
    -p:BibDocRoot="$root/../bibdoc" -p:BibuiSource="$root/../bibui-bibmd" >&2

mkdir -p "$work/$name" "$root/dist"
cp "$work/out/bibmd" "$work/src/LICENSE" "$work/src/README.md" "$work/src/assets/fonts/OFL.txt" "$work/$name/"
# A notebook to open first: md sourdough.bib.md
cp "$work/src/samples/sourdough.bib.md" "$work/$name/"
tar -C "$work" --owner=0 --group=0 -czf "$root/dist/$name.tar.gz" "$name"
sha256sum "$root/dist/$name.tar.gz"

#!/usr/bin/env bash
# Builds bibmd-bin from packaging/aur/bibmd-bin/PKGBUILD and publishes it as the pacman repo
# [bibmd]: the package and the repo database are the assets of the release "repo" of
# relu24/bibmd-releases, which pacman reads as
#   Server = https://github.com/relu24/bibmd-releases/releases/download/repo
# Run it after the PKGBUILD's pkgver and sha256sums are set to a release made with release.sh.
set -euo pipefail
root=$(cd "$(dirname "$0")/.." && pwd)
gh_repo=relu24/bibmd-releases
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

cp "$root/packaging/aur/bibmd-bin/PKGBUILD" "$work/"
(cd "$work" && makepkg -f --noconfirm >&2)
pkg=$(cd "$work" && ls bibmd-bin-*.pkg.tar.zst)

# The database lists only this package: the older ones are removed from the release below.
repo-add "$work/bibmd.db.tar.gz" "$work/$pkg" >&2
# A release asset cannot be a symlink, so bibmd.db and bibmd.files are copies.
rm "$work/bibmd.db" "$work/bibmd.files"
cp "$work/bibmd.db.tar.gz" "$work/bibmd.db"
cp "$work/bibmd.files.tar.gz" "$work/bibmd.files"
rm "$work/bibmd.db.tar.gz.old" 2>/dev/null || true

gh release view repo --repo "$gh_repo" >/dev/null 2>&1 ||
    gh release create repo --repo "$gh_repo" --latest=false --title "pacman repository [bibmd]" \
        --notes "The pacman repository [bibmd]: its database and the current bibmd-bin package. See the README to add it."
for old in $(gh release view repo --repo "$gh_repo" --json assets -q '.assets[].name' | grep -E '\.pkg\.tar\.zst$' | grep -vx "$pkg" || true); do
    gh release delete-asset repo "$old" --repo "$gh_repo" --yes
done
gh release upload repo --repo "$gh_repo" --clobber \
    "$work/$pkg" "$work/bibmd.db" "$work/bibmd.db.tar.gz" "$work/bibmd.files" "$work/bibmd.files.tar.gz"
echo "published $pkg"

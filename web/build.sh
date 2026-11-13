#!/usr/bin/env bash
# Publishes bibmd's web viewer to web/dist, a folder of static files any server can serve:
#
#   web/build.sh                     then  python3 -m http.server 8000 -d web/dist
#
# Needs a .NET 10 SDK with the wasm-tools workload (DOTNET_ROOT, by default ~/.dotnet-wasm, else
# the dotnet on PATH), and the browser's FreeType and HarfBuzz built in the bibui checkout
# (bibui-platform-browser/native/build.sh). Extra arguments go to dotnet publish.
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
echo "web/dist: python3 -m http.server 8000 -d $here/dist"

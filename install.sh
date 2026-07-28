#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
./build.sh "$@"
MODS_DIR="${VINTAGE_STORY_DATA:-$HOME/.config/VintagestoryData}/Mods"
rm -rf "$MODS_DIR/noticeboard"
cp -a Releases/noticeboard "$MODS_DIR/noticeboard"
echo "Installed to $MODS_DIR/noticeboard"

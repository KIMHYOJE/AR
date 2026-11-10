#!/usr/bin/env bash
# Export pixelfx effects as Unity-ready transparent sprite sheets into Assets/VFX/<category>/.
# usage: bash Tools/pixelfx_export.sh <id> [<id>...] [--palette "#a,#b,#c"] [--name custom] [--seed N] [--fps N]
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PFX="$ROOT/Tools/pixelfx"
[ -d "$PFX" ] || { echo "Tools/pixelfx missing -> bash Tools/setup_tools.sh" >&2; exit 1; }
command -v node >/dev/null || { echo "Node.js 18+ required" >&2; exit 1; }

IDS=(); PALETTE=""; NAME=""; EXTRA=()
while [ $# -gt 0 ]; do
  case "$1" in
    --palette) PALETTE="$2"; shift 2 ;;
    --name) NAME="$2"; shift 2 ;;
    --seed|--fps) EXTRA+=("$1" "$2"); shift 2 ;;
    *) IDS+=("$1"); shift ;;
  esac
done
[ ${#IDS[@]} -eq 0 ] && { echo "usage: pixelfx_export.sh <category/effect> ... [--palette ...] [--name ...]" >&2; exit 1; }

OPTS=()
if [ -n "$PALETTE" ]; then
  JSON="[\"${PALETTE//,/\",\"}\"]"
  OPTS=(--opts "{\"palette\": $JSON}")
fi

TMP="$PFX/records/_unity_export"
rm -rf "$TMP"
node "$PFX/tools/render.mjs" "${IDS[@]}" --sheet --no-actor --bg none --out "$TMP" "${OPTS[@]}" "${EXTRA[@]}"

for dir in "$TMP"/*/*/; do
  [ -d "$dir" ] || continue
  cat="$(basename "$(dirname "$dir")")"; fx="$(basename "$dir")"
  out_name="${NAME:-$fx}"
  dest="$ROOT/Assets/VFX/$cat"
  mkdir -p "$dest"
  cp "$dir/$fx.png"  "$dest/$out_name.png"
  cp "$dir/$fx.json" "$dest/$out_name.json"
  # keep the JSON's image field pointing at the renamed png
  sed -i "s/\"image\": *\"$fx.png\"/\"image\": \"$out_name.png\"/" "$dest/$out_name.json" 2>/dev/null || true
  echo "-> Assets/VFX/$cat/$out_name.png (+ .json)"
done

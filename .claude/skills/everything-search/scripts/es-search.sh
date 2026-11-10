#!/usr/bin/env bash
# Instant file search through Everything CLI (es.exe), with a rg/find fallback.
# usage: es-search.sh <query> [--ext png,wav] [--in DIR] [--max N] [--project]
set -uo pipefail

ROOT="$(cd "$(dirname "$0")/../../../.." && pwd)"
QUERY=""; EXT=""; IN=""; MAX=50

while [ $# -gt 0 ]; do
  case "$1" in
    --ext) EXT="$2"; shift 2 ;;
    --in) IN="$2"; shift 2 ;;
    --max) MAX="$2"; shift 2 ;;
    --project) IN="$ROOT/Assets"; shift ;;
    -h|--help) sed -n '2,3p' "$0"; exit 0 ;;
    *) QUERY="${QUERY:+$QUERY }$1"; shift ;;
  esac
done
[ -z "$QUERY" ] && [ -z "$EXT" ] && { echo "usage: es-search.sh <query> [--ext png,wav] [--in DIR] [--max N] [--project]" >&2; exit 1; }

to_win() { if command -v cygpath >/dev/null 2>&1; then cygpath -w "$1"; else echo "$1"; fi; }

ES=""
for c in es es.exe "/c/Program Files/Everything/es.exe" "/c/Program Files (x86)/Everything/es.exe"; do
  if command -v "$c" >/dev/null 2>&1; then ES="$c"; break; fi
  if [ -x "$c" ]; then ES="$c"; break; fi
done

if [ -n "$ES" ]; then
  ARGS=(-n "$MAX")
  [ -n "$IN" ] && ARGS+=(-path "$(to_win "$IN")")
  Q="$QUERY"
  [ -n "$EXT" ] && Q="${Q:+$Q }ext:${EXT//,/;}"
  # shellcheck disable=SC2086
  if "$ES" "${ARGS[@]}" $Q; then exit 0; fi
  echo "WARN: es failed (is Everything running?). Falling back to slow search." >&2
fi

# ---- fallback: project-scoped, slower ----
BASE="${IN:-$ROOT}"
PATTERN="${QUERY//\*/.*}"; PATTERN="${PATTERN//\?/.}"; PATTERN="${PATTERN// /.*}"
EXT_RE=""
[ -n "$EXT" ] && EXT_RE="\\.(${EXT//,/|})$"
if command -v rg >/dev/null 2>&1; then
  LIST=$(rg --files --no-ignore --hidden -g '!Library' -g '!Temp' -g '!Logs' -g '!.git' "$BASE" 2>/dev/null)
else
  LIST=$(find "$BASE" \( -name Library -o -name Temp -o -name Logs -o -name .git \) -prune -o -type f -print 2>/dev/null)
fi
echo "$LIST" | grep -iE "${PATTERN:-.}[^/\\\\]*${EXT_RE}" | grep -iE "${EXT_RE:-.}" | head -n "$MAX"

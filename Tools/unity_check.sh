#!/usr/bin/env bash
# Compile-check the Unity project and print only C# errors/warnings.
#   bash Tools/unity_check.sh            # auto: read the live Editor.log if the editor has the project open,
#                                        #       otherwise run a headless batchmode import+compile
#   bash Tools/unity_check.sh --batch    # force batchmode (fails if the editor has the project open)
#   bash Tools/unity_check.sh --log      # only read the Editor log
set -uo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
MODE="${1:-auto}"
EDITOR_LOG="${LOCALAPPDATA:-$HOME/AppData/Local}/Unity/Editor/Editor.log"
BATCH_LOG="$ROOT/Logs/batch_compile.log"

if [ ! -f "$ROOT/ProjectSettings/ProjectVersion.txt" ]; then
  echo "Not a Unity project yet (ProjectSettings/ProjectVersion.txt missing). Create the project first."
  exit 2
fi
VERSION="$(sed -n 's/^m_EditorVersion: //p' "$ROOT/ProjectSettings/ProjectVersion.txt" | tr -d '\r')"

find_unity() {
  for base in "/c/Program Files/Unity/Hub/Editor" "$HOME/AppData/Local/Unity/Hub/Editor"; do
    [ -x "$base/$VERSION/Editor/Unity.exe" ] && { echo "$base/$VERSION/Editor/Unity.exe"; return; }
  done
  ls -d "/c/Program Files/Unity/Hub/Editor/"*/Editor/Unity.exe 2>/dev/null | sort -V | tail -1
}

report() {  # $1 = log file
  local log="$1"
  [ -f "$log" ] || { echo "log not found: $log"; exit 2; }
  # last compilation block only
  local errs warns
  errs="$(grep -E "error CS[0-9]+" "$log" | sed 's/^.*Assets/Assets/' | sort -u)"
  warns="$(grep -E "warning CS[0-9]+" "$log" | grep -v "Library/PackageCache" | sed 's/^.*Assets/Assets/' | sort -u | head -30)"
  if [ -n "$errs" ]; then
    echo "COMPILE ERRORS ($(echo "$errs" | wc -l)):"; echo "$errs"
  else
    echo "OK: no C# compile errors"
  fi
  [ -n "$warns" ] && { echo "warnings:"; echo "$warns"; }
  grep -E "Scripts have compiler errors|Aborting batchmode" "$log" | tail -3
  [ -z "$errs" ]
}

EDITOR_OPEN=0
[ -f "$ROOT/Temp/UnityLockfile" ] && EDITOR_OPEN=1

if [ "$MODE" = "--log" ] || { [ "$MODE" = "auto" ] && [ $EDITOR_OPEN = 1 ]; }; then
  echo "Reading live editor log: $EDITOR_LOG (save scripts and let the editor recompile first)"
  report "$EDITOR_LOG"; exit $?
fi

if [ $EDITOR_OPEN = 1 ]; then
  echo "The editor has this project open; close it or use --log."; exit 2
fi
UNITY="$(find_unity)"
[ -n "$UNITY" ] || { echo "Unity $VERSION not found under Unity Hub/Editor"; exit 2; }
echo "Batch compile with $UNITY (first run imports everything and can take several minutes)..."
mkdir -p "$ROOT/Logs"
"$UNITY" -batchmode -nographics -quit -projectPath "$(cygpath -w "$ROOT" 2>/dev/null || echo "$ROOT")" \
  -logFile "$(cygpath -w "$BATCH_LOG" 2>/dev/null || echo "$BATCH_LOG")"
echo "Unity exited with $?"
report "$BATCH_LOG"

#!/usr/bin/env bash
# Re-creates external (gitignored) tool clones. Safe to run repeatedly.
#   bash Tools/setup_tools.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"

if [ ! -d "$ROOT/Tools/pixelfx/.git" ]; then
  git clone --depth 1 https://github.com/patch-gg/pixelfx.git "$ROOT/Tools/pixelfx"
else
  git -C "$ROOT/Tools/pixelfx" pull --ff-only
fi

command -v node >/dev/null || echo "WARN: Node.js 18+ is required for pixelfx (https://nodejs.org)"
command -v es   >/dev/null || echo "INFO: Everything CLI not found. Optional: winget install voidtools.Everything ; winget install voidtools.Everything.Cli"
python -c "import PIL" 2>/dev/null || echo "INFO: Pillow not installed. Optional for fetch_art.py --cutout: pip install pillow"
echo "Tools ready."

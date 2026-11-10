---
name: everything-search
description: Find files (sprites, sounds, fonts, prefabs, any asset) by name in under a second, inside or outside the Unity project, using the Windows Everything CLI (`es`). Use whenever you need to locate an asset/file by name, extension or folder instead of walking directories with find/Glob.
---

# Everything instant file search

Everything (voidtools, free) indexes every NTFS drive; its CLI `es.exe` queries that index in ~0.1 s.
Use this instead of recursive `find`/`Glob` over big trees (Library/, other drives, asset packs).

## Script

```bash
bash .claude/skills/everything-search/scripts/es-search.sh <query> [--ext png,wav] [--in <dir>] [--max 50] [--project]
```

| flag | meaning |
|---|---|
| `<query>` | Everything syntax: words are AND-ed, `*`/`?` wildcards, `"exact phrase"`, `regex:...` |
| `--ext png,wav,ogg` | only these extensions |
| `--in <dir>` | only under this folder (Windows or POSIX path) |
| `--project` | shortcut for `--in <project root>/Assets` |
| `--max N` | result limit (default 50) |

Examples:

```bash
# every explosion sound on any drive
bash .claude/skills/everything-search/scripts/es-search.sh explosion --ext wav,ogg,mp3
# character sprites inside this project
bash .claude/skills/everything-search/scripts/es-search.sh "char_" --ext png --project
# a downloaded asset pack anywhere under Downloads
bash .claude/skills/everything-search/scripts/es-search.sh "bullet*" --in "C:/Users/$USERNAME/Downloads"
```

Output is one absolute Windows path per line, ready to `cp` into `Assets/`.

## Rules
- Always prefer this script over walking directories when looking for files by name.
- After finding an external asset, copy it into the right `Assets/` folder (Sprites/, Audio/SFX, Audio/BGM, VFX/) and
  check its license before shipping. Never copy files from paid asset packs into the repo unless the user owns them.
- `es` searches names, not contents. For text inside code use Grep.

## If `es` is missing
The script automatically falls back to `rg --files` / `find` (slower, project-scoped). To get the fast path, the
user installs (free, no account):

```
winget install voidtools.Everything
winget install voidtools.Everything.Cli
```

Everything must be running (tray icon) for `es` to answer. Do not install these yourself without asking the user.

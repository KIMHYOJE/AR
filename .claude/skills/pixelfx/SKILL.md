---
name: pixelfx
description: Make pixel-art VFX (bullets, hits, explosions, magic, shields, pickups, UI pops, weather) for the Unity game and export them as transparent PNG sprite sheets + Aseprite JSON into Assets/VFX/. Use for any request for an effect, particle look, hit spark, muzzle flash, bullet sprite, or animated sprite sheet.
---

# PixelFX → Unity sprite sheets

Library: `Tools/pixelfx` (clone of https://github.com/patch-gg/pixelfx, MIT, 240+ code-drawn effects, Node 18+,
no npm install needed for rendering). If the folder is missing run `bash Tools/setup_tools.sh`.
The upstream authoring guide is `Tools/pixelfx/skills/pixelfx/SKILL.md` and `Tools/pixelfx/docs/AUTHORING.md`;
read them before *creating* a new effect.

## 1. Find an effect
```bash
node Tools/pixelfx/tools/list.mjs              # all ids, e.g. fire/campfire, projectiles/energy-disc
grep -i "bullet\|laser" Tools/pixelfx/docs/CATALOG.md
```
Good fits for a vertical bullet-hell defense: `projectiles/*` (bullets, energy-disc, homing-missile),
`impacts/*` (bullet-impact, hit-flash, critical-hit), `explosions/*`, `sci-fi/force-field`, `holy/divine-shield`,
`pickups/*`, `ui/*` (crit-number, combo-counter, level-up), `transitions/*` (VN scene changes).

## 2. Export to Unity
```bash
bash Tools/pixelfx_export.sh <id> [<id>...] [--palette "#102,#215,#43a,#8cf,#fff"] [--name custom_name]
# e.g.
bash Tools/pixelfx_export.sh impacts/bullet-impact explosions/big-explosion
bash Tools/pixelfx_export.sh projectiles/energy-disc --palette "#201,#503,#a16,#f5a,#fde,#fff" --name enemy_disc_pink
```
Writes `Assets/VFX/<category>/<name>.png` (transparent grid, no actor) and `<name>.json` (frame rects, durations,
`meta.pixelfx` with fps, loop, columns, pivot). Directional effects face **right**: rotate 90° in Unity for an upward
shot in the portrait layout.

## 3. Import settings in Unity (tell the user / apply via an editor script)
- Texture Type `Sprite (2D and UI)`, Sprite Mode `Multiple`, **Filter Mode `Point (no filter)`**, Compression `None`,
  Pixels Per Unit = game's PPU (default 16 for pixel art; keep consistent project-wide).
- Slice: Sprite Editor → Grid By Cell Size using the frame `w`/`h` from the JSON (`frames[0].frame`), pivot from
  `meta.pixelfx.pivot`. To automate, write an editor `AssetPostprocessor` that reads the sibling `.json` and slices
  the sheet (Unity's Aseprite Importer reads `.aseprite` files only, not this JSON).
- Play with an `Animator`/`SpriteRenderer` or a pooled `VfxPlayer` (see unity-6-coder skill: VFX must be pooled).
  Use `meta.pixelfx.fps` for the clip sample rate; non-loop effects return to the pool on the last frame.

## 4. Making / tweaking an effect
Follow the upstream loop: `node Tools/pixelfx/tools/new.mjs <category>/<name>` → render
(`node Tools/pixelfx/tools/render.mjs <id>`) → **look at** `Tools/pixelfx/records/<id>/contact.png` with Read →
fix the worst thing → repeat; `node Tools/pixelfx/tools/check.mjs <id>` before export. New effects live in
`Tools/pixelfx/src/fx/` (gitignored clone) — if the user wants to keep them, copy the `.js` into `Tools/fx-custom/`.

## Rules
- Free/MIT only. Keep the pixelfx LICENSE/NOTICE credit in the game's credits screen.
- Always `Read` the rendered contact sheet before claiming an effect looks right.

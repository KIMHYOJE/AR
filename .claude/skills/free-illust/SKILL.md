---
name: free-illust
description: Generate free (no API key, 0 cost) character standing sprites, VN backgrounds and event CGs via Pollinations.ai and save them as PNGs into Assets/Sprites/. Use for any request for character art, expressions, backgrounds, CG, title art, or placeholder illustrations.
---

# Free illustration pipeline (Pollinations.ai)

```bash
python Tools/fetch_art.py <character|background|cg|raw> "<description>" --name <file> [--seed N] [--cutout] [--count N]
```

| kind | size | output |
|---|---|---|
| `character` | 768×1344 (9:16) | `Assets/Sprites/Characters/` — full-body, white backdrop; add `--cutout` for transparency |
| `background` | 1080×1920 (9:16) | `Assets/Sprites/Backgrounds/` — scenery only |
| `cg` | 1920×1080 | `Assets/Sprites/CG/` |
| `raw` | 1024×1024 | `Assets/Sprites/Misc/` — prompt sent as-is (`--width/--height/--out` to override) |

## Consistent characters (sprite sets)
1. Write one **character sheet line** and reuse it verbatim for every expression, changing only the expression words:
   `"original character, silver-haired android girl, glowing blue visor, white and navy combat suit, <EXPRESSION>"`
2. Reuse the **same `--seed`** for the whole set.
3. Name files `<characterId>_<expression>` to match the story JSON (`speaker` + `expression`, e.g. `aria_smile`),
   and `background` names to match the JSON `background` path (`Backgrounds/tower_night` → `--name tower_night`).
4. Generate variations with `--count 4`, Read the PNGs, keep the best, delete the rest.

Batch for a chapter: read `Assets/Resources/Stories/ChapterN.json`, collect every unique `speaker`+`expression`
and `background`, and generate only the missing files.

## Facts and limits (verified)
- No key needed. The anonymous tier returns ~576×1024 with a small logo bottom-right; the script crops that strip
  and upscales to the target size (needs Pillow). Results are placeholder quality.
- Rate limit: anonymous requests sometimes get HTTP 402/429/503; the script backs off and retries 5×. Run batches
  sequentially, never in parallel.
- Optional, still free: a Pollinations account token in env `POLLINATIONS_TOKEN` gives full-size, logo-free images.
- `--cutout`: uses `rembg` (free, local, `pip install rembg`, downloads a ~170 MB model once) if installed; otherwise
  a flood-fill of the backdrop color, which can nibble soft, light-colored edges — check the result.
- Always look at each image (Read) before reporting it as done. Regenerate on bad anatomy, text, or extra characters.

## Unity import
Sprite (2D and UI), Single, Filter `Bilinear` (these are painted, not pixel art), Max Size 2048, Compression
`High Quality` / ASTC on mobile. Characters: pivot Bottom Center. Pack backgrounds and characters in separate
SpriteAtlases.

## Rules
- Original characters only. Never prompt for existing copyrighted characters, artists' names, or franchise titles.
- Note the source (Pollinations.ai + model) in the credits. Before a commercial release, re-check the current
  Pollinations terms of use.

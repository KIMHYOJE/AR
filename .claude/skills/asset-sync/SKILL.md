---
name: asset-sync
description: Cross-check every sprite, background, BGM, SFX and VFX referenced by story and stage JSON against the files in Assets/, and generate the missing ones with the free pipelines (fetch_art, gen_sfx, pixelfx). Use after writing or editing a chapter or stage, before a playtest/build, or when asked "what assets are missing".
---

# Asset sync (story/stage JSON ↔ Assets/)

```bash
python Tools/sync_story_assets.py           # report only — always run this first
python Tools/sync_story_assets.py --sfx     # synthesize missing SFX (offline, instant)
python Tools/sync_story_assets.py --vfx     # export missing pixelfx sheets
python Tools/sync_story_assets.py --art     # fetch missing character expressions/backgrounds (network, slow, rate limited)
python Tools/sync_story_assets.py --all
```

## Conventions it enforces
| JSON reference | file |
|---|---|
| character `sprite` + node `expression` (`Characters/aria` + `smile`) | `Assets/Sprites/Characters/aria_smile.png` |
| `background` `Backgrounds/x` | `Assets/Sprites/Backgrounds/x.png` |
| `bgm` / `sfx` `BGM/x`, `SFX/x` | `Assets/Audio/BGM/x.(wav/ogg/mp3)`, `Assets/Audio/SFX/x.*` |
| stage enemy/bullet sprites `Enemies/x`, `Bullets/x` | `Assets/Sprites/Enemies/x.png` … |
| stage `deathVfx` `impacts/x` | `Assets/VFX/impacts/x.png` |

At runtime these paths are **Addressables addresses** (free package `com.unity.addressables`): mark the folders
`Assets/Sprites`, `Assets/Audio`, `Assets/VFX` addressable with "simplified" address = path relative to that folder
without extension. Keep `Resources/` for the small JSON only.

## Character design sheets
`Tools/art_profiles.json` holds each character's fixed prompt + seed, expression phrases, and background prompts.
When the story adds a character or background, add it there first (original designs only), then run `--art`.
Review every generated image (Read) — regenerate bad ones with a different seed via `Tools/fetch_art.py`.

## Rules
- Report first, generate second; `--art` makes network calls one at a time — don't parallelize.
- BGM and gameplay sprites (enemies, bullets) are listed as manual; propose options to the user (CC0 packs found via
  everything-search, pixelfx critters/projectiles, or simple procedural sprites) instead of inventing files.

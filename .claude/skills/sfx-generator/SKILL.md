---
name: sfx-generator
description: Generate retro/arcade sound effects (shots, lasers, hits, explosions, pickups, UI clicks, VN text blips, alarms, heartbeat, static) offline with a pure-Python sfxr-style synth into Assets/Audio/SFX/ as WAV. Use for any request for sound effects, SFX, UI sounds, or when a story/stage JSON references an sfx file that does not exist.
---

# Procedural SFX (offline, 0원, CC0 — the output is yours)

```bash
python Tools/gen_sfx.py --list                                  # presets + aliases
python Tools/gen_sfx.py shoot --variants 4                      # shoot_1..4.wav, pick the best
python Tools/gen_sfx.py explosion --name enemy_die --seed 42    # reproducible
python Tools/gen_sfx.py all                                     # one of every preset
```
Output: `Assets/Audio/SFX/<name>.wav`, 16-bit 44.1 kHz mono, normalized to `--volume` (default 0.6).

Presets: shoot, laser, enemy_shot, hit, player_hurt, explosion, big_explosion, pickup, powerup, ui_click,
ui_confirm, ui_cancel, text_blip, alarm, heartbeat, static, whoosh, charge, shield, pod_open, radar.
Aliases map story names (siren→alarm, crash→big_explosion, battle_start→charge, typing→text_blip, …).
Unknown names are loosely matched (`enemy_shot_2` → enemy_shot).

## Tweaking a sound
Presets live in the `PRESETS` dict in `Tools/gen_sfx.py`: `wave` (square|saw|sine|noise), `freq`, `slide`
(octaves/s, negative = falling pitch = "pew"), `a/s/d` envelope seconds, `duty`, `vib` (Hz, depth), `tremolo`,
`arp` (pitch multiplier, time), `lp` (0..1 low-pass, lower = muffled), `sub` (sub-bass Hz), `echo` (delay, feedback),
`noise_mix`, `jitter` (per-variant randomness). Add a new preset rather than editing a shared one.
You cannot hear the result — describe what you changed and let the user audition it.

## Unity import (tell the user)
- Short SFX (< 1 s): Load Type `Decompress On Load`, Compression `ADPCM`, Force To Mono.
- Long (alarm, static loops): `Compressed In Memory`, Vorbis quality 70.
- Bullet-hell shots fire constantly: play through a pooled `AudioSource` set with a per-clip cooldown
  (max ~1 play per 50 ms per clip) and random pitch 0.95–1.05 — see unity-6-coder pooling rules.

BGM is not covered here (no free, key-less generator good enough). For BGM use CC0/CC-BY packs
(e.g. OpenGameArt, Kenney, FreePD) found via the everything-search skill once downloaded, and credit CC-BY authors.

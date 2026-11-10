---
name: stage-designer
description: Design vertical bullet-hell defense stages (waves, enemies, bullet patterns, difficulty curve) as JsonUtility-ready JSON in Assets/Resources/Stages/ and validate their density/fairness. Use for any request about stages, waves, enemies, bosses, bullet/danmaku patterns, difficulty or balance, and whenever a story battle node references a stageId that has no stage file yet.
---

# Stage designer (portrait bullet-hell defense)

The story JSON hands off to battle with `battle.stageId` (e.g. `stage_01_north_wall`). Each stage is
`Assets/Resources/Stages/<stageId>.json`, parsed by `JsonUtility.FromJson<StageData>()` in
`Assets/Scripts/Battle/StageData.cs`. Sample: `Assets/Resources/Stages/stage_01_north_wall.json`.

## Coordinates
The play column is normalized: `x` 0 (left) → 1 (right), `y` 0 (bottom, the defended tower) → 1 (top, spawn line).
Speeds are in **column heights per second** (0.25 = crosses the screen in 4 s). Angles: degrees, 0 = straight down,
positive = clockwise.

## JSON contract (all keys always present)
```jsonc
{
  "stageId": "stage_01_north_wall", "title": "북쪽 장벽", "background": "Backgrounds/tower_night", "bgm": "BGM/battle_01",
  "baseHp": 20,                       // tower HP; enemies reaching y=0 deal their "damage"
  "enemies": [ { "id": "drone", "hp": 3, "speed": 0.12, "damage": 1, "score": 10, "radius": 0.035,
                 "movement": "straight", "sprite": "Enemies/drone", "deathVfx": "explosions/small-explosion" } ],
  // movement: straight | sine | zigzag | dive (accelerates at the tower) | hover (stops at y=hoverY, then shoots)
  "patterns": [ { "id": "aimed_3", "type": "aimed", "bullets": 3, "spreadDeg": 24, "speed": 0.35, "interval": 1.6,
                  "burst": 1, "burstGap": 0.0, "rotatePerShotDeg": 0, "bulletSprite": "Bullets/orb_red", "radius": 0.012 } ],
  // type: aimed (at the player) | fan (fixed downward fan) | ring (360°) | spiral (ring + rotatePerShotDeg) | random
  "waves": [ { "startTime": 2.0, "spawns": [
      { "enemyId": "drone", "count": 6, "interval": 0.5, "formation": "line", "x": 0.5, "width": 0.6,
        "pattern": "", "hoverY": 0.0 } ] } ]
  // formation: line (spread over width) | column (same x) | v | random ; pattern "" = no shooting
}
```

## Design rules
1. **Teach → test → twist**: wave 1 introduces one enemy alone, wave 2 combines it, the last wave adds one new
   pattern or a mini-boss. One new idea per stage.
2. **Readable bullets**: enemy bullets never faster than 0.55; `aimed` patterns always leave a gap
   (`spreadDeg` ≥ 18 per bullet for ≤5 bullets); rings ≤ 24 bullets in early stages.
3. **Density budget** (enemy bullets alive per second, checked by the validator):
   chapter 1–2 ≤ 12/s, 3–5 ≤ 25/s, boss ≤ 45/s.
4. **Breathing room**: ≥ 2.5 s between the last spawn of a wave and the next wave's `startTime`.
5. Story `battle.waveCount` must equal `waves.length`.
6. Every enemy `deathVfx` and pattern visual should exist in `Assets/VFX/` — export missing ones with the pixelfx
   skill (`bash Tools/pixelfx_export.sh <id>`).

## Procedure
1. Read the story chapter that references the stage (tone, waveCount, what the twist needs).
2. Write the JSON, then validate (must print OK; warnings are design feedback):
   ```bash
   python Tools/validate_stage.py Assets/Resources/Stages/<stageId>.json --stories Assets/Resources/Stories
   ```
3. Report: waves summary, peak bullets/s, estimated duration, missing VFX/sprites.

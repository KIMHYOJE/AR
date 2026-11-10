---
description: Design a bullet-hell defense stage JSON (waves, enemies, patterns) and validate it
argument-hint: "[stageId] [theme / difficulty / which story chapter]"
---

Use the `stage-designer` skill (`.claude/skills/stage-designer/SKILL.md`) and follow its contract and design rules.

Request: $ARGUMENTS

- If no stageId is given, find `battle.stageId` values in `Assets/Resources/Stories/*.json` that have no
  `Assets/Resources/Stages/<stageId>.json` yet and design the first missing one.
- Validate with `python Tools/validate_stage.py <file> --stories Assets/Resources/Stories` until it prints OK.
- Export any missing VFX with `bash Tools/pixelfx_export.sh <id>`.

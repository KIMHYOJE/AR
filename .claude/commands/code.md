---
description: Write Unity 6 C# for this project following the unity-6-coder rules
argument-hint: "[feature to implement]"
---

Follow `.claude/skills/unity-6-coder/SKILL.md` strictly (Unity 6 APIs, `[RequireComponent]` + auto-wiring instead
of Inspector drag-and-drop, mandatory `ObjectPool<T>` for anything spawned repeatedly, zero per-frame GC,
portrait 9:16 + PC input).

Task: $ARGUMENTS

1. Look at existing code under `Assets/Scripts/` first and reuse its namespaces, pools and managers.
2. Write the files with the Write tool under `Assets/Scripts/<Feature>/`.
3. Run `bash Tools/unity_check.sh` (unity-check skill) and fix errors until it prints OK (skip if the project
   doesn't exist yet, and say so).
4. Reply with: files created, required packages, and the minimal scene setup steps (≤5).

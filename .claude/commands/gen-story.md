---
description: Generate (or continue) a branching VN chapter as Unity-ready JSON
argument-hint: "[chapter number] [premise / what should happen]"
---

Use the `story-generator` skill (`.claude/skills/story-generator/SKILL.md`) and follow its JSON contract and writing
rules exactly.

Request: $ARGUMENTS

- If no chapter number is given, pick the next free `Assets/Resources/Stories/ChapterN.json`.
- If a previous chapter exists, read it first and continue from its cliffhanger, flags and characters.
- Write the file, run `python Tools/validate_story.py <file>` until it prints OK, then reply with a 5-line summary:
  hook, choices + flags, battle stage id, twist, cliffhanger.

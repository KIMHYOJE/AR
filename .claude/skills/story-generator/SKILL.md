---
name: story-generator
description: Write subculture-style visual-novel chapters (twists, tension, branching choices, battle hand-offs) as pure JSON that Unity's JsonUtility parses directly into Assets/Resources/Stories/. Use for any request to write/extend/fix story, scenario, dialogue, chapter, script, or branching routes.
---

# Story generator (VN + bullet-defense hybrid)

Output is **one JSON file per chapter** at `Assets/Resources/Stories/ChapterN.json`, loaded at runtime with
`Resources.Load<TextAsset>("Stories/ChapterN")` and parsed by `JsonUtility.FromJson<StoryChapter>()`
(`Assets/Scripts/Story/StoryData.cs`). Reference sample: `Assets/Resources/Stories/Chapter1.json`.

## JSON contract (JsonUtility-safe: no dictionaries, no polymorphism, no nulls, no comments)

```jsonc
{
  "chapterId": "ch1",                 // unique
  "title": "string",
  "startNodeId": "n001",
  "characters": [ { "id": "aria", "name": "아리아", "sprite": "Characters/aria" } ],  // + "_<expression>" = Assets/Sprites/Characters/aria_smile.png (asset-sync skill)
  "nodes": [
    {
      "id": "n001",
      "type": "dialogue",             // dialogue | narration | choice | battle | jump | end
      "speaker": "aria",              // character id, "" for narration
      "expression": "neutral",        // neutral|smile|angry|sad|surprised|serious|hurt|blush  (sprite suffix)
      "background": "Backgrounds/tower_night",   // "" = keep current
      "bgm": "",                      // "" = keep current, "stop" = stop
      "sfx": "",
      "effect": "",                   // shake|flash|fade_black|fade_white|glitch|"" (VN screen effect)
      "text": "대사",
      "next": "n002",                 // dialogue/narration/jump: next node id
      "requireFlag": "",              // jump only: go to "next" if flag set, else "elseNext"
      "elseNext": "",
      "choices": [                    // choice only (2–3 options)
        { "text": "선택지", "next": "n010", "setFlag": "trust_aria", "affinityTarget": "aria", "affinity": 1 }
      ],
      "battle": { "stageId": "", "waveCount": 0, "winNext": "", "loseNext": "" }   // battle only
    }
  ]
}
```

Every node object should contain **all** keys (use `""`, `0`, `[]`, and the empty battle object) so the format stays
uniform for tools and diffs.

## Writing rules (what makes it land with subculture players)
1. **Cold open with stakes** in the first 3 nodes (an alarm, a death, a countdown) — no exposition dump.
2. **Character voice**: each heroine has a verbal tic / speech pattern held consistently; banter between tension beats.
3. **Every chapter has exactly one twist** set up by ≥2 earlier clues (fair-play), revealed after the battle.
4. **Choices matter**: 2–3 choices per chapter; each sets a flag or affinity, and at least one `jump` node later
   reads a flag so the player sees a consequence in the same chapter. Short divergent branches, then reconverge.
5. **Battle hand-off**: one `battle` node mid-to-late chapter; `winNext` continues the story, `loseNext` gives a
   short failure scene that loops back (soft retry) — never a dead end.
6. **Cliffhanger** on the last node before `end`.
7. Lines ≤ 60 Korean characters (fits a portrait 9:16 text box in 2–3 lines). Split longer lines.
8. Original characters/settings only — never reuse names, characters or plots from existing games/anime.

## Procedure
1. Read the previous chapter JSON (if any) for flags, characters and cliffhanger to continue from.
2. Outline (in your head): hook → bond → clue → choice → battle → twist → choice consequence → cliffhanger.
3. Write the file with the Write tool. Node ids `n001`… sequential, branches allowed out of order.
4. Validate — must print `OK`:
   ```bash
   python Tools/validate_story.py Assets/Resources/Stories/ChapterN.json
   ```
   Fix any reported dangling `next`, unreachable node, missing key, or overlong line, and re-run.
5. New `battle.stageId` → create the stage with the stage-designer skill. New character/background → add it to
   `Tools/art_profiles.json`. Then run `python Tools/sync_story_assets.py` (asset-sync skill) and report what is missing.

#!/usr/bin/env python
"""Validate a story chapter JSON against the story-generator contract.

usage: python Tools/validate_story.py Assets/Resources/Stories/Chapter1.json [more.json ...]
Prints OK per file, or the list of problems (exit code 1).
"""
import json
import sys

NODE_KEYS = {"id", "type", "speaker", "expression", "background", "bgm", "sfx", "effect", "text",
             "next", "requireFlag", "elseNext", "choices", "battle"}
TYPES = {"dialogue", "narration", "choice", "battle", "jump", "end"}
MAX_LINE = 60


def validate(path):
    errs = []
    with open(path, encoding="utf-8") as f:
        data = json.load(f)
    for k in ("chapterId", "title", "startNodeId", "characters", "nodes"):
        if k not in data:
            errs.append(f"missing top-level key '{k}'")
    nodes = data.get("nodes", [])
    ids = [n.get("id") for n in nodes]
    by_id = {n.get("id"): n for n in nodes}
    if len(ids) != len(set(ids)):
        errs.append("duplicate node ids: " + ", ".join(sorted({i for i in ids if ids.count(i) > 1})))
    chars = {c.get("id") for c in data.get("characters", [])}

    def ref(src, target, label):
        if not target:
            errs.append(f"{src}: empty {label}")
        elif target not in by_id:
            errs.append(f"{src}: {label} -> '{target}' does not exist")

    for n in nodes:
        nid = n.get("id", "?")
        missing = NODE_KEYS - n.keys()
        if missing:
            errs.append(f"{nid}: missing keys {sorted(missing)}")
        t = n.get("type")
        if t not in TYPES:
            errs.append(f"{nid}: bad type '{t}'")
        sp = n.get("speaker", "")
        if sp and sp not in chars:
            errs.append(f"{nid}: unknown speaker '{sp}'")
        if len(n.get("text", "")) > MAX_LINE:
            errs.append(f"{nid}: text {len(n['text'])} chars > {MAX_LINE}")
        if t in ("dialogue", "narration"):
            ref(nid, n.get("next"), "next")
        elif t == "jump":
            ref(nid, n.get("next"), "next")
            ref(nid, n.get("elseNext"), "elseNext")
            if not n.get("requireFlag"):
                errs.append(f"{nid}: jump without requireFlag")
        elif t == "choice":
            ch = n.get("choices", [])
            if not 2 <= len(ch) <= 3:
                errs.append(f"{nid}: choice needs 2-3 options")
            for i, c in enumerate(ch):
                ref(f"{nid}.choices[{i}]", c.get("next"), "next")
                for k in ("text", "next", "setFlag", "affinityTarget", "affinity"):
                    if k not in c:
                        errs.append(f"{nid}.choices[{i}]: missing '{k}'")
        elif t == "battle":
            b = n.get("battle", {})
            if not b.get("stageId"):
                errs.append(f"{nid}: battle without stageId")
            ref(nid, b.get("winNext"), "battle.winNext")
            ref(nid, b.get("loseNext"), "battle.loseNext")

    # reachability
    start = data.get("startNodeId")
    if start not in by_id:
        errs.append(f"startNodeId '{start}' does not exist")
    else:
        seen, stack = set(), [start]
        while stack:
            cur = stack.pop()
            if cur in seen or cur not in by_id:
                continue
            seen.add(cur)
            n = by_id[cur]
            stack += [n.get("next"), n.get("elseNext")]
            stack += [c.get("next") for c in n.get("choices", [])]
            b = n.get("battle", {})
            stack += [b.get("winNext"), b.get("loseNext")]
        unreachable = [i for i in ids if i not in seen]
        if unreachable:
            errs.append("unreachable nodes: " + ", ".join(unreachable))
        if not any(by_id[i].get("type") == "end" for i in seen):
            errs.append("no reachable 'end' node")
    return errs


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 1
    bad = 0
    for p in sys.argv[1:]:
        errs = validate(p)
        if errs:
            bad = 1
            print(f"FAIL {p}")
            for e in errs:
                print("  - " + e)
        else:
            print(f"OK {p}")
    return bad


if __name__ == "__main__":
    sys.exit(main())

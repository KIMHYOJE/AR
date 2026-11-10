#!/usr/bin/env python
"""Validate stage JSON (stage-designer contract) and estimate bullet density.

usage: python Tools/validate_stage.py Assets/Resources/Stages/stage_01.json [...] [--stories Assets/Resources/Stories]
Errors -> FAIL (exit 1). Warnings are design feedback and do not fail.
"""
import glob
import json
import math
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TOP = {"stageId", "title", "background", "bgm", "baseHp", "enemies", "patterns", "waves"}
ENEMY = {"id", "hp", "speed", "damage", "score", "radius", "movement", "sprite", "deathVfx"}
PATTERN = {"id", "type", "bullets", "spreadDeg", "speed", "interval", "burst", "burstGap", "rotatePerShotDeg",
           "bulletSprite", "radius"}
SPAWN = {"enemyId", "count", "interval", "formation", "x", "width", "pattern", "hoverY"}
MOVES = {"straight", "sine", "zigzag", "dive", "hover"}
PTYPES = {"aimed", "fan", "ring", "spiral", "random"}
FORMS = {"line", "column", "v", "random"}
MAX_BULLET_SPEED = 0.55


def density_limit(stage_id):
    import re
    m = re.search(r"stage_(\d+)", stage_id)
    n = int(m.group(1)) if m else 99
    return 12 if n <= 2 else 25 if n <= 5 else 45


def story_wave_counts(story_dir):
    out = {}
    for p in glob.glob(os.path.join(story_dir, "*.json")):
        with open(p, encoding="utf-8") as f:
            for n in json.load(f).get("nodes", []):
                b = n.get("battle", {})
                if n.get("type") == "battle" and b.get("stageId"):
                    out[b["stageId"]] = (b.get("waveCount", 0), os.path.basename(p))
    return out


def validate(path, stories):
    errs, warns = [], []
    with open(path, encoding="utf-8") as f:
        s = json.load(f)
    if TOP - s.keys():
        errs.append(f"missing top-level keys {sorted(TOP - s.keys())}")
    sid = s.get("stageId", "?")
    if os.path.splitext(os.path.basename(path))[0] != sid:
        errs.append(f"file name must equal stageId '{sid}'")
    enemies = {e.get("id"): e for e in s.get("enemies", [])}
    patterns = {p.get("id"): p for p in s.get("patterns", [])}

    for e in enemies.values():
        if ENEMY - e.keys():
            errs.append(f"enemy {e.get('id')}: missing {sorted(ENEMY - e.keys())}")
        if e.get("movement") not in MOVES:
            errs.append(f"enemy {e.get('id')}: bad movement '{e.get('movement')}'")
        if e.get("hp", 0) <= 0:
            errs.append(f"enemy {e.get('id')}: hp must be > 0")
        vfx = e.get("deathVfx")
        if vfx and not os.path.exists(os.path.join(ROOT, "Assets", "VFX", vfx + ".png")):
            warns.append(f"enemy {e.get('id')}: VFX missing -> bash Tools/pixelfx_export.sh {vfx}")

    for p in patterns.values():
        pid = p.get("id")
        if PATTERN - p.keys():
            errs.append(f"pattern {pid}: missing {sorted(PATTERN - p.keys())}")
        if p.get("type") not in PTYPES:
            errs.append(f"pattern {pid}: bad type '{p.get('type')}'")
        if p.get("interval", 0) <= 0:
            errs.append(f"pattern {pid}: interval must be > 0")
        if p.get("speed", 0) > MAX_BULLET_SPEED:
            warns.append(f"pattern {pid}: speed {p['speed']} > {MAX_BULLET_SPEED} (hard to read)")
        if p.get("type") in ("aimed", "fan") and 1 < p.get("bullets", 1) <= 5:
            gap = p.get("spreadDeg", 0) / max(1, p["bullets"] - 1)
            if gap < 18:
                warns.append(f"pattern {pid}: {gap:.0f}° between bullets < 18° (no gap to dodge through)")

    # timeline simulation: bullets/sec while each shooter is alive (lifetime = time to cross the column)
    events = []  # (t_start, t_end, bullets_per_sec)
    prev_end = None
    waves = s.get("waves", [])
    for wi, w in enumerate(waves):
        t0 = w.get("startTime", 0)
        if prev_end is not None and t0 - prev_end < 2.5:
            warns.append(f"wave {wi + 1}: only {t0 - prev_end:.1f}s after previous wave's last spawn (< 2.5s)")
        last = t0
        for si, sp in enumerate(w.get("spawns", [])):
            tag = f"wave {wi + 1} spawn {si + 1}"
            if SPAWN - sp.keys():
                errs.append(f"{tag}: missing {sorted(SPAWN - sp.keys())}")
            e = enemies.get(sp.get("enemyId"))
            if not e:
                errs.append(f"{tag}: unknown enemyId '{sp.get('enemyId')}'")
                continue
            if sp.get("formation") not in FORMS:
                errs.append(f"{tag}: bad formation '{sp.get('formation')}'")
            if not 0 <= sp.get("x", 0.5) <= 1:
                errs.append(f"{tag}: x must be 0..1")
            p = patterns.get(sp.get("pattern")) if sp.get("pattern") else None
            if sp.get("pattern") and not p:
                errs.append(f"{tag}: unknown pattern '{sp.get('pattern')}'")
            life = 1.0 / max(e.get("speed", 0.1), 0.01)
            if e.get("movement") == "hover":
                life = 8.0
            for k in range(sp.get("count", 0)):
                ts = t0 + k * sp.get("interval", 0)
                last = max(last, ts)
                if p:
                    bps = p["bullets"] * max(1, p.get("burst", 1)) / p["interval"]
                    events.append((ts, ts + life, bps))
        prev_end = last

    peak, peak_t = 0.0, 0.0
    for t in sorted({ev[0] for ev in events}):
        d = sum(b for a, z, b in events if a <= t < z)
        if d > peak:
            peak, peak_t = d, t
    limit = density_limit(sid)
    if peak > limit:
        warns.append(f"peak density {peak:.1f} bullets/s at t={peak_t:.1f}s > budget {limit}/s")
    duration = max([z for _, z, _ in events] + [prev_end or 0])

    if stories:
        refs = story_wave_counts(stories)
        if sid not in refs:
            warns.append("no story battle node references this stageId")
        elif refs[sid][0] != len(waves):
            errs.append(f"{refs[sid][1]} expects waveCount={refs[sid][0]} but stage has {len(waves)} waves")
    summary = f"{len(waves)} waves, {len(enemies)} enemy types, peak {peak:.1f} bullets/s, ~{math.ceil(duration)}s"
    return errs, warns, summary


def main():
    args, stories = [], None
    it = iter(sys.argv[1:])
    for a in it:
        if a == "--stories":
            stories = next(it)
        else:
            args.append(a)
    if not args:
        print(__doc__)
        return 1
    bad = 0
    for p in args:
        errs, warns, summary = validate(p, stories)
        print(("FAIL " if errs else "OK ") + f"{p}  ({summary})")
        for e in errs:
            print("  ERROR " + e)
        for w in warns:
            print("  warn  " + w)
        bad |= bool(errs)
    return bad


if __name__ == "__main__":
    sys.exit(main())

#!/usr/bin/env python
"""Check every asset referenced by story + stage JSON and (optionally) generate what is missing.

usage:
  python Tools/sync_story_assets.py                 # report only
  python Tools/sync_story_assets.py --sfx           # also synthesize missing SFX (Tools/gen_sfx.py, offline)
  python Tools/sync_story_assets.py --art           # also fetch missing character/background art (Pollinations)
  python Tools/sync_story_assets.py --vfx           # also export missing VFX (pixelfx)
  python Tools/sync_story_assets.py --all           # all of the above

Reference conventions (paths without extension):
  character sprite  <characters[].sprite>_<expression>  -> Assets/Sprites/<path>.png   e.g. Characters/aria_smile
  background        Backgrounds/<name>                  -> Assets/Sprites/<path>.png
  bgm / sfx         BGM/<name>, SFX/<name>              -> Assets/Audio/<path>.(wav|ogg|mp3)
  stage sprites     Enemies/..., Bullets/...            -> Assets/Sprites/<path>.png
  stage VFX         <category>/<effect>                 -> Assets/VFX/<path>.png
Art prompts/seeds come from Tools/art_profiles.json.
"""
import glob
import json
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PY = sys.executable
AUDIO_EXT = (".wav", ".ogg", ".mp3")


def exists(base, path, exts):
    return any(os.path.exists(os.path.join(ROOT, base, path + e)) for e in exts)


def collect():
    need = {"sprite": set(), "audio": set(), "vfx": set()}
    for p in glob.glob(os.path.join(ROOT, "Assets/Resources/Stories/*.json")):
        with open(p, encoding="utf-8") as f:
            ch = json.load(f)
        sprites = {c["id"]: c.get("sprite", "") for c in ch.get("characters", [])}
        for n in ch.get("nodes", []):
            sp, ex = n.get("speaker"), n.get("expression")
            if sp and sprites.get(sp) and ex:
                need["sprite"].add(f"{sprites[sp]}_{ex}")
            if n.get("background"):
                need["sprite"].add(n["background"])
            for k in ("bgm", "sfx"):
                if n.get(k) and n[k] != "stop":
                    need["audio"].add(n[k])
    for p in glob.glob(os.path.join(ROOT, "Assets/Resources/Stages/*.json")):
        with open(p, encoding="utf-8") as f:
            st = json.load(f)
        if st.get("background"):
            need["sprite"].add(st["background"])
        if st.get("bgm"):
            need["audio"].add(st["bgm"])
        for e in st.get("enemies", []):
            if e.get("sprite"):
                need["sprite"].add(e["sprite"])
            if e.get("deathVfx"):
                need["vfx"].add(e["deathVfx"])
        for b in st.get("patterns", []):
            if b.get("bulletSprite"):
                need["sprite"].add(b["bulletSprite"])
    return need


def run(cmd):
    print("  $ " + " ".join(cmd))
    return subprocess.call(cmd, cwd=ROOT) == 0


def main():
    flags = set(sys.argv[1:])
    if "--all" in flags:
        flags |= {"--sfx", "--art", "--vfx"}
    with open(os.path.join(ROOT, "Tools/art_profiles.json"), encoding="utf-8") as f:
        prof = json.load(f)
    need = collect()
    missing = {
        "sprite": sorted(s for s in need["sprite"] if not exists("Assets/Sprites", s, (".png", ".jpg"))),
        "audio": sorted(s for s in need["audio"] if not exists("Assets/Audio", s, AUDIO_EXT)),
        "vfx": sorted(s for s in need["vfx"] if not exists("Assets/VFX", s, (".png",))),
    }
    total = sum(len(v) for v in need.values())
    print(f"{total} referenced assets, {sum(len(v) for v in missing.values())} missing")
    for k, v in missing.items():
        for s in v:
            print(f"  missing {k:6s} {s}")

    manual = []
    if "--sfx" in flags:
        sys.path.insert(0, os.path.join(ROOT, "Tools"))
        import gen_sfx
        for a in missing["audio"]:
            folder, name = os.path.split(a)
            if folder != "SFX":
                manual.append(f"{a}  (BGM: add a CC0/CC-BY track manually)")
                continue
            if not gen_sfx.resolve(name):
                manual.append(f"{a}  (no matching SFX preset; add one to gen_sfx.py)")
                continue
            run([PY, "Tools/gen_sfx.py", name, "--name", name, "--seed", "1"])
    if "--vfx" in flags:
        for v in missing["vfx"]:
            run(["bash", "Tools/pixelfx_export.sh", v])
    if "--art" in flags:
        chars, exprs, bgs = prof["characters"], prof["expressions"], prof["backgrounds"]
        for s in missing["sprite"]:
            folder, name = os.path.split(s)
            if folder == "Characters" and "_" in name:
                cid, ex = name.split("_", 1)
                c = chars.get(cid)
                if not c:
                    manual.append(f"{s}  (add '{cid}' to Tools/art_profiles.json characters)")
                    continue
                prompt = f"{c['prompt']}, {exprs.get(ex, ex + ' expression')}"
                run([PY, "Tools/fetch_art.py", "character", prompt, "--name", name, "--seed", str(c["seed"]),
                     "--cutout"])
            elif folder == "Backgrounds":
                prompt = bgs.get(name)
                if not prompt:
                    manual.append(f"{s}  (add '{name}' to Tools/art_profiles.json backgrounds)")
                    continue
                run([PY, "Tools/fetch_art.py", "background", prompt, "--name", name, "--seed", "4120"])
            else:
                manual.append(f"{s}  (gameplay sprite: draw it or export a pixelfx effect)")
    if manual:
        print("needs a human / a decision:")
        for m in manual:
            print("  - " + m)
    return 0


if __name__ == "__main__":
    sys.exit(main())

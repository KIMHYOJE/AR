#!/usr/bin/env python
"""Free illustration pipeline: Pollinations.ai (no API key) -> Assets/Sprites/.

usage:
  python Tools/fetch_art.py character "silver-haired android girl, blue visor, combat suit" --name aria_neutral
  python Tools/fetch_art.py background "ruined futuristic city at night, lighthouse tower" --name tower_night
  python Tools/fetch_art.py character "..." --name aria_smile --seed 1234 --cutout   # white bg -> transparent
  python Tools/fetch_art.py raw "pixel art bullet sprite" --width 512 --height 512 --out Assets/Sprites/Misc

Kinds and defaults:
  character   768x1344 (9:16) -> Assets/Sprites/Characters/   full-body standing, plain white background
  background  1080x1920 (9:16) -> Assets/Sprites/Backgrounds/  no people
  cg          1920x1080        -> Assets/Sprites/CG/
  raw         1024x1024        -> Assets/Sprites/Misc/        prompt used as-is

Re-use the same --seed and character description for every expression of one character to keep them consistent.
Stdlib only; Pillow (pip install pillow) enables --cutout, watermark crop and resize to the exact size.
Anonymous requests come back ~576x1024 with a small logo bottom-right: the script crops that strip and upscales.
Optional: set POLLINATIONS_TOKEN (free account at auth.pollinations.ai) for full-size, logo-free images.
"""
import argparse
import os
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ENDPOINT = "https://image.pollinations.ai/prompt/"

STYLE = "2D anime illustration, visual novel art style, flat cel shading, clean bold lineart, high quality"
KINDS = {
    "character": dict(w=768, h=1344, dir="Assets/Sprites/Characters",
                      pre=f"{STYLE}, full body standing character sprite, single character, front view, ",
                      post=", isolated on a flat solid pure white background, no shadow, no text, no watermark"),
    "background": dict(w=1080, h=1920, dir="Assets/Sprites/Backgrounds",
                       pre=f"{STYLE}, visual novel background, vertical composition, ",
                       post=", scenery only, no people, no characters, no text, no watermark"),
    "cg": dict(w=1920, h=1080, dir="Assets/Sprites/CG",
               pre=f"{STYLE}, visual novel event CG, cinematic, ", post=", no text, no watermark"),
    "raw": dict(w=1024, h=1024, dir="Assets/Sprites/Misc", pre="", post=""),
}


def slug(s):
    return re.sub(r"[^a-z0-9]+", "_", s.lower()).strip("_")[:48] or "image"


def download(prompt, w, h, seed, model, retries=5):
    q = urllib.parse.urlencode({"width": w, "height": h, "seed": seed, "model": model,
                                "nologo": "true", "private": "true", "enhance": "false"})
    url = ENDPOINT + urllib.parse.quote(prompt, safe="") + "?" + q
    headers = {"User-Agent": "unity-art-fetcher/1.0"}
    token = os.environ.get("POLLINATIONS_TOKEN")  # optional free token from auth.pollinations.ai: no logo, full size
    if token:
        headers["Authorization"] = f"Bearer {token}"
    req = urllib.request.Request(url, headers=headers)
    for attempt in range(1, retries + 1):
        try:
            with urllib.request.urlopen(req, timeout=180) as r:
                ctype = r.headers.get("Content-Type", "")
                data = r.read()
            if not ctype.startswith("image/"):
                raise RuntimeError(f"not an image ({ctype}): {data[:200]!r}")
            return data, ctype
        except (urllib.error.URLError, RuntimeError, TimeoutError) as e:
            print(f"  attempt {attempt}/{retries} failed: {e}", file=sys.stderr)
            if attempt < retries:
                time.sleep(15 * attempt)  # anonymous tier is rate limited (402/429/503): back off
    raise SystemExit("download failed")


def strip_watermark(img, frac=0.055):
    """Anonymous tier stamps a logo bottom-right: cut the bottom strip, re-center-crop to keep the aspect ratio."""
    w, h = img.size
    nh = int(h * (1 - frac))
    nw = int(w * nh / h)
    x0 = (w - nw) // 2
    return img.crop((x0, 0, x0 + nw, nh))


def to_png(data, path, cutout, size=None):
    try:
        from PIL import Image
    except ImportError:
        if cutout:
            print("  --cutout needs Pillow: pip install pillow (saving without cutout)", file=sys.stderr)
        if data[:8] != b"\x89PNG\r\n\x1a\n":
            path = os.path.splitext(path)[0] + ".jpg"  # keep real format; Unity imports jpg fine
        with open(path, "wb") as f:
            f.write(data)
        return path
    import io
    img = Image.open(io.BytesIO(data)).convert("RGBA")
    if not os.environ.get("POLLINATIONS_TOKEN"):
        img = strip_watermark(img)
    if size and img.size != size:
        img = img.resize(size, Image.LANCZOS)
    if cutout:
        img = bg_to_alpha(img)
    img.save(path, "PNG")
    return path


def bg_to_alpha(img, tol=22):
    """Remove the backdrop. Uses rembg (free, MIT, local AI matting: pip install rembg) when installed,
    otherwise flood-fills from the borders every pixel within `tol` of the median border color
    (models often paint grey/off-white instead of pure white). Interior whites of the character stay opaque."""
    try:
        from rembg import remove
        return remove(img)
    except ImportError:
        pass
    from collections import deque
    w, h = img.size
    px = img.load()
    border = [px[x, 0] for x in range(w)] + [px[x, h - 1] for x in range(w)] + \
             [px[0, y] for y in range(h)] + [px[w - 1, y] for y in range(h)]
    bg = tuple(sorted(c[i] for c in border)[len(border) // 2] for i in range(3))

    def dist(a, b):
        return max(abs(a[0] - b[0]), abs(a[1] - b[1]), abs(a[2] - b[2]))

    seen = bytearray(w * h)
    q = deque()
    for x in range(w):
        q.append((x, 0)); q.append((x, h - 1))
    for y in range(h):
        q.append((0, y)); q.append((w - 1, y))
    while q:
        x, y = q.popleft()
        i = y * w + x
        if seen[i]:
            continue
        seen[i] = 1
        c = px[x, y]
        if dist(c, bg) > tol:
            continue
        px[x, y] = (c[0], c[1], c[2], 0)
        if x > 0: q.append((x - 1, y))
        if x < w - 1: q.append((x + 1, y))
        if y > 0: q.append((x, y - 1))
        if y < h - 1: q.append((x, y + 1))
    # soften the 1px edge: semi-transparent where an opaque pixel touches the cleared area
    alpha = img.getchannel("A")
    from PIL import ImageFilter
    img.putalpha(alpha.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.SMOOTH))
    return img


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("kind", choices=KINDS)
    ap.add_argument("prompt")
    ap.add_argument("--name", help="output file name without extension")
    ap.add_argument("--seed", type=int, default=None, help="fixed seed = reproducible / consistent character")
    ap.add_argument("--width", type=int)
    ap.add_argument("--height", type=int)
    ap.add_argument("--model", default="flux")
    ap.add_argument("--out", help="output folder relative to project root")
    ap.add_argument("--cutout", action="store_true", help="make the white background transparent (Pillow)")
    ap.add_argument("--count", type=int, default=1, help="variations (seed, seed+1, ...)")
    a = ap.parse_args()

    k = KINDS[a.kind]
    prompt = k["pre"] + a.prompt + k["post"]
    w, h = a.width or k["w"], a.height or k["h"]
    out_dir = os.path.join(ROOT, a.out or k["dir"])
    os.makedirs(out_dir, exist_ok=True)
    base_seed = a.seed if a.seed is not None else int(time.time()) % 1_000_000
    name = a.name or slug(a.prompt)

    for i in range(a.count):
        seed = base_seed + i
        fname = name if a.count == 1 else f"{name}_{i + 1}"
        path = os.path.join(out_dir, fname + ".png")
        print(f"[{i + 1}/{a.count}] {w}x{h} seed={seed} -> {os.path.relpath(path, ROOT)}")
        data, _ = download(prompt, w, h, seed, a.model)
        saved = to_png(data, path, a.cutout and a.kind != "background", (w, h))
        print(f"  saved {os.path.relpath(saved, ROOT)} ({len(data) // 1024} KB)")
        if i + 1 < a.count:
            time.sleep(3)


if __name__ == "__main__":
    main()

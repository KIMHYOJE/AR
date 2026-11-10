#!/usr/bin/env python
"""Procedural pixel-art placeholder sprites for the battle (Pillow). Replace with real art any time —
files keep the same names/addresses.

usage: python Tools/gen_placeholder_sprites.py [--force]
Writes Assets/Sprites/{Enemies,Bullets,Player}/*.png (skips existing files unless --force).
"""
import math
import os
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def orb(size, core, glow):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    c = (size - 1) / 2
    for y in range(size):
        for x in range(size):
            d = math.hypot(x - c, y - c) / (size / 2)
            if d <= 0.45:
                img.putpixel((x, y), (255, 255, 255, 255))
            elif d <= 0.75:
                img.putpixel((x, y), core + (255,))
            elif d <= 1.0:
                img.putpixel((x, y), glow + (150,))
    return img


def from_ascii(rows, palette):
    h, w = len(rows), max(len(r) for r in rows)
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch in palette:
                img.putpixel((x, y), palette[ch])
    return img


PAL = {
    "#": (40, 10, 30, 255),     # outline
    "r": (200, 40, 70, 255),    # body red
    "R": (255, 110, 120, 255),  # highlight
    "v": (110, 50, 170, 255),   # violet
    "V": (190, 130, 255, 255),
    "y": (255, 220, 90, 255),   # eye / core
    "w": (255, 255, 255, 255),
    "b": (40, 90, 170, 255),    # navy
    "c": (110, 220, 255, 255),  # cyan
    "s": (220, 230, 240, 255),  # suit white
}

MOTE = [
    "....####....",
    "..##rrrr##..",
    ".#rrRRrrrr#.",
    ".#rRyyyyrr#.",
    "#rrRywwyrrr#",
    "#rrrywwyrrr#",
    "#rrryyyyrrr#",
    ".#rrrrrrrr#.",
    ".#r#rrrr#r#.",
    "..#.#rr#.#..",
    "....#..#....",
]

WEAVER = [
    "#..........##..........#",
    "##........#vv#........##",
    "#v#......#vVVv#......#v#",
    ".#v#....#vVyyVv#....#v#.",
    "..#vv##vvVywwyVvv##vv#..",
    "...#vvvvvVywwyVvvvvv#...",
    "....#vvvvVyyyyVvvvv#....",
    "...#v#vvvvVVVVvvvv#v#...",
    "..#v#.#vvvvvvvvvv#.#v#..",
    ".#v#...#vv#vv#vv#...#v#.",
    "#v#.....##.##.##.....#v#",
    "##....................##",
]


def lantern(size=40):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    c = (size - 1) / 2
    for y in range(size):
        for x in range(size):
            dx, dy = x - c, y - c
            d = math.hypot(dx, dy)
            ang = math.atan2(dy, dx)
            spikes = 0.5 + 0.5 * math.cos(ang * 8)
            outer = size * (0.36 + 0.12 * spikes)
            if d <= size * 0.14:
                img.putpixel((x, y), PAL["w"])
            elif d <= size * 0.22:
                img.putpixel((x, y), PAL["y"])
            elif d <= size * 0.30:
                img.putpixel((x, y), PAL["V"])
            elif d <= outer - 1.5:
                img.putpixel((x, y), PAL["v"])
            elif d <= outer:
                img.putpixel((x, y), PAL["#"])
    return img


ARIA_UNIT = [
    "...........##...........",
    "..........#cc#..........",
    "..........#cc#..........",
    ".........#scsc#.........",
    ".........#ssss#.........",
    "........#ssccss#........",
    "...##...#sbccbs#...##...",
    "..#cc#.#ssbccbss#.#cc#..",
    "..#cs##ssbbccbbss##sc#..",
    ".#csssssbbbccbbbsssssc#.",
    "#cssssbbbb#cc#bbbbsssssc",
    "#cssbbb###.##.###bbbsssc",
    ".#cb##...........##bc#..",
    "..##...............##...",
]


def main():
    force = "--force" in sys.argv
    out = {
        "Bullets/orb_red.png": orb(8, (255, 70, 90), (255, 40, 60)),
        "Bullets/orb_violet.png": orb(8, (190, 110, 255), (140, 60, 255)),
        "Bullets/orb_cyan.png": orb(6, (120, 230, 255), (60, 180, 255)),
        "Enemies/static_mote.png": from_ascii(MOTE, PAL),
        "Enemies/static_weaver.png": from_ascii(WEAVER, PAL),
        "Enemies/static_lantern.png": lantern(40),
        "Player/aria_unit.png": from_ascii(ARIA_UNIT, PAL),
    }
    for rel, img in out.items():
        path = os.path.join(ROOT, "Assets", "Sprites", rel)
        if os.path.exists(path) and not force:
            print(f"skip   {rel}")
            continue
        os.makedirs(os.path.dirname(path), exist_ok=True)
        img.save(path)
        print(f"wrote  Assets/Sprites/{rel}  {img.size[0]}x{img.size[1]}")


if __name__ == "__main__":
    main()

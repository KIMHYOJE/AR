#!/usr/bin/env python
"""Procedural retro SFX generator (sfxr-style), stdlib only, fully offline, 0 cost, output is yours (CC0).

usage:
  python Tools/gen_sfx.py <preset> [--name file] [--variants N] [--seed N] [--out Assets/Audio/SFX] [--volume 0.6]
  python Tools/gen_sfx.py --list
  python Tools/gen_sfx.py all                 # one of every preset

Writes 16-bit 44.1 kHz mono WAV.
"""
import argparse
import math
import os
import random
import struct
import sys
import wave

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SR = 44100

# Each preset: wave, base freq (Hz), freq slide (octaves/sec), attack/sustain/decay (s), plus extras.
# "jitter" keys are randomized per variant by +-that fraction.
PRESETS = {
    "shoot":      dict(wave="square", freq=880, slide=-3.0, a=0.0, s=0.04, d=0.10, duty=0.3, jitter=0.15),
    "laser":      dict(wave="saw", freq=1400, slide=-4.5, a=0.0, s=0.05, d=0.18, vib=(30, 0.05), jitter=0.15),
    "enemy_shot": dict(wave="square", freq=520, slide=-1.5, a=0.0, s=0.03, d=0.12, duty=0.5, jitter=0.15),
    "hit":        dict(wave="noise", freq=900, slide=-2.0, a=0.0, s=0.02, d=0.10, lp=0.5, jitter=0.2),
    "player_hurt": dict(wave="square", freq=300, slide=-2.5, a=0.0, s=0.06, d=0.25, duty=0.5, noise_mix=0.4, jitter=0.1),
    "explosion":  dict(wave="noise", freq=500, slide=-1.2, a=0.0, s=0.10, d=0.70, lp=0.25, jitter=0.2),
    "big_explosion": dict(wave="noise", freq=300, slide=-0.8, a=0.01, s=0.25, d=1.4, lp=0.15, sub=55, jitter=0.15),
    "pickup":     dict(wave="square", freq=988, arp=(1.5, 0.06), a=0.0, s=0.05, d=0.14, duty=0.5, jitter=0.1),
    "powerup":    dict(wave="square", freq=330, slide=2.2, a=0.0, s=0.25, d=0.25, vib=(12, 0.08), duty=0.4, jitter=0.1),
    "ui_click":   dict(wave="square", freq=1800, slide=-6, a=0.0, s=0.005, d=0.03, duty=0.5, jitter=0.05),
    "ui_confirm": dict(wave="square", freq=660, arp=(1.5, 0.07), a=0.0, s=0.08, d=0.12, duty=0.25, jitter=0.05),
    "ui_cancel":  dict(wave="square", freq=440, arp=(0.75, 0.07), a=0.0, s=0.08, d=0.12, duty=0.25, jitter=0.05),
    "text_blip":  dict(wave="square", freq=760, a=0.0, s=0.012, d=0.02, duty=0.5, jitter=0.2),
    "alarm":      dict(wave="saw", freq=620, a=0.02, s=1.2, d=0.2, vib=(2.5, 0.35), lp=0.6, jitter=0.05),
    "heartbeat":  dict(wave="sine", freq=55, slide=-1.0, a=0.005, s=0.05, d=0.18, echo=(0.28, 0.7), jitter=0.05),
    "static":     dict(wave="noise", freq=4000, a=0.02, s=0.6, d=0.3, lp=0.9, tremolo=(18, 0.7), jitter=0.1),
    "whoosh":     dict(wave="noise", freq=800, slide=1.5, a=0.15, s=0.05, d=0.35, lp=0.2, jitter=0.15),
    "charge":     dict(wave="saw", freq=180, slide=3.0, a=0.3, s=0.4, d=0.1, vib=(20, 0.04), lp=0.5, jitter=0.1),
    "shield":     dict(wave="sine", freq=520, slide=0.5, a=0.02, s=0.3, d=0.4, vib=(9, 0.12), tremolo=(14, 0.4), jitter=0.1),
    "pod_open":   dict(wave="noise", freq=2500, slide=-1.0, a=0.05, s=0.3, d=0.5, lp=0.35, sub=90, jitter=0.1),
    "radar":      dict(wave="sine", freq=1320, a=0.0, s=0.03, d=0.25, echo=(0.18, 0.45), jitter=0.05),
}
ALIASES = {"siren": "alarm", "crash": "big_explosion", "battle_start": "charge", "beep": "ui_click",
           "coin": "pickup", "boom": "explosion", "bullet": "shoot", "damage": "player_hurt", "typing": "text_blip"}


def synth(p, rng, volume):
    def j(v):
        jit = p.get("jitter", 0)
        return v * (1 + rng.uniform(-jit, jit))

    freq0 = j(p["freq"])
    slide = p.get("slide", 0) * (1 + rng.uniform(-0.1, 0.1))
    a, s, d = p.get("a", 0), j(p.get("s", 0.1)), j(p.get("d", 0.2))
    total = a + s + d
    n = int(total * SR)
    duty = p.get("duty", 0.5)
    vib_hz, vib_depth = p.get("vib", (0, 0))
    trem_hz, trem_depth = p.get("tremolo", (0, 0))
    arp_mult, arp_t = p.get("arp", (1, 0))
    lp = p.get("lp", 1.0)
    noise_mix = p.get("noise_mix", 0.0)
    sub = p.get("sub", 0)
    out = [0.0] * n
    phase, sub_phase, lp_state = 0.0, 0.0, 0.0
    noise_val, noise_phase = rng.uniform(-1, 1), 0.0
    for i in range(n):
        t = i / SR
        f = freq0 * (2 ** (slide * t))
        if arp_t and t >= arp_t:
            f *= arp_mult
        if vib_hz:
            f *= 1 + vib_depth * math.sin(2 * math.pi * vib_hz * t)
        f = min(max(f, 20.0), SR / 2.2)
        phase = (phase + f / SR) % 1.0
        w = p["wave"]
        if w == "square":
            v = 1.0 if phase < duty else -1.0
        elif w == "saw":
            v = 2.0 * phase - 1.0
        elif w == "sine":
            v = math.sin(2 * math.pi * phase)
        else:  # noise, resampled at f for pitch
            noise_phase += f / SR
            if noise_phase >= 1.0:
                noise_phase -= int(noise_phase)
                noise_val = rng.uniform(-1, 1)
            v = noise_val
        if noise_mix:
            v = v * (1 - noise_mix) + rng.uniform(-1, 1) * noise_mix
        lp_state += (v - lp_state) * lp
        v = lp_state
        if sub:
            sub_phase = (sub_phase + sub * (2 ** (-1.0 * t)) / SR) % 1.0
            v = v * 0.7 + math.sin(2 * math.pi * sub_phase) * 0.6
        if trem_hz:
            v *= 1 - trem_depth * (0.5 + 0.5 * math.sin(2 * math.pi * trem_hz * t))
        if t < a:
            env = t / a if a else 1.0
        elif t < a + s:
            env = 1.0
        else:
            env = max(0.0, 1.0 - (t - a - s) / d) ** 2
        out[i] = v * env
    if "echo" in p:
        delay, fb = p["echo"]
        k = int(delay * SR)
        out += [0.0] * (k * 3)
        for i in range(k, len(out)):
            out[i] += out[i - k] * fb
    peak = max(1e-9, max(abs(x) for x in out))
    g = volume / peak
    return [x * g for x in out]


def write_wav(path, samples):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    fade = min(len(samples), int(0.004 * SR))  # declick
    for i in range(fade):
        samples[-1 - i] *= i / fade
    with wave.open(path, "wb") as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(SR)
        wf.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, x)) * 32767)) for x in samples))


def resolve(name):
    key = name.lower()
    if key in PRESETS:
        return key
    if key in ALIASES:
        return ALIASES[key]
    for k in PRESETS:  # loose match: "enemy_shot_2" -> enemy_shot
        if k in key or key in k:
            return k
    return None


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("preset", nargs="?")
    ap.add_argument("--name")
    ap.add_argument("--variants", type=int, default=1)
    ap.add_argument("--seed", type=int, default=None)
    ap.add_argument("--out", default="Assets/Audio/SFX")
    ap.add_argument("--volume", type=float, default=0.6)
    ap.add_argument("--list", action="store_true")
    a = ap.parse_args()
    if a.list or not a.preset:
        print("presets: " + ", ".join(PRESETS))
        print("aliases: " + ", ".join(f"{k}->{v}" for k, v in ALIASES.items()))
        return 0
    names = list(PRESETS) if a.preset == "all" else [a.preset]
    for nm in names:
        key = resolve(nm)
        if not key:
            print(f"unknown preset '{nm}' (try --list)", file=sys.stderr)
            return 1
        base = a.name if a.name and len(names) == 1 else nm
        for v in range(a.variants):
            seed = (a.seed if a.seed is not None else random.randrange(1 << 30)) + v
            samples = synth(PRESETS[key], random.Random(seed), a.volume)
            fname = base if a.variants == 1 else f"{base}_{v + 1}"
            path = os.path.join(ROOT, a.out, fname + ".wav")
            write_wav(path, samples)
            print(f"{key:14s} seed={seed:<10d} {len(samples) / SR:.2f}s -> {os.path.relpath(path, ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

"""Builds every cutout rig: parts + rig.json + clips.json into Assets/Art/Animations/<Name>/.

    python tools/build_rigs.py                 # all characters
    python tools/build_rigs.py Commander       # one
    python tools/build_rigs.py --preview       # also contact sheets in Assets/Art/Generated/_debug/rigs

Unity side: menu "Bastião/Reconstruir rigs" (Assets/Editor/RigBuilder.cs) turns them into prefabs + Animator.
"""
import os
import sys

import numpy as np

import figures_src
import rig_motion
from pixelkit import area_downscale, native_grid, to_image, trim
from rig_defs import CHARACTERS
from rigkit import ANIM_DIR, Rig, contact_sheet

DEBUG_DIR = os.path.join(os.path.dirname(ANIM_DIR), "Generated", "_debug", "rigs")
_cache = {}


def load_figure(name):
    if name not in _cache:
        _cache[name] = figures_src.figure(name)
    return _cache[name]


def build(name, spec, preview=False):
    rgb, alpha = spec["source"]() if "source" in spec else load_figure(spec["figure"])
    if "recolor" in spec:
        rgb = spec["recolor"](rgb, alpha)
    if "splash" in spec:
        export_splash(name, rgb, alpha, **spec["splash"])
    rig = Rig(name, rgb, alpha, spec["height"], spec["parts"], feet=spec.get("feet"),
              palette_k=spec.get("palette", 40)).cut()
    motion = dict(spec["motion"])
    kind = motion.pop("kind")
    clips = getattr(rig_motion, kind)(motion)
    if "extra_clips" in spec:
        clips.update(spec["extra_clips"](rig))
    extra = spec.get("rig_extra", {})
    if "post" in spec:
        extra = spec["post"](rig, clips) or extra
    rig.export(clips, extra)
    export_previews(rig, clips)
    if preview:
        os.makedirs(DEBUG_DIR, exist_ok=True)
        rig.overlay(os.path.join(DEBUG_DIR, f"{name}_parts.png"))
        names = [c for c in ("Idle", "Walk", "Run", "Attack", "Hit", "Death", "Cast", "Special", "Intro", "Spawn") if c in clips]
        contact_sheet(rig, clips, os.path.join(DEBUG_DIR, f"{name}_clips.png"), frames=6, zoom=3, names=names)
    print(f"rig {name}: {len(rig.images)} parts, {rig.out_w}x{rig.out_h}px, clips {sorted(clips)}")
    return rig, clips


def export_splash(name, rgb, alpha, pitch=None, height=190, portrait=(0.3, 0.0, 0.75, 0.4)):
    """Menu art in true pixel art: the source's native pixel grid (no rescaling blur), shown at 3x by the UI.
    Painted sources without a grid are reduced to the same height with area averaging."""
    if pitch:
        r, a = native_grid(rgb, alpha, pitch)
    else:
        H, W = alpha.shape
        r, a, _ = area_downscale(rgb, alpha, (max(1, round(W * height / H)), height), k=96)
    r, a = trim(r, a)
    d = os.path.join(ANIM_DIR, name)
    os.makedirs(d, exist_ok=True)
    to_image(r, a).save(os.path.join(d, "splash.png"))
    Hn, Wn = a.shape
    u0, v0, u1, v1 = portrait
    x0, y0, x1, y1 = int(u0 * Wn), int(v0 * Hn), int(u1 * Wn), int(v1 * Hn)
    side = max(x1 - x0, y1 - y0)
    cx, cy = (x0 + x1) // 2, (y0 + y1) // 2
    x0, y0 = max(0, cx - side // 2), max(0, cy - side // 2)
    to_image(r[y0:y0 + side, x0:x0 + side], a[y0:y0 + side, x0:x0 + side]).save(os.path.join(d, "portrait.png"))
    print(f"splash {name}: {Wn}x{Hn}, portrait {side}px")


def export_previews(rig, clips):
    """Rendered frames for the menus (skins, commander screen) and a rest-pose icon for enemy rosters."""
    d = os.path.join(ANIM_DIR, rig.name, "preview")
    os.makedirs(d, exist_ok=True)
    for f in os.listdir(d):
        if f.endswith(".png"):
            os.remove(os.path.join(d, f))
    canvas = (int(rig.out_w * 2.0), int(rig.out_h * 1.3))

    def crop_save(im, path):
        box = im.getbbox()
        if box:
            im = im.crop(box)
        im.save(path)
    rest = rig.render(None, 0, zoom=1, canvas=canvas)
    crop_save(rest, os.path.join(d, "icon.png"))
    for name, key, n in (("idle", "Idle", 6), ("attack", "Attack", 8), ("walk", "Walk", 8)):
        if key not in clips:
            continue
        c = clips[key]
        frames = [rig.render(c, c["length"] * i / n, zoom=1, canvas=canvas) for i in range(n)]
        # one shared crop box so the frames stay aligned
        boxes = [f.getbbox() for f in frames if f.getbbox()]
        if not boxes:
            continue
        x0, y0 = min(b[0] for b in boxes), min(b[1] for b in boxes)
        x1, y1 = max(b[2] for b in boxes), max(b[3] for b in boxes)
        for i, f in enumerate(frames):
            f.crop((x0, y0, x1, y1)).save(os.path.join(d, f"{name}_{i}.png"))


if __name__ == "__main__":
    preview = "--preview" in sys.argv
    wanted = [a for a in sys.argv[1:] if not a.startswith("--")] or list(CHARACTERS)
    for n in wanted:
        build(n, CHARACTERS[n], preview)

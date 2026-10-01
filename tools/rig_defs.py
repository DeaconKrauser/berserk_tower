"""Part maps of every rigged character, in figure-normalised coordinates (u right, v down, 0..1 of the
cleaned figure's bounding box; see figures_src.py). Each part: poly (one or several polygons), pivot (joint),
parent, z (draw order, higher in front), fill (inpaint what front parts hide). Leftover pixels join the
nearest part. Hierarchies follow rig_motion: hips -> legs / skirt / torso -> head, arms -> weapon, cape.
"""
import numpy as np
from PIL import Image, ImageDraw

import figures_src
from pixelkit import (area_downscale, depink, from_image, hsv_to_rgb, inpaint, key_magenta, label, load_rgb, rgb_to_hsv,
                      scale2x, to_image, trim)
import sources

H = "hips"


def joint(uv, parent=None):
    return dict(joint=True, pivot=uv, parent=parent, z=-10)


# ---------------------------------------------------------------- shared humanoid motion keys
def hm(**kw):
    base = dict(kind="humanoid", hips=H, torso="torso", head="head", armF="armF", armB="armB", weapon="weapon",
                cape="cape", skirt="skirt", legF="legF", legB="legB")
    base.update(kw)
    return base


# ---------------------------------------------------------------- commander (approved design sheet, SE view)
COMMANDER_PARTS = {
    H: joint((0.5, 0.6)),
    "cape": dict(poly=[[(0.0, 0.83), (0.02, 0.62), (0.08, 0.45), (0.14, 0.3), (0.2, 0.2), (0.3, 0.12), (0.42, 0.1),
                        (0.42, 0.3), (0.34, 0.45), (0.3, 0.6), (0.32, 0.78), (0.28, 0.92), (0.15, 0.93), (0.05, 0.9)],
                       [(0.64, 0.14), (0.74, 0.18), (0.8, 0.33), (0.84, 0.52), (0.86, 0.76), (0.78, 0.8), (0.7, 0.62), (0.66, 0.4)]],
                 pivot=(0.5, 0.16), parent="torso", z=0, fill=True),
    "legB": dict(poly=[(0.53, 0.6), (0.7, 0.6), (0.76, 0.75), (0.78, 0.88), (0.82, 0.95), (0.79, 0.985), (0.6, 0.985),
                       (0.55, 0.92), (0.56, 0.8), (0.52, 0.7)], pivot=(0.6, 0.62), parent=H, z=1, fill=True),
    "armB": dict(poly=[(0.62, 0.15), (0.7, 0.16), (0.75, 0.28), (0.76, 0.42), (0.75, 0.51), (0.65, 0.52), (0.63, 0.4), (0.61, 0.3)],
                 pivot=(0.66, 0.2), parent="torso", z=2, fill=True),
    "legF": dict(poly=[(0.3, 0.6), (0.52, 0.6), (0.5, 0.72), (0.46, 0.8), (0.44, 0.92), (0.42, 1.0), (0.17, 1.0),
                       (0.2, 0.93), (0.27, 0.88), (0.28, 0.75), (0.3, 0.68)], pivot=(0.42, 0.62), parent=H, z=3, fill=True),
    "skirt": dict(poly=[(0.39, 0.39), (0.66, 0.39), (0.67, 0.55), (0.65, 0.73), (0.55, 0.74), (0.45, 0.73), (0.38, 0.6)],
                  pivot=(0.52, 0.41), parent=H, z=4, fill=True),
    "torso": dict(poly=[(0.37, 0.07), (0.62, 0.05), (0.7, 0.14), (0.69, 0.3), (0.67, 0.43), (0.38, 0.44), (0.35, 0.3), (0.36, 0.15)],
                  pivot=(0.52, 0.42), parent=H, z=5, fill=True),
    "head": dict(poly=[(0.44, 0.0), (0.67, 0.0), (0.67, 0.08), (0.63, 0.155), (0.52, 0.165), (0.44, 0.1)],
                 pivot=(0.56, 0.14), parent="torso", z=6),
    "armF": dict(poly=[(0.24, 0.17), (0.33, 0.12), (0.44, 0.13), (0.49, 0.2), (0.47, 0.3), (0.41, 0.36), (0.39, 0.45),
                       (0.38, 0.535), (0.3, 0.555), (0.235, 0.5), (0.235, 0.4), (0.215, 0.3)], pivot=(0.37, 0.2), parent="torso", z=7),
    "weapon": dict(poly=[(0.18, 0.44), (0.24, 0.435), (0.42, 0.505), (0.48, 0.512), (0.49, 0.528), (1.0, 0.958),
                         (1.0, 1.0), (0.93, 1.0), (0.395, 0.6), (0.355, 0.58), (0.185, 0.508)],
                   pivot=(0.31, 0.5), parent="armF", z=8),
}
COMMANDER_MOTION = hm(kind="commander", stride=24, arm_swing=8, armF_swing=0.5, bob=1.5, lean=3, walk_period=0.9, cape_walk=4,
                      attack=dict(length=0.8, wind=-118, wind_weapon=-55, strike=32, strike_weapon=-8, lean_back=7, lean_fwd=-11))

# Back view (NE): used while he walks up the screen. The cape hangs over everything on the camera side.
COMMANDER_BACK_PARTS = {
    H: joint((0.6, 0.62)),
    "legB": dict(poly=[(0.26, 0.7), (0.46, 0.7), (0.47, 0.85), (0.45, 0.99), (0.27, 0.99), (0.29, 0.86)], pivot=(0.37, 0.71), parent=H, z=1, fill=True),
    "legF": dict(poly=[(0.55, 0.6), (0.8, 0.6), (0.8, 0.8), (0.84, 0.93), (0.86, 1.0), (0.6, 1.0), (0.62, 0.85), (0.58, 0.72)], pivot=(0.68, 0.62), parent=H, z=2, fill=True),
    "torso": dict(poly=[(0.46, 0.13), (0.78, 0.14), (0.8, 0.4), (0.79, 0.62), (0.5, 0.64), (0.45, 0.4)], pivot=(0.62, 0.6), parent=H, z=3, fill=True),
    "head": dict(poly=[(0.47, 0.0), (0.72, 0.0), (0.72, 0.13), (0.62, 0.16), (0.5, 0.15)], pivot=(0.6, 0.14), parent="torso", z=4),
    "armF": dict(poly=[(0.62, 0.15), (0.8, 0.16), (0.86, 0.3), (0.86, 0.45), (0.82, 0.53), (0.72, 0.53), (0.68, 0.4), (0.64, 0.27)], pivot=(0.72, 0.2), parent="torso", z=5),
    "weapon": dict(poly=[(0.74, 0.46), (0.86, 0.44), (0.92, 0.6), (1.0, 0.93), (0.98, 1.0), (0.94, 1.0), (0.83, 0.7), (0.76, 0.55)], pivot=(0.8, 0.5), parent="armF", z=6),
    "cape": dict(poly=[(0.3, 0.08), (0.55, 0.1), (0.62, 0.3), (0.6, 0.55), (0.62, 0.75), (0.5, 0.85), (0.3, 0.9), (0.1, 0.94), (0.0, 0.85), (0.05, 0.6), (0.15, 0.4), (0.25, 0.2)],
                 pivot=(0.5, 0.14), parent="torso", z=7, fill=True),
}
COMMANDER_BACK_MOTION = hm(kind="commander", back=True, stride=24, arm_swing=8, bob=1.5, lean=0, walk_period=0.9, cape_walk=6)


def portrait_box(u0, v0, u1, v1, size=40):
    """Extra output: square HUD portrait cropped from the clean figure."""
    def post(rig, clips):
        x0, y0, x1, y1 = int(u0 * rig.W), int(v0 * rig.H), int(u1 * rig.W), int(v1 * rig.H)
        r, a = rig.rgb[y0:y1, x0:x1], rig.alpha[y0:y1, x0:x1]
        rr, aa, _ = area_downscale(r, a, (size, size), palette=rig.palette)
        import os
        from rigkit import ANIM_DIR
        os.makedirs(os.path.join(ANIM_DIR, rig.name), exist_ok=True)
        to_image(rr, aa).save(os.path.join(ANIM_DIR, rig.name, "portrait.png"))
        return {"portrait": "portrait.png"}
    return post


# ---------------------------------------------------------------- palette swaps of the commander (skins)
def _recolor(rules, protect=((0.47, 0.0, 0.66, 0.125),)):
    """protect: boxes (u0, v0, u1, v1) of the figure kept untouched (the face)."""
    def f(rgb, alpha):
        h, s, v = rgb_to_hsv(rgb)
        out = rgb.copy()
        keep = np.zeros(alpha.shape, bool)
        Hh, Ww = alpha.shape
        for u0, v0, u1, v1 in protect:
            keep[int(v0 * Hh):int(v1 * Hh), int(u0 * Ww):int(u1 * Ww)] = True
        cloth = ((h >= 335) | (h <= 14)) & (s > 0.5) & (v > 0.12) & ~keep
        steel = (s < 0.24) & (v > 0.08) & ~keep
        brass = (h > 22) & (h < 55) & (s > 0.45) & (v > 0.45) & ~keep
        for mask, (hh, ss, vk) in ((cloth, rules["cloth"]), (steel, rules["steel"]), (brass, rules.get("brass", (None, None, 1.0)))):
            m = mask & alpha
            if hh is None:
                continue
            nh = np.full_like(h, hh)
            ns = np.clip(s * 0 + ss + s * 0.3, 0, 1) if ss is not None else s
            nv = np.clip(v * vk, 0, 1)
            col = hsv_to_rgb(nh, ns, nv)
            out[m] = col[m]
        return out
    return f


SCARLET = _recolor({"cloth": (34, 0.1, 1.75), "steel": (352, 0.52, 1.18), "brass": (44, 0.6, 1.25)})    # crimson-lacquered plate, ash-white cloak
ECLIPSE = _recolor({"cloth": (275, 0.62, 0.8), "steel": (262, 0.2, 0.72), "brass": (285, 0.25, 1.25)})   # black-violet plate


# ---------------------------------------------------------------- Espadachim Negro (own design sheet)
SWORDSMAN_PARTS = {
    H: joint((0.47, 0.6)),
    "cape": dict(poly=[[(0.04, 0.78), (0.1, 0.55), (0.16, 0.4), (0.22, 0.28), (0.34, 0.22), (0.36, 0.45), (0.3, 0.62),
                        (0.24, 0.8), (0.12, 0.82)], [(0.62, 0.3), (0.72, 0.36), (0.76, 0.6), (0.7, 0.72), (0.64, 0.6)]],
                 pivot=(0.5, 0.22), parent="torso", z=0, fill=True),
    "weapon": dict(poly=[(0.19, 0.0), (0.25, 0.0), (0.33, 0.13), (0.62, 0.42), (0.86, 0.62), (1.0, 0.8), (0.98, 0.84),
                         (0.8, 0.78), (0.58, 0.55), (0.28, 0.24), (0.2, 0.12)],
                   pivot=(0.27, 0.14), parent="armF", z=1, fill=True),
    "legB": dict(poly=[(0.52, 0.58), (0.72, 0.6), (0.76, 0.75), (0.8, 0.86), (0.84, 0.93), (0.62, 0.94), (0.58, 0.8), (0.54, 0.7)],
                 pivot=(0.6, 0.62), parent=H, z=2, fill=True),
    "armB": dict(poly=[(0.6, 0.25), (0.72, 0.27), (0.8, 0.38), (0.88, 0.5), (0.86, 0.57), (0.76, 0.56), (0.68, 0.45), (0.6, 0.38)],
                 pivot=(0.64, 0.29), parent="torso", z=3, fill=True),
    "legF": dict(poly=[(0.24, 0.58), (0.46, 0.58), (0.44, 0.72), (0.36, 0.82), (0.3, 0.92), (0.28, 1.0), (0.13, 1.0),
                       (0.16, 0.88), (0.22, 0.75)], pivot=(0.36, 0.6), parent=H, z=4, fill=True),
    "skirt": dict(poly=[(0.33, 0.47), (0.62, 0.47), (0.64, 0.62), (0.6, 0.8), (0.4, 0.8), (0.32, 0.62)],
                  pivot=(0.48, 0.49), parent=H, z=5, fill=True),
    "torso": dict(poly=[(0.36, 0.17), (0.68, 0.17), (0.72, 0.3), (0.66, 0.5), (0.36, 0.52), (0.32, 0.32)],
                  pivot=(0.5, 0.5), parent=H, z=6, fill=True),
    "head": dict(poly=[(0.45, 0.06), (0.6, 0.05), (0.7, 0.14), (0.68, 0.26), (0.5, 0.28), (0.45, 0.18)],
                 pivot=(0.56, 0.25), parent="torso", z=7),
    "armF": dict(poly=[(0.2, 0.22), (0.36, 0.2), (0.42, 0.3), (0.38, 0.45), (0.36, 0.6), (0.27, 0.63), (0.2, 0.55), (0.17, 0.38)],
                 pivot=(0.3, 0.25), parent="torso", z=8),
}
SWORDSMAN_MOTION = hm(stride=22, arm_swing=10, armF_swing=0.3, bob=1.5, lean=3, walk_period=0.9, cape_walk=5,
                      attack=dict(length=0.85, wind=-40, wind_weapon=-35, strike=95, strike_weapon=35, lean_back=6, lean_fwd=-12),
                      special=dict(length=0.95, wind=-95, slam=125, t_wind=0.22, t_slam=0.35))

SWORDSMAN_BACK_PARTS = {
    H: joint((0.42, 0.62)),
    "legB": dict(poly=[(0.56, 0.66), (0.76, 0.66), (0.78, 0.85), (0.74, 0.98), (0.58, 0.98), (0.58, 0.85)], pivot=(0.66, 0.68), parent=H, z=0, fill=True),
    "legF": dict(poly=[(0.1, 0.6), (0.34, 0.6), (0.33, 0.75), (0.26, 0.9), (0.25, 1.0), (0.08, 1.0), (0.12, 0.85), (0.12, 0.72)], pivot=(0.22, 0.62), parent=H, z=1, fill=True),
    "armB": dict(poly=[(0.56, 0.26), (0.72, 0.28), (0.74, 0.44), (0.7, 0.56), (0.6, 0.56), (0.57, 0.42)], pivot=(0.62, 0.3), parent="torso", z=2, fill=True),
    "torso": dict(poly=[(0.24, 0.14), (0.6, 0.16), (0.64, 0.4), (0.6, 0.64), (0.26, 0.66), (0.2, 0.4)], pivot=(0.42, 0.62), parent=H, z=3, fill=True),
    "cape": dict(poly=[(0.44, 0.24), (0.62, 0.26), (0.72, 0.5), (0.86, 0.66), (1.0, 0.86), (0.85, 0.85), (0.7, 0.84), (0.55, 0.86), (0.4, 0.84), (0.36, 0.6)],
                 pivot=(0.52, 0.27), parent="torso", z=4, fill=True),
    "head": dict(poly=[(0.3, 0.06), (0.48, 0.06), (0.52, 0.18), (0.47, 0.28), (0.32, 0.28), (0.28, 0.18)], pivot=(0.4, 0.25), parent="torso", z=5),
    "armF": dict(poly=[(0.12, 0.17), (0.3, 0.17), (0.32, 0.3), (0.26, 0.45), (0.2, 0.58), (0.1, 0.6), (0.04, 0.56), (0.08, 0.4), (0.1, 0.28)], pivot=(0.22, 0.22), parent="torso", z=6),
    "weapon": dict(poly=[(0.62, 0.0), (0.7, 0.02), (0.66, 0.12), (0.5, 0.3), (0.38, 0.52), (0.32, 0.66), (0.27, 0.65), (0.3, 0.5), (0.44, 0.27), (0.58, 0.1)],
                   pivot=(0.5, 0.25), parent="torso", z=7),
}
SWORDSMAN_BACK_MOTION = hm(kind="commander", back=True, stride=22, arm_swing=10, bob=1.5, lean=0, walk_period=0.9, cape_walk=6)


# ---------------------------------------------------------------- map 1 enemies
SOLDIER_PARTS = {
    H: joint((0.44, 0.62)),
    "legB": dict(poly=[(0.44, 0.66), (0.66, 0.66), (0.7, 0.8), (0.69, 0.93), (0.71, 1.0), (0.42, 1.0), (0.46, 0.9), (0.45, 0.78)],
                 pivot=(0.55, 0.67), parent=H, z=1, fill=True),
    "legF": dict(poly=[(0.14, 0.62), (0.4, 0.62), (0.38, 0.72), (0.3, 0.82), (0.25, 0.92), (0.26, 1.0), (0.02, 1.0), (0.06, 0.9),
                       (0.12, 0.8), (0.12, 0.7)], pivot=(0.3, 0.64), parent=H, z=2, fill=True),
    "skirt": dict(poly=[(0.28, 0.57), (0.58, 0.57), (0.6, 0.72), (0.5, 0.8), (0.3, 0.78)], pivot=(0.44, 0.59), parent=H, z=3, fill=True),
    "torso": dict(poly=[(0.3, 0.18), (0.62, 0.18), (0.68, 0.3), (0.66, 0.6), (0.3, 0.62), (0.26, 0.4)],
                  pivot=(0.46, 0.6), parent=H, z=4, fill=True),
    "head": dict(poly=[(0.4, 0.02), (0.62, 0.02), (0.66, 0.0), (0.76, 0.0), (0.72, 0.15), (0.66, 0.31), (0.46, 0.32), (0.4, 0.2)],
                 pivot=(0.53, 0.27), parent="torso", z=5),
    "shield": dict(poly=[(0.55, 0.23), (0.85, 0.26), (1.0, 0.31), (0.96, 0.55), (0.8, 0.73), (0.68, 0.77), (0.6, 0.62), (0.56, 0.4)],
                   pivot=(0.68, 0.42), parent="torso", z=6),
    "armF": dict(poly=[(0.16, 0.15), (0.38, 0.15), (0.41, 0.3), (0.33, 0.45), (0.27, 0.6), (0.2, 0.65), (0.08, 0.63), (0.02, 0.56),
                       (0.08, 0.5), (0.1, 0.3)], pivot=(0.28, 0.21), parent="torso", z=7),
    "weapon": dict(poly=[(0.0, 0.53), (0.09, 0.52), (0.21, 0.575), (0.46, 0.655), (0.78, 0.765), (0.79, 0.825), (0.7, 0.835),
                         (0.44, 0.745), (0.2, 0.675), (0.07, 0.625), (0.0, 0.605)], pivot=(0.15, 0.59), parent="armF", z=8),
}
SOLDIER_MOTION = hm(cape=None, armB=None, shield="shield", stride=20, arm_swing=10, bob=1.5, lean=4, walk_period=0.95,
                    attack=dict(length=0.7, wind=-100, wind_weapon=-30, strike=28, strike_weapon=-5, lean_back=6, lean_fwd=-10))

BLACK_KNIGHT_PARTS = {
    H: joint((0.5, 0.7)),
    "cape": dict(poly=[[(0.04, 0.6), (0.22, 0.42), (0.3, 0.55), (0.3, 0.78), (0.2, 0.93), (0.04, 0.9), (0.0, 0.75)],
                       [(0.72, 0.42), (0.86, 0.5), (0.9, 0.62), (0.86, 0.82), (0.74, 0.78)]],
                 pivot=(0.5, 0.22), parent="torso", z=0, fill=True),
    "legB": dict(poly=[(0.52, 0.68), (0.72, 0.68), (0.74, 0.84), (0.77, 1.0), (0.53, 1.0), (0.54, 0.84)], pivot=(0.62, 0.7), parent=H, z=1, fill=True),
    "legF": dict(poly=[(0.27, 0.68), (0.49, 0.68), (0.49, 0.84), (0.47, 1.0), (0.22, 1.0), (0.26, 0.84)], pivot=(0.38, 0.7), parent=H, z=2, fill=True),
    "skirt": dict(poly=[(0.33, 0.5), (0.67, 0.5), (0.69, 0.74), (0.6, 0.86), (0.4, 0.86), (0.31, 0.74)], pivot=(0.5, 0.52), parent=H, z=3, fill=True),
    "torso": dict(poly=[[(0.22, 0.17), (0.78, 0.17), (0.81, 0.35), (0.73, 0.55), (0.27, 0.55), (0.19, 0.35)],
                        [(0.1, 0.0), (0.21, 0.0), (0.3, 0.2), (0.2, 0.23)]], pivot=(0.5, 0.53), parent=H, z=4, fill=True),
    "head": dict(poly=[(0.4, 0.07), (0.63, 0.07), (0.67, 0.2), (0.6, 0.275), (0.42, 0.275), (0.37, 0.18)], pivot=(0.51, 0.25), parent="torso", z=5),
    "armF": dict(poly=[(0.13, 0.21), (0.32, 0.21), (0.34, 0.4), (0.31, 0.55), (0.31, 0.64), (0.15, 0.64), (0.11, 0.45)],
                 pivot=(0.23, 0.25), parent="torso", z=6),
    "armB": dict(poly=[(0.67, 0.21), (0.87, 0.23), (0.89, 0.45), (0.85, 0.63), (0.7, 0.63), (0.67, 0.4)], pivot=(0.76, 0.26), parent="torso", z=7),
    "weapon": dict(poly=[(0.73, 0.54), (0.83, 0.54), (0.98, 0.8), (0.98, 0.91), (0.89, 0.89), (0.75, 0.66)], pivot=(0.78, 0.58), parent="armB", z=8),
}
BLACK_KNIGHT_MOTION = hm(stride=9, lift=2.5, arm_swing=5, bob=1.5, lean=1, walk_period=1.15, cape_walk=3,
                         attack=dict(length=0.9, arm="armB", wind=125, wind_weapon=30, strike=-25, strike_weapon=-15, lean_back=-3, lean_fwd=4, lunge=1.5))

HOODED_PARTS = {
    H: joint((0.5, 0.62)),
    "hem": dict(poly=[(0.08, 0.58), (0.92, 0.58), (1.0, 0.97), (0.86, 1.0), (0.14, 1.0), (0.0, 0.97)], pivot=(0.5, 0.6), parent=H, z=1, fill=True),
    "armB": dict(poly=[(0.7, 0.3), (0.88, 0.36), (0.95, 0.6), (0.9, 0.76), (0.74, 0.72), (0.68, 0.5)], pivot=(0.76, 0.34), parent="torso", z=2),
    "torso": dict(poly=[(0.3, 0.24), (0.8, 0.24), (0.85, 0.45), (0.8, 0.64), (0.25, 0.64), (0.22, 0.45)], pivot=(0.52, 0.62), parent=H, z=3, fill=True),
    "head": dict(poly=[(0.25, 0.0), (0.86, 0.0), (0.89, 0.2), (0.82, 0.35), (0.3, 0.36), (0.22, 0.2)], pivot=(0.55, 0.32), parent="torso", z=4),
    "armF": dict(poly=[(0.12, 0.3), (0.35, 0.3), (0.42, 0.5), (0.38, 0.7), (0.16, 0.76), (0.06, 0.55)], pivot=(0.28, 0.33), parent="torso", z=5),
}
HOODED_MOTION = dict(kind="robed", hips=H, torso="torso", head="head", armF="armF", armB="armB", hem="hem", weapon=None,
                     hem_swing=6, arm_swing=10, walk_period=0.85)


# ---------------------------------------------------------------- bosses
def boss_knight_source():
    """The boss art is chunky pixel art (~23 source px per art pixel): sample it on its own grid, then Scale2x."""
    rgb, alpha = figures_src.figure("boss_knight")
    pitch_x, pitch_y, ph_x, ph_y = 23.1, 23.2, 1.0, -2.2
    H_, W_ = alpha.shape
    nx, ny = int((W_ - ph_x) / pitch_x), int((H_ - ph_y) / pitch_y)
    out = np.zeros((ny, nx, 3), np.int16)
    oa = np.zeros((ny, nx), bool)
    for j in range(ny):
        for i in range(nx):
            cx, cy = int(ph_x + pitch_x * (i + 0.5)), int(ph_y + pitch_y * (j + 0.5))
            if not (0 <= cy < H_ and 0 <= cx < W_):
                continue
            ys, xs = slice(max(0, cy - 4), cy + 5), slice(max(0, cx - 4), cx + 5)
            win_a = alpha[ys, xs]
            oa[j, i] = win_a.mean() > 0.5
            if oa[j, i]:
                out[j, i] = np.median(rgb[ys, xs][win_a], axis=0)
    out, oa = trim(out, oa)
    return scale2x(out, oa)


BOSS_KNIGHT_PARTS = {
    H: joint((0.47, 0.63)),
    "weapon": dict(poly=[(0.0, 0.39), (0.15, 0.36), (0.45, 0.19), (0.6, 0.03), (0.8, 0.0), (1.0, 0.09), (1.0, 0.16), (0.8, 0.17),
                         (0.63, 0.22), (0.48, 0.31), (0.17, 0.51), (0.0, 0.52)], pivot=(0.22, 0.45), parent="armF", z=0, fill=True),
    "legB": dict(poly=[(0.52, 0.6), (0.74, 0.6), (0.78, 0.8), (0.76, 1.0), (0.6, 1.0), (0.56, 0.8)], pivot=(0.62, 0.62), parent=H, z=1, fill=True),
    "legF": dict(poly=[(0.22, 0.6), (0.46, 0.6), (0.45, 0.8), (0.42, 1.0), (0.2, 1.0), (0.24, 0.8)], pivot=(0.35, 0.62), parent=H, z=2, fill=True),
    "armB": dict(poly=[(0.62, 0.27), (0.84, 0.3), (0.9, 0.48), (0.86, 0.62), (0.7, 0.62), (0.63, 0.45)], pivot=(0.7, 0.31), parent="torso", z=3, fill=True),
    "skirt": dict(poly=[(0.34, 0.55), (0.62, 0.55), (0.64, 0.78), (0.55, 0.88), (0.4, 0.88), (0.32, 0.75)], pivot=(0.48, 0.57), parent=H, z=4, fill=True),
    "torso": dict(poly=[(0.3, 0.2), (0.66, 0.18), (0.74, 0.32), (0.7, 0.6), (0.3, 0.62), (0.26, 0.4)], pivot=(0.48, 0.6), parent=H, z=5, fill=True),
    "head": dict(poly=[(0.37, 0.15), (0.57, 0.14), (0.6, 0.3), (0.55, 0.39), (0.4, 0.39), (0.35, 0.28)], pivot=(0.47, 0.36), parent="torso", z=6),
    "armF": dict(poly=[(0.14, 0.27), (0.36, 0.24), (0.42, 0.35), (0.36, 0.5), (0.26, 0.57), (0.09, 0.53), (0.07, 0.4)],
                 pivot=(0.3, 0.3), parent="torso", z=7),
}
BOSS_KNIGHT_MOTION = hm(cape=None, skirt="skirt", stride=13, lift=2.5, arm_swing=4, bob=2.5, lean=2, walk_period=1.3, death_drop=18,
                        attack=dict(length=1.0, wind=18, wind_weapon=12, strike=-62, strike_weapon=-40, lean_back=6, lean_fwd=-12, lunge=4,
                                    t_wind=0.38, t_strike=0.5, t_hold=0.62),
                        special=dict(length=1.4, wind=30, slam=-75, t_wind=0.62, t_slam=0.78), intro=True)

SWAMP_GUARDIAN_PARTS = {
    H: joint((0.5, 0.63)),
    "weapon_back": dict(poly=[(0.84, 0.05), (0.9, 0.09), (0.62, 0.36), (0.42, 0.55), (0.36, 0.52), (0.58, 0.32)],
                        pivot=(0.31, 0.62), parent="weapon", z=0, fill=True),
    "legB": dict(poly=[(0.55, 0.62), (0.72, 0.62), (0.76, 0.78), (0.79, 0.9), (0.62, 0.91), (0.58, 0.78)], pivot=(0.62, 0.64), parent=H, z=1, fill=True),
    "legF": dict(poly=[(0.3, 0.62), (0.47, 0.62), (0.46, 0.78), (0.44, 0.95), (0.43, 1.0), (0.29, 1.0), (0.31, 0.85), (0.29, 0.72)],
                 pivot=(0.38, 0.64), parent=H, z=2, fill=True),
    "skirt": dict(poly=[(0.37, 0.5), (0.63, 0.5), (0.65, 0.7), (0.58, 0.83), (0.45, 0.83), (0.37, 0.7)], pivot=(0.5, 0.52), parent=H, z=3, fill=True),
    "torso": dict(poly=[(0.3, 0.16), (0.7, 0.14), (0.76, 0.3), (0.7, 0.55), (0.3, 0.56), (0.25, 0.35)], pivot=(0.5, 0.53), parent=H, z=4, fill=True),
    "head": dict(poly=[(0.41, 0.06), (0.57, 0.06), (0.59, 0.2), (0.55, 0.275), (0.44, 0.275), (0.4, 0.18)], pivot=(0.49, 0.25), parent="torso", z=5),
    "armB": dict(poly=[(0.62, 0.27), (0.78, 0.31), (0.9, 0.39), (1.0, 0.6), (1.0, 0.9), (0.85, 0.93), (0.75, 0.86), (0.68, 0.7), (0.62, 0.5)],
                 pivot=(0.68, 0.32), parent="torso", z=6),
    "armF": dict(poly=[(0.26, 0.24), (0.41, 0.24), (0.43, 0.4), (0.39, 0.55), (0.36, 0.66), (0.25, 0.67), (0.23, 0.5), (0.24, 0.35)],
                 pivot=(0.33, 0.28), parent="torso", z=7),
    "weapon": dict(poly=[(0.38, 0.55), (0.42, 0.58), (0.35, 0.67), (0.28, 0.76), (0.25, 0.9), (0.14, 0.94), (0.0, 0.9), (0.04, 0.8),
                         (0.1, 0.66), (0.2, 0.63), (0.3, 0.6)], pivot=(0.31, 0.62), parent="armF", z=8),
}
SWAMP_GUARDIAN_MOTION = hm(cape=None, stride=12, lift=2.5, arm_swing=5, bob=2.5, lean=2, walk_period=1.25, death_drop=16,
                           attack=dict(length=1.0, wind=-70, wind_weapon=-25, strike=40, strike_weapon=10, lean_back=6, lean_fwd=-10, lunge=3),
                           special=dict(length=1.4, arm="armB", wind=60, slam=-35, t_wind=0.6, t_slam=0.76), intro=True, cast=True)


# ---------------------------------------------------------------- map 2 enemies
def warlock_source():
    """Bruxo Corrompido: the hooded monk of the old sheet, corrupted (violet robe, sickly skin) and given a staff."""
    rgb, alpha = figures_src.figure("warlock_base")
    Hh, Ww = alpha.shape
    alpha = alpha.copy()
    alpha[:, : int(Ww * 0.12)] = False            # a neighbour's sword tip at the left edge of the crop
    rgb, alpha = trim(rgb, alpha)
    h, s, v = rgb_to_hsv(rgb)
    robe = (s < 0.35) & alpha
    skin = (h > 15) & (h < 45) & (s > 0.3) & alpha
    out = rgb.copy()
    out[robe] = hsv_to_rgb(np.full_like(h, 282), np.clip(s * 0 + 0.42, 0, 1), np.clip(v * 1.05, 0, 1))[robe]
    out[skin] = hsv_to_rgb(np.full_like(h, 95), np.clip(s * 0.6, 0, 1), np.clip(v * 0.9, 0, 1))[skin]
    # staff to the right: widen the canvas and paint it (dark wood, glowing crystal)
    Hh, Ww = alpha.shape
    pad = int(Ww * 0.35)
    big = np.zeros((Hh, Ww + pad, 3), np.int16)
    ba = np.zeros((Hh, Ww + pad), bool)
    big[:, :Ww] = out
    ba[:, :Ww] = alpha
    im = to_image(big, ba)
    d = ImageDraw.Draw(im)
    x = Ww + pad * 0.35
    px = max(2, int(Ww * 0.05))
    d.line([(x, Hh * 0.2), (x - Ww * 0.06, Hh * 0.995)], fill=(58, 40, 34, 255), width=px)
    d.line([(x + px * 0.3, Hh * 0.2), (x - Ww * 0.06 + px * 0.3, Hh * 0.995)], fill=(88, 62, 48, 255), width=max(1, px // 3))
    c = (x, Hh * 0.13)
    r = Ww * 0.09
    d.polygon([(c[0], c[1] - r * 1.4), (c[0] + r, c[1]), (c[0], c[1] + r * 1.2), (c[0] - r, c[1])], fill=(150, 255, 120, 255))
    d.polygon([(c[0], c[1] - r * 0.8), (c[0] + r * 0.5, c[1]), (c[0], c[1] + r * 0.6), (c[0] - r * 0.5, c[1])], fill=(230, 255, 200, 255))
    rgb2, a2 = from_image(im)
    return trim(rgb2, a2)


WARLOCK_PARTS = {
    H: joint((0.4, 0.62)),
    "hem": dict(poly=[(0.04, 0.58), (0.74, 0.58), (0.78, 0.97), (0.7, 1.0), (0.08, 1.0), (0.0, 0.96)], pivot=(0.38, 0.6), parent=H, z=1, fill=True),
    "armB": dict(poly=[(0.56, 0.3), (0.72, 0.34), (0.76, 0.6), (0.72, 0.75), (0.58, 0.72), (0.54, 0.5)], pivot=(0.6, 0.34), parent="torso", z=2),
    "torso": dict(poly=[(0.16, 0.24), (0.66, 0.24), (0.7, 0.45), (0.66, 0.64), (0.12, 0.64), (0.1, 0.45)], pivot=(0.4, 0.62), parent=H, z=3, fill=True),
    "head": dict(poly=[(0.12, 0.0), (0.66, 0.0), (0.7, 0.2), (0.64, 0.33), (0.18, 0.34), (0.1, 0.2)], pivot=(0.42, 0.3), parent="torso", z=4),
    "armF": dict(poly=[(0.02, 0.3), (0.24, 0.3), (0.3, 0.5), (0.26, 0.7), (0.06, 0.76), (0.0, 0.55)], pivot=(0.15, 0.33), parent="torso", z=5),
    "weapon": dict(poly=[(0.78, 0.0), (1.0, 0.0), (1.0, 0.3), (0.86, 1.0), (0.72, 1.0), (0.8, 0.3)], pivot=(0.84, 0.6), parent="armB", z=6),
}
WARLOCK_MOTION = dict(kind="robed", hips=H, torso="torso", head="head", armF="armF", armB="armB", hem="hem", weapon="weapon",
                      hem_swing=5, arm_swing=8, walk_period=0.9)

GHOUL_PARTS = {
    "body": dict(poly=[(0.17, 0.05), (0.5, 0.02), (0.76, 0.08), (0.82, 0.3), (0.8, 0.5), (0.62, 0.56), (0.35, 0.5), (0.2, 0.42)],
                 pivot=(0.5, 0.42), parent=None, z=3, fill=True),
    "tail": dict(poly=[(0.0, 0.28), (0.04, 0.22), (0.14, 0.14), (0.24, 0.16), (0.24, 0.3), (0.12, 0.4), (0.03, 0.43)],
                 pivot=(0.22, 0.2), parent="body", z=1),
    "legBF": dict(poly=[(0.3, 0.38), (0.42, 0.4), (0.44, 0.55), (0.42, 0.68), (0.33, 0.66), (0.3, 0.5)], pivot=(0.36, 0.42), parent="body", z=0, fill=True),
    "legFF": dict(poly=[(0.63, 0.42), (0.76, 0.42), (0.78, 0.6), (0.79, 0.8), (0.76, 0.88), (0.7, 0.86), (0.67, 0.7), (0.64, 0.55)],
                  pivot=(0.7, 0.44), parent="body", z=2, fill=True),
    "legBN": dict(poly=[(0.12, 0.35), (0.3, 0.32), (0.3, 0.5), (0.22, 0.62), (0.18, 0.8), (0.08, 0.8), (0.1, 0.55)],
                  pivot=(0.24, 0.36), parent="body", z=4),
    "legFN": dict(poly=[(0.5, 0.42), (0.63, 0.42), (0.64, 0.6), (0.61, 0.8), (0.64, 1.0), (0.52, 1.0), (0.5, 0.75), (0.49, 0.55)],
                  pivot=(0.56, 0.44), parent="body", z=5),
    "head": dict(poly=[(0.72, 0.02), (0.88, 0.0), (0.98, 0.22), (1.0, 0.33), (0.9, 0.32), (0.82, 0.36), (0.74, 0.4), (0.7, 0.2)],
                 pivot=(0.77, 0.3), parent="body", z=6),
    "jaw": dict(poly=[(0.82, 0.32), (0.98, 0.33), (0.97, 0.43), (0.9, 0.5), (0.84, 0.46)], pivot=(0.84, 0.32), parent="head", z=7),
}
GHOUL_MOTION = dict(kind="quadruped", body="body", head="head", jaw="jaw", tail="tail", legFN="legFN", legFF="legFF",
                    legBN="legBN", legBF="legBF", stride=22, walk_period=0.62, bob=1.5, pitch=2.5)


# ---------------------------------------------------------------- Cão Infernal: real run frames + a rig of frame 0
_wolf_cache = {}


def wolf_frames_aligned(height=34):
    if "f" in _wolf_cache:
        return _wolf_cache["f"]
    frames = figures_src.wolf_frames()
    noses = []
    for rgb, a, off in frames:
        top = a[: int(a.shape[0] * 0.6)]
        noses.append(int(np.nonzero(top.any(axis=0))[0].max()))
    _wolf_cache["f"] = (frames, noses, height / frames[0][1].shape[0])
    return _wolf_cache["f"]


def wolf_source():
    frames, noses, s = wolf_frames_aligned()
    rgb, a, _ = frames[0]
    return rgb, a


def wolf_flipbook(rig, clips):
    """Run = the six delivered frames, nose-aligned to frame 0, feet on the ground, same palette as the rig."""
    import os
    from rigkit import ANIM_DIR
    frames, noses, s = wolf_frames_aligned()
    d = os.path.join(ANIM_DIR, rig.name, "run")
    os.makedirs(d, exist_ok=True)
    fx, fy = rig.px(rig.feet)
    imgs = []
    for i, (rgb, a, off) in enumerate(frames):
        w, h = max(1, round(a.shape[1] * s)), max(1, round(a.shape[0] * s))
        rr, aa, _ = area_downscale(rgb, a, (w, h), palette=rig.palette, coverage=0.45)
        dx = round((noses[0] - noses[i]) * s)
        imgs.append((to_image(rr, aa), dx))
    # one canvas for all frames, root at (fx, ground)
    left = min(dx for _, dx in imgs)
    right = max(dx + im.width for im, dx in imgs)
    top = max(im.height for im, _ in imgs)
    cw, ch = right - left, top
    names = []
    for i, (im, dx) in enumerate(imgs):
        c = Image.new("RGBA", (cw, ch))
        c.paste(im, (dx - left, ch - im.height), im)
        c.save(os.path.join(d, f"run_{i}.png"))
        names.append(f"run/run_{i}.png")
    clips["Run"] = {"length": 0.5, "loop": True, "curves": {}, "flipbook": {"part": "run", "fps": 12}}
    clips["Walk"] = clips["Run"]
    return {"flipbook": {"name": "run", "frames": names, "size": [cw, ch], "origin": [round(fx - left, 2), ch]}}


WOLF_PARTS = {
    "body": dict(poly=[(0.25, 0.14), (0.72, 0.05), (0.78, 0.3), (0.76, 0.55), (0.6, 0.62), (0.35, 0.6), (0.25, 0.45)],
                 pivot=(0.5, 0.45), parent=None, z=3, fill=True),
    "tail": dict(poly=[(0.0, 0.08), (0.12, 0.12), (0.3, 0.3), (0.32, 0.45), (0.2, 0.52), (0.05, 0.5), (0.0, 0.35)],
                 pivot=(0.3, 0.38), parent="body", z=1),
    "legBF": dict(poly=[(0.36, 0.55), (0.46, 0.55), (0.48, 0.7), (0.52, 0.88), (0.55, 0.97), (0.44, 0.97), (0.4, 0.82), (0.36, 0.68)],
                  pivot=(0.42, 0.57), parent="body", z=0, fill=True),
    "legFF": dict(poly=[(0.68, 0.5), (0.8, 0.5), (0.85, 0.7), (0.92, 0.85), (1.0, 0.92), (1.0, 1.0), (0.84, 1.0), (0.8, 0.85), (0.72, 0.7)],
                  pivot=(0.74, 0.53), parent="body", z=2, fill=True),
    "legBN": dict(poly=[(0.12, 0.55), (0.32, 0.5), (0.36, 0.6), (0.25, 0.75), (0.2, 0.9), (0.2, 1.0), (0.08, 1.0), (0.1, 0.85), (0.15, 0.7)],
                  pivot=(0.28, 0.55), parent="body", z=4),
    "legFN": dict(poly=[(0.56, 0.55), (0.66, 0.55), (0.64, 0.75), (0.63, 0.9), (0.65, 1.0), (0.55, 1.0), (0.56, 0.85), (0.55, 0.7)],
                  pivot=(0.61, 0.56), parent="body", z=5),
    "head": dict(poly=[(0.7, 0.0), (0.85, 0.02), (0.92, 0.25), (1.0, 0.35), (1.0, 0.5), (0.88, 0.58), (0.74, 0.55), (0.7, 0.35)],
                 pivot=(0.74, 0.35), parent="body", z=6),
}
WOLF_MOTION = dict(kind="quadruped", body="body", head="head", jaw=None, tail="tail", legFN="legFN", legFF="legFF",
                   legBN="legBN", legBF="legBF", stride=20, walk_period=0.45)


# ---------------------------------------------------------------- Aberração do Lodo: shaped from the swamp slime texture
def magenta_free(rgb):
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    return ~((r > 150) & (b > 150) & (g < 110))


def slime_source():
    """Aberração do Lodo: a living blob of the swamp sheet's slime pool. Its colours come from that pool
    (palette taken from the delivered art); the body shape and eyes are drawn here (no creature art was delivered)."""
    rgb = load_rgb(sources.path("map2_tiles"))
    alpha = key_magenta(rgb, glow=False)
    x0, y0, x1, y1 = 1470, 860, 2080, 1150
    tex, ta = rgb[y0:y1, x0:x1], alpha[y0:y1, x0:x1] & magenta_free(rgb[y0:y1, x0:x1])
    h, sat, v = rgb_to_hsv(tex)
    greens = tex[ta & (h > 60) & (h < 150) & (sat > 0.3)]
    purples = tex[ta & (h > 270) & (h < 320) & (sat > 0.3) & (v > 0.3)]
    order = np.argsort(greens.mean(axis=1))
    ramp = greens[order][np.linspace(0, len(order) - 1, 6).astype(int)]
    W, H = 150, 130
    yy, xx = np.mgrid[0:H, 0:W]
    rng = np.random.default_rng(5)
    noise = np.zeros((H, W))
    for sc, amp in ((22, 1.0), (11, 0.5), (6, 0.25)):
        g = rng.random((H // sc + 2, W // sc + 2))
        up = np.asarray(Image.fromarray((g * 255).astype(np.uint8)).resize((W + sc * 2, H + sc * 2), Image.BICUBIC))[:H, :W] / 255.0
        noise += up * amp
    noise /= 1.75
    shade = 1.05 - (yy / H) * 0.7 + (xx < W * 0.4) * 0.12          # light from top-left
    lum = np.clip(noise * 0.55 + shade * 0.6 - 0.15, 0, 0.999)
    out = ramp[(lum * len(ramp)).astype(int)].astype(np.int16)
    cx, base = W / 2, H * 0.9
    dome = (((xx - cx) / (W * 0.4)) ** 2 + ((yy - base) / (H * 0.85)) ** 2 <= 1) & (yy <= base)
    foot = ((xx - cx) / (W * 0.5)) ** 2 + ((yy - base) / (H * 0.1)) ** 2 <= 1
    bumps = np.zeros_like(dome)
    for bx, by, br in ((0.32, 0.38, 0.09), (0.62, 0.3, 0.1), (0.48, 0.18, 0.08), (0.75, 0.55, 0.07)):
        bumps |= ((xx - bx * W) ** 2 + (yy - by * H) ** 2) <= (br * W) ** 2
    shape = dome | foot | (bumps & (yy > H * 0.08))
    im = to_image(out, shape)
    d = ImageDraw.Draw(im)
    pc = tuple(int(c) for c in (purples.mean(axis=0) if len(purples) else (150, 60, 170)))
    for bx, by, br in ((0.3, 0.62, 0.035), (0.66, 0.7, 0.03), (0.72, 0.4, 0.025), (0.25, 0.45, 0.02)):
        d.ellipse([bx * W - br * W, by * H - br * W, bx * W + br * W, by * H + br * W], fill=pc + (255,))
    for ex, ey, er in ((0.42, 0.42, 0.045), (0.58, 0.4, 0.05), (0.5, 0.3, 0.035)):
        d.ellipse([ex * W - er * W, ey * H - er * W, ex * W + er * W, ey * H + er * W], fill=(250, 220, 90, 255))
        d.ellipse([ex * W - er * W * 0.35, ey * H - er * W * 0.6, ex * W + er * W * 0.35, ey * H + er * W * 0.6], fill=(20, 10, 18, 255))
    d.arc([W * 0.36, H * 0.52, W * 0.64, H * 0.66], 20, 160, fill=(30, 12, 26, 255), width=3)
    return from_image(im)


SLIME_PARTS = {
    "drip": dict(poly=[(0.0, 0.84), (1.0, 0.84), (1.0, 1.0), (0.0, 1.0)], pivot=(0.5, 0.95), parent=None, z=0),
    "body": dict(poly=[(0.08, 0.86), (0.08, 0.5), (0.2, 0.2), (0.4, 0.04), (0.6, 0.04), (0.82, 0.2), (0.92, 0.5), (0.92, 0.86)],
                 pivot=(0.5, 0.9), parent=None, z=1, fill=True),
    "eyes": dict(poly=[(0.32, 0.22), (0.68, 0.22), (0.68, 0.68), (0.32, 0.68)], pivot=(0.5, 0.45), parent="body", z=2),
}
SLIME_MOTION = dict(kind="slime", body="body", eyes="eyes", drip="drip", walk_period=0.7)


CHARACTERS = {
    "Commander": dict(figure="commander", height=74, feet=(0.48, 1.0), parts=COMMANDER_PARTS, motion=COMMANDER_MOTION,
                      splash=dict(pitch=4.02, portrait=(0.38, 0.0, 0.72, 0.27))),
    "CommanderScarlet": dict(figure="commander", height=74, feet=(0.48, 1.0), parts=COMMANDER_PARTS, motion=COMMANDER_MOTION,
                             recolor=SCARLET, splash=dict(pitch=4.02, portrait=(0.38, 0.0, 0.72, 0.27))),
    "CommanderEclipse": dict(figure="commander", height=74, feet=(0.48, 1.0), parts=COMMANDER_PARTS, motion=COMMANDER_MOTION,
                             recolor=ECLIPSE, splash=dict(pitch=4.02, portrait=(0.38, 0.0, 0.72, 0.27))),
    "BlackSwordsman": dict(figure="swordsman", height=76, feet=(0.42, 1.0), parts=SWORDSMAN_PARTS, motion=SWORDSMAN_MOTION,
                           splash=dict(height=196, portrait=(0.4, 0.04, 0.72, 0.3))),
    "CommanderBack": dict(figure="commander_back", height=74, feet=(0.6, 1.0), parts=COMMANDER_BACK_PARTS, motion=COMMANDER_BACK_MOTION),
    "CommanderScarletBack": dict(figure="commander_back", height=74, feet=(0.6, 1.0), parts=COMMANDER_BACK_PARTS, motion=COMMANDER_BACK_MOTION, recolor=SCARLET),
    "CommanderEclipseBack": dict(figure="commander_back", height=74, feet=(0.6, 1.0), parts=COMMANDER_BACK_PARTS, motion=COMMANDER_BACK_MOTION, recolor=ECLIPSE),
    "BlackSwordsmanBack": dict(figure="swordsman_back", height=76, feet=(0.42, 1.0), parts=SWORDSMAN_BACK_PARTS, motion=SWORDSMAN_BACK_MOTION),
    "CursedSoldier": dict(figure="soldier", height=62, feet=(0.4, 1.0), parts=SOLDIER_PARTS, motion=SOLDIER_MOTION),
    "BlackKnight": dict(figure="black_knight", height=92, feet=(0.5, 1.0), parts=BLACK_KNIGHT_PARTS, motion=BLACK_KNIGHT_MOTION),
    "Hooded": dict(figure="hooded", height=60, feet=(0.5, 1.0), parts=HOODED_PARTS, motion=HOODED_MOTION, palette=28),
    "CursedKnight": dict(source=boss_knight_source, height=134, feet=(0.47, 1.0), parts=BOSS_KNIGHT_PARTS, motion=BOSS_KNIGHT_MOTION, palette=36),
    "SwampGuardian": dict(figure="swamp_guardian", height=150, feet=(0.5, 1.0), parts=SWAMP_GUARDIAN_PARTS, motion=SWAMP_GUARDIAN_MOTION),
    "CorruptWarlock": dict(source=warlock_source, height=64, feet=(0.4, 1.0), parts=WARLOCK_PARTS, motion=WARLOCK_MOTION, palette=30),
    "Ghoul": dict(figure="ghoul", height=46, feet=(0.5, 0.97), parts=GHOUL_PARTS, motion=GHOUL_MOTION),
    "HellHound": dict(source=wolf_source, height=34, feet=(0.5, 1.0), parts=WOLF_PARTS, motion=WOLF_MOTION, post=wolf_flipbook),
    "SlimeAberration": dict(source=slime_source, height=44, feet=(0.5, 0.97), parts=SLIME_PARTS, motion=SLIME_MOTION, palette=24),
}

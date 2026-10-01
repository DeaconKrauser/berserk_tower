"""Pixel-art UI kit in the style of the delivered mockup (dark plates, bronze/gold trims).

    python tools/build_ui.py

Every sprite is drawn at 1 art pixel and shown at 2 screen pixels (UIFactory sets pixelsPerUnitMultiplier),
so the HUD shares the pixel grid of the 960x540 world. File names carry the 9-slice border: *_b<N>.png
(read by Assets/Editor/PixelArtImporter.cs).
"""
import os

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "UI", "Frames")

VOID = (11, 10, 13, 255)
FILL = (23, 20, 26, 238)
FILL_TOP = (31, 27, 34, 238)
BRZ_D = (78, 58, 33, 255)
BRZ = (138, 106, 58, 255)
BRZ_L = (201, 163, 94, 255)
GOLD = (232, 199, 126, 255)
EMBER = (242, 163, 58, 255)
EMBER_D = (150, 88, 30, 255)
STEEL = (74, 72, 82, 255)
STEEL_D = (46, 44, 54, 255)
BLOOD = (139, 26, 26, 255)


def canvas(w, h):
    return np.zeros((h, w, 4), np.uint8)


def save(a, name):
    os.makedirs(OUT, exist_ok=True)
    Image.fromarray(a, "RGBA").save(os.path.join(OUT, name))


def vgrad(a, x0, y0, x1, y1, top, bottom):
    for y in range(y0, y1):
        t = (y - y0) / max(1, y1 - y0 - 1)
        a[y, x0:x1] = [round(top[i] + (bottom[i] - top[i]) * t) for i in range(4)]


def rect_border(a, inset, color_tl, color_br=None):
    h, w = a.shape[:2]
    color_br = color_br or color_tl
    a[inset, inset:w - inset] = color_tl
    a[inset:h - inset, inset] = color_tl
    a[h - 1 - inset, inset:w - inset] = color_br
    a[inset:h - inset, w - 1 - inset] = color_br


def stud(a, cx, cy, c=GOLD, d=BRZ_D):
    for dx, dy in ((0, -1), (-1, 0), (1, 0), (0, 1)):
        a[cy + dy, cx + dx] = d
    a[cy, cx] = c


def panel(w=28, h=28, fill=FILL, fill_top=FILL_TOP, trim=(BRZ_L, BRZ), studs=True, name=None):
    a = canvas(w, h)
    vgrad(a, 0, 0, w, h, fill_top, fill)
    rect_border(a, 0, VOID)
    rect_border(a, 1, BRZ_D)
    rect_border(a, 2, trim[0], trim[1])
    rect_border(a, 3, BRZ_D)
    rect_border(a, 4, (8, 7, 10, 200))
    a[0, 0] = a[0, -1] = a[-1, 0] = a[-1, -1] = (0, 0, 0, 0)
    if studs:
        for cx, cy in ((2, 2), (w - 3, 2), (2, h - 3), (w - 3, h - 3)):
            stud(a, cx, cy)
    return a


def plate(w=40, h=18, fill_top=(44, 38, 44, 245), fill=(24, 21, 26, 245), trim=BRZ_L, point=6):
    """Title banner with pointed ends."""
    a = canvas(w, h)
    vgrad(a, 0, 0, w, h, fill_top, fill)
    mid = (h - 1) / 2
    for y in range(h):
        off = round(abs(y - mid) / mid * point)
        a[y, :off] = 0
        a[y, w - off:] = 0
        a[y, off] = VOID
        a[y, w - 1 - off] = VOID
        if 0 < y < h - 1:
            a[y, off + 1] = trim
            a[y, w - 2 - off] = trim
    a[0, point:w - point] = VOID
    a[h - 1, point:w - point] = VOID
    a[1, point:w - point] = trim
    a[h - 2, point:w - point] = BRZ
    a[2, point + 1:w - point - 1] = BRZ_D
    return a


def button(w=24, h=16, state="normal"):
    fills = {"normal": ((46, 40, 46, 255), (27, 23, 29, 255)), "hover": ((66, 56, 54, 255), (36, 30, 33, 255)),
             "pressed": ((22, 19, 24, 255), (34, 29, 34, 255)), "disabled": ((32, 31, 36, 255), (24, 23, 27, 255))}
    trims = {"normal": (BRZ_L, BRZ), "hover": (GOLD, BRZ_L), "pressed": (BRZ, BRZ_D), "disabled": (STEEL, STEEL_D)}
    a = canvas(w, h)
    vgrad(a, 0, 0, w, h, *fills[state])
    rect_border(a, 0, VOID)
    rect_border(a, 1, *trims[state])
    rect_border(a, 2, (8, 7, 10, 160))
    a[0, 0] = a[0, -1] = a[-1, 0] = a[-1, -1] = (0, 0, 0, 0)
    if state == "hover":
        a[3, 3:w - 3] = (90, 76, 66, 255)
    return a


def card(w=24, h=28, selected=False, disabled=False):
    a = canvas(w, h)
    vgrad(a, 0, 0, w, h, (36, 31, 38, 245), (17, 15, 19, 245))
    rect_border(a, 0, VOID)
    if selected:
        rect_border(a, 1, EMBER)
        rect_border(a, 2, EMBER_D)
    elif disabled:
        rect_border(a, 1, STEEL_D)
        rect_border(a, 2, (20, 19, 23, 255))
    else:
        rect_border(a, 1, BRZ, BRZ_D)
        rect_border(a, 2, (10, 9, 12, 255))
    a[0, 0] = a[0, -1] = a[-1, 0] = a[-1, -1] = (0, 0, 0, 0)
    return a


def round_button(d=22, state="normal"):
    a = canvas(d, d)
    c = (d - 1) / 2
    yy, xx = np.mgrid[0:d, 0:d]
    r = np.sqrt((xx - c) ** 2 + (yy - c) ** 2)
    ring = {"normal": BRZ_L, "hover": GOLD, "pressed": BRZ, "active": EMBER}[state]
    fill_t, fill_b = ((70, 58, 52, 255), (30, 25, 30, 255)) if state != "pressed" else ((24, 20, 25, 255), (40, 34, 38, 255))
    for y in range(d):
        t = y / (d - 1)
        col = [round(fill_t[i] + (fill_b[i] - fill_t[i]) * t) for i in range(4)]
        a[y][r[y] <= c - 2] = col
    a[(r > c - 2) & (r <= c - 1)] = ring
    a[(r > c - 1) & (r <= c)] = VOID
    a[(r > c - 3) & (r <= c - 2) & (yy > c)] = BRZ_D
    return a


def bar_frame(w=16, h=8):
    a = canvas(w, h)
    a[:, :] = (6, 5, 8, 235)
    rect_border(a, 0, VOID)
    rect_border(a, 1, BRZ_D)
    a[0, 0] = a[0, -1] = a[-1, 0] = a[-1, -1] = (0, 0, 0, 0)
    return a


def divider(w=64, h=5):
    a = canvas(w, h)
    a[2, 4:w - 4] = BRZ
    a[1, 8:w - 8] = BRZ_D
    a[3, 8:w - 8] = BRZ_D
    for cx in (w // 2,):
        a[0:5, cx] = GOLD
        a[2, cx - 2:cx + 3] = GOLD
    return a


def vignette(w=480, h=270, strength=0.85):
    """Dithered radial darkening, shown at 4x with point filtering."""
    yy, xx = np.mgrid[0:h, 0:w]
    dx, dy = (xx - w / 2) / (w / 2), (yy - h / 2) / (h / 2)
    r = np.clip(np.sqrt(dx * dx * 0.8 + dy * dy) - 0.35, 0, 1) / 0.75
    alpha = np.clip(r, 0, 1) ** 1.6 * strength
    bayer = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16 - 0.5
    alpha = np.clip(alpha + bayer[yy % 4, xx % 4] * 0.06, 0, 1)
    a = canvas(w, h)
    a[..., :3] = (6, 5, 8)
    a[..., 3] = (alpha * 255).astype(np.uint8)
    return a


def main():
    save(panel(), "panel_b6.png")
    save(panel(fill=(14, 12, 16, 250), fill_top=(20, 17, 22, 250)), "panel_dark_b6.png")
    save(panel(fill=(22, 19, 24, 255), fill_top=(30, 26, 32, 255)), "panel_solid_b6.png")
    save(panel(w=20, h=20, studs=False, trim=(BRZ, BRZ_D)), "panel_thin_b5.png")
    save(plate(), "plate_b8.png")
    for st in ("normal", "hover", "pressed", "disabled"):
        save(button(state=st), f"button_{st}_b4.png")
    save(card(), "card_b4.png")
    save(card(selected=True), "card_selected_b4.png")
    save(card(disabled=True), "card_disabled_b4.png")
    for st in ("normal", "hover", "pressed", "active"):
        save(round_button(state=st), f"round_{st}.png")
    save(bar_frame(), "bar_frame_b3.png")
    save(divider(), "divider.png")
    save(vignette(), "vignette.png")
    print("ui kit ok")


if __name__ == "__main__":
    main()

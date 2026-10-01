"""Builds every static sprite of the game from the originals in Assets/Art/Source.

    python tools/build_assets.py              # everything
    python tools/build_assets.py towers tiles # only some groups
    python tools/build_assets.py --debug      # also writes contact sheets to Assets/Art/Generated/_debug

Outputs (PNG, true alpha, palette-snapped pixel art, never bilinear/bicubic):
    Assets/Art/Generated/<group>/...      cleaned full-resolution cutouts (traceability, not used by the game)
    Assets/Art/Towers/<tower>_t<1-4>.png  Assets/Art/Tiles/<map>/...  Assets/Art/Sprites/Props/<map>/...
    Assets/Art/UI/Icons/...  Assets/Art/UI/Backgrounds/...  Assets/Art/Sprites/Projectiles/...
Unity import settings (PPU 32, Point, no compression, pivots) come from Assets/Editor/PixelArtImporter.cs.
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

import sources
from pixelkit import (VOID, add_outline, area_downscale, bbox, figures, from_image, key_checker, key_flat,
                      key_magenta, kmeans_palette, load_rgb, nn_upscale, remove_specks, scale_to_height, to_image, trim)

ROOT = sources.ROOT
ART = os.path.join(ROOT, "Assets", "Art")
GEN = os.path.join(ART, "Generated")
DEBUG = "--debug" in sys.argv
_sheets = {}


def out(*parts):
    p = os.path.join(ART, *parts)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    return p


def save(im, *parts):
    im.save(out(*parts))
    return im


def save_generated(rgb, alpha, group, name):
    """Full-resolution cleaned cutout, kept for traceability (thumbnailed to 1024 px to keep the repo light)."""
    im = to_image(rgb, alpha)
    im.thumbnail((1024, 1024), Image.NEAREST)
    p = os.path.join(GEN, group, name + ".png")
    os.makedirs(os.path.dirname(p), exist_ok=True)
    im.save(p)


def sheet(group, im):
    _sheets.setdefault(group, []).append(im)


def write_debug():
    if not DEBUG:
        return
    for group, ims in _sheets.items():
        ims = [nn_upscale(i, 3) for i in ims]
        W = min(2400, sum(i.width + 8 for i in ims) + 8)
        rows, x, y, h = [], 8, 8, 0
        place = []
        for im in ims:
            if x + im.width > W:
                x, y, h = 8, y + h + 8, 0
            place.append((im, x, y))
            x += im.width + 8
            h = max(h, im.height)
        canvas = Image.new("RGBA", (W, y + h + 8), (58, 52, 50, 255))
        for im, px, py in place:
            canvas.paste(im, (px, py), im)
        p = os.path.join(GEN, "_debug", group + ".png")
        os.makedirs(os.path.dirname(p), exist_ok=True)
        canvas.save(p)


def finish(rgb, alpha, outline=True, specks=6):
    alpha = remove_specks(alpha, specks)
    if outline:
        rgb, alpha = add_outline(rgb, alpha)
    rgb, alpha = trim(rgb, alpha)
    return to_image(rgb, alpha)


def cut(rgb, alpha, box):
    x0, y0, x1, y1 = box
    r, a = rgb[y0:y1, x0:x1], alpha[y0:y1, x0:x1]
    return trim(r, a)


def scaled(rgb, alpha, scale, k=32, outline=True):
    H, W = alpha.shape
    size = (max(1, round(W * scale)), max(1, round(H * scale)))
    r, a, _ = area_downscale(rgb, alpha, size, k=k)
    return finish(r, a, outline)


def polygon_mask(shape, pts):
    m = Image.new("L", (shape[1], shape[0]), 0)
    ImageDraw.Draw(m).polygon([tuple(p) for p in pts], fill=255)
    return np.asarray(m) > 0


# ---------------------------------------------------------------- towers

TOWER_SCALE = 0.07
TOWERS = {
    # key: (scale, key options)
    "archer": (TOWER_SCALE, dict(glow=True)),
    "ballista": (TOWER_SCALE, dict(glow=True)),
    "pyre": (TOWER_SCALE, dict(glow=True, glow_green=12, glow_blue=0.75)),   # flames are magenta-ish: strict glow test
    "chapel": (0.095, dict(glow=True)),
    "crow": (TOWER_SCALE, dict(glow=True)),
    "obelisk": (TOWER_SCALE, dict(glow=True)),
    "spikes": (TOWER_SCALE, dict(glow=True)),
}


def ballista_panels(rgb, opts):
    """The ballista sheet is four framed panels: key each panel on its own (the black lines block the flood fill)."""
    alpha = np.zeros(rgb.shape[:2], bool)
    cuts = [0, 685, 1373, 2061, rgb.shape[1]]
    boxes = []
    for i in range(4):
        x0, x1 = cuts[i] + 9, cuts[i + 1] - 3
        sub = key_magenta(rgb[6:-6, x0:x1], **opts)
        alpha[6:-6, x0:x1] = sub
        figs = figures(sub, merge_gap=30)
        fx0, fy0, fx1, fy1 = max(figs, key=lambda b: (b[2] - b[0]) * (b[3] - b[1]))
        boxes.append((x0 + fx0, 6 + fy0, x0 + fx1, 6 + fy1))
    return alpha, boxes


def tower_figures(key, rgb, alpha):
    for gap in (30, 16, 8, 4):
        figs = [f for f in figures(alpha, merge_gap=gap) if (f[3] - f[1]) > 200]
        if len(figs) >= 4:
            break
    return sorted(figs, key=lambda b: b[0])[:4]


def build_towers():
    for key, (scale, opts) in TOWERS.items():
        rgb = load_rgb(sources.path(key))
        if key == "ballista":
            alpha, boxes = ballista_panels(rgb, opts)
        else:
            alpha = key_magenta(rgb, **opts)
            boxes = tower_figures(key, rgb, alpha)
        assert len(boxes) == 4, (key, boxes)
        for tier, box in enumerate(boxes, 1):
            r, a = cut(rgb, alpha, box)
            save_generated(r, a, "Towers", f"{key}_t{tier}")
            im = save(scaled(r, a, scale), "Towers", f"{key}_t{tier}.png")
            sheet("towers", im)
            print(f"tower {key} t{tier}: {im.size}")


# ---------------------------------------------------------------- tiles

def tile_grid(rgb, x0, y0, x1, y1, cols, rows, inset=0.0):
    w, h = (x1 - x0) / cols, (y1 - y0) / rows
    for j in range(rows):
        for i in range(cols):
            cx0, cy0 = x0 + i * w, y0 + j * h
            yield (round(cx0 + inset * w), round(cy0 + inset * h), round(cx0 + w - inset * w), round(cy0 + h - inset * h))


def tile_image(rgb, box, size=32, palette=None, k=24, inset=4):
    x0, y0, x1, y1 = box
    x0, y0, x1, y1 = x0 + inset, y0 + inset, x1 - inset, y1 - inset
    r = rgb[y0:y1, x0:x1]
    a = np.ones(r.shape[:2], bool)
    rr, aa, pal = area_downscale(r, a, (size, size), k=k, palette=palette)
    return to_image(rr, np.ones_like(aa)), pal


def build_tiles():
    # ---- map 1: cobble road 3x3, dark dirt 3x3, crumbling cobble from the transition strip
    rgb = load_rgb(sources.path("map1_tiles"))
    cob_pal = kmeans_palette(rgb[40:700, 413:1099], np.ones((660, 686), bool), 20)
    for n, box in enumerate(tile_grid(rgb, 413, 40, 1099, 700, 3, 3)):
        im, _ = tile_image(rgb, box, palette=cob_pal)
        sheet("tiles", save(im, "Tiles", "Map1", f"cobble_{n}.png"))
    # dirt block: find its left edge on a row the transition strip does not touch
    row = rgb[120]
    mag = (row[:, 0] > 200) & (row[:, 2] > 200) & (row[:, 1] < 90)
    dx0 = next(x for x in range(1200, rgb.shape[1]) if not mag[x])
    dx1 = next(x for x in range(dx0 + 10, rgb.shape[1]) if mag[x])
    dirt_pal = kmeans_palette(rgb[40:705, dx0:dx1], np.ones((665, dx1 - dx0), bool), 20)
    for n, box in enumerate(tile_grid(rgb, dx0, 40, dx1, 705, 3, 3)):
        im, _ = tile_image(rgb, box, palette=dirt_pal)
        sheet("tiles", save(im, "Tiles", "Map1", f"dirt_{n}.png"))
    tw = (1099 - 413) / 3
    for n in range(2):
        box = (round(1100 + n * tw), 262, round(1100 + (n + 1) * tw), 482)
        im, _ = tile_image(rgb, box, palette=cob_pal if n == 0 else dirt_pal)
        sheet("tiles", save(im, "Tiles", "Map1", f"cobble_broken_{n}.png"))
    print(f"map1 tiles: dirt block x {dx0}..{dx1}")

    # ---- map 2: swamp sheet, every element is its own figure
    rgb = load_rgb(sources.path("map2_tiles"))
    alpha = key_magenta(rgb, glow=False)
    figs = figures(alpha, merge_gap=6)
    named = classify_swamp(figs)
    for name, box, inset in (("mud", None, 0.14), ("water", None, 0.03), ("planks", None, 0.03), ("roots", None, 0.07)):
        boxes = sorted([b for n, b in named if n == name], key=lambda b: b[0])
        pal = None
        for i, (x0, y0, x1, y1) in enumerate(boxes):
            w, h = x1 - x0, y1 - y0
            ib = (round(x0 + w * inset), round(y0 + h * inset), round(x1 - w * inset), round(y1 - h * (inset + (0.06 if name == "roots" else 0))))
            if pal is None:
                pal = kmeans_palette(rgb[ib[1]:ib[3], ib[0]:ib[2]], np.ones((ib[3] - ib[1], ib[2] - ib[0]), bool), 20)
            im, _ = tile_image(rgb, ib, palette=pal)
            sheet("tiles", save(im, "Tiles", "Map2", f"{name}_{i}.png"))
    return rgb, alpha, named


def classify_swamp(figs):
    """Names the swamp sheet elements by where they sit on the sheet (layout of the delivered image)."""
    out_ = []
    for b in figs:
        cx, cy = (b[0] + b[2]) / 2, (b[1] + b[3]) / 2
        if (b[2] - b[0]) * (b[3] - b[1]) < 4000:
            continue                                  # slime droplets around the puddle
        if cy < 420:
            out_.append(("mud" if cx < 1400 else "water", b))
        elif cy < 820:
            out_.append(("planks" if cx < 1400 else "roots", b))
        elif cy < 1200:
            if cx < 450:
                out_.append(("reeds_0", b))
            elif cx < 750:
                out_.append(("reeds_1", b))
            elif cx < 1050:
                out_.append(("bonepile_0", b))
            elif cx < 1400:
                out_.append(("bonepile_1", b))
            elif cx < 2150:
                out_.append(("slime_pool", b))
            else:
                out_.append(("mossy_rock", b))
        else:
            out_.append(("mushrooms" if cx < 450 else "stump" if cx < 750 else "totem", b))
    return out_


# ---------------------------------------------------------------- props

PROP_SCALE = 0.158         # map 1 sheet drawn at ~6.3 source px per art pixel: this keeps one art pixel per game pixel
BLOCK_TOP = 934            # props on the map 1 sheet stand on dirt blocks; their top face starts here
MAP1_PROPS = {
    # name: (block x range, ellipse (cx, cy, rx, ry) of ground kept around the base, extra rects, decal?)
    "ruin_wall": ((321, 860), (592, 1028, 262, 78), [], False),
    "cross_grave": ((894, 1170), (1028, 1040, 108, 62), [(1030, BLOCK_TOP, 1112, 1010)], False),
    "bones": ((1204, 1479), (1345, 1032, 128, 56), [], True),
    "torch_post": ((1513, 1789), (1652, 1066, 40, 16), [(1626, BLOCK_TOP, 1680, 1066)], False),
    "dead_tree": ((1823, 2098), (1946, 1048, 128, 56), [(1858, BLOCK_TOP, 2004, 1048)], False),
    "rock": ((2155, 2431), (2272, 1030, 118, 64), [], False),
}
PIT = ((1192, 1560), (1348, 1338, 146, 96))     # corruption pit: a ground decal on its own block


def noisy_ellipse(shape, cx, cy, rx, ry, seed=1, amp=0.12):
    H, W = shape
    yy, xx = np.mgrid[0:H, 0:W]
    ang = np.arctan2((yy - cy) / ry, (xx - cx) / rx)
    rng = np.random.default_rng(seed)
    ph = rng.uniform(0, 6.28, 4)
    wob = 1 + amp * (np.sin(3 * ang + ph[0]) * 0.5 + np.sin(5 * ang + ph[1]) * 0.3 + np.sin(9 * ang + ph[2]) * 0.2)
    return ((xx - cx) / rx) ** 2 + ((yy - cy) / ry) ** 2 <= wob ** 2


def build_props():
    # ---- map 1 props: silhouette above the block + a ragged patch of the block's dirt around the base
    #      (same dirt as the map 1 ground tiles, so the patch blends into the terrain)
    rgb = load_rgb(sources.path("map1_tiles"))
    alpha = key_magenta(rgb, glow=False)
    H = alpha.shape[0]
    for i, (name, ((bx0, bx1), (cx, cy, rx, ry), rects, decal)) in enumerate(MAP1_PROPS.items()):
        keep = np.zeros_like(alpha)
        keep[762:BLOCK_TOP, bx0:bx1] = True          # above the block, below the tile blocks of the sheet
        keep |= noisy_ellipse(alpha.shape, cx, cy, rx, ry, seed=i)
        for x0, y0, x1, y1 in rects:
            keep[y0:y1, x0:x1] = True
        m = alpha & keep
        m[:, :bx0] = m[:, bx1:] = False
        m[1110:] = False
        r, a = trim(rgb, m)
        save_generated(r, a, "Props", name)
        sheet("props", save(scaled(r, a, PROP_SCALE, outline=not decal), "Sprites", "Props", "Map1", f"{name}.png"))
    (bx0, bx1), (cx, cy, rx, ry) = PIT
    m = alpha & noisy_ellipse(alpha.shape, cx, cy, rx, ry, seed=9, amp=0.06)
    r, a = trim(rgb, m)
    sheet("props", save(scaled(r, a, PROP_SCALE, outline=False), "Sprites", "Props", "Map1", "corruption_pit.png"))

    # ---- old sheet (flat dark background): ruins, torches, banners, hooded figures for reference
    rgb = load_rgb(sources.path("allies_sheet"))
    for name, crop, scale in (("ruin_0", (1565, 120, 1945, 370), 0.21), ("ruin_1", (1535, 395, 1905, 712), 0.21),
                              ("ruin_2", (1535, 875, 1790, 1080), 0.21), ("torch_0", (1940, 40, 2020, 360), 0.21),
                              ("torch_1", (1795, 730, 1875, 1075), 0.21), ("banner_raven", (2025, 30, 2275, 575), 0.155),
                              ("banner_skull", (2025, 665, 2275, 1200), 0.155)):
        x0, y0, x1, y1 = crop
        r = rgb[y0:y1, x0:x1]
        a = key_flat(r, tol=48)
        r, a = trim(r, a)
        im = save(scaled(r, a, scale), "Sprites", "Props", "Map1", f"{name}.png")
        sheet("props", im)

    # ---- fortress: black tower (painted checkerboard) + stone watchtower + shrine
    rgb = load_rgb(sources.path("fortress"))
    a = key_checker(rgb)
    r, a = trim(rgb, a)
    save_generated(r, a, "Buildings", "fortress_black_tower")
    sheet("props", save(scaled(r, a, 196 / r.shape[0]), "Sprites", "Buildings", "fortress_keep.png"))
    rgb = load_rgb(sources.path("watchtower"))
    a = key_magenta(rgb)
    r, a = trim(rgb, a)
    sheet("props", save(scaled(r, a, 118 / r.shape[0]), "Sprites", "Buildings", "fortress_watchtower.png"))
    rgb = load_rgb(sources.path("chapel_alt"))
    a = key_magenta(rgb)
    figs = [f for f in figures(a, merge_gap=20) if f[3] - f[1] > 300]
    r, a = cut(rgb, a, sorted(figs, key=lambda b: b[0])[0])
    sheet("props", save(scaled(r, a, 50 / r.shape[0]), "Sprites", "Props", "Map1", "shrine.png"))

    # ---- map 2 props from the swamp sheet
    rgb = load_rgb(sources.path("map2_tiles"))
    alpha = key_magenta(rgb, glow=False)
    named = classify_swamp(figures(alpha, merge_gap=6))
    heights = {"reeds_0": 46, "reeds_1": 46, "bonepile_0": 34, "bonepile_1": 34, "slime_pool": 30,
               "mossy_rock": 58, "mushrooms": 44, "stump": 44, "totem": 54}
    for name, box in named:
        if name not in heights:
            continue
        if name == "slime_pool":                     # include its droplets
            box = (box[0] - 40, box[1] - 40, box[2] + 40, box[3] + 40)
        r, a = cut(rgb, alpha, box)
        save_generated(r, a, "Props", name)
        decal = name in ("bonepile_0", "bonepile_1", "slime_pool")
        im = save(scaled(r, a, heights[name] / r.shape[0], outline=not decal), "Sprites", "Props", "Map2", f"{name}.png")
        sheet("props", im)


# ---------------------------------------------------------------- UI

ICONS = ["gold", "heart", "wave", "skull", "boss", "attack", "archer", "axe", "armor", "fire", "magic", "heal", "sell", "upgrade"]


def build_icons():
    """14 framed icons on a 7x2 grid (frame centres measured on the delivered sheet)."""
    rgb = load_rgb(sources.path("ui_icons"))
    cols, rows, half = (314, 666, 1020, 1378, 1730, 2090, 2438), (592, 951), 150
    centres = [(cx, cy) for cy in rows for cx in cols]
    for name, (cx, cy) in zip(ICONS, centres):
        r = rgb[cy - half:cy + half, cx - half:cx + half]
        a = np.ones(r.shape[:2], bool)
        for size in (32, 64):
            rr, aa, _ = area_downscale(r, a, (size, size), k=40)
            im = to_image(rr, aa)
            save(im, "UI", "Icons", f"{name}_{size}.png")
        sheet("icons", im)


def fill_rows(rgb, box, band=6, noise=3, seed=0, smooth=1):
    """Paints over baked text/figures: every row takes the median of a thin band at both inner edges
    (optionally smoothed over `smooth` rows so frames do not leave stripes)."""
    x0, y0, x1, y1 = box
    rng = np.random.default_rng(seed)
    rows = np.array([np.median(np.concatenate([rgb[y, x0:x0 + band], rgb[y, x1 - band:x1]]), axis=0) for y in range(y0, y1)])
    if smooth > 1:
        k = np.ones(smooth) / smooth
        pad = np.pad(rows, ((smooth // 2, smooth - 1 - smooth // 2), (0, 0)), mode="edge")
        rows = np.stack([np.convolve(pad[:, c], k, mode="valid") for c in range(3)], axis=1)
    for i, y in enumerate(range(y0, y1)):
        rgb[y, x0 + band:x1 - band] = rows[i] + rng.integers(-noise, noise + 1, (x1 - x0 - 2 * band, 3))


def build_backgrounds():
    """Menu backdrops cut from the delivered UI mockup (960x540, shown at exactly 2x).
    Baked English labels / placeholder figures are painted over; the game draws its own UI on top."""
    rgb = load_rgb(sources.path("ui_mockup"))
    H, W = rgb.shape[:2]
    hx, hy = W // 2, H // 2
    quads = {"menu": (hx, 0, W, hy), "commander": (0, hy, hx, H), "skins": (hx, hy, W, H)}
    out_ = {}
    for name, (x0, y0, x1, y1) in quads.items():
        inset = 14
        r = rgb[y0 + inset:y1 - inset, x0 + inset:x1 - inset]
        rr, _, _ = area_downscale(r, np.ones(r.shape[:2], bool), (960, 540), k=64)
        out_[name] = rr
    fill_rows(out_["menu"], (372, 196, 590, 460), band=8)            # menu labels inside the gargoyle frame
    fill_rows(out_["commander"], (192, 102, 418, 402), band=5, smooth=61)   # placeholder mage inside the portrait frame
    fill_rows(out_["commander"], (232, 410, 380, 447), band=6)       # "COMMANDER LEVEL 3" plate
    # battlefield: the clean middle of the HUD mockup quadrant (its own HUD sits on the edges)
    r = rgb[170:537, 100:752]
    out_["battlefield"], _, _ = area_downscale(r, np.ones(r.shape[:2], bool), (960, 540), k=64)
    for name, rr in out_.items():
        im = to_image(rr, np.ones((540, 960), bool))
        save(im, "UI", "Backgrounds", f"{name}.png")
        sheet("backgrounds", im.resize((480, 270), Image.NEAREST))
    for stale in ("hud_reference.png",):
        p = out("UI", "Backgrounds", stale)
        if os.path.exists(p):
            os.remove(p)


# ---------------------------------------------------------------- projectiles (no art delivered: drawn here)

C = dict(void=VOID, bone=(232, 220, 192), iron=(138, 140, 142), s5=(74, 72, 82), blood=(139, 26, 26),
         rune=(255, 42, 42), ember=(242, 163, 58), rust=(107, 63, 38), corr=(91, 42, 122), violet=(150, 70, 200))


def build_projectiles():
    def new(w, h):
        im = Image.new("RGBA", (w, h))
        return im, ImageDraw.Draw(im)

    im, d = new(14, 5)                                                 # arrow
    d.line([(2, 2), (11, 2)], fill=(160, 110, 70))
    d.polygon([(11, 0), (13, 2), (11, 4)], fill=C["iron"])
    d.line([(0, 0), (2, 2)], fill=C["bone"])
    d.line([(0, 4), (2, 2)], fill=C["bone"])
    save(im, "Sprites", "Projectiles", "arrow.png")
    im, d = new(22, 7)                                                 # ballista bolt
    d.rectangle([3, 2, 16, 4], fill=C["s5"])
    d.line([(3, 3), (16, 3)], fill=C["iron"])
    d.polygon([(16, 0), (21, 3), (16, 6)], fill=C["iron"])
    d.polygon([(0, 0), (5, 2), (5, 4), (0, 6)], fill=C["blood"])
    rgb, a = from_image(im)
    save(finish(rgb, a), "Sprites", "Projectiles", "bolt.png")
    im, d = new(12, 12)                                                # cursed fire orb
    d.ellipse([0, 0, 11, 11], fill=(110, 40, 150))
    d.ellipse([2, 2, 9, 9], fill=C["violet"])
    d.ellipse([4, 3, 8, 7], fill=(235, 150, 255))
    save(im, "Sprites", "Projectiles", "cursed_fire.png")
    im, d = new(14, 9)                                                 # crow
    d.polygon([(0, 4), (5, 2), (9, 3), (13, 2), (10, 5), (6, 7), (2, 6)], fill=(26, 22, 30))
    d.polygon([(4, 3), (7, 0), (9, 3)], fill=(40, 34, 46))
    d.point([(11, 3)], fill=(200, 90, 255))
    d.point([(13, 3)], fill=C["bone"])
    save(im, "Sprites", "Projectiles", "crow.png")
    im, d = new(9, 9)                                                  # blood orb
    d.ellipse([0, 0, 8, 8], fill=(120, 10, 20))
    d.ellipse([2, 1, 6, 5], fill=(230, 40, 50))
    d.point([(3, 2)], fill=(255, 190, 190))
    save(im, "Sprites", "Projectiles", "blood_orb.png")


GROUPS = {"towers": build_towers, "tiles": build_tiles, "props": build_props, "icons": build_icons,
          "backgrounds": build_backgrounds, "projectiles": build_projectiles}

if __name__ == "__main__":
    sources.sync()
    wanted = [a for a in sys.argv[1:] if not a.startswith("--")] or list(GROUPS)
    for g in wanted:
        GROUPS[g]()
    write_debug()
    print("ok")

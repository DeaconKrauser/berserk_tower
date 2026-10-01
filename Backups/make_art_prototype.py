"""Temporary art for the vertical slice.

- Cuts the old concept images out of their fake-checkerboard / sheet backgrounds.
- Draws by code what has no art yet (ballista, pyre, chapel, hound, projectiles, tiles, props).
- Builds provisional Idle/Walk/Attack/Hit/Death frames from a single pose.

Final art replaces these PNGs 1:1 (same folder, same names). Run: python tools/make_art.py
"""
import glob
import os
import random
from collections import deque

import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SPR = os.path.join(ROOT, "Assets", "Sprites")
MAP = os.path.join(ROOT, "Assets", "Resources", "Map")
R = random.Random(1337)

# design-system.md palette
C = dict(
    void=(11, 10, 13), s9=(28, 27, 34), s7=(46, 44, 54), s5=(74, 72, 82), iron=(138, 140, 142),
    bone=(232, 220, 192), blood=(139, 26, 26), rune=(255, 42, 42), ember=(242, 163, 58),
    gold=(168, 137, 74), rust=(107, 63, 38), moss=(63, 90, 46), corr=(91, 42, 122), teal=(30, 42, 42),
)


def sh(c, k):
    return tuple(max(0, min(255, int(v * k))) for v in c)


def save(img, *parts):
    path = os.path.join(*parts)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path)


def trim(img):
    box = img.getbbox()
    return img.crop(box) if box else img


# ---------------------------------------------------------------- background removal

def flood_from_border(cand):
    H, W = cand.shape
    c = cand.ravel().tolist()
    seen = bytearray(H * W)
    q = deque()

    def push(i):
        if c[i] and not seen[i]:
            seen[i] = 1
            q.append(i)

    for x in range(W):
        push(x)
        push((H - 1) * W + x)
    for y in range(H):
        push(y * W)
        push(y * W + W - 1)
    while q:
        i = q.popleft()
        y, x = divmod(i, W)
        if x > 0: push(i - 1)
        if x < W - 1: push(i + 1)
        if y > 0: push(i - W)
        if y < H - 1: push(i + W)
    return np.frombuffer(bytes(seen), np.uint8).reshape(H, W).astype(bool)


def components(mask):
    H, W = mask.shape
    m = mask.ravel().tolist()
    lab = bytearray(H * W)
    comps = []
    for s in range(H * W):
        if m[s] and not lab[s]:
            lab[s] = 1
            stack, pix = [s], []
            while stack:
                i = stack.pop()
                pix.append(i)
                y, x = divmod(i, W)
                for j in (i - 1 if x > 0 else -1, i + 1 if x < W - 1 else -1,
                          i - W if y > 0 else -1, i + W if y < H - 1 else -1):
                    if j >= 0 and m[j] and not lab[j]:
                        lab[j] = 1
                        stack.append(j)
            comps.append(pix)
    return comps


def downscale(rgb, alpha, size):
    """Premultiplied box downscale + hard alpha, so no background color bleeds into the edge."""
    def rs(ch):
        return np.asarray(Image.fromarray(ch.astype(np.float32)).resize(size, Image.BOX))
    A = rs(alpha)
    out = np.zeros((size[1], size[0], 4), np.uint8)
    for k in range(3):
        out[..., k] = np.clip(rs(rgb[..., k] * alpha) / np.maximum(A, 1e-6), 0, 255)
    out[..., 3] = np.where(A >= 0.5, 255, 0)
    return trim(Image.fromarray(out, "RGBA"))


def extract(src, height=None, scale=None, crop=None, bg="checker", tol=40):
    """Cut the main figure (plus fragments touching it) out of a JPG. height = final px, or scale vs original."""
    im = Image.open(os.path.join(ROOT, src)).convert("RGB")
    if crop:
        im = im.crop(crop)
    work = min(1.0, 900 / im.height)
    im = im.resize((round(im.width * work), round(im.height * work)), Image.BOX)
    a = np.asarray(im).astype(np.int32)
    edge = np.concatenate([a[0], a[-1], a[:, 0], a[:, -1]])
    if bg == "checker":
        br, sat, eb = a.mean(2), a.max(2) - a.min(2), edge.mean(1)
        cand = (sat < 24) & (br > np.percentile(eb, 1) - 14) & (br < np.percentile(eb, 99) + 14)
    else:
        cand = np.abs(a - np.median(edge, axis=0)).sum(2) < tol
    fg = ~flood_from_border(cand)
    comps = sorted(components(fg), key=len, reverse=True)
    W = fg.shape[1]
    keep = np.zeros(fg.size, bool)
    keep[comps[0]] = True
    ys, xs = np.divmod(np.array(comps[0]), W)
    y0, y1, x0, x1 = ys.min(), ys.max(), xs.min(), xs.max()
    for comp in comps[1:]:  # sword tips, aura sparks, flames
        if len(comp) < 12:
            continue
        cy, cx = np.divmod(np.array(comp), W)
        if cx.max() >= x0 - 6 and cx.min() <= x1 + 6 and cy.max() >= y0 - 6 and cy.min() <= y1 + 6:
            keep[comp] = True
    keep = keep.reshape(fg.shape)
    ys, xs = np.nonzero(keep)
    y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    f = height / (y1 - y0) if height else scale / work
    size = (max(1, round((x1 - x0) * f)), max(1, round((y1 - y0) * f)))
    return downscale(a[y0:y1, x0:x1], keep[y0:y1, x0:x1].astype(np.float32), size)


def magenta_figures(src, ref_index, ref_height):
    """Figures of a #FF00FF design sheet, left to right, sharing one scale."""
    a = np.asarray(Image.open(os.path.join(ROOT, src)).convert("RGB")).astype(np.int32)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    fg = (np.abs(a - a[4, 4]).sum(2) > 120) & ~((r > 170) & (b > 170) & (g < 120))
    cols = np.append(fg.sum(0) > 2, False)
    segs, start = [], None
    for x, on in enumerate(cols):
        if on and start is None:
            start = x
        if not on and start is not None:
            if x - start > 40:
                segs.append((start, x))
            start = None

    def box(x0, x1):
        ys = np.nonzero(fg[:, x0:x1].any(1))[0]
        return ys[0], ys[-1] + 1

    t, b_ = box(*segs[ref_index])
    f = ref_height / (b_ - t)
    out = []
    for x0, x1 in segs:
        y0, y1 = box(x0, x1)
        out.append(downscale(a[y0:y1, x0:x1], fg[y0:y1, x0:x1].astype(np.float32),
                             (round((x1 - x0) * f), round((y1 - y0) * f))))
    return out


# ---------------------------------------------------------------- pixel helpers

def outline(im, color=C["void"]):
    a = np.asarray(im).copy()
    al = a[..., 3] > 0
    n = np.zeros_like(al)
    n[1:, :] |= al[:-1, :]
    n[:-1, :] |= al[1:, :]
    n[:, 1:] |= al[:, :-1]
    n[:, :-1] |= al[:, 1:]
    a[n & ~al] = (*color, 255)
    return Image.fromarray(a, "RGBA")


def pad(im, n=1):
    out = Image.new("RGBA", (im.width + 2 * n, im.height + 2 * n))
    out.paste(im, (n, n))
    return out


def lean(img, deg):
    """Rotate around the feet (bottom-center). deg > 0 leans forward (+x). Pivot stays at the feet."""
    w, h = img.size
    S = 2 * max(w, h) + 4
    cx = cy = S // 2
    cv = Image.new("RGBA", (S, S))
    cv.paste(img, (cx - w // 2, cy - h), img)
    cv = cv.rotate(-deg, resample=Image.NEAREST, center=(cx, cy))
    ys, xs = np.nonzero(np.asarray(cv)[..., 3])
    half = max(cx - xs.min(), xs.max() + 1 - cx)
    return cv.crop((cx - half, ys.min(), cx + half, ys.max() + 1))


def squash(img, n):
    return img.resize((img.width, max(1, img.height - n)), Image.NEAREST)


def tint(img, color, k):
    a = np.asarray(img).astype(np.float32)
    m = a[..., 3] > 0
    a[m, :3] = a[m, :3] * (1 - k) + np.array(color) * k
    return Image.fromarray(a.astype(np.uint8), "RGBA")


def slash(img):
    """Adds a swing arc in front; widened symmetrically so the pivot stays centered."""
    w, h = img.size
    p = max(8, w // 3)
    cv = Image.new("RGBA", (w + 2 * p, h))
    cv.paste(img, (p, 0), img)
    d = ImageDraw.Draw(cv)
    box = [p + w // 3, h // 5, w + 2 * p - 1, h - h // 6]
    d.arc(box, 290, 70, fill=(*C["bone"], 255), width=2)
    d.arc([box[0] + 3, box[1] + 3, box[2] - 3, box[3] - 3], 300, 60, fill=(*C["iron"], 255), width=1)
    return cv


def character(name, base, **custom):
    d = os.path.join(SPR, "Characters", name)
    for f in glob.glob(os.path.join(d, "*.png")):
        os.remove(f)
    frames = dict(
        idle=[base, squash(base, 1), squash(base, 2), squash(base, 1)],
        walk=[lean(base, -3), squash(base, 1), lean(base, 3), squash(base, 1)],
        attack=[lean(base, -10), slash(lean(base, 14)), lean(base, 5)],
        hit=[tint(lean(base, -6), C["rune"], 0.55), base],
        death=[lean(base, -25), lean(base, -55), lean(base, -80), tint(lean(base, -90), C["void"], 0.45)],
    )
    frames.update(custom)
    for state, imgs in frames.items():
        for i, im in enumerate(imgs):
            save(im, d, f"{state}_{i}.png")
    return frames["idle"][0]


# ---------------------------------------------------------------- code-drawn sprites

def hound(legs=1, lunge=0, mouth=False, head=0):
    """Carrion hound, facing right. ~50x30."""
    im = Image.new("RGBA", (54, 32))
    d = ImageDraw.Draw(im)
    fur, dark, light = (66, 46, 40), (44, 30, 28), (92, 66, 52)
    o = 2 + lunge
    swing = [(-3, 3), (0, 0), (3, -3), (0, 0)][legs]
    for hip, s in ((14, swing[0]), (18, swing[1]), (34, swing[1]), (38, swing[0])):
        d.line([(hip + o, 20), (hip + o + s, 29)], fill=dark, width=2)
    d.line([(12 + o, 12), (4 + o, 6)], fill=dark, width=2)                      # tail
    d.ellipse([10 + o, 9, 40 + o, 23], fill=fur)                                  # body
    d.ellipse([14 + o, 17, 38 + o, 23], fill=dark)                                # belly shadow
    for x in range(18, 33, 3):                                                   # ribs showing
        d.line([(x + o, 13), (x + o, 17)], fill=C["bone"])
    for x in range(14, 34, 5):                                                   # spine spikes
        d.polygon([(x + o, 10), (x + o + 2, 6), (x + o + 3, 10)], fill=C["bone"])
    hy = head
    d.polygon([(36 + o, 8 + hy), (47 + o, 11 + hy), (50 + o, 15 + hy), (45 + o, 19 + hy), (37 + o, 18 + hy)], fill=fur)
    d.polygon([(38 + o, 9 + hy), (40 + o, 3 + hy), (42 + o, 9 + hy)], fill=light)   # ear
    d.point([(44 + o, 12 + hy)], fill=C["rune"])
    if mouth:
        d.polygon([(44 + o, 16 + hy), (50 + o, 15 + hy), (47 + o, 21 + hy)], fill=C["void"])
        d.point([(46 + o, 16 + hy), (48 + o, 16 + hy), (46 + o, 19 + hy)], fill=C["bone"])
    else:
        d.line([(45 + o, 17 + hy), (49 + o, 16 + hy)], fill=C["void"])
    return trim(outline(pad(im)))


def ballista(lv):
    im = Image.new("RGBA", (58, 50))
    d = ImageDraw.Draw(im)
    wood, wd, wl = C["rust"], sh(C["rust"], 0.65), sh(C["rust"], 1.35)
    d.polygon([(9, 36), (48, 36), (44, 30), (13, 30)], fill=C["s5"])            # base top face
    d.rectangle([9, 36, 48, 47], fill=C["s7"])                                   # base front face
    for x in range(12, 48, 7):
        d.line([(x, 37), (x, 46)], fill=C["s9"])
    d.line([(9, 41), (48, 41)], fill=C["s9"])
    if lv >= 2:
        d.rectangle([9, 36, 48, 37], fill=C["iron"])
        d.rectangle([9, 46, 48, 47], fill=sh(C["iron"], 0.7))
    d.ellipse([19, 27, 38, 34], fill=wd)                                         # turntable
    d.rectangle([26, 19, 31, 30], fill=wood)                                     # post
    d.polygon([(8, 27), (11, 30), (47, 15), (45, 12)], fill=wood)                # stock
    d.line([(10, 27), (46, 13)], fill=wl)
    arm = C["iron"] if lv >= 2 else wl
    width = 3 if lv >= 3 else 2
    d.line([(38, 1), (43, 13), (38, 25)], fill=arm, width=width)                 # bow arms
    d.line([(38, 1), (27, 19), (38, 25)], fill=C["bone"])                        # string
    d.line([(24, 20), (51, 9)], fill=C["iron"])                                  # bolt
    d.polygon([(51, 7), (56, 8), (51, 11)], fill=C["iron"])
    if lv >= 3:
        d.point([(20, 22), (25, 20), (30, 18)], fill=C["rune"])                  # runes on stock
    if lv >= 4:
        for x in (10, 47):                                                        # bone spikes
            d.polygon([(x - 2, 30), (x, 22), (x + 2, 30)], fill=C["bone"])
        d.rectangle([4, 14, 5, 30], fill=wd)                                     # war banner
        d.polygon([(6, 14), (13, 15), (11, 19), (13, 23), (6, 22)], fill=C["blood"])
    return trim(outline(pad(im)))


def pyre(lv):
    im = Image.new("RGBA", (50, 64))
    d = ImageDraw.Draw(im)
    if lv >= 4:
        d.ellipse([1, 50, 48, 62], outline=C["rune"])
    d.ellipse([7, 47, 42, 60], fill=C["s5"])                                     # stone ring
    d.rectangle([7, 53, 42, 56], fill=C["s5"])
    d.ellipse([7, 51, 42, 62], fill=C["s7"])
    d.ellipse([11, 48, 38, 56], fill=C["s9"])                                    # pit
    d.line([(12, 54), (36, 46)], fill=C["rust"], width=3)                        # logs
    d.line([(13, 46), (37, 54)], fill=sh(C["rust"], 0.7), width=3)
    for sx, sy in ((14, 49), (31, 50)) + (((22, 52),) if lv >= 2 else ()):       # skulls
        d.ellipse([sx - 2, sy - 2, sx + 2, sy + 2], fill=C["bone"])
        d.point([(sx - 1, sy), (sx + 1, sy)], fill=C["void"])
    fh = 16 + 5 * lv
    cx, base = 24, 50

    def flame(w, h, col):
        pts = [(cx - w, base)]
        for i in range(1, 6):
            t = i / 6
            pts.append((cx - w + 2 * w * t, base - h * (0.55 + 0.45 * (1 - abs(t - 0.5) * 2)) - (4 if i % 2 else 0)))
        pts.append((cx + w, base))
        d.polygon(pts, fill=col)

    flame(10 + lv, fh, (140, 60, 180))
    flame(7 + lv, fh * 0.75, C["rune"])
    flame(4 + lv // 2, fh * 0.45, C["ember"])
    if lv >= 3:
        for x in (8, 40):
            d.polygon([(x - 2, 54), (x, 44), (x + 2, 54)], fill=C["bone"])
    return trim(outline(pad(im)))


def chapel(lv):
    im = Image.new("RGBA", (56, 72))
    d = ImageDraw.Draw(im)
    if lv >= 4:
        d.ellipse([2, 60, 53, 71], outline=C["gold"])
    top = 30 if lv < 3 else 27
    d.rectangle([12, top + 2, 43, 66], fill=C["s5"])                             # nave
    for y in range(top + 5, 66, 4):
        d.line([(12, y), (43, y)], fill=C["s7"])
        for x in range(12 + (y // 4 % 2) * 3, 43, 6):
            d.point([(x, y - 2)], fill=C["s7"])
    d.polygon([(9, top + 3), (27, top - 15), (46, top + 3)], fill=C["s9"])     # roof
    d.line([(9, top + 3), (27, top - 15), (46, top + 3)], fill=C["gold"] if lv >= 3 else C["iron"])
    d.rectangle([23, 52, 32, 66], fill=C["void"])                                # door
    d.pieslice([23, 48, 32, 57], 180, 360, fill=C["void"])
    d.rectangle([24, 62, 31, 65], fill=C["ember"])
    d.ellipse([23, top + 9, 32, top + 18], fill=C["blood"])                      # rose window
    d.point([(27, top + 13), (28, top + 13)], fill=C["rune"])
    bt = top - 22                                                                 # bell tower
    d.rectangle([23, bt, 32, top - 10], fill=C["s7"])
    d.polygon([(22, bt + 1), (27, bt - 8 - (4 if lv >= 3 else 0)), (33, bt + 1)], fill=C["s9"])
    d.ellipse([25, bt + 3, 30, bt + 8], fill=C["gold"])
    if lv >= 2:
        d.rectangle([38, top + 8, 42, top + 22], fill=C["blood"])                # banner
        d.polygon([(38, top + 22), (40, top + 19), (42, top + 22)], fill=C["s5"])
    if lv >= 3:
        d.line([(27, bt - 16), (27, bt - 10)], fill=C["gold"])                   # spire cross
        d.line([(25, bt - 14), (29, bt - 14)], fill=C["gold"])
    if lv >= 4:
        for x in (6, 49):                                                         # buttress towers
            d.rectangle([x - 3, top + 12, x + 3, 66], fill=C["s7"])
            d.polygon([(x - 4, top + 13), (x, top + 5), (x + 4, top + 13)], fill=C["s9"])
    return trim(outline(pad(im)))


def archer(base, lv):
    """Level markers on top of the extracted watchtower."""
    im = base.copy()
    d = ImageDraw.Draw(im)
    w = im.width
    if lv >= 2:
        d.rectangle([w // 2 + 1, 0, w // 2 + 1, 6], fill=C["void"])
        d.polygon([(w // 2 + 2, 0), (w // 2 + 9, 1), (w // 2 + 6, 3), (w // 2 + 9, 5), (w // 2 + 2, 5)], fill=C["blood"])
    if lv >= 3:
        for x in (6, w - 8):
            d.rectangle([x, 22, x + 2, 26], fill=C["iron"])
    if lv >= 4:
        for x in (4, w - 7):
            d.rectangle([x, 8, x + 3, 10], fill=C["s7"])
            d.point([(x + 1, 7), (x + 2, 6), (x + 2, 7)], fill=C["ember"])
    return im


def projectile_arrow():
    im = Image.new("RGBA", (16, 5))
    d = ImageDraw.Draw(im)
    d.line([(2, 2), (12, 2)], fill=sh(C["rust"], 1.5))
    d.polygon([(12, 0), (15, 2), (12, 4)], fill=C["iron"])
    d.line([(0, 0), (3, 2)], fill=C["bone"])
    d.line([(0, 4), (3, 2)], fill=C["bone"])
    return im


def projectile_bolt():
    im = Image.new("RGBA", (24, 7))
    d = ImageDraw.Draw(im)
    d.rectangle([3, 2, 17, 4], fill=C["s5"])
    d.line([(3, 3), (17, 3)], fill=C["iron"])
    d.polygon([(17, 0), (23, 3), (17, 6)], fill=C["iron"])
    d.polygon([(0, 0), (5, 2), (5, 4), (0, 6)], fill=C["blood"])
    return outline(pad(im))


def projectile_fireball():
    im = Image.new("RGBA", (12, 12))
    d = ImageDraw.Draw(im)
    d.ellipse([0, 0, 11, 11], fill=(140, 60, 180))
    d.ellipse([2, 2, 9, 9], fill=C["rune"])
    d.ellipse([4, 4, 7, 7], fill=C["ember"])
    return im


# ---------------------------------------------------------------- tiles (16x16, 0.5 unit)

def blotch_tile(base, tones, specks, seed):
    r = random.Random(seed)
    im = Image.new("RGBA", (16, 16), (*base, 255))
    d = ImageDraw.Draw(im)
    for _ in range(5):
        x, y, rw, rh = r.randrange(16), r.randrange(16), r.randint(2, 6), r.randint(1, 4)
        col = r.choice(tones)
        for dx in (-16, 0, 16):              # wrap so the tile stays seamless
            for dy in (-16, 0, 16):
                d.ellipse([x - rw + dx, y - rh + dy, x + rw + dx, y + rh + dy], fill=(*col, 255))
    px = im.load()
    for col, n in specks:
        for _ in range(n):
            px[r.randrange(16), r.randrange(16)] = (*col, 255)
    return im


def cobble_tile(seed):
    r = random.Random(seed)
    im = Image.new("RGBA", (16, 16), (40, 35, 38, 255))
    d = ImageDraw.Draw(im)
    y = 0
    while y < 16:
        h = r.randint(3, 5)
        x = -r.randint(0, 3)
        while x < 16:
            w = r.randint(4, 6)
            col = r.choice([(78, 74, 84), (70, 66, 74), (86, 80, 88), (64, 60, 66)])
            d.rectangle([x + 1, y + 1, x + w - 1, y + h - 1], fill=col)
            d.line([(x + 1, y + 1), (x + w - 2, y + 1)], fill=sh(col, 1.18))
            x += w
        y += h
    return im


def decal(kind, seed):
    r = random.Random(seed)
    im = Image.new("RGBA", (16, 16))
    d = ImageDraw.Draw(im)
    if kind == "bones":
        for _ in range(r.randint(2, 3)):
            x, y = r.randint(3, 12), r.randint(3, 12)
            dx, dy = r.choice([(4, 1), (3, -2), (-4, 1), (1, 3)])
            d.line([(x, y), (x + dx, y + dy)], fill=C["bone"])
            d.point([(x, y), (x + dx, y + dy)], fill=sh(C["bone"], 0.8))
    elif kind == "skull":
        d.ellipse([5, 6, 11, 11], fill=C["bone"])
        d.rectangle([6, 10, 10, 12], fill=sh(C["bone"], 0.85))
        d.point([(7, 8), (9, 8)], fill=C["void"])
        return outline(im)
    elif kind == "stones":
        for _ in range(r.randint(1, 3)):
            x, y, s = r.randint(2, 12), r.randint(2, 12), r.randint(1, 2)
            d.rectangle([x, y, x + s, y + s - 1], fill=C["s5"])
            d.point([(x, y)], fill=sh(C["s5"], 1.3))
            d.line([(x, y + s), (x + s, y + s)], fill=C["void"])
    elif kind == "blood":
        for _ in range(3):
            x, y = r.randint(4, 11), r.randint(4, 11)
            d.ellipse([x - 3, y - 2, x + 3, y + 2], fill=(*sh(C["blood"], 0.6), 190))
    elif kind == "crack":
        x, y = r.randint(1, 4), r.randint(4, 12)
        pts = [(x, y)]
        for _ in range(4):
            x, y = x + r.randint(2, 4), y + r.randint(-2, 2)
            pts.append((x, y))
        d.line(pts, fill=(16, 12, 16, 255))
    elif kind == "grass":
        for _ in range(r.randint(3, 5)):
            x, y = r.randint(3, 12), r.randint(8, 13)
            d.line([(x, y), (x + r.randint(-1, 1), y - r.randint(2, 4))], fill=r.choice([(58, 70, 44), (72, 78, 50), (52, 60, 40)]))
    elif kind == "grit":
        for _ in range(r.randint(10, 18)):
            x, y = r.randrange(16), r.randrange(16)
            d.point([(x, y)], fill=r.choice([(60, 50, 42), (52, 44, 38), (70, 60, 50), (44, 40, 40)]))
    return im


# ---------------------------------------------------------------- standing props

def tomb(kind):
    im = Image.new("RGBA", (16, 20))
    d = ImageDraw.Draw(im)
    if kind == 0:
        d.rectangle([3, 7, 12, 18], fill=C["s5"])
        d.pieslice([3, 2, 12, 12], 180, 360, fill=C["s5"])
        d.line([(5, 9), (7, 13)], fill=C["s7"])
    elif kind == 1:
        d.rectangle([7, 2, 9, 18], fill=C["s5"])
        d.rectangle([4, 6, 12, 8], fill=C["s5"])
    else:
        d.polygon([(3, 18), (4, 8), (10, 5), (12, 18)], fill=sh(C["s5"], 0.85))
        d.line([(6, 9), (9, 15)], fill=C["s7"])
    d.rectangle([2, 18, 13, 19], fill=(40, 34, 30))
    return trim(outline(pad(im)))


def dead_tree(seed):
    r = random.Random(seed)
    im = Image.new("RGBA", (40, 52))
    d = ImageDraw.Draw(im)
    bark = (44, 36, 34)
    d.polygon([(17, 51), (19, 20), (22, 20), (25, 51)], fill=bark)

    def branch(x, y, ang, ln, w):
        if ln < 3:
            return
        import math
        x2, y2 = x + ln * math.cos(ang), y - ln * math.sin(ang)
        d.line([(x, y), (x2, y2)], fill=bark, width=w)
        branch(x2, y2, ang + r.uniform(0.25, 0.6), ln * 0.65, max(1, w - 1))
        branch(x2, y2, ang - r.uniform(0.25, 0.6), ln * 0.6, max(1, w - 1))

    branch(20, 22, 1.57 + r.uniform(-0.2, 0.2), 12, 3)
    d.line([(20, 36), (9, 29)], fill=bark, width=2)
    return trim(outline(pad(im)))


def rock(w, h, seed):
    r = random.Random(seed)
    im = Image.new("RGBA", (w + 2, h + 2))
    d = ImageDraw.Draw(im)
    d.polygon([(1, h), (3, h // 3), (w // 3, 1), (2 * w // 3, 2), (w, h // 2), (w - 1, h)], fill=C["s5"])
    d.polygon([(3, h // 3), (w // 3, 1), (2 * w // 3, 2), (w // 2, h // 3)], fill=sh(C["s5"], 1.25))
    d.line([(w // 2, h // 3), (w // 2 + r.randint(-2, 2), h)], fill=C["s7"])
    return trim(outline(im))


def pike():
    im = Image.new("RGBA", (10, 34))
    d = ImageDraw.Draw(im)
    d.rectangle([4, 8, 5, 33], fill=C["rust"])
    d.ellipse([1, 1, 8, 8], fill=C["bone"])
    d.rectangle([3, 7, 6, 9], fill=sh(C["bone"], 0.85))
    d.point([(3, 4), (6, 4)], fill=C["void"])
    return trim(outline(pad(im)))


# ---------------------------------------------------------------- main

def main():
    sheet = "ba12be6a8910117fc699c32d5082ce10_84f3fa25-9753-4a3f-8b24-2a3a761b4ff5.jpg"

    # characters: one pose each -> provisional state frames
    figs =magenta_figures("Tríptico de Cavaleiros em Pixel Art.png", 1, 74)
    character("Commander", figs[1])
    character("CommanderBack", figs[2])
    character("CursedSoldier", extract("7e12e66a9361812354cd3d1187c030b5_ea967247-24e5-42ab-b30f-bfccd01bad4b.jpg", height=64))
    character("BlackKnight", extract("9e8a1bacd2c5c2e5b6e8e68498b972d7_1d64f5fe-e2f6-4a39-b2d9-37e8eedd0da6.jpg", height=90, crop=(0, 0, 1210, 1856)))
    character("Hooded", extract(sheet, height=62, crop=(1330, 50, 1530, 372), bg="flat"))
    character("CursedKnight", extract("da63c9a8d879df839b8b51867e58f786_25a316fa-0377-480a-8817-7c3f3df439d8.jpg", height=192, crop=(0, 0, 2304, 1680)))
    h = hound()
    character("Hound", h,
              idle=[hound(1), hound(1, head=1)],
              walk=[hound(0), hound(1), hound(2), hound(3)],
              attack=[hound(1, lunge=-2), hound(1, lunge=4, mouth=True), hound(1, lunge=1, mouth=True)],
              hit=[tint(hound(1), C["rune"], 0.55), hound(1)],
              death=[lean(h, -15), tint(h.transpose(Image.FLIP_TOP_BOTTOM), C["void"], 0.45)])

    # towers (level 1..4)
    watch = extract("a6b7b87be9e19d1c1ff4f087a3f29008_e3005c07-6847-4303-ba19-8f8ece1db1ef.jpg", height=80)
    for lv in range(1, 5):
        save(archer(watch, lv), SPR, "Towers", f"archer_{lv}.png")
        save(ballista(lv), SPR, "Towers", f"ballista_{lv}.png")
        save(pyre(lv), SPR, "Towers", f"pyre_{lv}.png")
        save(chapel(lv), SPR, "Towers", f"chapel_{lv}.png")

    save(projectile_arrow(), SPR, "Projectiles", "arrow.png")
    save(projectile_bolt(), SPR, "Projectiles", "bolt.png")
    save(projectile_fireball(), SPR, "Projectiles", "fireball.png")

    # map tiles
    T = os.path.join(MAP, "Tiles")
    for i in range(6):
        save(blotch_tile((27, 31, 31), [(31, 34, 32), (24, 28, 29), (33, 31, 29)], [((54, 52, 58), 2), ((44, 54, 38), 2)], i), T, f"soil_{i}.png")
    for i in range(4):
        save(blotch_tile((62, 51, 43), [(55, 45, 39), (70, 58, 48), (50, 42, 37)], [((80, 70, 58), 3), ((40, 34, 32), 3)], 100 + i), T, f"dirt_{i}.png")
        save(cobble_tile(200 + i), T, f"cobble_{i}.png")
        save(blotch_tile((31, 22, 35), [(38, 26, 44), (25, 18, 29), (44, 28, 50)], [((110, 30, 40), 1), ((16, 12, 18), 4)], 300 + i), T, f"cursed_{i}.png")
        save(decal("grit", 400 + i), T, f"grit_{i}.png")
        save(decal("bones", 500 + i), T, f"bones_{i}.png")
        save(decal("stones", 600 + i), T, f"stones_{i}.png")
        save(decal("grass", 700 + i), T, f"grass_{i}.png")
    for i in range(2):
        save(decal("blood", 800 + i), T, f"blood_{i}.png")
        save(decal("crack", 900 + i), T, f"crack_{i}.png")
    save(decal("skull", 1000), T, "skull_0.png")

    # standing props
    P = os.path.join(MAP, "Props")
    save(extract("182d48e550a9197e478cf405921e2ab2_2d6f1691-2679-4040-8d3a-02bbb6ad031d.jpg", height=192), P, "fortress.png")
    save(extract(sheet, scale=0.21, crop=(1565, 120, 1945, 370), bg="flat"), P, "ruin_0.png")
    save(extract(sheet, scale=0.21, crop=(1535, 395, 1905, 712), bg="flat"), P, "ruin_1.png")
    save(extract(sheet, scale=0.21, crop=(1535, 875, 1790, 1080), bg="flat"), P, "ruin_2.png")
    save(extract(sheet, scale=0.21, crop=(1940, 40, 2020, 360), bg="flat"), P, "torch_0.png")
    save(extract(sheet, scale=0.21, crop=(1795, 730, 1875, 1075), bg="flat"), P, "torch_1.png")
    save(extract(sheet, height=84, crop=(2025, 30, 2275, 575), bg="flat"), P, "banner_raven.png")
    save(extract(sheet, height=84, crop=(2025, 665, 2275, 1200), bg="flat"), P, "banner_skull.png")
    for i in range(3):
        save(tomb(i), P, f"tomb_{i}.png")
    for i in range(2):
        save(dead_tree(40 + i), P, f"deadtree_{i}.png")
    save(rock(18, 12, 1), P, "rock_0.png")
    save(rock(26, 16, 2), P, "rock_1.png")
    save(pike(), P, "pike_0.png")
    print("ok")


if __name__ == "__main__":
    main()

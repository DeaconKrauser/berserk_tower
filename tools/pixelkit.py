"""Pixel-art processing primitives shared by the asset tools.

Pipeline for a Syntx / Nano Banana source image:
    load -> key background (#FF00FF, painted checkerboard or flat colour) -> true alpha
    -> clean the pink fringe -> find figures (connected components) -> crop
    -> pixel downscale (palette + block majority, never bilinear/bicubic) -> outline -> PNG

Everything here is deterministic so re-running the tools reproduces the same sprites.
"""
from collections import deque

import numpy as np
from PIL import Image

VOID = (11, 10, 13)


# ---------------------------------------------------------------- io

def load_rgb(path, crop=None):
    im = Image.open(path).convert("RGB")
    if crop:
        im = im.crop(crop)
    return np.asarray(im).astype(np.int16)


def to_image(rgb, alpha):
    out = np.zeros((*alpha.shape, 4), np.uint8)
    out[..., :3] = np.clip(rgb, 0, 255)
    out[..., 3] = np.where(alpha, 255, 0)
    return Image.fromarray(out, "RGBA")


def from_image(im):
    a = np.asarray(im.convert("RGBA"))
    return a[..., :3].astype(np.int16), a[..., 3] > 127


# ---------------------------------------------------------------- background

def border_color(rgb, width=6):
    edge = np.concatenate([rgb[:width].reshape(-1, 3), rgb[-width:].reshape(-1, 3),
                           rgb[:, :width].reshape(-1, 3), rgb[:, -width:].reshape(-1, 3)])
    return np.median(edge, axis=0)


def flood_from_border(cand):
    """Pixels of `cand` connected to the image border (4-neighbourhood)."""
    H, W = cand.shape
    c = cand.ravel()
    seen = np.zeros(H * W, bool)
    q = deque()
    for i in np.concatenate([np.arange(W), (H - 1) * W + np.arange(W), np.arange(H) * W, np.arange(H) * W + W - 1]):
        if c[i] and not seen[i]:
            seen[i] = True
            q.append(int(i))
    cl, sl = c.tolist(), seen.tolist()
    while q:
        i = q.popleft()
        y, x = divmod(i, W)
        for j in (i - 1 if x else -1, i + 1 if x < W - 1 else -1, i - W if y else -1, i + W if y < H - 1 else -1):
            if j >= 0 and cl[j] and not sl[j]:
                sl[j] = True
                q.append(j)
    return np.array(sl, bool).reshape(H, W)


def magenta_dominance(rgb, bg):
    """0..1: how much a pixel looks like the chroma key (high R and B, low G), relative to the key itself."""
    r, g, b = rgb[..., 0].astype(np.float32), rgb[..., 1].astype(np.float32), rgb[..., 2].astype(np.float32)
    m = np.minimum(r, b) - g                     # key: ~250 - ~0
    key = min(bg[0], bg[2]) - bg[1]
    return np.clip(m / max(key, 1), 0, 1)


def glow_like(rgb, green_max=28, blue_ratio=0.35):
    """Painted glow over the chroma key: almost no green, plenty of blue relative to red (magenta/crimson haze).
    Real object colours (stone, metal, wood, flame cores, red runes) carry green or very little blue."""
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    return (g < green_max) & (b > blue_ratio * r) & (r + b > 110)


def key_magenta(rgb, tol=70, fringe=0.55, glow=True, keep_interior=True, interior_tol=30, glow_green=28, glow_blue=0.35):
    """True alpha from a #FF00FF-ish chroma background.

    tol: Chebyshev distance to the key colour below which a pixel is background.
    glow: magenta/crimson haze painted around effects counts as background too (the game adds Light2D instead).
    fringe: pixels touching the background whose magenta dominance is above this are eaten (pink halo).
    keep_interior: only background connected to the border is removed, so purple flames inside a figure survive;
        enclosed pockets are removed only when they are almost exactly the key colour (interior_tol).
    """
    bg = border_color(rgb)
    dist = np.abs(rgb - bg).max(axis=2)
    cand = dist < tol
    if glow:
        cand |= glow_like(rgb, glow_green, glow_blue)
    bgmask = (flood_from_border(cand) | (dist < interior_tol)) if keep_interior else cand
    fg = ~bgmask
    pink = magenta_dominance(rgb, bg) > fringe
    for _ in range(4):                           # peel the pink fringe a few pixels deep
        eat = fg & neighbours_any(~fg) & pink
        if not eat.any():
            break
        fg &= ~eat
    return fg


def key_flat(rgb, tol=40):
    bg = border_color(rgb)
    cand = np.abs(rgb - bg).sum(axis=2) < tol
    return ~flood_from_border(cand)


def key_checker(rgb, sat_max=24, margin=14):
    """Painted transparency checkerboard: low saturation, brightness within the border's range."""
    br = rgb.mean(axis=2)
    sat = rgb.max(axis=2) - rgb.min(axis=2)
    edge = np.concatenate([rgb[0], rgb[-1], rgb[:, 0], rgb[:, -1]]).mean(axis=1)
    cand = (sat < sat_max) & (br > np.percentile(edge, 1) - margin) & (br < np.percentile(edge, 99) + margin)
    return ~flood_from_border(cand)


def neighbours_any(mask):
    n = np.zeros_like(mask)
    n[1:] |= mask[:-1]
    n[:-1] |= mask[1:]
    n[:, 1:] |= mask[:, :-1]
    n[:, :-1] |= mask[:, 1:]
    return n


def despill(rgb, alpha, bg):
    """Pull remaining magenta tint out of edge pixels (G up to min(R,B) - key amount)."""
    out = rgb.copy()
    dom = magenta_dominance(rgb, bg)
    edge = alpha & neighbours_any(~alpha)
    m = edge & (dom > 0.15)
    r, g, b = out[..., 0], out[..., 1], out[..., 2]
    target = np.minimum(np.minimum(r, b), g + 40)
    out[..., 0] = np.where(m, np.minimum(r, target + 20), r)
    out[..., 2] = np.where(m, np.minimum(b, target + 20), b)
    return out


# ---------------------------------------------------------------- figures

def label(mask):
    """4-connected component labels (0 = background). Returns (labels, count)."""
    H, W = mask.shape
    lab = np.zeros(H * W, np.int32)
    m = mask.ravel().tolist()
    labl = lab.tolist()
    n = 0
    for s in range(H * W):
        if m[s] and not labl[s]:
            n += 1
            labl[s] = n
            stack = [s]
            while stack:
                i = stack.pop()
                y, x = divmod(i, W)
                for j in (i - 1 if x else -1, i + 1 if x < W - 1 else -1, i - W if y else -1, i + W if y < H - 1 else -1):
                    if j >= 0 and m[j] and not labl[j]:
                        labl[j] = n
                        stack.append(j)
    return np.array(labl, np.int32).reshape(H, W), n


def figures(alpha, min_area=400, merge_gap=24, step=4):
    """Bounding boxes (x0, y0, x1, y1) of separate figures, merging fragments closer than merge_gap px.
    Works on a `step`-downsampled mask for speed; boxes are returned in full resolution."""
    small = alpha[::step, ::step]
    lab, n = label(small)
    boxes = []
    for k in range(1, n + 1):
        ys, xs = np.nonzero(lab == k)
        if len(ys) * step * step < min_area:
            continue
        boxes.append([xs.min() * step, ys.min() * step, (xs.max() + 1) * step, (ys.max() + 1) * step, len(ys)])
    merged = True
    while merged:
        merged = False
        for i in range(len(boxes)):
            for j in range(i + 1, len(boxes)):
                a, b = boxes[i], boxes[j]
                if a[0] - merge_gap <= b[2] and b[0] - merge_gap <= a[2] and a[1] - merge_gap <= b[3] and b[1] - merge_gap <= a[3]:
                    boxes[i] = [min(a[0], b[0]), min(a[1], b[1]), max(a[2], b[2]), max(a[3], b[3]), a[4] + b[4]]
                    del boxes[j]
                    merged = True
                    break
            if merged:
                break
    H, W = alpha.shape
    out = []
    for x0, y0, x1, y1, _ in boxes:
        x0, y0, x1, y1 = max(0, x0 - step), max(0, y0 - step), min(W, x1 + step), min(H, y1 + step)
        sub = alpha[y0:y1, x0:x1]
        ys, xs = np.nonzero(sub)
        out.append((x0 + xs.min(), y0 + ys.min(), x0 + xs.max() + 1, y0 + ys.max() + 1))
    return sorted(out, key=lambda b: (round(b[3] / 400), b[0]))


def bbox(alpha):
    ys, xs = np.nonzero(alpha)
    return xs.min(), ys.min(), xs.max() + 1, ys.max() + 1


# ---------------------------------------------------------------- palette + downscale

def kmeans_palette(rgb, alpha, k=40, iters=12, seed=3):
    px = rgb[alpha].reshape(-1, 3).astype(np.float32)
    if len(px) == 0:
        return np.zeros((1, 3), np.float32)
    rng = np.random.default_rng(seed)
    sample = px[rng.choice(len(px), min(len(px), 60000), replace=False)]
    # k-means++ style init: spread over luminance order
    order = np.argsort(sample.sum(axis=1))
    cent = sample[order[np.linspace(0, len(order) - 1, k).astype(int)]].copy()
    for _ in range(iters):
        d = ((sample[:, None, :] - cent[None, :, :]) ** 2).sum(axis=2)
        idx = d.argmin(axis=1)
        for c in range(k):
            sel = sample[idx == c]
            if len(sel):
                cent[c] = sel.mean(axis=0)
    return cent


def nearest_index(rgb, palette):
    flat = rgb.reshape(-1, 3).astype(np.float32)
    out = np.empty(len(flat), np.int32)
    for s in range(0, len(flat), 200000):
        chunk = flat[s:s + 200000]
        out[s:s + 200000] = ((chunk[:, None, :] - palette[None, :, :]) ** 2).sum(axis=2).argmin(axis=1)
    return out.reshape(rgb.shape[:2])


def pixel_downscale(rgb, alpha, size, k=40, palette=None, coverage=0.5, center_weight=2.0):
    """Downscale without inventing colours: every output pixel takes the most frequent palette colour
    of its source block (block centre counts double), alpha by coverage. Returns (rgb, alpha, palette)."""
    W2, H2 = size
    H, W = alpha.shape
    if palette is None:
        palette = kmeans_palette(rgb, alpha, k)
    K = len(palette)
    idx = nearest_index(rgb, palette)
    ys = np.minimum((np.arange(H) * H2) // H, H2 - 1)
    xs = np.minimum((np.arange(W) * W2) // W, W2 - 1)
    cell = (ys[:, None] * W2 + xs[None, :])
    # weight: pixels near the block centre count more (keeps the "nearest" character, stays robust to noise)
    fy = ((np.arange(H) * H2) / H) % 1.0
    fx = ((np.arange(W) * W2) / W) % 1.0
    wy = 1 + (center_weight - 1) * (1 - np.abs(fy - 0.5) * 2)
    wx = 1 + (center_weight - 1) * (1 - np.abs(fx - 0.5) * 2)
    w = (wy[:, None] * wx[None, :]).astype(np.float32)
    a = alpha.ravel()
    counts = np.bincount((cell.ravel()[a] * K + idx.ravel()[a]), weights=w.ravel()[a], minlength=W2 * H2 * K).reshape(H2 * W2, K)
    cov = np.bincount(cell.ravel(), weights=alpha.ravel().astype(np.float32), minlength=W2 * H2) / np.bincount(cell.ravel(), minlength=W2 * H2)
    best = counts.argmax(axis=1)
    out_rgb = palette[best].reshape(H2, W2, 3)
    out_a = (cov >= coverage).reshape(H2, W2)
    return np.round(out_rgb).astype(np.int16), out_a, palette


def area_downscale(rgb, alpha, size, k=32, palette=None, coverage=0.5):
    """Premultiplied area average, then every pixel snaps to a palette taken from the source.
    No colour is invented and alpha stays hard, so the result is crisp pixel art (no blur).
    This is the default: plain nearest-neighbour turns 3x+ reductions of detailed art into noise."""
    if palette is None:
        palette = kmeans_palette(rgb, alpha, k)
    af = alpha.astype(np.float32)

    def rs(ch):
        return np.asarray(Image.fromarray(ch.astype(np.float32)).resize(size, Image.BOX))
    A = rs(af)
    avg = np.stack([rs(rgb[..., c] * af) / np.maximum(A, 1e-6) for c in range(3)], axis=2)
    idx = nearest_index(avg, palette)
    return np.round(palette[idx]).astype(np.int16), A >= coverage, palette


def nearest_downscale(rgb, alpha, size):
    im = to_image(rgb, alpha).resize(size, Image.NEAREST)
    return from_image(im)


def add_outline(rgb, alpha, color=VOID, min_luma=55):
    """1 px outline outside the silhouette, only where the edge pixel is not already dark."""
    rgb = rgb.copy()
    luma = rgb.mean(axis=2)
    bright = alpha & (luma > min_luma)
    ring = ~alpha & neighbours_any(bright)
    rgb[ring] = color
    return rgb, alpha | ring


def remove_specks(alpha, min_size=3):
    lab, n = label(alpha)
    out = alpha.copy()
    for k in range(1, n + 1):
        if (lab == k).sum() < min_size:
            out[lab == k] = False
    return out


def trim(rgb, alpha):
    x0, y0, x1, y1 = bbox(alpha)
    return rgb[y0:y1, x0:x1], alpha[y0:y1, x0:x1]


def downscale(rgb, alpha, size, method="area", **kw):
    if method == "nearest":
        return (*nearest_downscale(rgb, alpha, size), None)
    if method == "mode":
        return pixel_downscale(rgb, alpha, size, **kw)
    return area_downscale(rgb, alpha, size, **kw)


def scale_to_height(rgb, alpha, height, **kw):
    H, W = alpha.shape
    f = height / H
    return downscale(rgb, alpha, (max(1, round(W * f)), max(1, height)), **kw)


def nn_upscale(im, n):
    return im.resize((im.width * n, im.height * n), Image.NEAREST)


def scale2x(rgb, alpha):
    """EPX / Scale2x: doubles pixel art without inventing colours (smoother diagonals than nearest)."""
    H, W = alpha.shape
    # encode colour+alpha as one int so equality tests are exact
    key = (rgb[..., 0].astype(np.int64) << 16) | (rgb[..., 1].astype(np.int64) << 8) | rgb[..., 2].astype(np.int64)
    key = np.where(alpha, key, -1)
    P = np.pad(key, 1, mode="edge")
    A, B, C, D = P[:-2, 1:-1], P[1:-1, :-2], P[1:-1, 2:], P[2:, 1:-1]   # up, left, right, down
    E = key
    e0 = np.where((B == A) & (B != D) & (A != C), A, E)
    e1 = np.where((A == C) & (A != B) & (C != D), C, E)
    e2 = np.where((B == D) & (B != A) & (D != C), B, E)
    e3 = np.where((D == C) & (D != B) & (C != A), D, E)
    out = np.zeros((H * 2, W * 2), np.int64)
    out[0::2, 0::2], out[0::2, 1::2], out[1::2, 0::2], out[1::2, 1::2] = e0, e1, e2, e3
    a2 = out >= 0
    o = np.where(a2, out, 0)
    rgb2 = np.stack([(o >> 16) & 255, (o >> 8) & 255, o & 255], axis=2).astype(np.int16)
    return rgb2, a2


def rgb_to_hsv(rgb):
    r, g, b = [rgb[..., i].astype(np.float32) / 255 for i in range(3)]
    mx, mn = np.maximum(np.maximum(r, g), b), np.minimum(np.minimum(r, g), b)
    d = mx - mn
    h = np.zeros_like(mx)
    m = d > 1e-6
    rm, gm, bm = m & (mx == r), m & (mx == g) & (mx != r), m & (mx == b) & (mx != r) & (mx != g)
    h[rm] = ((g - b)[rm] / d[rm]) % 6
    h[gm] = (b - r)[gm] / d[gm] + 2
    h[bm] = (r - g)[bm] / d[bm] + 4
    h = h * 60
    s = np.where(mx > 1e-6, d / np.maximum(mx, 1e-6), 0)
    return h, s, mx


def hsv_to_rgb(h, s, v):
    h = (h % 360) / 60
    i = np.floor(h).astype(int) % 6
    f = h - np.floor(h)
    p, q, t = v * (1 - s), v * (1 - s * f), v * (1 - s * (1 - f))
    r = np.choose(i, [v, q, p, p, t, v])
    g = np.choose(i, [t, v, v, q, p, p])
    b = np.choose(i, [p, p, t, v, v, q])
    return np.clip(np.stack([r, g, b], axis=2) * 255, 0, 255).astype(np.int16)


def inpaint(rgb, known, region, seed=0, noise=4.0):
    """Fills region & ~known by propagating the average colour of known 4-neighbours, layer by layer."""
    rgb = rgb.astype(np.float32).copy()
    K = known.copy()
    todo = region & ~K
    rng = np.random.default_rng(seed)
    while todo.any():
        acc = np.zeros_like(rgb)
        cnt = np.zeros(K.shape, np.float32)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            sk = np.roll(K, (dy, dx), axis=(0, 1))
            sr = np.roll(rgb, (dy, dx), axis=(0, 1))
            acc += sr * sk[..., None]
            cnt += sk
        new = todo & (cnt > 0)
        if not new.any():
            break
        rgb[new] = acc[new] / cnt[new][:, None] + rng.normal(0, noise, (int(new.sum()), 3))
        K |= new
        todo &= ~new
    return np.clip(rgb, 0, 255).astype(np.int16)


def depink(rgb, alpha, thresh=0.42):
    """Replaces leftover chroma-key pixels inside a figure (JPEG pockets, pink rims) with nearby real colours."""
    dom = magenta_dominance(rgb, np.array([250, 5, 250]))
    pink = alpha & (dom > thresh)
    if not pink.any():
        return rgb
    return inpaint(rgb, alpha & ~pink, alpha, noise=2.0)


def native_grid(rgb, alpha, pitch, phase_x=0.0, phase_y=0.0):
    """Recovers the native pixel grid of upscaled pixel art: one output pixel per source cell
    (median colour of the cell's central area, alpha by majority). No interpolation, no new colours."""
    H, W = alpha.shape
    nx, ny = int((W - phase_x) / pitch), int((H - phase_y) / pitch)
    out = np.zeros((ny, nx, 3), np.int16)
    oa = np.zeros((ny, nx), bool)
    k = max(1, int(pitch * 0.25))
    for j in range(ny):
        cy = int(phase_y + pitch * (j + 0.5))
        y0, y1 = max(0, cy - k), min(H, cy + k + 1)
        for i in range(nx):
            cx = int(phase_x + pitch * (i + 0.5))
            x0, x1 = max(0, cx - k), min(W, cx + k + 1)
            win = alpha[y0:y1, x0:x1]
            if win.mean() > 0.5:
                oa[j, i] = True
                out[j, i] = np.median(rgb[y0:y1, x0:x1][win], axis=0)
    return out, oa

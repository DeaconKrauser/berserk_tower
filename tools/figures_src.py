"""Clean full-resolution figures of every animated character, straight from Assets/Art/Source.

    figure(name) -> (rgb, alpha) cropped to the figure (y down, origin top-left)
"""
import numpy as np

import sources
from pixelkit import depink, figures, key_checker, key_flat, key_magenta, label, load_rgb, trim


def _largest(alpha, boxes):
    return max(boxes, key=lambda b: alpha[b[1]:b[3], b[0]:b[2]].sum())


def _pick(rgb, alpha, index, gap=24):
    figs = sorted(figures(alpha, merge_gap=gap), key=lambda b: b[0])
    x0, y0, x1, y1 = figs[index]
    r, a = trim(rgb[y0:y1, x0:x1], alpha[y0:y1, x0:x1])
    return depink(r, a), a


def _main_component(rgb, alpha):
    """Keeps the biggest connected blob plus fragments close to it (sword tips, sparks)."""
    small = alpha[::2, ::2]
    lab, n = label(small)
    if n <= 1:
        return trim(rgb, alpha)
    sizes = np.bincount(lab.ravel())
    sizes[0] = 0
    keep_lab = np.argmax(sizes)
    ys, xs = np.nonzero(lab == keep_lab)
    y0, y1, x0, x1 = ys.min(), ys.max(), xs.min(), xs.max()
    keep = np.zeros_like(small)
    for k in range(1, n + 1):
        if sizes[k] < 6:
            continue
        ky, kx = np.nonzero(lab == k)
        if kx.max() >= x0 - 8 and kx.min() <= x1 + 8 and ky.max() >= y0 - 8 and ky.min() <= y1 + 8:
            keep |= lab == k
    full = np.repeat(np.repeat(keep, 2, axis=0), 2, axis=1)[:alpha.shape[0], :alpha.shape[1]]
    return trim(rgb, alpha & full)


def figure(name):
    if name == "commander":
        rgb = load_rgb(sources.path("commander_sheet"))
        return _pick(rgb, key_magenta(rgb), 1, gap=4)
    if name == "commander_back":
        rgb = load_rgb(sources.path("commander_sheet"))
        return _pick(rgb, key_magenta(rgb), 2, gap=4)
    if name == "swordsman":
        rgb = load_rgb(sources.path("skin_black_swordsman"))
        return _pick(rgb, key_magenta(rgb), 0)
    if name == "swordsman_back":
        rgb = load_rgb(sources.path("skin_black_swordsman"))
        return _pick(rgb, key_magenta(rgb), 1)
    if name == "soldier":
        rgb = load_rgb(sources.path("cursed_soldier"))
        return _main_component(rgb, key_checker(rgb))
    if name == "black_knight":
        rgb = load_rgb(sources.path("black_knight"), crop=(0, 0, 1210, 1856))
        return _main_component(rgb, key_checker(rgb))
    if name == "hooded":
        rgb = load_rgb(sources.path("allies_sheet"), crop=(1330, 50, 1530, 372))
        return _main_component(rgb, key_flat(rgb, tol=48))
    if name == "warlock_base":
        rgb = load_rgb(sources.path("allies_sheet"), crop=(1080, 40, 1260, 372))
        return _main_component(rgb, key_flat(rgb, tol=48))
    if name == "boss_knight":
        rgb = load_rgb(sources.path("boss_cursed_knight"), crop=(0, 0, 2304, 1680))
        return _main_component(rgb, key_checker(rgb))
    if name == "ghoul":
        rgb = load_rgb(sources.path("hellhound_static"))
        alpha = key_magenta(rgb)
        lab, n = label(alpha[::4, ::4])
        sizes = np.bincount(lab.ravel())
        sizes[0] = 0
        # the left (front-right facing) hound is the component touching the left part of the sheet
        best = None
        for k in np.argsort(sizes)[::-1][:4]:
            ys, xs = np.nonzero(lab == k)
            if xs.min() * 4 < 300:
                best = k
                break
        keep = np.repeat(np.repeat(lab == best, 4, axis=0), 4, axis=1)[:alpha.shape[0], :alpha.shape[1]]
        return trim(rgb, alpha & keep)
    if name == "swamp_guardian":
        rgb = load_rgb(sources.path("swamp_guardian"))
        return _pick(rgb, key_magenta(rgb), 0)
    raise KeyError(name)


def split_components(alpha, step=2, big=0.08):
    """Separate figures by pixel connectivity (not by bounding boxes): big blobs become figures,
    small fragments join the closest figure. Returns full-resolution masks sorted left to right."""
    small = alpha[::step, ::step]
    lab, n = label(small)
    sizes = np.bincount(lab.ravel())
    sizes[0] = 0
    bigs = [k for k in range(1, n + 1) if sizes[k] >= big * sizes.max()]
    boxes = {}
    for k in range(1, n + 1):
        ys, xs = np.nonzero(lab == k)
        boxes[k] = (xs.min(), ys.min(), xs.max(), ys.max())
    owner = {k: k for k in bigs}
    for k in range(1, n + 1):
        if k in owner or sizes[k] < 3:
            continue
        bx = boxes[k]

        def dist(b):
            dx = max(0, b[0] - bx[2], bx[0] - b[2])
            dy = max(0, b[1] - bx[3], bx[1] - b[3])
            return dx * dx + dy * dy
        owner[k] = min(bigs, key=lambda b: dist(boxes[b]))
    masks = []
    for b in sorted(bigs, key=lambda b: boxes[b][0]):
        m = np.isin(lab, [k for k, o in owner.items() if o == b])
        full = np.repeat(np.repeat(m, step, axis=0), step, axis=1)[:alpha.shape[0], :alpha.shape[1]]
        masks.append(alpha & full)
    return masks


def wolf_frames():
    """Six run frames (two rows of three). The drawn ground lines are removed; each frame keeps its
    position inside its third of the strip and its height above the ground line (the run bounce)."""
    rgb = load_rgb(sources.path("hellhound_run"))
    alpha = key_magenta(rgb)
    lum = rgb.mean(axis=2)
    H, W = alpha.shape
    dark_rows = ((lum < 60) & alpha).mean(axis=1) > 0.5
    ground = [int(y) for y in np.nonzero(dark_rows)[0]]
    for y in ground:
        alpha[max(0, y - 4):y + 5] = False
    out = []
    for r0, r1 in ((0, H // 2), (H // 2, H)):
        line = [y for y in ground if r0 <= y < r1]
        ground_y = (min(line) if line else r1) - r0
        masks = split_components(alpha[r0:r1])
        masks = sorted(masks, key=lambda m: np.nonzero(m.any(axis=0))[0].min())[:3]
        for i, m in enumerate(masks):
            ys, xs = np.nonzero(m)
            x0, y0, x1, y1 = xs.min(), ys.min(), xs.max() + 1, ys.max() + 1
            cell_x = i * W // 3
            out.append((rgb[r0 + y0:r0 + y1, x0:x1], m[y0:y1, x0:x1], (int(x0 - cell_x), int(ground_y - y1))))
    return out

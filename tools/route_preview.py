"""Renders every layout of Assets/Editor/Content/routes.json with the game's spline (centripetal Catmull-Rom)
and reports the narrowest gap between two different stretches of road (towers need ~1.6 units between edges).

    python tools/route_preview.py [out_dir]
"""
import json
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ROUTES = os.path.join(ROOT, "Assets", "Editor", "Content", "routes.json")


def catmull(p0, p1, p2, p3, t):
    def d(a, b):
        return math.hypot(b[0] - a[0], b[1] - a[1]) ** 0.5 + 1e-4
    t0, t1 = 0, d(p0, p1)
    t2 = t1 + d(p1, p2)
    t3 = t2 + d(p2, p3)
    u = t1 + (t2 - t1) * t

    def lerp(a, b, ta, tb):
        return ((tb - u) / (tb - ta) * a[0] + (u - ta) / (tb - ta) * b[0], (tb - u) / (tb - ta) * a[1] + (u - ta) / (tb - ta) * b[1])
    a1, a2, a3 = lerp(p0, p1, t0, t1), lerp(p1, p2, t1, t2), lerp(p2, p3, t2, t3)
    b1, b2 = lerp(a1, a2, t0, t2), lerp(a2, a3, t1, t3)
    return lerp(b1, b2, t1, t2)


def spline(ctrl):
    out = []
    n = len(ctrl)
    for i in range(n - 1):
        p0 = ctrl[i - 1] if i > 0 else (2 * ctrl[0][0] - ctrl[1][0], 2 * ctrl[0][1] - ctrl[1][1])
        p1, p2 = ctrl[i], ctrl[i + 1]
        p3 = ctrl[i + 2] if i + 2 < n else (2 * ctrl[-1][0] - ctrl[-2][0], 2 * ctrl[-1][1] - ctrl[-2][1])
        steps = max(4, int(math.ceil(math.hypot(p2[0] - p1[0], p2[1] - p1[1]) / 0.1)))
        for k in range(steps):
            out.append(catmull(p0, p1, p2, p3, k / steps))
    out.append(tuple(ctrl[-1]))
    return np.array(out)


def length(p):
    return float(np.sum(np.hypot(*np.diff(p, axis=0).T)))


def min_gap(paths, half):
    """Smallest distance between points of the road far apart along the same path (or on different paths that are not merging)."""
    best = 99
    for pi, p in enumerate(paths):
        cum = np.concatenate([[0], np.cumsum(np.hypot(*np.diff(p, axis=0).T))])
        sub = p[::4]
        cs = cum[::4]
        d = np.hypot(sub[:, None, 0] - sub[None, :, 0], sub[:, None, 1] - sub[None, :, 1])
        along = np.abs(cs[:, None] - cs[None, :])
        d[along < 6.0] = 99
        best = min(best, d.min())
    return best - 2 * half


def render(route, out, W=960, H=540):
    im = Image.new("RGB", (W, H), (40, 34, 32))
    d = ImageDraw.Draw(im)

    def px(x, y):
        return ((x + 15) / 30 * W, (8.4375 - y) / 16.875 * H)
    # HUD-covered regions
    for (x0, y0, x1, y1) in ((-15, 6.9, 15, 8.44), (-7.4, -8.44, 8.2, -5.5), (-15, -8.44, -7.9, -5.85), (9.6, 6.0, 15, 8.44)):
        a, b = px(x0, y1), px(x1, y0)
        d.rectangle([a, b], fill=(70, 40, 40))
    for z in route["zones"]:
        cx, cy = px(*z["c"])
        r = z["r"] / 30 * W
        col = {"Water": (20, 40, 60), "Cursed": (70, 30, 90), "Graveyard": (60, 60, 60), "Ruins": (80, 80, 60)}.get(z["kind"], (90, 90, 90))
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=col)
    half = route["half"]
    paths = [spline(p) for p in route["paths"]]
    for p in paths:
        pts = [px(x, y) for x, y in p]
        d.line(pts, fill=(140, 130, 140), width=int(2 * half / 30 * W))
        d.line(pts, fill=(200, 200, 90), width=1)
    fx, fy = px(*route["fortress"])
    d.rectangle([fx - 18, fy - 60, fx + 18, fy], fill=(150, 40, 40))
    cx, cy = px(*route["commander"])
    d.ellipse([cx - 5, cy - 5, cx + 5, cy + 5], fill=(240, 160, 60))
    for pr in route["props"]:
        x, y = px(*pr["at"])
        d.rectangle([x - 3, y - 3, x + 3, y + 3], fill=(120, 200, 120))
    gap = min_gap(paths, half)
    lens = ", ".join(f"{length(p):.1f}" for p in paths)
    d.text((8, 8), f"{route['name']} ({route['layout']}) len {lens} min gap {gap:.2f}", fill=(255, 255, 255))
    im.save(out)
    return gap, lens


if __name__ == "__main__":
    out_dir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "Assets", "Art", "Generated", "_debug", "routes")
    os.makedirs(out_dir, exist_ok=True)
    data = json.load(open(ROUTES, encoding="utf-8"))
    for m, routes in data.items():
        for r in routes:
            gap, lens = render(r, os.path.join(out_dir, f"{r['id']}.png"))
            print(f"{r['id']:14s} length {lens:12s} min free gap between road stretches {gap:.2f}")

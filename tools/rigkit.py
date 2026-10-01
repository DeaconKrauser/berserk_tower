"""Cutout-rig engine: turns one static pose into separately animatable parts.

A character definition (rig_defs.py) gives, in figure-normalised coordinates (u, v in 0..1, v down):
    parts: name -> dict(poly=[(u, v), ...], pivot=(u, v), parent=name|None, z=int, fill=bool, catch=bool)
The engine:
  1. claims pixels front-to-back (higher z first): a part owns the pixels of its polygon not owned by a part in front;
     the part flagged catch=True also takes every leftover pixel of the silhouette;
  2. inpaints, for parts with fill=True, the area of their polygon hidden by parts in front (so moving a front part
     never reveals a hole), using only the part's own colours;
  3. downscales every part on the SAME pixel grid and palette (area average + palette snap, hard alpha),
     with a one-pixel overlap so joints never show seams;
  4. writes Assets/Art/Animations/<Name>/parts/*.png and rig.json (pivots, hierarchy, sorting).
Motion (clips.json) comes from rig_motion.py; preview() renders poses with the same maths Unity uses.
"""
import json
import math
import os

import numpy as np
from PIL import Image, ImageDraw

from pixelkit import area_downscale, inpaint, kmeans_palette, nearest_index, neighbours_any, to_image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ANIM_DIR = os.path.join(ROOT, "Assets", "Art", "Animations")


def poly_mask(shape, poly_uv, W, H):
    """poly_uv: one polygon [(u, v), ...] or a list of polygons."""
    m = Image.new("L", (shape[1], shape[0]), 0)
    d = ImageDraw.Draw(m)
    polys = poly_uv if poly_uv and isinstance(poly_uv[0][0], (list, tuple)) else [poly_uv]
    for poly in polys:
        d.polygon([(u * W, v * H) for u, v in poly], fill=255)
    return np.asarray(m) > 0


def dilate(mask, n):
    for _ in range(n):
        mask = mask | neighbours_any(mask)
    return mask


class Rig:
    def __init__(self, name, rgb, alpha, height, parts, palette_k=40, ppu=32, feet=None, extra_palette=None, work=4):
        # work at `work` x the target resolution: same result, a fraction of the inpainting cost
        if alpha.shape[0] > height * work * 1.25:
            f = height * work / alpha.shape[0]
            size = (max(1, round(alpha.shape[1] * f)), height * work)
            af = alpha.astype(np.float32)

            def rs(ch):
                return np.asarray(Image.fromarray(ch.astype(np.float32)).resize(size, Image.BOX))
            A = rs(af)
            rgb = np.stack([rs(rgb[..., c] * af) / np.maximum(A, 1e-6) for c in range(3)], axis=2).astype(np.int16)
            alpha = A >= 0.5
        self.name, self.rgb, self.alpha, self.parts = name, rgb, alpha, parts
        self.H, self.W = alpha.shape
        self.height = height
        self.scale = height / self.H
        self.ppu = ppu
        self.out_w = math.ceil(self.W * self.scale)
        self.out_h = height
        # feet: (u, v) of the ground point under the character (pivot of the root), default bottom centre
        self.feet = feet or (0.5, 1.0)
        pal_rgb = rgb if extra_palette is None else np.concatenate([rgb.reshape(-1, 3), extra_palette.reshape(-1, 3)])[:, None, :]
        pal_a = alpha if extra_palette is None else np.ones(pal_rgb.shape[:2], bool)
        self.palette = kmeans_palette(pal_rgb, pal_a, palette_k)
        self.images = {}
        self.rects = {}

    # ---------------------------------------------------------------- cutting
    def cut(self):
        order = sorted([n for n in self.parts if not self.parts[n].get("joint")], key=lambda n: -self.parts[n]["z"])  # front first
        claimed = np.zeros_like(self.alpha)
        masks, polys = {}, {}
        for n in order:
            p = self.parts[n]
            if p.get("poly"):
                polys[n] = poly_mask(self.alpha.shape, p["poly"], self.W, self.H)
            else:
                polys[n] = np.zeros_like(self.alpha)
            own = polys[n] & self.alpha & ~claimed
            masks[n] = own
            claimed |= own
        # leftover silhouette pixels go to the nearest part (grown ring by ring, front parts win ties),
        # so a stray pixel never rides on a far-away part
        left = self.alpha & ~claimed
        while left.any():
            grew = False
            for n in order:
                new = neighbours_any(masks[n]) & left
                if new.any():
                    masks[n] |= new
                    polys[n] |= new
                    left &= ~new
                    grew = True
            if not grew:
                break
        margin = max(1, round(0.7 / self.scale))                              # ~0.7 output px of overlap
        for n in order:
            p = self.parts[n]
            own = masks[n]
            if not own.any():
                raise ValueError(f"{self.name}: part {n} is empty")
            rgb = self.rgb
            shape = own
            if p.get("fill", False):
                hidden = polys[n] & ~own & (self.alpha if not p.get("fill_outside") else True)
                if p.get("fill_mask") is not None:
                    hidden &= poly_mask(self.alpha.shape, p["fill_mask"], self.W, self.H)
                rgb = inpaint(self.rgb, own, own | hidden, seed=len(n))
                shape = own | hidden
            # overlap into neighbours (real pixels), never outside the silhouette
            shape = shape | (dilate(own, margin) & self.alpha)
            self._downscale_part(n, rgb, shape)
        return self

    def _downscale_part(self, n, rgb, mask):
        ys, xs = np.nonzero(mask)
        s = self.scale
        ox0, oy0 = int(math.floor(xs.min() * s)), int(math.floor(ys.min() * s))
        ox1, oy1 = int(math.ceil((xs.max() + 1) * s)), int(math.ceil((ys.max() + 1) * s))
        sx0, sy0 = int(round(ox0 / s)), int(round(oy0 / s))
        sx1, sy1 = min(self.W, int(round(ox1 / s))), min(self.H, int(round(oy1 / s)))
        crop_rgb = rgb[sy0:sy1, sx0:sx1]
        crop_a = mask[sy0:sy1, sx0:sx1]
        size = (ox1 - ox0, oy1 - oy0)
        r, a, _ = area_downscale(crop_rgb, crop_a, size, palette=self.palette, coverage=0.45)
        if not a.any():
            a[a.shape[0] // 2, a.shape[1] // 2] = True
        self.images[n] = to_image(r, a)
        self.rects[n] = (ox0, oy0, size[0], size[1])

    # ---------------------------------------------------------------- export
    def px(self, uv):
        return (uv[0] * self.W * self.scale, uv[1] * self.H * self.scale)

    def export(self, clips=None, extra=None):
        d = os.path.join(ANIM_DIR, self.name)
        pd = os.path.join(d, "parts")
        os.makedirs(pd, exist_ok=True)
        for f in os.listdir(pd):
            if f.endswith(".png"):
                os.remove(os.path.join(pd, f))
        fx, fy = self.px(self.feet)
        data = {"name": self.name, "ppu": self.ppu, "height": self.out_h, "width": self.out_w,
                "feet": [round(fx, 2), round(fy, 2)], "parts": []}
        for n, p in sorted(self.parts.items(), key=lambda kv: kv[1]["z"]):
            pvx, pvy = self.px(p["pivot"])
            entry = {"name": n, "parent": p.get("parent"), "z": p["z"], "pivot": [round(pvx, 2), round(pvy, 2)]}
            if not p.get("joint"):
                self.images[n].save(os.path.join(pd, f"{n}.png"))
                x, y, w, h = self.rects[n]
                entry.update(sprite=f"parts/{n}.png", rect=[x, y, w, h])
            data["parts"].append(entry)
        if extra:
            data.update(extra)
        with open(os.path.join(d, "rig.json"), "w", encoding="utf-8") as f:
            json.dump(data, f, indent=1)
        if clips is not None:
            with open(os.path.join(d, "clips.json"), "w", encoding="utf-8") as f:
                json.dump(clips, f, indent=1)
        return data

    # ---------------------------------------------------------------- preview
    def pose(self, clip, t):
        """Per-part (rot_deg, dx, dy, sx, sy) at time t (Unity convention: CCW positive, y up, px)."""
        out = {}
        curves = clip.get("curves", {})
        for n in self.parts:
            c = curves.get(n, {})
            rot = sample(c.get("rot"), t, 0.0)
            pos = sample(c.get("pos"), t, (0.0, 0.0))
            sc = sample(c.get("scale"), t, (1.0, 1.0))
            out[n] = (rot, pos[0], pos[1], sc[0], sc[1])
        root = curves.get("_root", {})
        out["_root"] = (sample(root.get("rot"), t, 0.0), *sample(root.get("pos"), t, (0.0, 0.0)), *sample(root.get("scale"), t, (1.0, 1.0)))
        return out

    def render(self, clip=None, t=0.0, zoom=4, canvas=(None, None), images=None):
        """Composites the rig like Unity does (parent chain of joints, sprites offset from their pivot)."""
        images = images or self.images
        pose = self.pose(clip, t) if clip else {n: (0, 0, 0, 1, 1) for n in list(self.parts) + ["_root"]}
        fx, fy = self.px(self.feet)
        cw = canvas[0] or int(self.out_w * 2.2)
        ch = canvas[1] or int(self.out_h * 1.6)
        ox, oy = cw * 0.45, ch * 0.85        # where the feet land on the canvas
        out = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))

        def mat(n):
            """2x3 affine (image coords, y down) mapping joint-local px -> canvas px."""
            p = self.parts[n]
            pv = np.array(self.px(p["pivot"]))
            if p.get("parent"):
                parent = mat(p["parent"])
                ppv = np.array(self.px(self.parts[p["parent"]]["pivot"]))
            else:
                r, dx, dy, sx, sy = pose["_root"]
                parent = affine(ox, oy, r, sx, sy)
                parent = parent @ translate(dx, -dy)
                ppv = np.array([fx, fy])
            r, dx, dy, sx, sy = pose[n]
            local = translate(pv[0] - ppv[0] + dx, pv[1] - ppv[1] - dy) @ rot_scale(r, sx, sy)
            return parent @ local

        for n, p in sorted(self.parts.items(), key=lambda kv: kv[1]["z"]):
            if p.get("joint") or n not in images:
                continue
            im = images[n]
            x, y, w, h = self.rects[n]
            pv = np.array(self.px(p["pivot"]))
            M = mat(n) @ translate(x - pv[0], y - pv[1])          # sprite pixel -> canvas
            if abs(np.linalg.det(M[:2, :2])) < 1e-4:
                continue
            inv = np.linalg.inv(M)
            out_im = im.transform((cw, ch), Image.AFFINE, tuple(inv[:2].ravel()), resample=Image.NEAREST)
            out.alpha_composite(out_im)
        return out.resize((cw * zoom, ch * zoom), Image.NEAREST)

    def overlay(self, path, max_h=760):
        """Debug: the source figure with every part's polygon and pivot."""
        im = to_image(self.rgb, self.alpha).convert("RGBA")
        s = max_h / self.H
        im = im.resize((round(self.W * s), max_h), Image.NEAREST)
        bg = Image.new("RGBA", im.size, (70, 64, 60, 255))
        bg.alpha_composite(im)
        d = ImageDraw.Draw(bg)
        cols = [(255, 80, 80), (80, 255, 80), (80, 160, 255), (255, 255, 80), (255, 80, 255), (80, 255, 255),
                (255, 160, 60), (180, 120, 255), (255, 255, 255), (120, 255, 160), (255, 120, 180)]
        for i, (n, p) in enumerate(sorted(self.parts.items(), key=lambda kv: kv[1]["z"])):
            c = cols[i % len(cols)]
            if p.get("poly"):
                polys = p["poly"] if isinstance(p["poly"][0][0], (list, tuple)) else [p["poly"]]
                for poly in polys:
                    pts = [(u * bg.width, v * bg.height) for u, v in poly]
                    d.line(pts + [pts[0]], fill=c + (255,), width=2)
            px_, py_ = p["pivot"][0] * bg.width, p["pivot"][1] * bg.height
            d.ellipse([px_ - 4, py_ - 4, px_ + 4, py_ + 4], outline=c + (255,), width=2)
            d.text((px_ + 6, py_ - 6), n, fill=c + (255,))
        bg.save(path)


def translate(x, y):
    return np.array([[1, 0, x], [0, 1, y], [0, 0, 1]], np.float64)


def rot_scale(deg, sx=1.0, sy=1.0):
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    # image coords (y down): a visual CCW rotation by `deg`
    return np.array([[c * sx, s * sy, 0], [-s * sx, c * sy, 0], [0, 0, 1]], np.float64)


def affine(x, y, deg, sx, sy):
    return translate(x, y) @ rot_scale(deg, sx, sy)


def sample(keys, t, default):
    """Linear sample of [[t, v...], ...] (looping handled by the caller)."""
    if not keys:
        return default
    if t <= keys[0][0]:
        k = keys[0]
    elif t >= keys[-1][0]:
        k = keys[-1]
    else:
        for a, b in zip(keys, keys[1:]):
            if a[0] <= t <= b[0]:
                f = (t - a[0]) / max(1e-6, b[0] - a[0])
                f = f * f * (3 - 2 * f)                                # smooth like Unity's auto tangents
                vals = [a[i] + (b[i] - a[i]) * f for i in range(1, len(a))]
                return vals[0] if len(vals) == 1 else tuple(vals)
    vals = k[1:]
    return vals[0] if len(vals) == 1 else tuple(vals)


def contact_sheet(rig, clips, path, frames=6, zoom=3, names=None):
    names = names or list(clips)
    rows = []
    for cname in names:
        clip = clips[cname]
        L = clip["length"]
        ims = [rig.render(clip, L * i / (frames - (0 if clip.get("loop") else 1) if frames > 1 else 1), zoom=zoom) for i in range(frames)]
        rows.append((cname, ims))
    w = max(sum(i.width for i in ims) for _, ims in rows) + 120
    h = sum(ims[0].height for _, ims in rows)
    sheet = Image.new("RGBA", (w, h), (52, 48, 46, 255))
    y = 0
    d = ImageDraw.Draw(sheet)
    for cname, ims in rows:
        x = 110
        d.text((6, y + 10), cname, fill=(255, 255, 255, 255))
        for im in ims:
            sheet.alpha_composite(im, (x, y))
            x += im.width
        y += ims[0].height
    sheet.save(path)

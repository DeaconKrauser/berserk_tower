"""Cut isolated figures from a #FF00FF design sheet, keep relative scale.
usage: python cut_sheet.py in.png out_dir ref_index ref_body_px name1 name2 ...
Figures are found left->right by empty-column gaps; ref figure is scaled to ref_body_px.
"""
import sys, os
import numpy as np
from PIL import Image

src, out, ref, ref_px, names = sys.argv[1], sys.argv[2], int(sys.argv[3]), int(sys.argv[4]), sys.argv[5:]
rgb = np.asarray(Image.open(src).convert("RGB")).astype(np.int32)
r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
fg = np.abs(rgb - rgb[4, 4]).sum(axis=2) > 120
fg &= ~((r > 170) & (b > 170) & (g < 120))  # magenta fringe
cols = fg.sum(axis=0) > 2
segs, start = [], None
for x, c in enumerate(list(cols) + [False]):
    if c and start is None: start = x
    if not c and start is not None:
        if x - start > 40: segs.append((start, x))
        start = None
assert len(segs) == len(names), f"found {len(segs)} figures"
def bbox(x0, x1):
    ys = np.where(fg[:, x0:x1].any(axis=1))[0]; return ys[0], ys[-1]
t, bt = bbox(*segs[ref]); scale = ref_px / (bt - t)
os.makedirs(out, exist_ok=True)
for (x0, x1), name in zip(segs, names):
    y0, y1 = bbox(x0, x1)
    crop = np.zeros((y1 - y0 + 1, x1 - x0, 4), np.uint8)
    crop[..., :3] = rgb[y0:y1 + 1, x0:x1]; crop[..., 3] = fg[y0:y1 + 1, x0:x1] * 255
    img = Image.fromarray(crop, "RGBA")
    small = np.asarray(img.resize((round(img.width * scale), round(img.height * scale)), Image.BOX)).copy()
    small[..., 3] = np.where(small[..., 3] >= 128, 255, 0)
    im = Image.fromarray(small, "RGBA"); im.save(os.path.join(out, name + ".png"))
    print(name, im.size)

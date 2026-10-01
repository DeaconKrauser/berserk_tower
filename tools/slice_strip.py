"""Chroma-key a Nano Banana strip (#FF00FF bg) and slice it into Unity-ready frames.
usage: python slice_strip.py in.jpg out_dir name frames [body_px=74] [cell_w=96] [cell_h=96]
"""
import sys, os
import numpy as np
from PIL import Image

src, out, name, n = sys.argv[1], sys.argv[2], sys.argv[3], int(sys.argv[4])
body = int(sys.argv[5]) if len(sys.argv) > 5 else 74
cw = int(sys.argv[6]) if len(sys.argv) > 6 else 96
ch = int(sys.argv[7]) if len(sys.argv) > 7 else 96

rgb = np.asarray(Image.open(src).convert("RGB")).astype(np.int32)
bg = rgb[4, 4]
# ponytail: plain RGB distance key; magenta fringe dies in the alpha threshold after downscale
fg = np.abs(rgb - bg).sum(axis=2) > 120
# also kill anything strongly magenta-ish (JPG fringe)
r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
fg &= ~((r > 180) & (b > 180) & (g < 110))

ys = np.where(fg.any(axis=1))[0]
top, bottom = ys[0], ys[-1]
scale = body / (bottom - top)
H, W = fg.shape
os.makedirs(out, exist_ok=True)
sw = W / n
for i in range(n):
    x0, x1 = int(i * sw), int((i + 1) * sw)
    crop = np.zeros((bottom - top + 1, x1 - x0, 4), np.uint8)
    crop[..., :3] = rgb[top:bottom + 1, x0:x1]
    crop[..., 3] = fg[top:bottom + 1, x0:x1] * 255
    img = Image.fromarray(crop, "RGBA")
    w, h = max(1, round(img.width * scale)), max(1, round(img.height * scale))
    small = np.asarray(img.resize((w, h), Image.BOX)).copy()
    a = small[..., 3]
    small[..., 3] = np.where(a >= 128, 255, 0)
    # un-premultiply-ish: pixels next to bg were averaged with black; fine for a placeholder
    frame = Image.fromarray(small, "RGBA")
    cell = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    cell.paste(frame, ((cw - w) // 2, ch - h), frame)  # feet on bottom edge
    cell.save(os.path.join(out, f"{name}_{i}.png"))
print(f"{n} frames, scale {scale:.4f}")

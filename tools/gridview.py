"""Debug helper: source image with a labelled coordinate grid (source pixels), for picking crop boxes."""
import sys
from PIL import Image, ImageDraw


def grid(path, out, crop=None, step=100, maxw=1400):
    im = Image.open(path).convert("RGB")
    ox, oy = 0, 0
    if crop:
        im = im.crop(crop)
        ox, oy = crop[0], crop[1]
    d = ImageDraw.Draw(im)
    for x in range(((ox + step - 1) // step) * step, ox + im.width, step):
        d.line([(x - ox, 0), (x - ox, im.height)], fill=(0, 255, 255) if x % (step * 5) else (255, 255, 0), width=1 if x % (step * 5) else 3)
        d.text((x - ox + 3, 3), str(x), fill=(255, 255, 255))
    for y in range(((oy + step - 1) // step) * step, oy + im.height, step):
        d.line([(0, y - oy), (im.width, y - oy)], fill=(0, 255, 255) if y % (step * 5) else (255, 255, 0), width=1 if y % (step * 5) else 3)
        d.text((3, y - oy + 3), str(y), fill=(255, 255, 255))
    if im.width > maxw:
        im = im.resize((maxw, round(im.height * maxw / im.width)), Image.LANCZOS)
    im.save(out)


if __name__ == "__main__":
    a = sys.argv
    crop = tuple(int(v) for v in a[3].split(",")) if len(a) > 3 and a[3] != "-" else None
    step = int(a[4]) if len(a) > 4 else 100
    grid(a[1], a[2], crop, step)

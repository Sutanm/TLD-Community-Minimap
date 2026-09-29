"""Show both map images scaled to their own content, to separate crop from scale.

If the 1024 base map is simply a smaller copy of the same drawing, matching their
content boxes makes them agree. If it only covers part of the region, matching
the boxes still disagrees - which is what decides whether the HUD was handed a
thumbnail or a fragment.

Source: AI written for the layer-split investigation.
"""
from PIL import Image, ImageOps, ImageDraw

GAME = r"D:\Program Files (x86)\Steam\steamapps\common\TheLongDark\Mods\CommunityMinimap"
PANEL = GAME + r"\panelmap_MountainTownRegion.png"
BASE = GAME + r"\basemap_MountainTownRegion.png"
OUT = r"D:\CommunityMinimap-Workspace\tools\content-vs-content.png"

SIZE = 900


def content_box(img, threshold=40):
    mask = img.getchannel("A").point(lambda v: 255 if v > threshold else 0)
    return mask.getbbox()


def prep(path):
    img = Image.open(path).convert("RGBA")
    box = content_box(img)
    cropped = img.crop(box) if box else img
    bg = Image.new("RGBA", cropped.size, (255, 255, 255, 255))
    bg.alpha_composite(cropped)
    return bg.convert("RGB").resize((SIZE, SIZE), Image.LANCZOS), box, cropped.size


p, pbox, psize = prep(PANEL)
b, bbox, bsize = prep(BASE)
print(f"panel: canvas {Image.open(PANEL).size} content_box={pbox} content={psize}")
print(f"base : canvas {Image.open(BASE).size} content_box={bbox} content={bsize}")

pe = ImageOps.equalize(p.convert("L"))
be = ImageOps.equalize(b.convert("L"))
overlay = Image.merge("RGB", (pe, be, Image.new("L", (SIZE, SIZE), 70)))

canvas = Image.new("RGB", (SIZE * 3 + 40, SIZE + 40), (18, 18, 22))
canvas.paste(p, (0, 40))
canvas.paste(b, (SIZE + 20, 40))
canvas.paste(overlay, (SIZE * 2 + 40, 40))

d = ImageDraw.Draw(canvas)
d.text((10, 12), "game capture, content box", fill=(255, 255, 255))
d.text((SIZE + 30, 12), "old base map, content box", fill=(255, 255, 255))
d.text((SIZE * 2 + 50, 12), "overlay  R=game capture  G=old base map", fill=(255, 255, 255))

canvas.save(OUT)
print(f"wrote {OUT} {canvas.size}")

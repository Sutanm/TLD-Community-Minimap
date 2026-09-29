"""Compare the freshly captured game map against the base map the HUD used to get.

The capture is 2048x2048 and the base map is 1024x1024. If the panel image is
the same drawing at higher resolution, its content should match once scaled; if
it is a different picture, the difference shows up immediately.

Source: AI written for the layer-split investigation.
"""
from PIL import Image, ImageOps, ImageDraw

GAME = r"D:\Program Files (x86)\Steam\steamapps\common\TheLongDark\Mods\CommunityMinimap"
PANEL = GAME + r"\panelmap_MountainTownRegion.png"
BASE = GAME + r"\basemap_MountainTownRegion.png"
OUT = r"D:\CommunityMinimap-Workspace\tools\panel-vs-base-fresh.png"

SIZE = 900


def flatten(path):
    img = Image.open(path).convert("RGBA")
    bg = Image.new("RGBA", img.size, (255, 255, 255, 255))
    bg.alpha_composite(img)
    return bg.convert("RGB")


panel = flatten(PANEL)
base = flatten(BASE)
print(f"panel {panel.size}   base {base.size}")

p = panel.resize((SIZE, SIZE), Image.LANCZOS)
b = base.resize((SIZE, SIZE), Image.LANCZOS)

# Structure comparison: equalising each separately shows shape agreement without
# letting overall brightness differences masquerade as mismatch.
pe = ImageOps.equalize(p.convert("L"))
be = ImageOps.equalize(b.convert("L"))
overlay = Image.merge("RGB", (pe, be, Image.new("L", (SIZE, SIZE), 70)))

canvas = Image.new("RGB", (SIZE * 3 + 40, SIZE + 40), (18, 18, 22))
canvas.paste(p, (0, 40))
canvas.paste(b, (SIZE + 20, 40))
canvas.paste(overlay, (SIZE * 2 + 40, 40))

d = ImageDraw.Draw(canvas)
d.text((10, 12), "F7 capture from the game (2048)", fill=(255, 255, 255))
d.text((SIZE + 30, 12), "base map the HUD used (1024)", fill=(255, 255, 255))
d.text((SIZE * 2 + 50, 12), "overlay  R=game capture  G=old base map", fill=(255, 255, 255))

canvas.save(OUT)
print(f"wrote {OUT} {canvas.size}")

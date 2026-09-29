"""Put the vanilla base map and the community map side by side at the same size.

The two are different drawings of the same region, so the shapes should match.
If one covers less ground than the other, the difference is visible directly
instead of being argued about from content bounding boxes.

Source: AI written for the layer-split investigation.
"""
from PIL import Image, ImageDraw

GAME = r"D:\Program Files (x86)\Steam\steamapps\common\TheLongDark\Mods\CommunityMinimap"
BASE = GAME + r"\basemap_MountainTownRegion.png"
COMM = GAME + r"\maps\mountain_town.jpg"
OUT = r"D:\CommunityMinimap-Workspace\tools\extent-compare.png"

SIZE = 900

base = Image.open(BASE).convert("RGBA")
comm = Image.open(COMM).convert("RGBA")

print(f"basemap : {base.size}")
print(f"community: {comm.size}")


def flatten(img):
    """Composite onto white so transparency reads as background, then resize."""
    bg = Image.new("RGBA", img.size, (255, 255, 255, 255))
    bg.alpha_composite(img)
    return bg.convert("RGB").resize((SIZE, SIZE), Image.LANCZOS)


a = flatten(base)
b = flatten(comm)

canvas = Image.new("RGB", (SIZE * 2 + 30, SIZE + 40), (20, 20, 24))
canvas.paste(a, (0, 40))
canvas.paste(b, (SIZE + 30, 40))

d = ImageDraw.Draw(canvas)
d.text((10, 12), "vanilla base map (what the HUD is given)", fill=(255, 255, 255))
d.text((SIZE + 40, 12), "community map (mountain_town.jpg)", fill=(255, 255, 255))

canvas.save(OUT)
print(f"wrote {OUT}  {canvas.size}")

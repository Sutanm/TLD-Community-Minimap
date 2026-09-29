"""Overlay the two maps with each normalised to its own opaque content box.

If the vanilla base map covers a smaller patch of ground than the community
map, the community map's outer areas will have no vanilla underneath them, and
that shows up as the vanilla layer failing to reach the edges.

Each image is cropped to its opaque bounds and resized to a common square, so
the comparison is about CONTENT, not about padding.

Source: AI written for the layer-split investigation.
"""
from PIL import Image, ImageChops, ImageDraw

GAME = r"D:\Program Files (x86)\Steam\steamapps\common\TheLongDark\Mods\CommunityMinimap"
BASE = GAME + r"\basemap_MountainTownRegion.png"
COMM = GAME + r"\maps\mountain_town.jpg"
OUT = r"D:\CommunityMinimap-Workspace\tools\extent-overlay.png"

SIZE = 1000


def autosize_content(path, threshold=8):
    img = Image.open(path).convert("RGBA")
    # Alpha bounding box: where the drawing actually is.
    alpha = img.getchannel("A")
    mask = alpha.point(lambda v: 255 if v > threshold else 0)
    box = mask.getbbox()
    print(f"{path.split(chr(92))[-1]}: size={img.size} content_bbox={box}")
    return img.crop(box)


base = autosize_content(BASE)
comm = autosize_content(COMM)

print(f"base content: {base.size}  aspect={base.size[0]/base.size[1]:.3f}")
print(f"comm content: {comm.size}  aspect={comm.size[0]/comm.size[1]:.3f}")

white = (255, 255, 255, 255)


def prep(img):
    bg = Image.new("RGBA", img.size, white)
    bg.alpha_composite(img)
    return bg.convert("RGB").resize((SIZE, SIZE), Image.LANCZOS)


a = prep(base)
b = prep(comm)

# Red = vanilla only, green = community only, grey = both agree.
diff = ImageChops.difference(a.convert("L"), b.convert("L"))
overlay = Image.merge("RGB", (
    a.convert("L"),
    b.convert("L"),
    Image.new("L", (SIZE, SIZE), 80),
))

canvas = Image.new("RGB", (SIZE * 3 + 40, SIZE + 40), (20, 20, 24))
canvas.paste(a, (0, 40))
canvas.paste(b, (SIZE + 20, 40))
canvas.paste(overlay, (SIZE * 2 + 40, 40))

d = ImageDraw.Draw(canvas)
d.text((10, 12), "vanilla base map, content box", fill=(255, 255, 255))
d.text((SIZE + 30, 12), "community map, content box", fill=(255, 255, 255))
d.text((SIZE * 2 + 50, 12), "overlay: R=vanilla  G=community  B=80", fill=(255, 255, 255))

canvas.save(OUT)
print(f"wrote {OUT}  {canvas.size}")

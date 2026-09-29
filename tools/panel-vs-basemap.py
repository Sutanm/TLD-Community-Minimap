"""Overlay the in-game lit map against our exported base map, content-box aligned.

The two are supposed to be the same region. Aligning each to its own opaque
content box removes padding from the comparison, so any remaining disagreement
is about terrain, not about framing.

Source: AI written for the layer-split investigation.
"""
from PIL import Image, ImageChops, ImageDraw, ImageOps

SIZE = 1000
OUT = r"D:\CommunityMinimap-Workspace\tools\panel-vs-basemap.png"

# The in-game map the user captured (second attachment), and our export.
PANEL = r"C:\Users\95470\.dsh\attachments\v1\objects\92\9256d364a02a4738ff84584c39f153f7754cc65a7c740323ae306327e64197f4"
BASE = r"D:\Program Files (x86)\Steam\steamapps\common\TheLongDark\Mods\CommunityMinimap\basemap_MountainTownRegion.png"


def load_content(path, threshold=8):
    img = Image.open(path).convert("RGBA")
    mask = img.getchannel("A").point(lambda v: 255 if v > threshold else 0)
    box = mask.getbbox()
    print(f"{path.split(chr(92))[-1]}: {img.size} content_bbox={box}")
    return img.crop(box) if box else img


def grey_square(img):
    bg = Image.new("RGBA", img.size, (255, 255, 255, 255))
    bg.alpha_composite(img)
    return ImageOps.equalize(bg.convert("L")).resize((SIZE, SIZE), Image.LANCZOS)


panel = grey_square(load_content(PANEL))
base = grey_square(load_content(BASE))

# Structure-only comparison: equalised luminance shows terrain shapes, not tone.
overlay = Image.merge("RGB", (
    panel,
    base,
    Image.new("L", (SIZE, SIZE), 70),
))

canvas = Image.new("RGB", (SIZE * 3 + 40, SIZE + 40), (18, 18, 22))
canvas.paste(panel.convert("RGB"), (0, 40))
canvas.paste(base.convert("RGB"), (SIZE + 20, 40))
canvas.paste(overlay, (SIZE * 2 + 40, 40))

d = ImageDraw.Draw(canvas)
d.text((10, 12), "in-game map (lit), content box", fill=(255, 255, 255))
d.text((SIZE + 30, 12), "our exported base map, content box", fill=(255, 255, 255))
d.text((SIZE * 2 + 50, 12), "overlay  R=in-game  G=our base map  B=70", fill=(255, 255, 255))

canvas.save(OUT)
print(f"wrote {OUT} {canvas.size}")

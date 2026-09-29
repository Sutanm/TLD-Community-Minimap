"""Ask the player to check the five landmark positions before any of this is trusted.

Two of the five line up with a 1.98 px/unit similarity and a 2 degree rotation, which is right.
The other three are impossible: the Mountaineer's Hut and the landing gear are 461 world units
apart but their labels are only 216px apart, where 2px/unit says they should be about 920. So the
labels are not sitting on the features, and guessing which way they are offset is how this went
wrong twice already.
"""

import os
from PIL import Image, ImageDraw, ImageFont

MOD = r"D:\Program Files (x86)\Steam\steamapps\common\TheLongDark\Mods\CommunityMinimap"
WORKSPACE = r"D:\CommunityMinimap-Workspace"

CANDIDATES = [
    (1, "机尾", 1728, 1656),
    (2, "登山者棚屋", 1656, 3408),
    (3, "起落架", 1756, 3600),
    (4, "机翼-右下角", 2416, 3120),
    (5, "瀑布山洞", 792, 360),
]

image = Image.open(os.path.join(MOD, "maps", "timberwolf_mountain.jpg")).convert("RGB")
try:
    big = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 64)
    small = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 30)
except OSError:
    big = small = ImageFont.load_default()

marked = image.copy()
draw = ImageDraw.Draw(marked)
for number, name, x, y in CANDIDATES:
    radius = 90
    draw.ellipse([x - radius, y - radius, x + radius, y + radius], outline=(255, 0, 0), width=10)
    draw.line([x - 140, y, x + 140, y], fill=(255, 0, 0), width=8)
    draw.line([x, y - 140, x, y + 140], fill=(255, 0, 0), width=8)
    draw.text((x + 100, y - 130), str(number), fill=(255, 0, 0), font=big)

overview = marked.resize((1500, int(1500 * image.height / image.width)), Image.LANCZOS)

# A strip of the five actual label crops, numbered the same way, so the player can say which number
# is not the place they stood.
tiles = []
for number, name, x, y in CANDIDATES:
    half = 260
    box = (max(0, x - half), max(0, y - half),
           min(image.width, x + half), min(image.height, y + half))
    tile = image.crop(box).resize((360, 360), Image.LANCZOS)
    canvas = Image.new("RGB", (360, 400), (25, 25, 25))
    canvas.paste(tile, (0, 40))
    ImageDraw.Draw(canvas).text((8, 6), f"{number}. {name}", fill=(255, 220, 0), font=small)
    tiles.append(canvas)

strip = Image.new("RGB", (len(tiles) * 370, 400), (25, 25, 25))
for i, tile in enumerate(tiles):
    strip.paste(tile, (i * 370, 0))

panel = Image.new("RGB", (max(overview.width, strip.width),
                          overview.height + strip.height + 20), (25, 25, 25))
panel.paste(overview, (0, 0))
panel.paste(strip, (0, overview.height + 20))
out = os.path.join(WORKSPACE, "tools", "verify-timberwolf-points.png")
panel.save(out)
print(f"wrote {out}  ({panel.width}x{panel.height})")
for number, name, x, y in CANDIDATES:
    print(f"  {number}. {name:<12} ({x},{y})")

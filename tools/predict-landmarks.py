"""Predict the three suspect landmarks from the two that agree, then look at the predictions.

Machine Hut, landing gear and wing are impossible with the label positions: the landing gear's
label sits 216px from the hut's while the two are 461 world units apart, where the scale the other
two points establish says about 920. So the labels are not on the features.

Two points fix a similarity exactly. Machine tail and waterfall cave agree with each other to 2.2
degrees of rotation and 1.98 px per world unit, both of which are plausible for a hand-drawn map,
so they are used as the anchor and the other three are predicted from them.
"""

import math
import os
from PIL import Image, ImageDraw, ImageFont

MOD = r"D:\Program Files (x86)\Steam\steamapps\common\TheLongDark\Mods\CommunityMinimap"
WORKSPACE = r"D:\CommunityMinimap-Workspace"

# The anchor pair, which agrees with itself.
TAIL_W, TAIL_P = (927.089, 1178.602), (1728.0, 1656.0)          # 机尾
CAVE_W, CAVE_P = (429.086, 1816.675), (792.0, 360.0)            # 瀑布山洞

dw = (CAVE_W[0] - TAIL_W[0], CAVE_W[1] - TAIL_W[1])
dp = (CAVE_P[0] - TAIL_P[0], CAVE_P[1] - TAIL_P[1])
scale = math.hypot(*dp) / math.hypot(*dw)
# Image y grows downward, so compare against a y-up frame before taking the rotation.
theta = math.atan2(-dp[1], dp[0]) - math.atan2(dw[1], dw[0])
print(f"anchor pair: scale={scale:.4f} px/unit  rotation={math.degrees(theta):+.2f} deg")

cos_t, sin_t = math.cos(theta), math.sin(theta)


def predict(world_x, world_z):
    dx, dz = world_x - TAIL_W[0], world_z - TAIL_W[1]
    u = scale * (cos_t * dx - sin_t * dz)
    v = scale * (sin_t * dx + cos_t * dz)
    return TAIL_P[0] + u, TAIL_P[1] - v


TARGETS = [
    ("2 登山者棚屋", 888.605, 342.435, 1656, 3408),
    ("3 起落架", 431.252, 283.077, 1756, 3600),
    ("4 机翼-右下角", 1570.803, 475.992, 2416, 3120),
]

image = Image.open(os.path.join(MOD, "maps", "timberwolf_mountain.jpg")).convert("RGB")
try:
    font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 34)
except OSError:
    font = ImageFont.load_default()

tiles = []
for name, wx, wz, label_x, label_y in TARGETS:
    px, py = predict(wx, wz)
    gap = math.hypot(px - label_x, py - label_y)
    print(f"  {name:<14} predicted=({px:6.0f},{py:6.0f})  my label=({label_x},{label_y})  "
          f"{gap:6.0f}px apart")

    half = 330
    box = (max(0, int(px - half)), max(0, int(py - half)),
           min(image.width, int(px + half)), min(image.height, int(py + half)))
    tile = image.crop(box)
    canvas = Image.new("RGB", (tile.width, tile.height + 44), (25, 25, 25))
    canvas.paste(tile, (0, 44))
    draw = ImageDraw.Draw(canvas)
    draw.text((8, 6), f"{name}  predicted ({px:.0f},{py:.0f})", fill=(255, 220, 0), font=font)
    # crosshair on the predicted point
    cx, cy = int(px) - box[0], int(py) - box[1] + 44
    draw.line([cx - 60, cy, cx + 60, cy], fill=(255, 0, 0), width=4)
    draw.line([cx, cy - 60, cx, cy + 60], fill=(255, 0, 0), width=4)
    tiles.append(canvas)

width = sum(t.width for t in tiles) + 20 * (len(tiles) - 1)
height = max(t.height for t in tiles)
panel = Image.new("RGB", (width, height), (25, 25, 25))
x = 0
for tile in tiles:
    panel.paste(tile, (x, 0))
    x += tile.width + 20
out = os.path.join(WORKSPACE, "tools", "predicted-landmarks.png")
panel.save(out)
print(f"\nwrote {out} ({panel.width}x{panel.height})")

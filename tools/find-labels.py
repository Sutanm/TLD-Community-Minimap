"""Find the blue place labels on a community map so landmarks can be located precisely.

Guessing pixel positions from a screenshot was already wrong twice, so the labels are located by
colour instead: they are the only saturated-blue text on the map, and each one sits next to the
feature it names.
"""

import os
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont

MOD = r"D:\Program Files (x86)\Steam\steamapps\common\TheLongDark\Mods\CommunityMinimap"
WORKSPACE = r"D:\CommunityMinimap-Workspace"

map_id = sys.argv[1] if len(sys.argv) > 1 else "timberwolf_mountain"
image = Image.open(os.path.join(MOD, "maps", f"{map_id}.jpg")).convert("RGB")
arr = np.asarray(image, dtype=np.int16)
print(f"{map_id}: {image.size[0]}x{image.size[1]}")

r, g, b = arr[:, :, 0], arr[:, :, 1], arr[:, :, 2]
# Saturated blue text: blue clearly above both other channels. The terrain blues (water, ice) are
# much lighter, so requiring a low red and green keeps them out.
blue = (b - np.maximum(r, g) > 60) & (b > 110) & (r < 130) & (g < 130)
print(f"blue pixels: {int(blue.sum())}")

# Group into blobs on a coarse grid: text characters are separate, the grid joins them per label.
cell = 48
gh, gw = (image.height + cell - 1) // cell, (image.width + cell - 1) // cell
grid = np.zeros((gh, gw), dtype=bool)
ys, xs = np.nonzero(blue)
grid[ys // cell, xs // cell] = True

seen = np.zeros_like(grid)
blobs = []
for gy in range(gh):
    for gx in range(gw):
        if not grid[gy, gx] or seen[gy, gx]:
            continue
        stack = [(gy, gx)]
        seen[gy, gx] = True
        min_y = max_y = gy
        min_x = max_x = gx
        count = 0
        while stack:
            cy, cx = stack.pop()
            count += 1
            min_y, max_y = min(min_y, cy), max(max_y, cy)
            min_x, max_x = min(min_x, cx), max(max_x, cx)
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    ny, nx = cy + dy, cx + dx
                    if 0 <= ny < gh and 0 <= nx < gw and grid[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        stack.append((ny, nx))
        blobs.append((count * cell * cell, min_x * cell, min_y * cell,
                      min((max_x + 1) * cell, image.width), min((max_y + 1) * cell, image.height)))

blobs.sort(key=lambda t: -t[0])
print(f"candidate label blobs: {len(blobs)}")

try:
    font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 26)
except OSError:
    font = ImageFont.load_default()

max_tiles = 32
tiles = []
for area, x0, y0, x1, y1 in blobs:
    if area < cell * cell * 2 or (x1 - x0) < 90 or (y1 - y0) < 40:
        continue
    cx, cy = (x0 + x1) // 2, (y0 + y1) // 2
    pad = 40
    box = (max(0, x0 - pad), max(0, y0 - pad),
           min(image.width, x1 + pad), min(image.height, y1 + pad))
    tile = image.crop(box).resize(((box[2] - box[0]) * 2, (box[3] - box[1]) * 2), Image.LANCZOS)
    canvas = Image.new("RGB", (tile.width, tile.height + 34), (25, 25, 25))
    canvas.paste(tile, (0, 34))
    draw = ImageDraw.Draw(canvas)
    draw.text((6, 4), f"({cx},{cy})", fill=(255, 220, 0), font=font)
    tiles.append((cy, cx, canvas))

tiles.sort(key=lambda t: (t[0] // 300, t[1]))

# One montage, two columns, so every label can be read in a single look.
if tiles:
    col_w = max(t[2].width for t in tiles)
    half = (len(tiles) + 1) // 2
    rows = []
    for i in range(half):
        left = tiles[i][2]
        right = tiles[i + half][2] if i + half < len(tiles) else None
        height = max(left.height, right.height if right else 0)
        row = Image.new("RGB", (col_w * 2 + 10, height), (25, 25, 25))
        row.paste(left, (0, 0))
        if right:
            row.paste(right, (col_w + 10, 0))
        rows.append(row)
    total_h = sum(r.height for r in rows) + 6 * len(rows)
    montage = Image.new("RGB", (col_w * 2 + 10, total_h), (25, 25, 25))
    y = 0
    for row in rows:
        montage.paste(row, (0, y))
        y += row.height + 6
    out = os.path.join(WORKSPACE, "tools", f"labels-{map_id}.png")
    montage.save(out)
    print(f"wrote {out}  ({len(tiles)} tiles, {montage.width}x{montage.height})")
else:
    print("no label tiles found")

"""Match each landmark to the label blob nearest where it is predicted to be.

The montage listed a coordinate above every tile, and reading those off by eye mismatched three of
the five labels - which is what wrecked the first fit. Nothing here is read by eye: the predicted
position comes from the two landmarks that agree with each other, and the label is whichever blob
is closest to that prediction.
"""

import math
import os
import numpy as np
from PIL import Image

MOD = r"D:\Program Files (x86)\Steam\steamapps\common\TheLongDark\Mods\CommunityMinimap"

TAIL_W, TAIL_P = (927.089, 1178.602), (1728.0, 1656.0)
CAVE_W, CAVE_P = (429.086, 1816.675), (792.0, 360.0)

image = Image.open(os.path.join(MOD, "maps", "timberwolf_mountain.jpg")).convert("RGB")
arr = np.asarray(image, dtype=np.int16)
r, g, b = arr[:, :, 0], arr[:, :, 1], arr[:, :, 2]
blue = (b - np.maximum(r, g) > 60) & (b > 110) & (r < 130) & (g < 130)

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
        stack, cells = [(gy, gx)], []
        seen[gy, gx] = True
        while stack:
            cy, cx = stack.pop()
            cells.append((cy, cx))
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    ny, nx = cy + dy, cx + dx
                    if 0 <= ny < gh and 0 <= nx < gw and grid[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        stack.append((ny, nx))
        x0 = min(c[1] for c in cells) * cell
        x1 = (max(c[1] for c in cells) + 1) * cell
        y0 = min(c[0] for c in cells) * cell
        y1 = (max(c[0] for c in cells) + 1) * cell
        if len(cells) >= 2 and (x1 - x0) >= 90 and (y1 - y0) >= 40:
            blobs.append(((x0 + x1) // 2, (y0 + y1) // 2, x1 - x0, y1 - y0))

print(f"{len(blobs)} label blobs found\n")

dw = (CAVE_W[0] - TAIL_W[0], CAVE_W[1] - TAIL_W[1])
dp = (CAVE_P[0] - TAIL_P[0], CAVE_P[1] - TAIL_P[1])
scale = math.hypot(*dp) / math.hypot(*dw)
theta = math.atan2(-dp[1], dp[0]) - math.atan2(dw[1], dw[0])
cos_t, sin_t = math.cos(theta), math.sin(theta)


def predict(world_x, world_z):
    dx, dz = world_x - TAIL_W[0], world_z - TAIL_W[1]
    return (TAIL_P[0] + scale * (cos_t * dx - sin_t * dz),
            TAIL_P[1] - scale * (sin_t * dx + cos_t * dz))


TARGETS = [
    ("登山者棚屋", 888.605, 342.435),
    ("起落架", 431.252, 283.077),
    ("机翼-右下角", 1570.803, 475.992),
]

print(f"{'landmark':<14} {'predicted':>16}   nearest label blobs")
resolved = {"机尾": TAIL_P, "瀑布山洞": CAVE_P}
for name, wx, wz in TARGETS:
    px, py = predict(wx, wz)
    ranked = sorted(blobs, key=lambda bl: (bl[0] - px) ** 2 + (bl[1] - py) ** 2)[:3]
    print(f"{name:<14} ({px:6.0f},{py:6.0f})")
    for bx, by, bw, bh in ranked:
        print(f"      blob ({bx:5},{by:5})  size {bw}x{bh}  distance {math.hypot(bx - px, by - py):6.0f}px")
    resolved[name] = (ranked[0][0], ranked[0][1])

# Fit again with the label positions that were resolved by proximity rather than by eye.
ORDER = ["机尾", "登山者棚屋", "起落架", "机翼-右下角", "瀑布山洞"]
WORLD = {
    "机尾": (927.089, 1178.602),
    "登山者棚屋": (888.605, 342.435),
    "起落架": (431.252, 283.077),
    "机翼-右下角": (1570.803, 475.992),
    "瀑布山洞": (429.086, 1816.675),
}
WIDTH, HEIGHT = image.size

world = np.array([[WORLD[n][0], WORLD[n][1], 1.0] for n in ORDER])
target = np.array([[resolved[n][0] / WIDTH, 1.0 - resolved[n][1] / HEIGHT] for n in ORDER])
coeffs, *_ = np.linalg.lstsq(world, target, rcond=None)

print("\n=== refit residuals ===")
worst = 0.0
for n in ORDER:
    wx, wz = WORLD[n]
    u, v = np.array([wx, wz, 1.0]) @ coeffs
    px, py = u * WIDTH, (1.0 - v) * HEIGHT
    ix, iy = resolved[n]
    error = math.hypot(px - ix, py - iy)
    worst = max(worst, error)
    print(f"  {n:<12} label=({ix:5},{iy:5})  fitted=({px:7.1f},{py:7.1f})  error={error:6.1f}px")
print(f"  worst {worst:.1f}px ({worst / WIDTH * 100:.2f}% of width)")

import json
store_path = os.path.join(r"D:\CommunityMinimap-Workspace", "calibrations.json")
with open(store_path, encoding="utf-8") as handle:
    store = json.load(handle)
store["maps"] = [m for m in store["maps"] if m["mapId"] != "timberwolf_mountain"]
store["maps"].append({
    "mapId": "timberwolf_mountain",
    "imageWidth": WIDTH,
    "imageHeight": HEIGHT,
    "points": [
        {"worldX": WORLD[n][0], "worldZ": WORLD[n][1],
         "mapX": resolved[n][0], "mapY": resolved[n][1], "label": n}
        for n in ORDER
    ],
})
store["maps"].sort(key=lambda m: m["mapId"])
with open(store_path, "w", encoding="utf-8") as handle:
    json.dump(store, handle, ensure_ascii=False, indent=2)
print(f"\nwrote timberwolf_mountain into calibrations.json ({len(store['maps'])} maps)")

"""Fit the affine for Timberwolf Mountain and check it against data it was not fitted to.

Five landmarks were recorded in game, each with a world position from the game and a position on
the community image found by locating the map's blue place labels. The census CSV holds 15 cave
markers with their own world positions; if the fit is right those land on the map's cave boxes,
which is a check the five points cannot provide on their own.
"""

import csv
import json
import os

import numpy as np

WORKSPACE = r"D:\CommunityMinimap-Workspace"
MOD = r"D:\Program Files (x86)\Steam\steamapps\common\TheLongDark\Mods\CommunityMinimap"

MAP_ID = "timberwolf_mountain"
WIDTH, HEIGHT = 4380, 4302

# (label, world x, world z, image x, image y) - the world pair is from calibration_points_v2.csv in
# the order the points were taken, the image pair from the blue label located in find-labels.py.
POINTS = [
    ("机尾",         927.089, 1178.602, 1728, 1656),
    ("登山者棚屋",   888.605,  342.435, 1656, 3408),
    ("起落架",       431.252,  283.077, 1756, 3600),
    ("机翼-右下角", 1570.803,  475.992, 2416, 3120),
    ("瀑布山洞",     429.086, 1816.675,  792,  360),
]

world = np.array([[p[1], p[2], 1.0] for p in POINTS])
target = np.array([[p[3] / WIDTH, 1.0 - p[4] / HEIGHT] for p in POINTS])
coeffs, *_ = np.linalg.lstsq(world, target, rcond=None)

print("=== fit residuals on the five recorded points ===")
worst = 0.0
for label, wx, wz, ix, iy in POINTS:
    u, v = np.array([wx, wz, 1.0]) @ coeffs
    px, py = u * WIDTH, (1.0 - v) * HEIGHT
    error = ((px - ix) ** 2 + (py - iy) ** 2) ** 0.5
    worst = max(worst, error)
    print(f"  {label:<12} recorded=({ix:6},{iy:6})  fitted=({px:7.1f},{py:7.1f})  error={error:6.1f}px")
print(f"  worst {worst:.1f}px ({worst / WIDTH * 100:.2f}% of width)")

print("\n=== transform shape ===")
for name, (wx, wz) in (("origin", (0, 0)), ("+x 1000", (1000, 0)), ("+z 1000", (0, 1000))):
    u, v = np.array([wx, wz, 1.0]) @ coeffs
    print(f"  {name:<9} -> image ({u * WIDTH:8.1f}, {(1 - v) * HEIGHT:8.1f})")
u0, v0 = np.array([0, 0, 1.0]) @ coeffs
ux, vx = np.array([1000, 0, 1.0]) @ coeffs
uz, vz = np.array([0, 1000, 1.0]) @ coeffs
ax = np.array([ux - u0, -(vx - v0)]) * [WIDTH, HEIGHT]
az = np.array([uz - u0, -(vz - v0)]) * [WIDTH, HEIGHT]
print(f"  1000 world x -> {np.linalg.norm(ax):.0f}px, 1000 world z -> {np.linalg.norm(az):.0f}px")
print(f"  angle between the two axes: "
      f"{np.degrees(np.arccos(np.clip(ax @ az / (np.linalg.norm(ax) * np.linalg.norm(az)), -1, 1))):.1f} deg "
      "(90 = no shear or rotation)")

# --- independent check: caves from the census, which were not used in the fit ------------------
census = os.path.join(MOD, "mapdetails_CrashMountainRegion_205211.csv")
caves = []
with open(census, encoding="utf-8") as handle:
    for row in csv.DictReader(handle):
        if row["sprite"] == "icoMap_cave":
            caves.append((float(row["world_x"]), float(row["world_z"]), row["locid"]))

print(f"\n=== {len(caves)} cave markers from the census, projected ===")
for wx, wz, loc in caves:
    u, v = np.array([wx, wz, 1.0]) @ coeffs
    px, py = u * WIDTH, (1.0 - v) * HEIGHT
    inside = 0 <= px <= WIDTH and 0 <= py <= HEIGHT
    print(f"  {loc:<32} world=({wx:8.1f},{wz:8.1f}) -> image ({px:7.1f},{py:7.1f})"
          f"{'' if inside else '   OUTSIDE'}")

# --- write the entry --------------------------------------------------------------------------
path = os.path.join(WORKSPACE, "calibrations.json")
with open(path, encoding="utf-8") as handle:
    store = json.load(handle)

store["maps"] = [m for m in store["maps"] if m["mapId"] != MAP_ID]
store["maps"].append({
    "mapId": MAP_ID,
    "imageWidth": WIDTH,
    "imageHeight": HEIGHT,
    "points": [
        {"worldX": wx, "worldZ": wz, "mapX": ix, "mapY": iy, "label": label}
        for label, wx, wz, ix, iy in POINTS
    ],
})
store["maps"].sort(key=lambda m: m["mapId"])
with open(path, "w", encoding="utf-8") as handle:
    json.dump(store, handle, ensure_ascii=False, indent=2)
print(f"\nwrote {MAP_ID} into {path} ({len(store['maps'])} maps now calibrated)")

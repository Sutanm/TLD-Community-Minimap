"""Install the calibration the player produced with tools/calibrate.html, and verify it.

The player clicked the features themselves, which is the part that cannot be automated: the map's
blue label boxes sit about 150px up and to the left of the things they name, so reading positions
off the labels - which is what produced the 75px fit - carries a systematic error that no amount
of care recovers.
"""

import json
import math
import os

import numpy as np

WORKSPACE = r"D:\CommunityMinimap-Workspace"
MOD = r"D:\Program Files (x86)\Steam\steamapps\common\TheLongDark\Mods\CommunityMinimap"

# Pasted from the calibrate.html output, with the landmark names filled back in for the record.
POINTS = [
    ("机尾", 927.089, 1178.602, 1938.7, 1712.1),
    ("登山者棚屋", 888.605, 342.435, 1855.1, 3447.6),
    ("起落架", 431.252, 283.077, 906.0, 3568.8),
    ("机翼-右下角", 1570.803, 475.992, 3271.2, 3173.4),
    ("瀑布山洞", 429.086, 1816.675, 908.3, 385.5),
]
WIDTH, HEIGHT = 4380, 4302

world = np.array([[p[1], p[2], 1.0] for p in POINTS])
target = np.array([[p[3] / WIDTH, 1.0 - p[4] / HEIGHT] for p in POINTS])
coeffs, *_ = np.linalg.lstsq(world, target, rcond=None)

print("=== residuals ===")
worst = 0.0
for label, wx, wz, ix, iy in POINTS:
    u, v = np.array([wx, wz, 1.0]) @ coeffs
    px, py = u * WIDTH, (1.0 - v) * HEIGHT
    error = math.hypot(px - ix, py - iy)
    worst = max(worst, error)
    print(f"  {label:<12} clicked=({ix:7.1f},{iy:7.1f})  fitted=({px:7.1f},{py:7.1f})  "
          f"error={error:5.1f}px")
print(f"  worst {worst:.1f}px ({worst / WIDTH * 100:.2f}% of width)")

p0 = np.array([0.0, 0.0, 1.0]) @ coeffs
px_ = np.array([1000.0, 0.0, 1.0]) @ coeffs
pz_ = np.array([0.0, 1000.0, 1.0]) @ coeffs
ax = np.array([px_[0] - p0[0], -(px_[1] - p0[1])]) * [WIDTH, HEIGHT]
az = np.array([pz_[0] - p0[0], -(pz_[1] - p0[1])]) * [WIDTH, HEIGHT]
angle = math.degrees(math.acos(max(-1.0, min(1.0, float(ax @ az / (np.linalg.norm(ax) * np.linalg.norm(az)))))))
print(f"\n  1000 world x -> {np.linalg.norm(ax):.1f}px   1000 world z -> {np.linalg.norm(az):.1f}px")
print(f"  anisotropy {abs(np.linalg.norm(ax) - np.linalg.norm(az)) / np.linalg.norm(az) * 100:.2f}%"
      f"   axes {angle:.2f} deg apart")

# Every other calibrated map, for comparison.
path = os.path.join(WORKSPACE, "calibrations.json")
with open(path, encoding="utf-8") as handle:
    store = json.load(handle)

print("\n=== how the other maps' fits compare ===")
for m in store["maps"]:
    if m["mapId"] == "timberwolf_mountain":
        continue
    w = np.array([[p["worldX"], p["worldZ"], 1.0] for p in m["points"]])
    t = np.array([[p["mapX"] / m["imageWidth"], 1.0 - p["mapY"] / m["imageHeight"]]
                  for p in m["points"]])
    c, *_ = np.linalg.lstsq(w, t, rcond=None)
    errors = []
    for p in m["points"]:
        u, v = np.array([p["worldX"], p["worldZ"], 1.0]) @ c
        errors.append(math.hypot(u * m["imageWidth"] - p["mapX"],
                                 (1 - v) * m["imageHeight"] - p["mapY"]))
    print(f"  {m['mapId']:<22} {len(m['points'])} points  worst {max(errors):6.1f}px")

store["maps"] = [m for m in store["maps"] if m["mapId"] != "timberwolf_mountain"]
store["maps"].append({
    "mapId": "timberwolf_mountain",
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

installed = os.path.join(MOD, "calibrations.json")
with open(installed, "w", encoding="utf-8") as handle:
    json.dump(store, handle, ensure_ascii=False, indent=2)
print(f"\nwrote and deployed; {len(store['maps'])} maps calibrated")

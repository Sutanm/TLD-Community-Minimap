"""Offline check: would the game's own map markers land correctly on a community map?

The marker positions come from MapDetail.GetWorldPosition() (recorded in HANDOFF section 24),
the transform is the same affine CalibrationStore fits for the player pointer, and the backdrop
is the community map the mod actually uses. So this is exactly what an overlay would look like,
rendered without needing the game running.
"""

import json
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFont

WORKSPACE = r"D:\CommunityMinimap-Workspace"
GAME_MOD = r"D:\Program Files (x86)\Steam\steamapps\common\TheLongDark\Mods\CommunityMinimap"

# From HANDOFF section 24: sprite, loc id, world (x, y, z) as MapDetail reported them.
MARKERS = [
    ("icoMap_churchMilton", "GAMEPLAY_mtChurch", 688.6, 2102.0),
    ("(Text)", "GAMEPLAY_mtSchool", 981.7, 1727.1),
    ("icoMap_crossroads", "SCENENAME_MiltonHouse", 1131.6, 1755.1),
]

with open(os.path.join(WORKSPACE, "calibrations.json"), encoding="utf-8") as handle:
    store = json.load(handle)

entry = next(m for m in store["maps"] if m["mapId"] == "mountain_town")
width, height = entry["imageWidth"], entry["imageHeight"]
points = entry["points"]


def fit_affine(pts):
    """world (x, z) -> (u, v), u = mapX/W, v = 1 - mapY/H, same normalisation as CalibrationStore."""
    world = np.array([[p["worldX"], p["worldZ"], 1.0] for p in pts])
    target = np.array([[p["mapX"] / width, 1.0 - p["mapY"] / height] for p in pts])
    coeffs, *_ = np.linalg.lstsq(world, target, rcond=None)
    return coeffs


coeffs = fit_affine(points)


def project(world_x, world_z):
    u, v = np.array([world_x, world_z, 1.0]) @ coeffs
    return u * width, (1.0 - v) * height


print("=== fit self-check on the recorded calibration points ===")
worst = 0.0
for p in points:
    px, py = project(p["worldX"], p["worldZ"])
    dx, dy = px - p["mapX"], py - p["mapY"]
    error = (dx * dx + dy * dy) ** 0.5
    worst = max(worst, error)
    print(f"  {p['label']:<12} recorded=({p['mapX']:7.1f},{p['mapY']:7.1f})  "
          f"fitted=({px:7.1f},{py:7.1f})  error={error:5.1f}px")
print(f"  worst residual on the fit's own points: {worst:.1f}px "
      f"({worst / width * 100:.2f}% of the image width)")

print()
print("=== where the game's own markers would land ===")
projected = []
for sprite, loc, wx, wz in MARKERS:
    px, py = project(wx, wz)
    projected.append((sprite, loc, px, py))
    print(f"  {loc:<28} world=({wx:7.1f},{wz:7.1f})  ->  image=({px:7.1f},{py:7.1f})")

# The church has both a MapDetail position and a player-recorded calibration point, so the two can
# be compared directly: it is the one marker whose correct answer is already known.
church_recorded = next(p for p in points if "教堂" in p["label"])
church_marker = projected[0]
gap = ((church_marker[2] - church_recorded["mapX"]) ** 2
       + (church_marker[3] - church_recorded["mapY"]) ** 2) ** 0.5
print()
print(f"cross-check: the survey marker sits {gap:.0f}px from the player-recorded church position")
print(f"             ({gap / width * 100:.2f}% of the image width)")

# --- render -----------------------------------------------------------------------------------
map_path = os.path.join(GAME_MOD, "maps", "mountain_town.jpg")
image = Image.open(map_path).convert("RGB")
draw = ImageDraw.Draw(image)
try:
    font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 64)
    small = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 44)
except OSError:
    font = small = ImageFont.load_default()

for p in points:
    x, y = p["mapX"], p["mapY"]
    draw.ellipse([x - 18, y - 18, x + 18, y + 18], outline=(0, 200, 255), width=8)
    draw.text((x + 26, y - 30), p["label"], fill=(0, 200, 255), font=small)

for sprite, loc, x, y in projected:
    draw.line([x - 40, y, x + 40, y], fill=(255, 40, 40), width=10)
    draw.line([x, y - 40, x, y + 40], fill=(255, 40, 40), width=10)
    draw.text((x + 26, y + 12), loc.replace("GAMEPLAY_", "").replace("SCENENAME_", ""),
              fill=(255, 40, 40), font=font)

out = os.path.join(WORKSPACE, "tools", "marker-overlay-preview.png")
image.save(out)
print()
print(f"wrote {out}")

# Crops around each projected marker, so the alignment can be judged without zooming by hand.
for i, (sprite, loc, x, y) in enumerate(projected):
    half = 500
    box = (max(0, int(x - half)), max(0, int(y - half)),
           min(image.width, int(x + half)), min(image.height, int(y + half)))
    crop = image.crop(box).resize((700, 700), Image.LANCZOS)
    crop_path = os.path.join(WORKSPACE, "tools", f"marker-crop-{i}-{loc.split('_')[-1]}.png")
    crop.save(crop_path)
    print(f"wrote {crop_path}")

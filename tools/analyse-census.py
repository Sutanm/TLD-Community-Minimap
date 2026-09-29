"""Analyse one MapDetail census and crop the community image where a dark patch was reported."""

import collections
import csv
import os
import sys

import numpy as np
from PIL import Image

MOD = r"D:\Program Files (x86)\Steam\steamapps\common\TheLongDark\Mods\CommunityMinimap"
WORKSPACE = r"D:\CommunityMinimap-Workspace"

census = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
    MOD, "mapdetails_CrashMountainRegion_205211.csv")

rows = list(csv.DictReader(open(census, encoding="utf-8")))
print(f"{census}")
print(f"entries: {len(rows)}")

print("\n=== by m_IconType ===")
for kind, n in collections.Counter(r["type"] for r in rows).most_common():
    print(f"  {kind:<14} {n}")

print("\n=== state flags ===")
for flag in ("surveyed", "discovered", "unlocked"):
    n = sum(1 for r in rows if r[flag].strip().lower() == "true")
    print(f"  {flag:<12} true on {n}")

with_sprite = [r for r in rows if r["sprite"].strip()]
print(f"\n=== {len(with_sprite)} entries carry a sprite name ===")
# Sprite names are like icoMap_cattails, icoMap_churchMilton, ...; the suffix is the meaning.
kinds = collections.Counter(r["sprite"].split("_", 1)[-1] for r in with_sprite)
for name, n in kinds.most_common(24):
    print(f"  {name:<32} {n}")
print(f"  ... {len(kinds)} distinct sprite names in total")

print("\n=== sprite names that look like places rather than resources ===")
place_like = [r for r in with_sprite
              if not r["sprite"].lower().startswith(("icomap_cattail", "icomap_mushroom",
                                                     "icomap_rosehip", "icomap_reishi",
                                                     "icomap_birch", "icomap_maple",
                                                     "icomap_oldman", "icomap_burdock",
                                                     "icomap_acorn", "icomap_lichens"))]
for r in place_like[:20]:
    print(f"  [{r['index']:>4}] {r['sprite']:<34} {r['locid']:<28} type={r['type']}")

with_harvest = [r for r in rows if int(r["harvestables_for_visibility"]) > 0]
print(f"\n=== harvestable links: {len(with_harvest)} entries ===")
for r in with_harvest[:10]:
    print(f"  [{r['index']:>4}] {r['sprite']:<30} n={r['harvestables_for_visibility']} "
          f"surveyed={r['surveyed']} loc={r['locid']}")

# --- the dark patch -------------------------------------------------------------------------
# The full map fits the texture into the screen with a 32px margin, so a point in the screenshot
# converts to image pixels by that fit; the caller passes the converted guess.
image_path = os.path.join(MOD, "maps", "timberwolf_mountain.jpg")
image = Image.open(image_path)
print(f"\n=== {os.path.basename(image_path)}: {image.size[0]}x{image.size[1]} "
      f"mode={image.mode} ===")

original = os.path.join(WORKSPACE, "prepared-maps", "timberwolf_mountain.jpg")
if os.path.exists(original):
    other = Image.open(original)
    same = (other.size == image.size) and (other.tobytes() == image.convert(other.mode).tobytes())
    print(f"prepared-maps copy: {other.size[0]}x{other.size[1]}, identical to the deployed one: {same}")
else:
    print("prepared-maps copy: not found")

import hashlib
for label, path in (("deployed", image_path), ("workspace", original)):
    if os.path.exists(path):
        digest = hashlib.sha256(open(path, "rb").read()).hexdigest()[:16]
        print(f"  {label:<10} sha256={digest} size={os.path.getsize(path)}")

# Locate the darkest large blob: the reported patch is much darker than the map's paper and than
# the tan highlands, so a downscaled min-filter finds it wherever the screenshot estimate was off.
small = image.convert("L").resize((image.width // 8, image.height // 8), Image.BOX)
arr = np.asarray(small, dtype=np.float32)
print(f"\ndownscaled to {arr.shape[1]}x{arr.shape[0]}; darkest 5x5 neighbourhoods:")
flat = arr.copy()
for _ in range(5):
    idx = int(np.argmin(flat))
    y, x = divmod(idx, flat.shape[1])
    print(f"  image=({x * 8:>5},{y * 8:>5})  luma={arr[y, x]:.0f}")
    flat[max(0, y - 6):y + 7, max(0, x - 6):x + 7] = 255

guess = (1767, 1871)
half = 420
box = (max(0, guess[0] - half), max(0, guess[1] - half),
       min(image.width, guess[0] + half), min(image.height, guess[1] + half))
crop = image.crop(box)
crop = crop.resize((crop.width * 2, crop.height * 2), Image.LANCZOS)
out = os.path.join(WORKSPACE, "tools", "tm-dark-patch.png")
crop.save(out)
print(f"\nwrote {out}  (crop of {box})")

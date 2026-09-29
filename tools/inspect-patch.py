"""Locate a soft, large-scale darkening in the community image by measurement.

A hand-drawn map's own features are either high contrast (lines, icons, text) or flat colour. A
smudge is neither: it is a wide, low-contrast dip in brightness. So compare a wide local mean
against a much wider one, and reject any candidate whose neighbourhood contains near-black
pixels, which is what artwork strokes are.
"""

import os
import numpy as np
from PIL import Image

MOD = r"D:\Program Files (x86)\Steam\steamapps\common\TheLongDark\Mods\CommunityMinimap"
WORKSPACE = r"D:\CommunityMinimap-Workspace"

image = Image.open(os.path.join(MOD, "maps", "timberwolf_mountain.jpg")).convert("RGB")
grey = np.asarray(image.convert("L"), dtype=np.float32)
height, width = grey.shape


def box_mean(a, radius):
    pad = np.pad(a, radius, mode="edge")
    integral = np.pad(pad.cumsum(0).cumsum(1), ((1, 0), (1, 0)))
    size = radius * 2 + 1
    h, w = a.shape
    total = (integral[size:size + h, size:size + w]
             - integral[:h, size:size + w]
             - integral[size:size + h, :w]
             + integral[:h, :w])
    return total / (size * size)


narrow = box_mean(grey, 25)
wide = box_mean(grey, 110)
darkest = box_mean(grey, 25)  # placeholder, replaced below by a min filter

# Reject artwork: a neighbourhood holding near-black pixels is a stroke or an icon.
black = (grey < 90).astype(np.float32)
black_ratio = box_mean(black, 60)

drop = wide - narrow
drop[black_ratio > 0.0005] = -1e9

print("candidates: a 51px area noticeably darker than its 221px surroundings, with no artwork in sight")
work = drop.copy()
for _ in range(6):
    idx = int(np.argmax(work))
    y, x = divmod(idx, work.shape[1])
    print(f"  image=({x:>5},{y:>5})  drop={drop[y, x]:6.1f}  "
          f"narrow={narrow[y, x]:5.1f}  wide={wide[y, x]:5.1f}")
    work[max(0, y - 200):y + 201, max(0, x - 200):x + 201] = -1e9

# The strongest candidate, rendered 1:1 next to a contrast-stretched version of the same pixels.
idx = int(np.argmax(drop))
cy, cx = divmod(idx, drop.shape[1])
half = 200
box = (max(0, cx - half), max(0, cy - half), min(width, cx + half), min(height, cy + half))
crop = image.crop(box)

arr = np.asarray(crop.convert("L"), dtype=np.float32)
lo, hi = np.percentile(arr, 2), np.percentile(arr, 98)
stretched = np.clip((arr - lo) / max(1.0, hi - lo) * 255.0, 0, 255).astype(np.uint8)
stretched = np.stack([stretched] * 3, axis=-1)

panel = Image.new("RGB", (crop.width * 2 + 12, crop.height), (20, 20, 20))
panel.paste(crop, (0, 0))
panel.paste(Image.fromarray(stretched), (crop.width + 12, 0))
out = os.path.join(WORKSPACE, "tools", "tm-patch-candidate.png")
panel.save(out)
print(f"\nstrongest candidate at ({cx},{cy}); wrote {out}  crop={box}")

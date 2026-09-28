"""Survey every prepared map for leftover sheet furniture (ruler, legend, credits, insets).

The artwork is printed on cream paper; a sheet's legend, ruler and credit blocks sit on
plain white. Scanning up from the bottom, the boundary is where the paper stops being
cream. Maps whose artwork is not cream report as undetermined rather than guessing.
"""
import os
import numpy as np
from PIL import Image

PREP = r"D:\CommunityMinimap-Workspace\prepared-maps"

def survey(path):
    a = np.asarray(Image.open(path).convert("RGB"), dtype=np.int16)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    cream = (((r - b) > 16) & (r > 195)).mean(axis=1)
    white = ((np.abs(r - b) < 10) & (r > 235)).mean(axis=1)
    H = len(cream)
    overall = cream.mean()
    if overall < 0.20:
        return "undetermined (artwork is not cream)", H, None
    y = H - 1
    while y > 0 and cream[y] < 0.35:
        y -= 1
    while y > 0:
        lo = max(0, y - 30)
        if cream[lo:y + 1].mean() > 0.45:
            break
        y -= 1
    below = cream[y + 1:].mean() if y + 1 < H else 0.0
    if below > 0.20:
        return "undetermined (bottom is cream too)", H, None
    if (H - y) < 40:
        return "clean", H, None
    return f"TRIM to {y + 1}  (remove {H - y - 1} rows)", H, y + 1

print(f"{'map':<40}{'size':>12}  result")
todo = []
for f in sorted(os.listdir(PREP)):
    if not f.endswith(".jpg"):
        continue
    p = os.path.join(PREP, f)
    im = Image.open(p)
    W, H = im.size
    verdict, h, keep = survey(p)
    print(f"{f:<40}{W:>5}x{H:<6}  {verdict}")
    if keep is not None:
        todo.append((f, W, H, keep))

print()
if todo:
    print("bottom trims still to apply:")
    for f, W, H, keep in todo:
        print(f"   {f:<40} {W}x{H} -> {W}x{keep}")
else:
    print("nothing to trim")

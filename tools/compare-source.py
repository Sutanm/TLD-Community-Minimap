"""Compare the prepared map against the source pack it was cropped from.

If the smudge is in the source artwork it is the map author's own editing artefact and there is
nothing for us to fix; if it is only in our prepared copy then our crop or save introduced it.
"""

import os
import numpy as np
from PIL import Image

SOURCE = r"D:\BaiduNetdiskDownload\漫漫长夜v2.39地图高清重制\05 林狼雪岭.jpg"
PREPARED = r"D:\CommunityMinimap-Workspace\prepared-maps\timberwolf_mountain.jpg"
DEPLOYED = (r"D:\Program Files (x86)\Steam\steamapps\common\TheLongDark"
            r"\Mods\CommunityMinimap\maps\timberwolf_mountain.jpg")
CROP = (158, 145, 4538, 4447)
WORKSPACE = r"D:\CommunityMinimap-Workspace"

source = Image.open(SOURCE).convert("RGB")
print(f"source   {source.size}  {os.path.getsize(SOURCE)} bytes")
prepared = Image.open(PREPARED).convert("RGB")
print(f"prepared {prepared.size}  {os.path.getsize(PREPARED)} bytes")

cropped = source.crop(CROP)
print(f"source.crop{CROP} -> {cropped.size}, matches prepared: {cropped.size == prepared.size}")

a = np.asarray(cropped, dtype=np.int16)
b = np.asarray(prepared, dtype=np.int16)
diff = np.abs(a - b)
print(f"pixel difference: max={diff.max()} mean={diff.mean():.3f} "
      f"pixels differing by >8: {(diff.max(axis=2) > 8).sum()}")

# The reported spot, in both frames.
px, py = 1662, 1862
sx, sy = px + CROP[0], py + CROP[1]
half = 300
box_prepared = (px - half, py - half, px + half, py + half)
box_source = (sx - half, sy - half, sx + half, sy + half)

left = source.crop(box_source)
right = prepared.crop(box_prepared)

panel = Image.new("RGB", (left.width * 2 + 16, left.height), (20, 20, 20))
panel.paste(left, (0, 0))
panel.paste(right, (left.width + 16, 0))
out = os.path.join(WORKSPACE, "tools", "tm-source-vs-prepared.png")
panel.save(out)
print(f"\nleft = source{box_source}, right = prepared{box_prepared}")
print(f"wrote {out}")

# And the same spot contrast-stretched, which is what makes a faint smudge visible.
for label, img in (("source", left), ("prepared", right)):
    arr = np.asarray(img.convert("L"), dtype=np.float32)
    lo, hi = np.percentile(arr, 2), np.percentile(arr, 98)
    stretched = np.clip((arr - lo) / max(1.0, hi - lo) * 255.0, 0, 255).astype(np.uint8)
    Image.fromarray(stretched).save(
        os.path.join(WORKSPACE, "tools", f"tm-stretch-{label}.png"))
print("wrote contrast-stretched versions of each half")

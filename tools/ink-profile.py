"""Compare feature placement between the game capture and the old base map.

Both are the same region, so a distinctive dark feature should sit at the same
normalised position inside each image's content box. Comparing centroids and
row/column ink profiles is objective where eyeballing two blurry grey maps is
not.

Source: AI written for the layer-split investigation.
"""
from PIL import Image
import numpy as np

GAME = r"D:\Program Files (x86)\Steam\steamapps\common\TheLongDark\Mods\CommunityMinimap"
PANEL = GAME + r"\panelmap_MountainTownRegion.png"
BASE = GAME + r"\basemap_MountainTownRegion.png"


def ink_profile(path, threshold=40):
    img = Image.open(path).convert("RGBA")
    alpha = np.array(img.getchannel("A"))
    box = Image.fromarray(alpha).point(lambda v: 255 if v > threshold else 0).getbbox()
    rgb = np.array(img.convert("RGB")).astype(float)
    grey = rgb.mean(axis=2)
    alpha_f = alpha.astype(float) / 255.0
    # Ink = how dark the pixel is, weighted by opacity so padding does not count.
    ink = (255.0 - grey) * alpha_f
    sub = ink[box[1]:box[3], box[0]:box[2]]
    h, w = sub.shape
    total = sub.sum()
    ys, xs = np.mgrid[0:h, 0:w]
    cx = (sub * xs).sum() / total / w
    cy = (sub * ys).sum() / total / h
    col = sub.sum(axis=0)
    row = sub.sum(axis=1)
    print(f"{path.split(chr(92))[-1]}")
    print(f"  content {w}x{h}  aspect={w/h:.3f}")
    print(f"  ink centroid (normalised in content): x={cx:.4f} y={cy:.4f}")
    print(f"  ink total={total/1e6:.1f}M")
    return cx, cy, w, h, col / col.sum(), row / row.sum()


pcx, pcy, pw, ph, pcol, prow = ink_profile(PANEL)
bcx, bcy, bw, bh, bcol, brow = ink_profile(BASE)

print()
print(f"centroid delta: dx={abs(pcx-bcx):.4f} dy={abs(pcy-bcy):.4f}")
print(f"content aspect: panel={pw/ph:.3f} base={bw/bh:.3f} ratio={ (pw/ph)/(bw/bh):.4f}")

# Where does the ink sit along each axis? Compare the cumulative profiles, which
# says whether the same terrain occupies the same fraction of each image.
def half_points(profile):
    c = np.cumsum(profile)
    return float(np.searchsorted(c, 0.25) / len(profile)), float(np.searchsorted(c, 0.75) / len(profile))


p25, p75 = half_points(pcol)
b25, b75 = half_points(bcol)
print(f"column ink quartiles: panel {p25:.3f}/{p75:.3f}  base {b25:.3f}/{b75:.3f}")
p25r, p75r = half_points(prow)
b25r, b75r = half_points(brow)
print(f"row    ink quartiles: panel {p25r:.3f}/{p75r:.3f}  base {b25r:.3f}/{b75r:.3f}")

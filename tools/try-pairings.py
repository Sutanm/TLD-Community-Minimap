"""Which world position belongs to which landmark?

The five recorded positions were paired with the five map labels in the order the player listed
them, and that produced a 267px worst residual plus a 2.8x anisotropy, which is impossible for a
map whose world span and image span are both roughly square. Either a label position is wrong or
the pairing is. Trying every permutation separates those two cases: if some other pairing fits to
a few pixels, the pairing was the problem.
"""

import itertools

import numpy as np

WIDTH, HEIGHT = 4380, 4302

# World positions, in the order the calibration points were recorded (from the CSV).
WORLD = [
    ("P1", 927.089, 1178.602),
    ("P2", 888.605, 342.435),
    ("P3", 431.252, 283.077),
    ("P4", 1570.803, 475.992),
    ("P5", 429.086, 1816.675),
]

# Image positions of the named labels, as located by colour (find-labels.py).
LABELS = [
    ("机尾", 1728, 1656),
    ("登山者棚屋", 1656, 3408),
    ("起落架", 1756, 3600),
    ("机翼-右下角", 2416, 3120),
    ("瀑布山洞", 792, 360),
]


def fit(pairs):
    world = np.array([[p[0][1], p[0][2], 1.0] for p in pairs])
    target = np.array([[p[1][1] / WIDTH, 1.0 - p[1][2] / HEIGHT] for p in pairs])
    coeffs, *_ = np.linalg.lstsq(world, target, rcond=None)
    errors = []
    for (_, wx, wz), (_, ix, iy) in pairs:
        u, v = np.array([wx, wz, 1.0]) @ coeffs
        errors.append(((u * WIDTH - ix) ** 2 + ((1 - v) * HEIGHT - iy) ** 2) ** 0.5)
    return coeffs, max(errors), errors


results = []
for order in itertools.permutations(range(5)):
    pairs = list(zip(WORLD, [LABELS[i] for i in order]))
    coeffs, worst, _ = fit(pairs)
    # Scale per 1000 world units along each axis, and how far the axes are from perpendicular.
    p0 = np.array([0.0, 0.0, 1.0]) @ coeffs
    px = np.array([1000.0, 0.0, 1.0]) @ coeffs
    pz = np.array([0.0, 1000.0, 1.0]) @ coeffs
    ax = np.array([px[0] - p0[0], -(px[1] - p0[1])]) * [WIDTH, HEIGHT]
    az = np.array([pz[0] - p0[0], -(pz[1] - p0[1])]) * [WIDTH, HEIGHT]
    angle = np.degrees(np.arccos(np.clip(
        ax @ az / (np.linalg.norm(ax) * np.linalg.norm(az)), -1, 1)))
    results.append((worst, np.linalg.norm(ax), np.linalg.norm(az), angle, pairs, order))

results.sort(key=lambda r: r[0])
print("best five pairings by worst residual:\n")
for worst, sx, sz, angle, pairs, order in results[:5]:
    print(f"  worst={worst:7.1f}px  1000x={sx:6.0f}px  1000z={sz:6.0f}px  axes={angle:5.1f}deg")
    print("     " + "  ".join(f"{w[0]}->{l[0]}" for w, l in pairs))
    print()

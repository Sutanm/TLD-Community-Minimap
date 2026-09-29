"""Find which UILabel objects carry real place names rather than templates.

The panel shows place names through per-marker HoverWidget/Label objects that sit
at 'NEW LABEL' until something fills them in. If the game has already filled any
of them, the finished text is sitting in the hierarchy dump and can be read
directly instead of resolving the location key ourselves.

Source: AI written for the layer-split investigation.
"""
import re
from collections import Counter

PATH = (r"D:\Program Files (x86)\Steam\steamapps\common\TheLongDark"
        r"\Mods\CommunityMinimap\vanilla_map_hierarchy.txt")

ui_re = re.compile(r"UILabel='(?P<text>[^']*)'")
template_values = {"NEW LABEL", "", "MAP NAME", "OBJECTIVE", "OBJECTIVE DESCRIPTION"}

rows = []
with open(PATH, encoding="utf-8", errors="replace") as handle:
    for line in handle:
        line = line.rstrip("\n")
        if "|" not in line:
            continue
        path, rest = line.split("|", 1)
        m = ui_re.search(rest)
        if not m:
            continue
        rows.append((path.strip(), m.group("text"), "activeInHierarchy=True" in rest))

print(f"total UILabel rows: {len(rows)}")
cjk = [r for r in rows if any("\u4e00" <= ch <= "\u9fff" for ch in r[1])]
print(f"rows whose text contains CJK: {len(cjk)}")

print("\n--- CJK labels, first 30 ---")
for path, value, active in cjk[:30]:
    print(f"  {value!r:24} {'active' if active else 'inactive'}  <- {path}")

print("\n--- value counts (top 20) ---")
for value, count in Counter(r[1] for r in rows).most_common(20):
    print(f"  {count:5d}  {value!r}")

print("\n--- HoverWidget labels that are NOT the template ---")
hover = [r for r in rows if "HoverWidget" in r[0] and r[1] not in template_values]
print(f"count: {len(hover)}")
for path, value, active in hover[:30]:
    print(f"  {value!r:24} active={active} <- {path}")

"""Remove superseded localization probes from ModEntry.cs.

Three methods were replaced by ResolveLocalizationMembers, which holds the
members the locator scan actually verified:
  ProbeLocaleMembers  - enumerated Il2Cpp.Locale, whose GetText echoes the key
  ScanForLocator      - found Il2Cpp.Localization.Get; its job is done
  ProbeLocalization   - the original name-guessing attempt

Deleting by explicit line ranges, with the ranges printed first so the edit can
be checked rather than trusted.

Source: AI written for the layer-split investigation.
"""
import re
import sys

PATH = r"D:\CommunityMinimap-Workspace\ModEntry.cs"

with open(PATH, encoding="utf-8", newline="") as handle:
    text = handle.read()

newline = "\r\n" if "\r\n" in text else "\n"
lines = text.split(newline)

# Find each method's start and the line that closes it, by brace counting from its
# signature. This avoids hard-coded line numbers going stale.
def span(signature, keep=False):
    for i, line in enumerate(lines):
        if signature in line:
            depth = 0
            started = False
            for j in range(i, len(lines)):
                depth += lines[j].count("{") - lines[j].count("}")
                if "{" in lines[j]:
                    started = True
                if started and depth == 0:
                    return i, j
            return i, i
    return None, None


targets = [
    "private void ProbeLocaleMembers()",
    "private void ScanForLocator()",
    "private void ProbeLocalization()",
]

ranges = []
for signature in targets:
    start, end = span(signature)
    if start is None:
        print(f"NOT FOUND: {signature}")
        sys.exit(1)
    ranges.append((start, end))
    print(f"{signature}: lines {start + 1}..{end + 1}")

# Also drop the doc comment block immediately above each method.
for start, end in ranges:
    top = start
    while top - 1 >= 0 and lines[top - 1].lstrip().startswith("//"):
        top -= 1
    ranges[ranges.index((start, end))] = (top, end)

drop = set()
for start, end in ranges:
    for i in range(start, end + 1):
        drop.add(i)

kept = [line for i, line in enumerate(lines) if i not in drop]
with open(PATH, "w", encoding="utf-8", newline="") as handle:
    handle.write(newline.join(kept))

print(f"removed {len(drop)} lines; {len(lines)} -> {len(kept)}")

"""Prove the partial-class split changed nothing.

Compiling is not proof of equivalence: a member could have been dropped from one
file while another still compiles cleanly. This compares the monolith against the
split set member by member, ignoring only leading indentation and blank lines -
which the splitter is allowed to change and nothing else is.

Any difference is reported with the member's name, so a failure points at one
member rather than at "the refactor".

Source: AI written for the decoupling refactor.
"""
import os
import re
import sys

ROOT = r"D:\CommunityMinimap-Workspace"
MONOLITH = os.path.join(ROOT, "obj", "ModEntry.monolith.backup.cs")
SPLIT_GLOB = re.compile(r"^ModEntry\.[A-Za-z]+\.cs$")

DECL = re.compile(
    r"^    (?:private|internal|public|protected)\s"
)
METHOD = re.compile(r"\b([A-Za-z_][A-Za-z0-9_]*)\s*\(")


def bracket_delta(line):
    return (line.count("{") - line.count("}")
            + line.count("(") - line.count(")")
            + line.count("[") - line.count("]"))


def members_of(path):
    with open(path, encoding="utf-8", newline="") as handle:
        text = handle.read()
    newline = "\r\n" if "\r\n" in text else "\n"
    lines = text.split(newline)

    # find the class body
    open_i = None
    for i, l in enumerate(lines):
        if re.match(r"^public (?:sealed )?(?:partial )?class ModEntry\b", l):
            for j in range(i, len(lines)):
                if "{" in lines[j]:
                    open_i = j
                    break
            break
    depth = 0
    close_i = None
    for i in range(open_i, len(lines)):
        depth += lines[i].count("{") - lines[i].count("}")
        if depth == 0:
            close_i = i
            break
    body = lines[open_i + 1:close_i]

    spans = []
    i = 0
    while i < len(body):
        l = body[i]
        if not (DECL.match(l) and i == 0 or (DECL.match(l))):
            i += 1
            continue
        d = 0
        end = len(body) - 1
        j = i
        while j < len(body):
            d += bracket_delta(body[j])
            s = body[j].rstrip()
            if d == 0 and j > i and s.endswith("}"):
                end = j
                break
            if d == 0 and s.endswith(";"):
                end = j
                break
            j += 1
        spans.append((i, end))
        i = end + 1

    out = []
    for s, e in spans:
        # name from the first non-comment, non-blank line
        k = s
        while k <= e and (not body[k].strip() or body[k].strip().startswith("//")):
            k += 1
        if k > e:
            continue
        head = re.split(r"[=;{]", body[k].split("//")[0], maxsplit=1)[0].strip()
        m = METHOD.search(head)
        if m:
            name = m.group(1)
        else:
            m2 = re.search(r"([A-Za-z_][A-Za-z0-9_]*)\s*$", head)
            name = m2.group(1) if m2 else head
        # normalise: drop blank lines and strip indentation
        norm = tuple(x.strip() for x in body[s:e + 1] if x.strip())
        out.append((name, norm))
    return out


mono = members_of(MONOLITH)
print(f"monolith members: {len(mono)}")

split = []
for fname in sorted(os.listdir(ROOT)):
    if SPLIT_GLOB.match(fname):
        got = members_of(os.path.join(ROOT, fname))
        print(f"  {fname:28} {len(got):3} members")
        split.extend(got)
print(f"split members total: {len(split)}")

mono_by = {}
for n, b in mono:
    mono_by.setdefault(n, []).append(b)
split_by = {}
for n, b in split:
    split_by.setdefault(n, []).append(b)

print()
missing = [n for n in mono_by if n not in split_by]
extra = [n for n in split_by if n not in mono_by]
print(f"missing from split : {len(missing)}  {missing[:10]}")
print(f"added by split     : {len(extra)}  {extra[:10]}")

differing = []
for n, bodies in mono_by.items():
    if n not in split_by:
        continue
    a = sorted(bodies, key=len)
    b = sorted(split_by[n], key=len)
    if len(a) != len(b) or any(x != y for x, y in zip(a, b)):
        differing.append(n)

print(f"members whose body changed: {len(differing)}  {differing[:10]}")

if not missing and not extra and not differing:
    print("\nEQUIVALENT: every member is present exactly once with an identical body.")
    sys.exit(0)
print("\nNOT equivalent - see the lists above.")
sys.exit(1)

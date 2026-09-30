"""Partition ModEntry's members into partial-class files by functional area.

Members carry their leading comments and blank separators (the parser attaches
them), so splitting by member index keeps every comment with its code.

The assignment is by member NAME rather than by line range: line ranges go stale
the moment anything above them moves, and a name either matches or is reported as
unassigned. Anything unassigned is a hard error, so a new member cannot be
silently dropped.

Source: AI written for the decoupling refactor.
"""
import os
import re
import sys
from collections import OrderedDict

SRC = r"D:\CommunityMinimap-Workspace\ModEntry.cs"
STAGE = r"D:\CommunityMinimap-Workspace\obj\split-preview"

with open(SRC, encoding="utf-8", newline="") as handle:
    text = handle.read()

newline = "\r\n" if "\r\n" in text else "\n"
lines = text.split(newline)

for i, line in enumerate(lines):
    if re.match(r"^public sealed class ModEntry\b", line):
        class_line = i
        break

for i in range(class_line, len(lines)):
    if "{" in lines[i]:
        open_line = i
        break

depth = 0
for i in range(open_line, len(lines)):
    depth += lines[i].count("{") - lines[i].count("}")
    if depth == 0:
        close_line = i
        break

header = lines[:open_line]
body = lines[open_line + 1:close_line]
# tail already contains the class's closing brace; only the attribution comment that follows
# it is worth keeping. Keeping the whole tail added a second closing brace to every file.
tail = [l for l in lines[close_line:] if not l.strip().startswith("}")]


def bracket_delta(line):
    return (line.count("{") - line.count("}")
            + line.count("(") - line.count(")")
            + line.count("[") - line.count("]"))


member_start_re = re.compile(
    r"^    (?:\[|//|///|private |internal |public |protected |internal static |private static |public static |protected static )"
)

starts = []
i = 0
while i < len(body):
    if not (member_start_re.match(body[i]) and not body[i].strip().startswith("//")):
        i += 1
        continue
    depth2 = 0
    end = len(body) - 1
    j = i
    while j < len(body):
        depth2 += bracket_delta(body[j])
        stripped = body[j].rstrip()
        if depth2 == 0 and j > i and stripped.endswith("}"):
            end = j
            break
        if depth2 == 0 and stripped.endswith(";"):
            end = j
            break
        j += 1
    starts.append((i, end))
    i = end + 1

members = []
for start, end in starts:
    lead = start
    while lead - 1 >= 0:
        prev = body[lead - 1].strip()
        if prev == "" or prev.startswith("//"):
            lead -= 1
            continue
        break
    if members and lead <= members[-1][1]:
        lead = members[-1][1] + 1
    members.append((lead, end))

print(f"{len(members)} members, {sum(e - s + 1 for s, e in members)} of {len(body)} lines covered")

# Name each member.
#
# The span begins at the member's leading comments and blank separators, not at its
# declaration, so the name has to be read from the first line that is not blank and not a
# comment. Reading body[start] directly returned None for most members and they were then
# mistaken for comment runs - which is why the area partition looked as if only a handful
# of members existed while the parse had found all 230.
def member_name(start, end):
    decl_index = start
    while decl_index <= end:
        stripped = body[decl_index].strip()
        if stripped and not stripped.startswith("//"):
            break
        decl_index += 1
    if decl_index > end:
        return None

    raw = body[decl_index]
    # Cut the declaration at its first '=', ';' or '{'. Everything after that belongs to
    # the initialiser or the body, and matching inside it is how a field initialised with
    # '= new(...)' was reported as a member called 'new'.
    head = re.split(r"[=;{]", raw.split("//")[0], maxsplit=1)[0].strip()
    if not head:
        return None
    if head.startswith("["):
        return None

    # A method: the last identifier before '(' on the declaration head.
    m = re.search(r"\b([A-Za-z_][A-Za-z0-9_]*)\s*\(", head)
    if m:
        return m.group(1)

    # A field, constant or property: the identifier after the modifiers and the type.
    #
    # The type-matching group is non-greedy on purpose. `(?:word\s+)+` followed by a name
    # group matches everything up to the LAST identifier, so `private static bool
    # s_fullMapActive` gave the name group nothing and the member was reported as
    # unnamed. Lazy matching leaves the final identifier for the name, which is correct
    # for qualified types too (`System.Reflection.MethodInfo s_x`).
    m = re.match(
        r"(?:private|internal|public|protected)\s+"
        r"(?:static\s+)?(?:readonly\s+)?(?:const\s+)?"
        r"(?:[A-Za-z_][A-Za-z0-9_]*\s*<[^>]*>\s*"      # generic type
        r"|[A-Za-z_][A-Za-z0-9_\.]*\s*\[\s*\]\s*"       # array type
        r"|(?:[A-Za-z_][A-Za-z0-9_\.]*\s+)+?)"          # one or more type words, lazy
        r"([A-Za-z_][A-Za-z0-9_]*)",
        head)
    if m:
        return m.group(1)

    # A nested type.
    m = re.search(r"\b(?:partial\s+)?(?:sealed\s+)?(?:class|enum|struct)\s+([A-Za-z_][A-Za-z0-9_]*)", head)
    if m:
        return m.group(1)

    # A property with an accessor block, or an expression-bodied member.
    m = re.search(r"\b([A-Za-z_][A-Za-z0-9_]*)\s*(?:=>|\{)", raw)
    if m:
        return m.group(1)

    ids = re.findall(r"[A-Za-z_][A-Za-z0-9_]*", head)
    return ids[-1] if ids else None


named = [(member_name(s, e), s, e) for s, e in members]

# Drop spans that are not declarations at all: pure comment or blank runs between
# members. Leaving them in made the accounting look wrong (230 "members" of which many
# were comment blocks reported as <unnamed@N>), and a comment-only span has no
# declaration to move and belongs with the layout rather than with any one area.
declared = [(n, s, e) for n, s, e in named if n]
orphans = [(s, e) for n, s, e in named if not n]
print(f"comment/blank runs (kept in place, not moved): {len(orphans)}")
named = declared

if "--dump" in sys.argv:
    for n, s, e in named:
        print(f"{open_line + 2 + s:5}  {n:46} :: {body[s].strip()[:64]}")
    sys.exit(0)

# Functional areas. Names are assigned explicitly; an unmatched member is an error.
AREAS = OrderedDict()
# Core holds the entry point and, by default, everything not named elsewhere: the
# field block and the nested types. That is deliberate rather than lazy - the field
# audit showed the UI handles and settings are touched by eight to eleven different
# areas, so they ARE the shared state, and putting them in one place names that
# honestly instead of pretending the areas own them.
AREAS["ModEntry.Core"] = [
    "OnInitializeMelon", "OnUpdate", "OnSceneWasInitialized",
]
AREAS["ModEntry.Layers"] = [
    "LayerMini", "LayerFull", "_layers", "ActiveLayerId", "ActiveLayer", "LayerName",
    "ObserveScene", "LayerWantsCommunity", "ShouldUseCommunityMap", "ReadLayerSettings",
    "DescribeSource", "ApplyMapSourceSelection", "ClearVanillaProjection", "RequestMapLoad",
    "AnyVanillaLayerReady", "AnyLayerReady", "TryGetOpenVanillaMap", "DescribeView",
    "ApplyViewState", "CycleMiniFullNone", "CycleMiniOnly", "CycleFullOnly", "CycleView",
    "OpenFullMap", "CloseFullMap", "CategoryEnabled",
]
AREAS["ModEntry.Input"] = [
    "TryRedirectGameMap", "HandleSurveyMapPopup", "LeaveFullMap", "PollOpenMapKey",
    "HandleFullMapInput", "ApplyMapInputContext", "ReleaseMapInputContext",
    "ShouldSuppressGameEscape", "UpdateFullMapHints", "BuildHintText", "EnsureHintLabel",
    "ResolveHintFont",
]
AREAS["ModEntry.Rendering"] = [
    "RetireTexture", "SweepRetiredTextures", "SetUiVisible", "UpdateUnityUi",
    "LoadCurrentMapIntoUnityUi", "EnsureUnityUi", "CreateUiObject", "SamplePointer",
    "GetPointerAccent", "PointInTriangle", "Sign", "AlphaOver", "ApplyPointerPalette",
    "GetTextureAspect", "TryPlayerToMapUv", "TryWorldToMarkerUv", "VanillaMapPositionToUv",
    "VanillaLocalToTextureUv", "FindChildByName", "WriteTextureToPng",
    "SanitizeFileName", "EscapeCsv", "AppendTransformDiagnostics",
]
AREAS["ModEntry.Markers"] = [
    "RefreshMarkerCategoryFlags", "UpdateVanillaIcons", "TryResolveIcon", "BuildMarkerIcon",
    "RebuildMarkersFromMapDetails", "ClearVanillaIcons", "CaptureVanillaIcons",
    "CaptureVanillaIconsRecursive", "IsMapIconChrome", "TryGetSpriteUv",
    "BuildMarkersAndLabels", "CategorizeSprite",
]
AREAS["ModEntry.Labels"] = [
    "RebuildLabelsFromMapDetails", "ClearLabels", "UpdateMapLabels", "ShowHoverLabel",
    "CreateHoverTooltip", "AddTextShadow", "LocalizeLabel", "LocalizeLabelCached",
    "ResolveLocalizationMembers", "CapturePanelLabelTexts",
]
AREAS["ModEntry.VanillaMap"] = [
    "TryRequestVanillaBaseMap", "PollVanillaBaseMap", "UseVanillaBaseMap",
    "UseCapturedVanillaMap", "TryRefreshVanillaIcons", "ComputeVanillaIconSignature",
    "FindActiveRegionMap", "CaptureVanillaMap", "PopulateIconTableOnce",
    "TryLoadVanillaElementsWithoutPanel", "MeasureOpaqueUv",
]
AREAS["ModEntry.CapturedMap"] = [
    "CapturedMapPath", "SaveCapturedMap", "WriteFramingSidecar", "TryLoadCapturedMap",
    "CaptureGameMapImage", "MeasureAndExportPanelTexture",
]
AREAS["ModEntry.Diagnostics"] = [
    "TryReloadCalibrations", "RecordCalibrationPoint", "RecordVanillaMapCoordinate",
    "LogStateHeartbeat", "DescribeLayer", "CensusMapDetails", "DumpMapDetails",
    "ClassifyMapDetails", "CleanHarvestedMapMarkers", "IsCollected",
    "AllHarvestablesCollected", "ProbeRegionTextures", "DescribeObject",
    "ProbeRegionAssetReferences", "PollPrefabProbes", "PollRegionAssetProbes",
    "DumpVanillaMapHierarchy", "ProbeVanillaFraming", "ProbeMapDetailMembers",
    "TryExportSceneCatalog",
]

name_to_area = {}
for area, names in AREAS.items():
    for n in names:
        name_to_area[n] = area

unassigned = [(n, s, e) for n, s, e in named if n not in name_to_area]
# Everything unnamed or unmatched goes to Core as the shared-state home. The audit is
# still reported so the split is visible rather than silent.
print(f"\nmembers routed to Core by default: {len(unassigned)}")
kinds = {}
for n, s, e in unassigned:
    first = body[s].strip()
    kind = "field/const" if re.match(r"(private|internal|public)\s+(static\s+)?(readonly\s+)?[A-Za-z_<>\[\],\.]+(\[\])?\s+_?\w+\s*(=|;)", first) else (
        "nested type" if "class " in first or "enum " in first or "struct " in first else "other")
    kinds[kind] = kinds.get(kind, 0) + 1
for k, v in sorted(kinds.items()):
    print(f"  {k:14} {v}")
print("  (first few: " + ", ".join(n for n, _, _ in unassigned[:6]) + ")")

# --- emit ----------------------------------------------------------------
os.makedirs(STAGE, exist_ok=True)
grouped = OrderedDict((a, []) for a in AREAS)
for n, s, e in named:
    area = name_to_area.get(n, "ModEntry.Core")
    grouped[area].append((n, s, e))

# The class declaration line is reused, with 'sealed' intact and 'partial' added.
decl = header[class_line]
if "partial" not in decl:
    decl = decl.replace("public sealed class", "public sealed partial class").replace(
        "public class", "public partial class")

# The file-leading comment describes the whole original file, so it would be wrong
# repeated verbatim in nine of them. Each output gets its own line naming what it holds.
AREA_TITLES = {
    "ModEntry.Core": "入口点与共享状态：字段、嵌套类型、OnUpdate 调度",
    "ModEntry.Layers": "图层与图源：每层的定义、贴图、投影基准",
    "ModEntry.Input": "输入与视图：地图键接管、全屏地图输入、按键提示",
    "ModEntry.Rendering": "渲染：Canvas/RawImage、指针、投影换算、通用工具",
    "ModEntry.Markers": "标记：从 MapDetail 构建、分类筛选、绘制",
    "ModEntry.Labels": "地名与悬停：标签构建、本地化、悬停提示",
    "ModEntry.VanillaMap": "原版地图数据：底图请求、面板捕获、图标表",
    "ModEntry.CapturedMap": "抓取的底图：按 P 抓取、持久化、加载",
    "ModEntry.Diagnostics": "诊断与校准：普查、探针、导出、采集清理",
}

written = []
for area, items in grouped.items():
    if not items:
        continue
    chunk = []
    for idx, (n, s, e) in enumerate(items):
        if idx > 0:
            chunk.append("")          # keep members visually separated
        chunk.extend(body[s:e + 1])
    while chunk and chunk[-1].strip() == "":
        chunk.pop()

    out = []
    out.extend(header[:class_line])      # usings + namespace
    out.append(decl)
    out.append("{")
    out.extend(chunk)
    out.append("}")
    out.extend(tail)

    # Replace the original file's summary comment with one for this file, and keep the
    # attribution line the project uses at the end of every source file.
    for k, line in enumerate(out):
        if line.startswith("// 社区HUD地图"):
            out[k] = f"// 社区HUD地图 · sutanm — {AREA_TITLES.get(area, area)}"
            break
    if not any(l.startswith("// — sutanm") for l in out):
        # insert before the final closing brace
        out.insert(len(out) - 1, "// — sutanm · 社区HUD地图")

    path = os.path.join(STAGE, area + ".cs")
    with open(path, "w", encoding="utf-8", newline="") as handle:
        handle.write(newline.join(out))
    written.append((area, len(items), len(out)))
    print(f"  wrote {area:26} {len(items):3} members {len(out):5} lines")

print(f"\nstaged in {STAGE}")
print(f"total staged lines: {sum(w[2] for w in written)} (original {len(lines)})")

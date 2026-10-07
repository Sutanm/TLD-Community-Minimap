// 社区HUD地图 · sutanm — 场景 → 地图定义的目录
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CommunityMinimap;

internal enum CalibrationProfile
{
    None,
    MysteryLakeV033
}

internal sealed class MapDefinition
{
    public MapDefinition(
        string id,
        string displayName,
        string fileName,
        CalibrationProfile calibration,
        params string[] scenes)
        : this(id, displayName, fileName, calibration, false, scenes)
    {
    }

    public MapDefinition(
        string id,
        string displayName,
        string fileName,
        CalibrationProfile calibration,
        bool probeOnly,
        params string[] scenes)
    {
        Id = id;
        DisplayName = displayName;
        FileName = fileName;
        Calibration = calibration;
        ProbeOnly = probeOnly;
        Scenes = scenes;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public string FileName { get; }
    public CalibrationProfile Calibration { get; }
    public bool ProbeOnly { get; }
    public IReadOnlyList<string> Scenes { get; }
    // Region maps benefit from opening at the screen-covering local scale. Cave and interior
    // sheets are diagrams with large intentional white areas; enlarging those until they cover a
    // widescreen display makes the drawing look sparse and can crop away exits. Their useful first
    // view is therefore the complete-sheet overview, independent of whether calibration exists.
    public bool PreferFullMapOverview =>
        FileName.StartsWith("cave_", StringComparison.OrdinalIgnoreCase) ||
        FileName.StartsWith("interior_", StringComparison.OrdinalIgnoreCase);
    public bool IsCalibrated => Calibration != CalibrationProfile.None ||
                                CalibrationStore.IsCalibrated(Id);

    public bool TryWorldToMap(Vector3 position, out Vector2 uv)
    {
        uv = default;
        if (CalibrationStore.TryWorldToMap(Id, position, out uv))
            return true;
        if (Calibration != CalibrationProfile.MysteryLakeV033)
            return false;

        const float mapWidth = 2048f;
        const float mapHeight = 2028f;
        const float mapOffsetX = 190f;
        const float mapOffsetZ = 244f;
        const float southCarX = 783.618f;
        const float southCarZ = -49.576f;
        const float southCorrectionRadius = 500f;
        const float southCorrectionPixelsX = -6f;
        const float southCorrectionPixelsY = -54f;

        float dx = position.x - southCarX;
        float dz = position.z - southCarZ;
        float normalizedDistance = Mathf.Clamp01(
            Mathf.Sqrt(dx * dx + dz * dz) / southCorrectionRadius);
        float southWeight = 1f - Mathf.SmoothStep(0f, 1f, normalizedDistance);

        float pixelX = position.x + mapOffsetX + southCorrectionPixelsX * southWeight;
        float pixelYFromTop = (mapHeight - mapOffsetZ) - position.z +
                              southCorrectionPixelsY * southWeight;
        uv = new Vector2(pixelX / mapWidth, 1f - pixelYFromTop / mapHeight);
        return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
    }
}

internal static class MapCatalog
{
    // Cinder Hills Coal Mine is one Unity scene with two physically separated floor plans.
    // The lower floor is an optional Coastal Highway interior; the upper floor connects Coastal
    // Highway and Pleasant Valley. Captured player positions put the lower spawn at Y=-80.992 and
    // the upper floor at Y=0.909, leaving a large, safe gap for a spatial variant boundary.
    private const float MineTransitionUpperFloorMinY = -70f;
    private static readonly MapDefinition MineTransitionLower = new(
        "probe_interior_coastal_mine_lower",
        "沿海公路废弃矿地／煤渣山煤矿下层（探针）",
        "interior_coastal_mine_lower.jpg", CalibrationProfile.None, true,
        "MineTransitionZone");
    private static readonly MapDefinition MineTransitionUpper = new(
        "probe_cave_pleasant_valley_coastal",
        "煤渣山矿洞上层：沿海公路—怡人山谷（探针）",
        "cave_pleasant_valley_coastal.jpg", CalibrationProfile.None, true,
        "MineTransitionZone");

    private static readonly Dictionary<string, MapDefinition> ByScene =
        new(StringComparer.OrdinalIgnoreCase);

    // These definitions deliberately do not belong to the public map catalogue yet.  They let a
    // developer prove that an indoor scene has a stable Unity scene name and can load an
    // independent community image before we spend time calibrating it.  A probe has no projection,
    // so it draws the background but never invents a player position or marker coordinates.
    private static readonly Dictionary<string, MapDefinition> ProbeByScene =
        new(StringComparer.OrdinalIgnoreCase);

    static MapCatalog()
    {
        Add(new MapDefinition("mystery_lake", "神秘湖", "mystery_lake.jpg",
            CalibrationProfile.MysteryLakeV033, "LakeRegion"));
        Add(new MapDefinition("forlorn_muskeg", "孤寂沼地", "forlorn_muskeg.jpg",
            CalibrationProfile.None, "MarshRegion"));
        Add(new MapDefinition("coastal_highway", "沿海公路", "coastal_highway.jpg",
            CalibrationProfile.None, "CoastalRegion"));
        Add(new MapDefinition("pleasant_valley", "怡人山谷", "pleasant_valley.jpg",
            CalibrationProfile.None, "RuralRegion"));
        Add(new MapDefinition("mountain_town", "山间小镇", "mountain_town.jpg",
            CalibrationProfile.None, "MountainTownRegion"));
        Add(new MapDefinition("timberwolf_mountain", "林狼雪岭", "timberwolf_mountain.jpg",
            CalibrationProfile.None, "CrashMountainRegion"));
        Add(new MapDefinition("ash_canyon", "灰烬峡谷", "ash_canyon.jpg",
            CalibrationProfile.None, "AshCanyonRegion"));
        Add(new MapDefinition("hushed_river_valley", "寂静河谷", "hushed_river_valley.jpg",
            CalibrationProfile.None, "RiverValleyRegion"));
        Add(new MapDefinition("bleak_inlet", "荒凉水湾", "bleak_inlet.jpg",
            CalibrationProfile.None, "CanneryRegion"));
        Add(new MapDefinition("desolation_point", "荒芜据点", "desolation_point.jpg",
            CalibrationProfile.None, "WhalingStationRegion"));
        Add(new MapDefinition("blackrock", "黑岩地区", "blackrock.jpg",
            CalibrationProfile.None, "BlackrockRegion"));
        Add(new MapDefinition("broken_railroad", "断开的铁路", "broken_railroad.jpg",
            CalibrationProfile.None, "TracksRegion"));
        Add(new MapDefinition("forsaken_airfield", "废弃机场", "forsaken_airfield.jpg",
            CalibrationProfile.None, "AirfieldRegion"));
        Add(new MapDefinition("crumbling_highway", "公路废墟", "crumbling_highway.jpg",
            CalibrationProfile.None, "HighwayTransitionZone"));
        Add(new MapDefinition("ravine", "深谷", "ravine.jpg",
            CalibrationProfile.None, "RavineTransitionZone"));
        // Composite source images contain multiple independent Unity scenes. Give each
        // scene its own calibration id even though it shares the same texture file.
        Add(new MapDefinition("keepers_pass_north", "守山人山隘北侧", "keepers_pass_north.jpg",
            CalibrationProfile.None, "BlackrockTransitionZone"));
        Add(new MapDefinition("keepers_pass_south", "守山人山隘南侧", "keepers_pass_south.jpg",
            CalibrationProfile.None, "CanyonRoadTransitionZone"));
        Add(new MapDefinition("winding_river", "蜿蜒河流", "winding_river_dam.jpg",
            CalibrationProfile.None, "DamRiverTransitionZoneB"));
        Add(new MapDefinition("zone_of_contamination", "污染区", "zone_of_contamination.jpg",
            CalibrationProfile.None, "ZoneOfContaminationRegion", "MiningRegion"));
        Add(new MapDefinition("sundered_pass", "破碎山道", "sundered_pass.jpg",
            CalibrationProfile.None, "SunderedPassRegion", "MountainPassRegion"));
        Add(new MapDefinition("far_range_branch_line", "远境支路", "far_range_branch_line.jpg",
            CalibrationProfile.None, "LongRailTransitionZone"));
        Add(new MapDefinition("transfer_pass", "中转通道", "transfer_pass.jpg",
            CalibrationProfile.None, "TransferPass", "TransferPassRegion", "HubRegion"));

        AddProbe(new MapDefinition("probe_cave_mystery_lake_mountain_town",
            "连接洞穴：神秘湖—山间小镇（探针）",
            "cave_mystery_lake_mountain_town.jpg", CalibrationProfile.None, true,
            "MountainTownCaveB"));
        AddProbe(new MapDefinition("probe_interior_mountain_town_cave",
            "山间小镇本地洞穴（探针）",
            "interior_mountain_town_cave.jpg", CalibrationProfile.None, true,
            "MountainTownCaveA"));
        AddProbe(new MapDefinition("probe_interior_coal_mine_no3",
            "三号煤矿厂：公路废墟—荒芜据点（探针）",
            "interior_coal_mine_no3.jpg", CalibrationProfile.None, true,
            "HighwayMineTransitionZone"));
        // The scene catalogue proves that both names exist, but the source pack does not say
        // which one is the connector and which one is Ash Canyon's local cave.  Showing the same
        // candidate image in both is intentional for this one probe pass; the log/F11 record tells
        // us which scene owns the connector, after which the other alias will be removed.
        AddProbe(new MapDefinition("probe_cave_ash_canyon_timberwolf",
            "连接洞穴：林狼雪岭—灰烬峡谷（候选探针）",
            "cave_ash_canyon_timberwolf.jpg", CalibrationProfile.None, true,
            "AshCaveA", "AshCaveB"));
        AddProbe(new MapDefinition("probe_cave_timberwolf_blackrock",
            "连接洞穴：林狼雪岭—黑岩（探针）",
            "cave_timberwolf_blackrock.jpg", CalibrationProfile.None, true,
            "BlackrockCaveA"));
        AddProbe(new MapDefinition("probe_cave_forlorn_bleak_inlet",
            "连接洞穴：孤寂沼地—荒凉水湾（探针）",
            "cave_forlorn_bleak_inlet.jpg", CalibrationProfile.None, true,
            "CanneryMarshTransitionCave"));
        AddProbe(new MapDefinition("probe_cave_hrv_mountain_town",
            "连接洞穴：山间小镇—寂静河谷（探针）",
            "cave_hrv_mountain_town.jpg", CalibrationProfile.None, true,
            "RiverValleyTransitionCave"));
        AddProbe(new MapDefinition("probe_interior_railway_worker_tunnel",
            "铁路工人小道（探针）",
            "interior_railway_worker_tunnel.jpg", CalibrationProfile.None, true,
            "LongTransitionCave"));

        // Embedded submaps are cropped independently from their parent region image.  Add them to
        // the runtime one scene at a time as their scene-to-image relationship is verified in
        // game; this avoids silently attaching one of the generic CaveB/C/D aliases to the wrong
        // drawing.  WhalingMine is unambiguous and is the first embedded-map probe.
        AddProbe(new MapDefinition("probe_interior_desolation_mine",
            "五号废弃矿井（探针）",
            "interior_desolation_mine.jpg", CalibrationProfile.None, true,
            "WhalingMine"));
        AddProbe(MineTransitionLower);
        AddProbe(new MapDefinition("probe_interior_blackrock_prison_grounds",
            "黑岩监狱场地（探针）",
            "interior_blackrock_prison_grounds.jpg", CalibrationProfile.None, true,
            "BlackrockPrisonSurvivalZone"));
    }

    public static MapDefinition Find(string sceneName) =>
        sceneName != null && ByScene.TryGetValue(sceneName, out MapDefinition definition)
            ? definition
            : null;

    public static MapDefinition Find(string sceneName, bool includeProbes)
    {
        MapDefinition definition = Find(sceneName);
        if (definition != null || !includeProbes || sceneName == null)
            return definition;
        return ProbeByScene.TryGetValue(sceneName, out definition) ? definition : null;
    }

    public static MapDefinition FindSpatialVariant(string sceneName, float worldY)
    {
        if (!string.Equals(sceneName, "MineTransitionZone",
                StringComparison.OrdinalIgnoreCase))
            return null;
        return worldY >= MineTransitionUpperFloorMinY
            ? MineTransitionUpper
            : MineTransitionLower;
    }

    private static void Add(MapDefinition definition)
    {
        foreach (string scene in definition.Scenes)
            ByScene[scene] = definition;
    }

    private static void AddProbe(MapDefinition definition)
    {
        foreach (string scene in definition.Scenes)
            ProbeByScene[scene] = definition;
    }
}
// — sutanm · 社区HUD地图

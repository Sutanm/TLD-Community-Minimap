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
    // Cinder Hills Coal Mine is one Unity scene with two map sheets and three independently drawn
    // spaces.  Even the lower sheet contains two elevator-linked floors whose X/Z-to-pixel
    // projections differ, so each floor needs its own calibration id despite sharing one image.
    // F11 samples put the deep floor around Y=-81..-75, the elevator floor at Y=-57..-49, and the
    // Coastal Highway--Pleasant Valley connector around Y=-20 and above.  Boundaries sit in the
    // measured empty bands so normal movement cannot flap between variants.
    private const float MineTransitionElevatorFloorMinY = -66f;
    private const float MineTransitionUpperFloorMinY = -35f;
    private static readonly MapDefinition MineTransitionDeep = new(
        "probe_interior_coastal_mine_lower",
        "沿海公路废弃矿地深层",
        "interior_coastal_mine_lower.jpg", CalibrationProfile.None, false,
        "MineTransitionZone");
    private static readonly MapDefinition MineTransitionElevator = new(
        "probe_interior_coastal_mine_elevator",
        "沿海公路废弃矿地电梯上层",
        "interior_coastal_mine_lower.jpg", CalibrationProfile.None, false,
        "MineTransitionZone");
    private static readonly MapDefinition MineTransitionUpper = new(
        "probe_cave_pleasant_valley_coastal",
        "煤渣山矿洞上层：沿海公路—怡人山谷",
        "cave_pleasant_valley_coastal.jpg", CalibrationProfile.None, false,
        "MineTransitionZone");

    // Carter Dam's community sheet arranges three disconnected main-floor drawings plus four
    // tiny stairwell drawings on one image. Several of those spaces overlap in X/Z, so the
    // stairwell is recognized by its tight horizontal footprint first and then split by Y.
    private static readonly MapDefinition DamUpperZoneA = DamUpperVariant(
        "probe_interior_carter_dam_upper_zone_a", "卡特大坝上层区域 A");
    private static readonly MapDefinition DamUpperZoneB = DamUpperVariant(
        "probe_interior_carter_dam_upper_zone_b", "卡特大坝上层区域 B");
    private static readonly MapDefinition DamUpperZoneC = DamUpperVariant(
        "probe_interior_carter_dam_upper_zone_c", "卡特大坝上层区域 C");
    private static readonly MapDefinition DamUpperStairsB1 = DamUpperVariant(
        "probe_interior_carter_dam_upper_stairs_b1", "卡特大坝楼梯间负一层");
    private static readonly MapDefinition DamUpperStairsF1 = DamUpperVariant(
        "probe_interior_carter_dam_upper_stairs_f1", "卡特大坝楼梯间一层");
    private static readonly MapDefinition DamUpperStairsF2 = DamUpperVariant(
        "probe_interior_carter_dam_upper_stairs_f2", "卡特大坝楼梯间二层");
    private static readonly MapDefinition DamUpperStairsF3 = DamUpperVariant(
        "probe_interior_carter_dam_upper_stairs_f3", "卡特大坝楼梯间三层");
    private static readonly MapDefinition DamLowerUpper = DamLowerVariant(
        "probe_interior_carter_dam_lower_upper", "卡特大坝下层·上部区域");
    private static readonly MapDefinition DamLowerMiddle = DamLowerVariant(
        "probe_interior_carter_dam_lower_middle", "卡特大坝下层·中部区域");
    private static readonly MapDefinition DamLowerDeep = DamLowerVariant(
        "probe_interior_carter_dam_lower_deep", "卡特大坝下层·深部区域");
    private static readonly MapDefinition BlackrockPrisonFloor1 = BlackrockPrisonVariant(
        "probe_interior_blackrock_prison_floor1", "黑岩监狱室内一层");
    private static readonly MapDefinition BlackrockPrisonFloor2 = BlackrockPrisonVariant(
        "probe_interior_blackrock_prison_floor2", "黑岩监狱室内二层");
    private static readonly MapDefinition BlackrockPrisonFloor3 = new(
        "probe_interior_blackrock_prison_floor3",
        "黑岩监狱室内三层",
        "interior_blackrock_prison.jpg", CalibrationProfile.None, false,
        "BlackrockPrisonSurvivalZone");
    private static readonly MapDefinition BlackrockPrisonGrounds = new(
        "probe_interior_blackrock_prison_grounds",
        "黑岩监狱场地",
        "interior_blackrock_prison_grounds.jpg", CalibrationProfile.None, false,
        "BlackrockPrisonSurvivalZone");
    private static readonly MapDefinition BlackrockMineFloor1 = BlackrockMineVariant(
        "probe_interior_blackrock_mine_floor1", "黑岩废弃矿地一层");
    private static readonly MapDefinition BlackrockMineFloor2 = BlackrockMineVariant(
        "probe_interior_blackrock_mine_floor2", "黑岩废弃矿地二层");
    private static readonly MapDefinition BlackrockMineFloor3 = BlackrockMineVariant(
        "probe_interior_blackrock_mine_floor3", "黑岩废弃矿地三层");

    // The concentrator scene contains three physically separate buildings on one composite
    // community sheet. X separates the buildings cleanly; Y then selects a floor inside each
    // building. The empty X bands are outdoor/connecting passages absent from the drawings and
    // are treated as transitions so the last trustworthy pointer position is retained.
    private static readonly MapDefinition ConcentratorMiningFloor1 = ConcentratorVariant(
        "probe_interior_concentrator_mining_floor1", "集中器·采矿建筑一层");
    private static readonly MapDefinition ConcentratorMiningFloor2 = ConcentratorVariant(
        "probe_interior_concentrator_mining_floor2", "集中器·采矿建筑二层");
    private static readonly MapDefinition ConcentratorMiningFloor3 = ConcentratorVariant(
        "probe_interior_concentrator_mining_floor3", "集中器·采矿建筑三层");
    private static readonly MapDefinition ConcentratorMiningFloor4 = ConcentratorVariant(
        "probe_interior_concentrator_mining_floor4", "集中器·采矿建筑四层");
    private static readonly MapDefinition ConcentratorMiningFloor5 = ConcentratorVariant(
        "probe_interior_concentrator_mining_floor5", "集中器·采矿建筑五层");
    private static readonly MapDefinition ConcentratorMiningFloor6 = ConcentratorVariant(
        "probe_interior_concentrator_mining_floor6", "集中器·采矿建筑六层");
    private static readonly MapDefinition ConcentratorConveyorFloor1 = ConcentratorVariant(
        "probe_interior_concentrator_conveyor_floor1", "集中器·输送建筑一层");
    private static readonly MapDefinition ConcentratorConveyorFloor2 = ConcentratorVariant(
        "probe_interior_concentrator_conveyor_floor2", "集中器·输送建筑二层");
    private static readonly MapDefinition ConcentratorHeadframeFloor1 = ConcentratorVariant(
        "probe_interior_concentrator_headframe_floor1", "集中器·井架建筑一层");
    private static readonly MapDefinition ConcentratorHeadframeFloor2 = ConcentratorVariant(
        "probe_interior_concentrator_headframe_floor2", "集中器·井架建筑二层");
    private static readonly MapDefinition ConcentratorHeadframeRoofLower = ConcentratorVariant(
        "probe_interior_concentrator_headframe_roof_lower", "集中器·井架屋顶下段");
    private static readonly MapDefinition ConcentratorHeadframeRoofUpper = ConcentratorVariant(
        "probe_interior_concentrator_headframe_roof_upper", "集中器·井架屋顶上段");

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
            "连接洞穴：神秘湖—山间小镇",
            "cave_mystery_lake_mountain_town.jpg", CalibrationProfile.None, false,
            "MountainTownCaveB"));
        AddProbe(new MapDefinition("probe_interior_mountain_town_cave",
            "山间小镇本地洞穴",
            "interior_mountain_town_cave.jpg", CalibrationProfile.None, false,
            "MountainTownCaveA"));
        AddProbe(new MapDefinition("probe_interior_coal_mine_no3",
            "三号煤矿厂：公路废墟—荒芜据点",
            "interior_coal_mine_no3.jpg", CalibrationProfile.None, false,
            "HighwayMineTransitionZone"));
        // The scene catalogue proves that both names exist, but the source pack does not say
        // which one is the connector and which one is Ash Canyon's local cave.  Showing the same
        // candidate image in both is intentional for this one probe pass; the log/F11 record tells
        // us which scene owns the connector, after which the other alias will be removed.
        AddProbe(new MapDefinition("probe_cave_ash_canyon_timberwolf",
            "连接洞穴：林狼雪岭—灰烬峡谷",
            "cave_ash_canyon_timberwolf.jpg", CalibrationProfile.None, false,
            "AshCaveA", "AshCaveB"));
        AddProbe(new MapDefinition("probe_cave_timberwolf_blackrock",
            "连接洞穴：林狼雪岭—黑岩",
            "cave_timberwolf_blackrock.jpg", CalibrationProfile.None, false,
            "BlackrockCaveA"));
        AddProbe(new MapDefinition("probe_cave_forlorn_bleak_inlet",
            "连接洞穴：孤寂沼地—荒凉水湾",
            "cave_forlorn_bleak_inlet.jpg", CalibrationProfile.None, false,
            "CanneryMarshTransitionCave"));
        AddProbe(new MapDefinition("probe_cave_hrv_mountain_town",
            "连接洞穴：山间小镇—寂静河谷",
            "cave_hrv_mountain_town.jpg", CalibrationProfile.None, false,
            "RiverValleyTransitionCave"));
        AddProbe(new MapDefinition("probe_interior_railway_worker_tunnel",
            "铁路工人小道",
            "interior_railway_worker_tunnel.jpg", CalibrationProfile.None, false,
            "LongTransitionCave"));

        // Embedded submaps are cropped independently from their parent region image.  Add them to
        // the runtime one scene at a time as their scene-to-image relationship is verified in
        // game; this avoids silently attaching one of the generic CaveB/C/D aliases to the wrong
        // drawing.  WhalingMine is unambiguous and is the first embedded-map probe.
        AddProbe(new MapDefinition("probe_interior_desolation_mine",
            "五号废弃矿井",
            "interior_desolation_mine.jpg", CalibrationProfile.None, false,
            "WhalingMine"));
        AddProbe(MineTransitionDeep);
        AddProbe(BlackrockPrisonGrounds);
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

    public static MapDefinition FindSpatialVariant(string sceneName, Vector3 position)
    {
        if (string.Equals(sceneName, "BlackrockPrisonSurvivalZone",
                StringComparison.OrdinalIgnoreCase))
        {
            // Floor 3 is embedded in the outdoor prison scene rather than the two-floor interior
            // scene. Keep the detailed floor-plan override tightly bounded to the measured prison
            // building footprint; everywhere else must continue using the prison-grounds sheet.
            bool inFloor3 = position.x >= -245f && position.x <= -225f &&
                            position.z >= 153f && position.z <= 169f &&
                            position.y >= 232f && position.y <= 236f;
            return inFloor3 ? BlackrockPrisonFloor3 : BlackrockPrisonGrounds;
        }

        if (string.Equals(sceneName, "BlackrockInteriorASurvival",
                StringComparison.OrdinalIgnoreCase))
        {
            if (IsSpatialTransition(sceneName, position))
                return null;
            return position.y >= 3f
                ? BlackrockPrisonFloor2
                : BlackrockPrisonFloor1;
        }

        if (string.Equals(sceneName, "BlackrockMineA",
                StringComparison.OrdinalIgnoreCase))
        {
            if (IsSpatialTransition(sceneName, position))
                return null;
            if (position.y >= -60.5f)
                return BlackrockMineFloor1;
            return position.y >= -76f
                ? BlackrockMineFloor2
                : BlackrockMineFloor3;
        }

        if (string.Equals(sceneName, "MineConcentratorBuilding",
                StringComparison.OrdinalIgnoreCase))
        {
            if (IsSpatialTransition(sceneName, position))
                return null;

            if (position.x < 55f)
            {
                if (position.y < 3f)
                    return ConcentratorMiningFloor1;
                if (position.y < 20f)
                    return ConcentratorMiningFloor2;
                if (position.y < 26f)
                    return ConcentratorMiningFloor3;
                if (position.y < 35f)
                    return ConcentratorMiningFloor4;
                if (position.y < 47f)
                    return ConcentratorMiningFloor5;
                return ConcentratorMiningFloor6;
            }

            if (position.x < 105f)
                return position.y < 0f
                    ? ConcentratorConveyorFloor1
                    : ConcentratorConveyorFloor2;

            if (position.y < 5f)
                return ConcentratorHeadframeFloor1;
            if (position.y < 15f)
                return ConcentratorHeadframeFloor2;
            return position.y < 32f
                ? ConcentratorHeadframeRoofLower
                : ConcentratorHeadframeRoofUpper;
        }

        if (string.Equals(sceneName, "MineTransitionZone",
                StringComparison.OrdinalIgnoreCase))
        {
            if (position.y >= MineTransitionUpperFloorMinY)
                return MineTransitionUpper;
            return position.y >= MineTransitionElevatorFloorMinY
                ? MineTransitionElevator
                : MineTransitionDeep;
        }

        if (string.Equals(sceneName, "DamTransitionZone",
                StringComparison.OrdinalIgnoreCase))
        {
            if (IsSpatialTransition(sceneName, position))
                return null;
            if (position.y > -5.5f)
                return DamLowerUpper;
            if (position.y > -6.25f)
                return DamLowerMiddle;
            return DamLowerDeep;
        }

        if (!string.Equals(sceneName, "Dam", StringComparison.OrdinalIgnoreCase))
            return null;

        // The inset stairwell is stacked almost directly above itself. Its nearest main-drawing
        // sample is only 0.23 world units away in Z, so keep this boundary deliberately tight.
        bool inStairwell = position.x >= -16f && position.x <= -4f &&
                           position.z > -4.75f && position.z < 0.5f;
        if (inStairwell)
        {
            if (position.y < -1f)
                return DamUpperStairsB1;
            if (position.y < 3.5f)
                return DamUpperStairsF1;
            if (position.y < 8f)
                return DamUpperStairsF2;
            return DamUpperStairsF3;
        }

        if (position.y >= 3.5f)
            return DamUpperZoneC;
        if (position.z < -22f || (position.y < -4f && position.z < -18f))
            return DamUpperZoneB;
        return DamUpperZoneA;
    }

    public static bool IsSpatialTransition(string sceneName, Vector3 position)
    {
        if (string.Equals(sceneName, "BlackrockInteriorASurvival",
                StringComparison.OrdinalIgnoreCase))
        {
            // The two calibrated floors sit around Y=0 and Y=3.9. The community image draws the
            // stair symbolically, so keep the previous floor through the uncalibrated height gap.
            return position.y > 0.75f && position.y < 3f;
        }

        if (string.Equals(sceneName, "BlackrockMineA",
                StringComparison.OrdinalIgnoreCase))
        {
            // The mine's drawings are separated by elevator symbols while the world joins them
            // with long sloped passages. Keep the previous floor through each measured height gap
            // and switch only after reaching the next stable band.
            return (position.y > -62f && position.y < -59f) ||
                   (position.y > -80f && position.y < -72f);
        }

        if (string.Equals(sceneName, "MineConcentratorBuilding",
                StringComparison.OrdinalIgnoreCase))
        {
            // No calibrated drawing covers the passages between the three buildings.
            if ((position.x >= 45f && position.x < 65f) ||
                (position.x >= 95f && position.x < 110f))
                return true;

            if (position.x < 55f)
            {
                // Stable mining-floor samples form six distinct height bands. Floor 2 contains
                // its own incline (Y=11.5..17.4), so that whole interval intentionally remains
                // one variant; only the empty bands between drawings are frozen.
                return (position.y >= 3f && position.y < 10f) ||
                       (position.y >= 20f && position.y < 21f) ||
                       (position.y >= 26f && position.y < 29f) ||
                       (position.y >= 35f && position.y < 42f) ||
                       (position.y >= 47f && position.y < 53f);
            }

            if (position.x < 105f)
                return position.y >= -1f && position.y < 1f;

            return (position.y >= 5f && position.y < 11f) ||
                   (position.y >= 15f && position.y < 28f) ||
                   (position.y >= 31f && position.y < 34f);
        }

        if (!string.Equals(sceneName, "DamTransitionZone",
                StringComparison.OrdinalIgnoreCase))
            return false;

        // The lower dam drawing contains three separate floor plans but omits both stair flights.
        // Never extrapolate a floor calibration through those missing areas. While the player is
        // between landings, the runtime keeps the previous floor and freezes the last valid map
        // position; it switches exactly once after reaching the next calibrated height band.
        // The upper flight is not separable by height alone: its upper landing is already at
        // Y=-0.313, overlapping the Y=0 upper room. X=-54.312 from the F11 stair-end sample is far
        // outside the upper room anchors (X=-13.477..1.807), so X cleanly distinguishes it.
        return (position.x < -20f && position.y > -5.5f) ||
               (position.y <= -6.25f && position.y > -8.4f);
    }

    private static MapDefinition DamUpperVariant(string id, string displayName) =>
        new(id, displayName, "interior_carter_dam_upper.jpg",
            CalibrationProfile.None, false, "Dam");

    private static MapDefinition DamLowerVariant(string id, string displayName) =>
        new(id, displayName, "interior_carter_dam_lower.jpg",
            CalibrationProfile.None, false, "DamTransitionZone");

    private static MapDefinition BlackrockPrisonVariant(string id, string displayName) =>
        new(id, displayName, "interior_blackrock_prison.jpg",
            CalibrationProfile.None, false, "BlackrockInteriorASurvival");

    private static MapDefinition BlackrockMineVariant(string id, string displayName) =>
        new(id, displayName, "interior_blackrock_mine.png",
            CalibrationProfile.None, false, "BlackrockMineA");

    private static MapDefinition ConcentratorVariant(string id, string displayName) =>
        new(id, displayName, "interior_concentrator_complex.jpg",
            CalibrationProfile.None, false, "MineConcentratorBuilding");

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

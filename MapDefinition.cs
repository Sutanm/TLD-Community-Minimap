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
    {
        Id = id;
        DisplayName = displayName;
        FileName = fileName;
        Calibration = calibration;
        Scenes = scenes;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public string FileName { get; }
    public CalibrationProfile Calibration { get; }
    public IReadOnlyList<string> Scenes { get; }
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
    private static readonly Dictionary<string, MapDefinition> ByScene =
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
        Add(new MapDefinition("keepers_pass", "守山人山隘", "keepers_pass.jpg",
            CalibrationProfile.None, "BlackrockTransitionZone"));
        Add(new MapDefinition("winding_river_dam", "蜿蜒河流/卡特大坝", "winding_river_dam.jpg",
            CalibrationProfile.None, "Dam", "DamTransitionZone", "DamRiverTransitionZoneB"));
        Add(new MapDefinition("zone_of_contamination", "污染区", "zone_of_contamination.jpg",
            CalibrationProfile.None, "ZoneOfContaminationRegion", "MiningRegion"));
        Add(new MapDefinition("sundered_pass", "破碎山道", "sundered_pass.jpg",
            CalibrationProfile.None, "SunderedPassRegion", "MountainPassRegion"));
        Add(new MapDefinition("far_range_branch_line", "远境支路", "far_range_branch_line.jpg",
            CalibrationProfile.None, "FarRangeBranchLine", "FarRangeBranchLineRegion"));
        Add(new MapDefinition("transfer_pass", "中转通道", "transfer_pass.jpg",
            CalibrationProfile.None, "TransferPass", "TransferPassRegion", "HubRegion"));
    }

    public static MapDefinition Find(string sceneName) =>
        sceneName != null && ByScene.TryGetValue(sceneName, out MapDefinition definition)
            ? definition
            : null;

    private static void Add(MapDefinition definition)
    {
        foreach (string scene in definition.Scenes)
            ByScene[scene] = definition;
    }
}

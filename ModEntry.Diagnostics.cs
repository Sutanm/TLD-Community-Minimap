// 社区HUD地图 · sutanm — 诊断与校准：普查、探针、导出、采集清理
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppTLD.Scenes;
using MelonLoader;
using MelonLoader.Utils;
using ModSettings;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace CommunityMinimap;

public sealed partial class ModEntry : MelonMod
{

    private void TryReloadCalibrations()
    {
        DateTime now = DateTime.UtcNow;
        if (now < _nextCalibrationCheckUtc)
            return;

        _nextCalibrationCheckUtc = now.AddSeconds(1);
        DateTime writeUtc = File.GetLastWriteTimeUtc(_calibrationPath);
        if (writeUtc == _calibrationLastWriteUtc)
            return;

        bool loaded = CalibrationStore.Load(_calibrationPath,
            message => LoggerInstance.Msg(message),
            message => LoggerInstance.Warning(message));
        _calibrationLastWriteUtc = writeUtc;
        if (loaded)
            LoggerInstance.Msg("Reloaded calibrations.json after file change.");
        else
            LoggerInstance.Warning(
                "Calibration reload failed; keeping the last valid projections until the file changes again.");
    }


    private void RecordCalibrationPoint()
    {
        try
        {
            if (GameManager.m_Instance == null || GameManager.IsMainMenuActive())
                return;
            Transform player = GameManager.GetPlayerTransform();
            if (player == null)
                return;
            var scene = UnitySceneManager.GetActiveScene();
            string sceneName = scene.name;
            Vector3 position = player.position;
            float heading = player.eulerAngles.y;
            string captureId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff",
                CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N")[..8];
            string screenshotDirectory = Path.Combine(_modDirectory, "calibration_screenshots");
            Directory.CreateDirectory(screenshotDirectory);
            string screenshotPath = Path.Combine(screenshotDirectory,
                $"{captureId}_{SanitizeFileName(sceneName)}.png");
            ScreenCapture.CaptureScreenshot(screenshotPath);

            string path = Path.Combine(_modDirectory, "calibration_points_v2.csv");
            if (!File.Exists(path))
            {
                File.AppendAllText(path,
                    "timestamp,capture_id,scene,scene_handle,map_id,map_file,is_calibrated," +
                    "x,y,z,heading,screenshot,note\r\n");
            }
            string line = string.Join(",",
                DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                EscapeCsv(captureId),
                EscapeCsv(sceneName),
                scene.handle.ToString(CultureInfo.InvariantCulture),
                EscapeCsv(ActiveLayer.Definition?.Id ?? "unmapped"),
                EscapeCsv(ActiveLayer.Definition?.FileName ?? ""),
                ActiveLayer.Definition?.IsCalibrated == true ? "true" : "false",
                position.x.ToString("F3", CultureInfo.InvariantCulture),
                position.y.ToString("F3", CultureInfo.InvariantCulture),
                position.z.ToString("F3", CultureInfo.InvariantCulture),
                heading.ToString("F2", CultureInfo.InvariantCulture),
                EscapeCsv(screenshotPath),
                "填写地标名称");
            File.AppendAllText(path, line + "\r\n");
            RecordVanillaMapCoordinate(captureId, sceneName, position, player.rotation,
                heading);
            LoggerInstance.Msg(
                $"Calibration point recorded: {sceneName} " +
                $"({position.x:F3}, {position.y:F3}, {position.z:F3}), capture={captureId}.");
        }
        catch (Exception ex)
        {
            LoggerInstance.Error($"Failed recording calibration point: {ex}");
        }
    }


    private void RecordVanillaMapCoordinate(string captureId, string sceneName,
        Vector3 worldPosition, Quaternion worldRotation, float worldHeading)
    {
        string mapName = "";
        Vector3 mapPosition = default;
        float mapHeading = 0f;
        bool available = false;
        string error = "";

        try
        {
            Panel_Map panel = InterfaceManager.GetPanel<Panel_Map>();
            if (panel == null)
                throw new InvalidOperationException("Panel_Map is unavailable.");

            mapName = panel.GetMapNameOfScene(sceneName) ?? "";
            mapPosition = panel.WorldPositionToMapPosition(sceneName, worldPosition);
            Quaternion mapRotation = panel.WorldRotationToMapRotation(sceneName, worldRotation);
            mapHeading = mapRotation.eulerAngles.z;
            available = true;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name + ": " + ex.Message;
        }

        string path = Path.Combine(_modDirectory, "vanilla_map_coordinates.csv");
        if (!File.Exists(path))
        {
            File.AppendAllText(path,
                "timestamp,capture_id,scene,map_name,available," +
                "world_x,world_y,world_z,world_heading," +
                "map_x,map_y,map_z,map_heading,error\r\n");
        }

        string line = string.Join(",",
            DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            EscapeCsv(captureId),
            EscapeCsv(sceneName),
            EscapeCsv(mapName),
            available ? "true" : "false",
            worldPosition.x.ToString("F3", CultureInfo.InvariantCulture),
            worldPosition.y.ToString("F3", CultureInfo.InvariantCulture),
            worldPosition.z.ToString("F3", CultureInfo.InvariantCulture),
            worldHeading.ToString("F2", CultureInfo.InvariantCulture),
            available ? mapPosition.x.ToString("F3", CultureInfo.InvariantCulture) : "",
            available ? mapPosition.y.ToString("F3", CultureInfo.InvariantCulture) : "",
            available ? mapPosition.z.ToString("F3", CultureInfo.InvariantCulture) : "",
            available ? mapHeading.ToString("F2", CultureInfo.InvariantCulture) : "",
            EscapeCsv(error));
        File.AppendAllText(path, line + "\r\n");

        if (available)
        {
            LoggerInstance.Msg(
                $"Vanilla map projection: {sceneName} -> {mapName} " +
                $"({mapPosition.x:F3}, {mapPosition.y:F3}, {mapPosition.z:F3}), " +
                $"heading={mapHeading:F2}.");
        }
        else
        {
            LoggerInstance.Warning(
                $"Vanilla map projection unavailable for {sceneName}: {error}");
        }
    }


    private void DumpVanillaMapHierarchy(Panel_Map panel)
    {
        try
        {
            var output = new StringBuilder();
            output.AppendLine($"capturedUtc={DateTime.UtcNow:O}");
            output.AppendLine($"panelActive={panel.gameObject.activeInHierarchy}");
            AppendTransformDiagnostics(panel.transform, "", output);
            string path = Path.Combine(_modDirectory, "vanilla_map_hierarchy.txt");
            File.WriteAllText(path, output.ToString());
            LoggerInstance.Msg($"Exported active vanilla map hierarchy: {path}");
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Failed exporting vanilla map hierarchy: {ex}");
        }
    }


    // Measures what each vanilla path would hand the HUD, so the layer split rests on numbers
    // instead of on the note in UseVanillaBaseMap that says the two agree. Reports the panel's own
    // framing, the hard-coded base-map framing, and how the two relate - identity, a scale, or an
    // offset - because only identity means one layer can carry the other's projection.
    //
    // This runs from the per-frame update, so it rate-limits itself: the panel becomes active a
    // moment before its region map is built, and FindActiveRegionMap needs that map to exist. It
    // gives up after 30s so a scene whose panel never builds one does not retry forever. Whatever
    // it reports about the panel path is equally true in either map-source mode, since this reads
    // the game's objects rather than our copy of them.
    private void ProbeVanillaFraming(string sceneName)
    {
        if (!_settings.DeveloperMode)
            return;
        if (!string.Equals(_framingScene, sceneName, StringComparison.Ordinal))
        {
            _framingScene = sceneName;
            _framingAttempts = 0;
            _framingDone = false;
            _framingAfterUtc = DateTime.MinValue;
        }
        if (_framingDone || DateTime.UtcNow < _framingAfterUtc)
            return;

        _framingAfterUtc = DateTime.UtcNow.AddSeconds(1.5);
        _framingAttempts++;
        if (_framingAttempts > 20)
        {
            LoggerInstance.Msg($"Framing probe [{sceneName}]: gave up after 20 attempts; " +
                "the panel never produced a region map with a texture.");
            _framingDone = true;
            return;
        }
        try
        {
            Panel_Map panel = InterfaceManager.GetPanel<Panel_Map>();
            if (panel == null)
            {
                LoggerInstance.Msg($"Framing probe {_framingAttempts}: no panel object yet.");
                return;
            }

            Transform regionMap = FindActiveRegionMap(panel.transform);
            if (regionMap == null)
            {
                LoggerInstance.Msg($"Framing probe {_framingAttempts}: no active *_RegionMap yet.");
                return;
            }

            UITexture main = regionMap.GetComponent<UITexture>();
            if (main == null)
            {
                LoggerInstance.Msg($"Framing probe {_framingAttempts}: {regionMap.name} has no UITexture.");
                return;
            }

            Vector4 drawing = main.drawingDimensions;
            Rect uvRect = main.uvRect;
            Texture mainTexture = main.mainTexture;

            // Captured on the panel path and consumed on the base-map path, so the two have to
            // agree or the player pointer lands somewhere else depending on which one loaded.
            var panelBounds = new Rect(drawing.x, drawing.y,
                drawing.z - drawing.x, drawing.w - drawing.y);
            var baseBounds = new Rect(-325f, -325f, 650f, 650f);

            float widthRatio = baseBounds.width > 1e-6f
                ? panelBounds.width / baseBounds.width : 0f;
            float heightRatio = baseBounds.height > 1e-6f
                ? panelBounds.height / baseBounds.height : 0f;

            LoggerInstance.Msg(
                $"Framing probe {_framingAttempts} [{sceneName}]: regionMap={regionMap.name} " +
                $"widget={main.width}x{main.height} " +
                $"texture={(mainTexture != null ? $"{mainTexture.width}x{mainTexture.height}" : "null")} " +
                $"drawing=({drawing.x:F1},{drawing.y:F1})-({drawing.z:F1},{drawing.w:F1}) " +
                $"uvRect=({uvRect.x:F4},{uvRect.y:F4},{uvRect.width:F4},{uvRect.height:F4}) " +
                $"panelBounds=({panelBounds.xMin:F1},{panelBounds.yMin:F1}," +
                $"{panelBounds.width:F1}x{panelBounds.height:F1}) " +
                $"baseBounds=({baseBounds.xMin:F1},{baseBounds.yMin:F1}," +
                $"{baseBounds.width:F1}x{baseBounds.height:F1}) " +
                $"panelOverBase={widthRatio:F4}x{heightRatio:F4} " +
                $"offset=({panelBounds.xMin - baseBounds.xMin:F1},{panelBounds.yMin - baseBounds.yMin:F1}) " +
                $"identical={panelBounds.xMin == baseBounds.xMin && panelBounds.yMin == baseBounds.yMin && panelBounds.width == baseBounds.width && panelBounds.height == baseBounds.height}");

            // The rotation question from section 18.4 changes which way the raw texture has to be
            // read, and it is free to answer while the object is in hand.
            Vector3 euler = regionMap.localEulerAngles;
            if (euler.x != 0f || euler.y != 0f || euler.z != 0f)
            {
                LoggerInstance.Msg(
                    $"Framing probe: {regionMap.name} is rotated " +
                    $"({euler.x:F1},{euler.y:F1},{euler.z:F1}); the raw texture is not upright.");
            }

            // The bounds comparison above is an intermediate. What actually decides whether one
            // layer can inherit the other's projection is the FINAL uv: the pointer and the
            // markers both run world -> panel map position -> local bounds -> texture uv, so feed
            // one world position through both framings and compare the two answers. Run for the
            // player, who is guaranteed to be a point on this region's map, and repeated across
            // the region so a pure scale error cannot hide by cancelling at the centre.
            //
            // Both sides go through VanillaMapPositionToUv, the same helper the live pointer path
            // calls, so this cannot quietly measure a re-derivation instead of the real thing.
            Transform player = GameManager.GetPlayerTransform();
            if (player != null)
            {
                Vector3 world = player.position;
                Vector3 mapPosition = panel.WorldPositionToMapPosition(sceneName, world);

                // Base map path: frozen bounds, full-texture uv (both set by UseVanillaBaseMap).
                var baseBoundsFrozen = new Rect(-325f, -325f, 650f, 650f);
                var identityUv = new Rect(0f, 0f, 1f, 1f);
                Vector2 baseUv = VanillaMapPositionToUv(mapPosition, baseBoundsFrozen, identityUv);

                // Panel path: the widget's own bounds and uv rect, exactly as CaptureVanillaMap
                // stores them.
                Vector2 panelUv = VanillaMapPositionToUv(mapPosition, panelBounds, uvRect);

                float du = Mathf.Abs(panelUv.x - baseUv.x);
                float dv = Mathf.Abs(panelUv.y - baseUv.y);
                LoggerInstance.Msg(
                    $"Framing probe player [{sceneName}]: world=({world.x:F1},{world.y:F1},{world.z:F1}) " +
                    $"mapPos=({mapPosition.x:F1},{mapPosition.y:F1}) " +
                    $"panelUv=({panelUv.x:F5},{panelUv.y:F5}) baseUv=({baseUv.x:F5},{baseUv.y:F5}) " +
                    $"delta=({du:F5},{dv:F5}) px@2048=({du * 2048f:F1},{dv * 2048f:F1}) " +
                    $"agree={(du < 0.002f && dv < 0.002f)}");

                // Markers ride the same conversion, so an error that is invisible on the pointer
                // still throws every icon off. Re-check ~600 world units away, which is roughly the
                // spacing of the points the marker rewrite has to place.
                Vector3 probeWorld = world + new Vector3(600f, 0f, 0f);
                Vector3 probePos = panel.WorldPositionToMapPosition(sceneName, probeWorld);
                Vector2 baseFarUv = VanillaMapPositionToUv(probePos, baseBoundsFrozen, identityUv);
                Vector2 panelFarUv = VanillaMapPositionToUv(probePos, panelBounds, uvRect);
                float farDu = Mathf.Abs(panelFarUv.x - baseFarUv.x);
                float farDv = Mathf.Abs(panelFarUv.y - baseFarUv.y);
                LoggerInstance.Msg(
                    $"Framing probe offset [{sceneName}]: +600 world units -> " +
                    $"delta=({farDu:F5},{farDv:F5}) px@2048=({farDu * 2048f:F1},{farDv * 2048f:F1}) " +
                    $"agree={(farDu < 0.002f && farDv < 0.002f)}");
            }

            _framingDone = true;
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Framing probe failed: {ex.Message}");
            _framingDone = true;
        }
    }



    // One compact line describing the whole state, so a session can be reasoned about without
    // asking the player what they saw. Deliberately cheap: it never walks the 816 map entries,
    // because touching two interop lists on every one of them is not a per-frame cost worth paying.
    private void LogStateHeartbeat()
    {
        // Diagnostics only: a line every ten seconds is useful in a bug report and pure noise in
        // a normal session, so it sits behind the same switch as the rest of the developer rows.
        if (!_settings.DeveloperMode)
            return;
        if (DateTime.UtcNow < _nextHeartbeatUtc)
            return;
        _nextHeartbeatUtc = DateTime.UtcNow.AddSeconds(10);

        string fog;
        bool panelOpen = TryGetOpenVanillaMap(out _);
        try
        {
            Panel_Map panel = InterfaceManager.GetPanel<Panel_Map>();
            if (ReferenceEquals(panel, null))
            {
                fog = "no panel object";
            }
            else
            {
                var fogOfWar = panel.m_FogOfWar;
                var surveys = panel.m_DetailSurveyPositions;
                int fogCount = ReferenceEquals(fogOfWar, null) ? -1 : fogOfWar.Count;
                int surveyCount = ReferenceEquals(surveys, null) ? -1 : surveys.Count;

                // The revealed texture is the game's own "base map with fog already applied". If it
                // is alive while the panel is shut, the fog feature can reuse it instead of us
                // reconstructing the mask from survey circles.
                string revealed = "no entry";
                if (!ReferenceEquals(fogOfWar, null))
                {
                    foreach (var pair in fogOfWar)
                    {
                        if (ReferenceEquals(pair.Value, null))
                            continue;
                        revealed = ReferenceEquals(pair.Value.m_RevealedMapTex, null)
                            ? "null" : $"{pair.Value.m_RevealedMapTex.width}x{pair.Value.m_RevealedMapTex.height}";
                        break;
                    }
                }
                fog = $"fogOfWar={fogCount} surveyScenes={surveyCount} revealedTex={revealed}";
            }
        }
        catch (Exception ex)
        {
            fog = $"unreadable ({ex.GetType().Name})";
        }

        int detailCount;
        try
        {
            var details = MapDetailManager.s_MapDetails;
            detailCount = ReferenceEquals(details, null) ? -1 : details.Count;
        }
        catch { detailCount = -1; }

        bool hasUv = false;
        try
        {
            Transform player = GameManager.GetPlayerTransform();
            hasUv = !ReferenceEquals(player, null) && TryPlayerToMapUv(player.position, out Vector2 _);
        }
        catch { }

        string texture = "none";
        MapLayer shown = ActiveLayer;
        if (!ReferenceEquals(shown.Texture, null))
            texture = $"{shown.Texture.width}x{shown.Texture.height}";

        LoggerInstance.Msg(
            $"[state] scene={_observedSceneName} map={shown.Definition?.Id ?? "-"} " +
            $"calibrated={shown.Definition?.IsCalibrated.ToString() ?? "-"} " +
            $"layer={LayerName(ActiveLayerId)} " +
            $"source={(shown.UsingVanilla ? "vanilla" : "community")} tex={texture} " +
            $"textureReady={shown.TextureReady} playerUv={hasUv} markers={_vanillaIcons.Count} " +
            $"mapDetails={detailCount} vanillaPanelOpen={panelOpen} view={DescribeView()} {fog}");

        // The two layers side by side. The split is only correct if each one's texture, framing and
        // request state survive the other one changing, and that is invisible in any single-layer
        // line: a cross-layer clobber looks exactly like a working layer until the other one moves.
        LoggerInstance.Msg($"[layers] {DescribeLayer(_layers[LayerMini], LayerMini)} | " +
                           DescribeLayer(_layers[LayerFull], LayerFull));
    }


    private string DescribeLayer(MapLayer layer, int layerId)
    {
        string texture = ReferenceEquals(layer.Texture, null)
            ? "none"
            : $"{layer.Texture.width}x{layer.Texture.height}";
        string projection = string.IsNullOrEmpty(layer.VanillaProjectionScene)
            ? "-"
            : layer.VanillaProjectionScene;
        return $"{LayerName(layerId)}: src={DescribeSource(layer.Source)} " +
               $"using={(layer.UsingVanilla ? "vanilla" : "community")} tex={texture} " +
               $"ready={layer.TextureReady} bounds=({layer.VanillaMapLocalBounds.xMin:F0}," +
               $"{layer.VanillaMapLocalBounds.yMin:F0}," +
               $"{layer.VanillaMapLocalBounds.width:F0}x{layer.VanillaMapLocalBounds.height:F0}) " +
               $"proj={projection} baseReq='{layer.BaseMapRequestedScene}' " +
               $"elems='{layer.ElementsLoadedForScene}'";
    }


    // How many entries actually carry the links that would let us tell a collected marker from a
    // live one. Section 20 recorded both fields as empty across all 816 entries, yet MapIconFix is
    // said to work from exactly these fields - so either we read them at the wrong moment or we
    // read them wrong. The panel open/close trigger exists to tell those two apart.
    private void CensusMapDetails(string reason)
    {
        // This writes a 70 KB CSV, so it must never run for a player who has not asked for it.
        if (!_settings.DeveloperMode)
            return;
        if (DateTime.UtcNow < _nextCensusUtc)
            return;
        _nextCensusUtc = DateTime.UtcNow.AddSeconds(2);

        try
        {
            var details = MapDetailManager.s_MapDetails;
            if (ReferenceEquals(details, null))
            {
                LoggerInstance.Warning($"Census ({reason}): s_MapDetails is null.");
                return;
            }

            int withSprite = 0, surveyed = 0, withVisible = 0, withShared = 0;
            int totalVisible = 0, totalShared = 0;
            var csv = new StringBuilder();
            csv.AppendLine("index,sprite,locid,type,surveyed,discovered,unlocked," +
                           "world_x,world_y,world_z,harvestables_for_visibility,harvestables_sharing_icon");

            for (int i = 0; i < details.Count; i++)
            {
                MapDetail detail = details[i];
                if (ReferenceEquals(detail, null))
                    continue;

                int visible = 0, shared = 0;
                try
                {
                    var list = detail.m_HarvestablesForMapVisibility;
                    if (!ReferenceEquals(list, null))
                        visible = list.Length;
                }
                catch { }
                try
                {
                    var list = detail.m_HarvestablesSharingIcon;
                    if (!ReferenceEquals(list, null))
                        shared = list.Count;
                }
                catch { }

                if (visible > 0) { withVisible++; totalVisible += visible; }
                if (shared > 0) { withShared++; totalShared += shared; }
                if (detail.m_IsSurveyed) surveyed++;
                if (!string.IsNullOrEmpty(detail.m_SpriteName)) withSprite++;

                Vector3 world = detail.GetWorldPosition();
                csv.AppendLine(
                    $"{i},{EscapeCsv(detail.m_SpriteName)},{EscapeCsv(detail.m_LocID)}," +
                    $"{detail.m_IconType},{detail.m_IsSurveyed},{detail.m_IsDiscovered}," +
                    $"{detail.m_IsUnlocked},{world.x:F2},{world.y:F2},{world.z:F2},{visible},{shared}");
            }

            LoggerInstance.Msg(
                $"Census ({reason}): {details.Count} entries, {withSprite} with a sprite name, " +
                $"{surveyed} surveyed. m_HarvestablesForMapVisibility populated on {withVisible} " +
                $"entries ({totalVisible} objects); m_HarvestablesSharingIcon populated on " +
                $"{withShared} entries ({totalShared} objects).");

            string path = Path.Combine(_modDirectory,
                $"mapdetails_{_observedSceneName}_{DateTime.Now:HHmmss}.csv");
            File.WriteAllText(path, csv.ToString());
            LoggerInstance.Msg($"Census written: {path}");
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Census ({reason}) failed: {ex.Message}");
        }
    }


    // Diagnostic: the marker data behind the game's map, which is the source the refactor will
    // read instead of scraping the panel's sprites. Prints what a marker actually carries.
    private void DumpMapDetails()
    {
        CensusMapDetails("F11");
        ClassifyMapDetails("F11");
        try
        {
            var details = MapDetailManager.s_MapDetails;
            if (details == null)
            {
                LoggerInstance.Warning("MapDetail dump: s_MapDetails is null.");
                return;
            }
            LoggerInstance.Msg(
                $"MapDetail dump: {details.Count} entries; " +
                $"icon table holds {_iconBySpriteName.Count} sprite names.");
            int withSprite = 0, resolved = 0, atlasResolved = 0;
            for (int i = 0; i < details.Count; i++)
            {
                MapDetail all = details[i];
                if (ReferenceEquals(all, null))
                    continue;
                // text labels carry no sprite name, which is the cheapest way to tell them
                // apart without depending on the MapIconType enum's namespace
                if (string.IsNullOrEmpty(all.m_SpriteName))
                    continue;
                withSprite++;
                if (_iconBySpriteName.ContainsKey(all.m_SpriteName))
                    resolved++;
                else if (!ReferenceEquals(_mapIconAtlas, null) &&
                         !ReferenceEquals(_mapIconAtlas.GetSprite(all.m_SpriteName), null))
                    atlasResolved++;
            }
            LoggerInstance.Msg(
                $"MapDetail summary: {withSprite} markers with a sprite name; " +
                $"{resolved} found among the scraped sprites, " +
                $"{atlasResolved} more resolvable through the atlas " +
                $"({(resolved + atlasResolved) * 100 / Mathf.Max(1, withSprite)}% total); " +
                $"atlas present: {!ReferenceEquals(_mapIconAtlas, null)}; " +
                $"{details.Count - withSprite} carry no sprite name (labels and areas).");

            // Which harvestable flags actually mean "collected". IsHarvested alone matched every
            // unloaded object, while requiring the object to be active matched none, so print the
            // combinations next to whether the game itself still has the marker on the map.
            int shownHarvest = 0;
            for (int i = 0; i < details.Count && shownHarvest < 30; i++)
            {
                MapDetail hd = details[i];
                if (ReferenceEquals(hd, null))
                    continue;
                var arr = hd.m_HarvestablesForMapVisibility;
                var shared = hd.m_HarvestablesSharingIcon;
                int total = 0;
                if (!ReferenceEquals(arr, null)) total += arr.Length;
                if (!ReferenceEquals(shared, null)) total += shared.Count;
                if (total == 0)
                    continue;
                shownHarvest++;

                string flags = "";
                if (!ReferenceEquals(arr, null))
                {
                    for (int k = 0; k < arr.Length && k < 4; k++)
                    {
                        Harvestable h = arr[k];
                        if (ReferenceEquals(h, null)) { flags += "[null]"; continue; }
                        GameObject hgo = h.gameObject;
                        bool gone = ReferenceEquals(hgo, null);
                        flags += $"[h={h.IsHarvested()} gone={gone} act={(!gone && hgo.activeInHierarchy)}]";
                    }
                }

                bool drawn = false;
                try
                {
                    GameObject dgo = hd.gameObject;
                    drawn = !ReferenceEquals(dgo, null) && dgo.activeInHierarchy;
                }
                catch { }

                LoggerInstance.Msg(
                    $"  H[{i}] sprite='{hd.m_SpriteName}' surveyed={hd.m_IsSurveyed} " +
                    $"drawn={drawn} n={total} {flags}");
            }
            int shown = 0;
            for (int i = 0; i < details.Count && shown < 40; i++)
            {
                MapDetail d = details[i];
                if (ReferenceEquals(d, null))
                    continue;
                shown++;
                Vector3 world = d.GetWorldPosition();
                LoggerInstance.Msg(
                    $"  [{i}] sprite='{d.m_SpriteName}' loc='{d.m_LocID}' type={d.m_IconType} " +
                    $"surveyed={d.m_IsSurveyed} discovered={d.m_IsDiscovered} unlocked={d.m_IsUnlocked} " +
                    $"world=({world.x:F1},{world.y:F1},{world.z:F1}) target=({d.m_TargetPosition.x:F1},{d.m_TargetPosition.y:F1},{d.m_TargetPosition.z:F1})");
            }
        }
        catch (Exception ex)
        {            LoggerInstance.Warning($"MapDetail dump failed: {ex.Message}");
        }
    }


    // Counts what the MapDetail-driven marker path WOULD draw, without building a single object.
    //
    // This exists because section 37.2 calls the object count the most under-estimated part of the
    // rewrite, and the number that matters is not 723 - it is how many of those resolve to a
    // sprite, survive the label filter, and fall inside the region. Measuring it here costs nothing
    // and decides whether the rewrite needs clustering before it needs drawing.
    private void ClassifyMapDetails(string reason)
    {
        if (!_settings.DeveloperMode)
            return;
        try
        {
            var details = MapDetailManager.s_MapDetails;
            if (ReferenceEquals(details, null))
            {
                LoggerInstance.Warning($"Marker census ({reason}): s_MapDetails is null.");
                return;
            }

            int noSprite = 0, resolved = 0, fromTable = 0, fromAtlas = 0, unresolvable = 0;
            int projected = 0, unprojected = 0;
            int surveyed = 0, unlocked = 0, surveyedAndUnlocked = 0, surveyedOrUnlocked = 0;
            int fullyHarvested = 0, wouldDraw = 0;
            // Per sprite name, so the "166 cattails" kind of pile-up is visible as a number rather
            // than as an opinion about density.
            var perSprite = new Dictionary<string, int>(StringComparer.Ordinal);
            var perSpriteScraped = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int i = 0; i < details.Count; i++)
            {
                MapDetail detail = details[i];
                if (ReferenceEquals(detail, null))
                    continue;

                string name = detail.m_SpriteName;
                // Text labels and area blobs carry no sprite name; section 37.2 filters them here.
                if (string.IsNullOrEmpty(name))
                {
                    noSprite++;
                    continue;
                }

                if (_iconBySpriteName.ContainsKey(name))
                {
                    resolved++;
                    fromTable++;
                }
                else if (!ReferenceEquals(_mapIconAtlas, null) &&
                         !ReferenceEquals(_mapIconAtlas.GetSprite(name), null))
                {
                    resolved++;
                    fromAtlas++;
                }
                else
                {
                    unresolvable++;
                }

                perSprite.TryGetValue(name, out int seen);
                perSprite[name] = seen + 1;

                // Which groups the game actually instantiated, counted the same way. The scraped
                // set is the only trustworthy statement about what the panel draws: the 22:12
                // session showed 765 of 802 entries with surveyed, discovered and unlocked all
                // false, so no flag combination reproduces the 162 the game built.
                if (_iconBySpriteName.ContainsKey(name))
                {
                    perSpriteScraped.TryGetValue(name, out int scrapedSeen);
                    perSpriteScraped[name] = scrapedSeen + 1;
                }

                // The marker path shares the pointer's conversion, so this is the real test of
                // whether a marker would land on the map at all.
                if (TryPlayerToMapUv(detail.GetWorldPosition(), out Vector2 _))
                    projected++;
                else
                    unprojected++;

                // How many would survive a fog-aware visibility rule. Section 39.3 found that only
                // 39 of 802 are surveyed and that the largest groups are all harvestables, which
                // suggests the icon-wall worry is mostly fog - but that is a hypothesis until the
                // combinations are counted. Which combination is right is still open (section
                // 25.6), so every one is reported rather than a chosen rule.
                if (detail.m_IsSurveyed)
                    surveyed++;
                if (detail.m_IsUnlocked)
                    unlocked++;
                if (detail.m_IsSurveyed && detail.m_IsUnlocked)
                    surveyedAndUnlocked++;
                if (detail.m_IsSurveyed || detail.m_IsUnlocked)
                    surveyedOrUnlocked++;
                if (AllHarvestablesCollected(detail))
                    fullyHarvested++;
                if ((detail.m_IsSurveyed || detail.m_IsUnlocked) && !AllHarvestablesCollected(detail))
                    wouldDraw++;
            }

            // Largest groups first: that is the clustering question.
            var groups = new List<KeyValuePair<string, int>>(perSprite);
            groups.Sort((a, b) => b.Value.CompareTo(a.Value));
            var top = new StringBuilder();
            for (int i = 0; i < groups.Count && i < 8; i++)
            {
                if (i > 0)
                    top.Append(", ");
                top.Append(groups[i].Key).Append('=').Append(groups[i].Value);
            }

            LoggerInstance.Msg(
                $"Marker census ({reason}): {details.Count} entries; {noSprite} without a sprite name " +
                $"(labels/areas); {perSprite.Count} distinct sprite names. " +
                $"Resolvable: {resolved} ({fromTable} from the scraped table, {fromAtlas} via the atlas), " +
                $"unresolvable {unresolvable}. Projected onto the map: {projected}, not projected: {unprojected}. " +
                $"Largest groups: {(top.Length > 0 ? top.ToString() : "none")}.");

            // The visibility combinations, so the choice of rule can be made from numbers. The last
            // count is what a rule of "revealed and not fully harvested" would actually draw, and it
            // is the number that decides whether clustering is needed before drawing.
            LoggerInstance.Msg(
                $"Marker visibility ({reason}): surveyed {surveyed}, unlocked {unlocked}, " +
                $"surveyed&&unlocked {surveyedAndUnlocked}, surveyed||unlocked {surveyedOrUnlocked}, " +
                $"fully harvested {fullyHarvested}. " +
                $"Would draw under (surveyed||unlocked) && !fullyHarvested: {wouldDraw}. " +
                $"Currently scraped from the panel's sprites: {_vanillaIcons.Count}.");

            // Group by group: the total (in MapDetail data) next to what the game instantiated.
            // A group the game builds none of is the one to leave out; a group it builds all of is
            // the one to draw. This is the comparison that decides the rewrite's filtering rule.
            var compared = new List<KeyValuePair<string, int>>(perSprite);
            compared.Sort((a, b) => b.Value.CompareTo(a.Value));
            var groupLine = new StringBuilder();
            for (int i = 0; i < compared.Count; i++)
            {
                string key = compared[i].Key;
                perSpriteScraped.TryGetValue(key, out int scraped);
                if (i > 0)
                    groupLine.Append(", ");
                groupLine.Append(key).Append(' ').Append(scraped).Append('/').Append(compared[i].Value);
            }
            LoggerInstance.Msg($"Marker groups (scraped/total) [{reason}]: {groupLine}.");

            // Which sprite names the filter table does not place, and how the five buckets come
            // out. Unclassified names are always drawn, so this is the list that has to be worked
            // off before the filter can be trusted not to hide something by omission.
            var categoryCounts = new Dictionary<MarkerCategory, int>();
            var unclassified = new List<string>();
            foreach (var pair in perSprite)
            {
                MarkerCategory category = CategorizeSprite(pair.Key);
                categoryCounts.TryGetValue(category, out int seen);
                categoryCounts[category] = seen + pair.Value;
                if (category == MarkerCategory.Unclassified)
                    unclassified.Add($"{pair.Key}={pair.Value}");
            }
            var categoryLine = new StringBuilder();
            foreach (MarkerCategory value in Enum.GetValues(typeof(MarkerCategory)))
            {
                categoryCounts.TryGetValue(value, out int count);
                if (categoryLine.Length > 0)
                    categoryLine.Append(", ");
                categoryLine.Append(CategoryName(value)).Append(' ').Append(count);
            }
            LoggerInstance.Msg($"Marker categories [{reason}]: {categoryLine}.");

            if (unclassified.Count > 0)
            {
                unclassified.Sort(StringComparer.Ordinal);
                LoggerInstance.Msg($"Marker categories unclassified [{reason}] " +
                    $"({unclassified.Count} names, always drawn): {string.Join(", ", unclassified)}.");
            }
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Marker census ({reason}) failed: {ex.Message}");
        }
    }

    // The game leaves a harvested resource in MapDetailManager.s_MapDetails and on the map, so
    // collected markers never disappear by themselves. Unregister is the game's own counterpart
    // to Register, which is cleaner than editing the list by hand the way other mods do.
    // Entries are collected first and removed afterwards: mutating the list while iterating it
    // throws.
    private void CleanHarvestedMapMarkers()
    {
        if (!_settings.DeveloperMode || !_settings.ReportHarvestedMarkers)
            return;

        List<MapDetail> stale = null;
        try
        {
            var details = MapDetailManager.s_MapDetails;
            if (details == null)
                return;
            for (int i = 0; i < details.Count; i++)
            {
                MapDetail detail = details[i];
                if (ReferenceEquals(detail, null) || !detail.m_IsSurveyed)
                    continue;
                if (!AllHarvestablesCollected(detail))
                    continue;
                stale ??= new List<MapDetail>();
                stale.Add(detail);
            }

            if (stale == null)
            {
                LoggerInstance.Msg($"Harvested sweep: none of {details.Count} entries look collected.");
                return;
            }

            if (!_settings.RemoveHarvestedMarkers)
            {
                LoggerInstance.Msg(
                    $"Harvested sweep (report only): {stale.Count} of {details.Count} entries look collected; " +
                    $"first: sprite='{stale[0].m_SpriteName}' loc='{stale[0].m_LocID}'.");
                return;
            }

            for (int i = 0; i < stale.Count; i++)
                MapDetailManager.Unregister(stale[i]);

            LoggerInstance.Msg($"Removed {stale.Count} of {details.Count} fully harvested map markers.");
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Harvested marker cleanup failed: {ex.Message}");
        }
    }


    // Only a harvested object that is also loaded counts as picked up. A harvestable that has
    // simply not been streamed in yet reports harvested too, which is what made the first
    // version wipe almost every marker at once.
    private static bool IsCollected(Harvestable harvestable)
    {
        GameObject go = harvestable.gameObject;
        if (ReferenceEquals(go, null) || !go.activeInHierarchy)
            return false;
        return harvestable.IsHarvested();
    }


    private static bool AllHarvestablesCollected(MapDetail detail)
    {
        bool any = false;
        var array = detail.m_HarvestablesForMapVisibility;
        if (array != null)
        {
            for (int i = 0; i < array.Length; i++)
            {
                Harvestable harvestable = array[i];
                if (ReferenceEquals(harvestable, null))
                    continue;
                any = true;
                if (!IsCollected(harvestable))
                    return false;
            }
        }

        var shared = detail.m_HarvestablesSharingIcon;
        if (shared != null)
        {
            for (int i = 0; i < shared.Count; i++)
            {
                Harvestable harvestable = shared[i];
                if (ReferenceEquals(harvestable, null))
                    continue;
                any = true;
                if (!IsCollected(harvestable))
                    return false;
            }
        }

        return any;
    }


    // Lists every texture-shaped member the region specification exposes, and exports the ones that
    // are readable.
    //
    // The reason to look rather than guess: the HUD is handed GetMiniMapTextureAsync(), which is
    // named for the CORNER map, while the map panel draws a different 2048x2048 image. If the region
    // also holds the full-resolution terrain, exporting it would give a clean base map with no fog
    // to light up and no marker objects on it - which is exactly what is wanted, and what the panel
    // route cannot provide because the panel's texture carries the reveal state.
    //
    // Reflection because the member names are only partly known: m_MapTex was found by string search
    // but the type around it could not be reflected offline (the interop assemblies do not resolve
    // outside the game), so enumerating at runtime is the only reliable way.
    private void ProbeRegionTextures(RegionSpecification region, string sceneName)
    {
        if (!_settings.DeveloperMode)
            return;
        try
        {
            Type type = region.GetType();
            LoggerInstance.Msg($"Region texture probe [{sceneName}]: type={type.FullName}.");
            int found = 0;

            foreach (System.Reflection.FieldInfo field in type.GetFields(
                         System.Reflection.BindingFlags.Public |
                         System.Reflection.BindingFlags.NonPublic |
                         System.Reflection.BindingFlags.Instance))
            {
                string kind = field.FieldType.Name;
                bool textureish = kind.Contains("Texture") || kind.Contains("Sprite");
                if (!textureish)
                    continue;
                found++;

                string value = "?";
                try
                {
                    object raw = field.GetValue(region);
                    value = DescribeObject(raw);
                }
                catch (Exception ex)
                {
                    value = $"unreadable ({ex.GetType().Name})";
                }
                LoggerInstance.Msg($"Region texture probe [{sceneName}]: field {field.Name} " +
                                   $": {kind} = {value}");
            }

            foreach (System.Reflection.PropertyInfo property in type.GetProperties(
                         System.Reflection.BindingFlags.Public |
                         System.Reflection.BindingFlags.NonPublic |
                         System.Reflection.BindingFlags.Instance))
            {
                string kind = property.PropertyType.Name;
                if (!kind.Contains("Texture") && !kind.Contains("Sprite"))
                    continue;
                found++;

                string value = "?";
                try
                {
                    value = DescribeObject(property.GetValue(region));
                }
                catch (Exception ex)
                {
                    value = $"unreadable ({ex.GetType().Name})";
                }
                LoggerInstance.Msg($"Region texture probe [{sceneName}]: property {property.Name} " +
                                   $": {kind} = {value}");
            }

            if (found == 0)
                LoggerInstance.Msg($"Region texture probe [{sceneName}]: no texture members found.");
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Region texture probe [{sceneName}] failed: {ex.Message}");
        }
    }


    private static string DescribeObject(object raw)
    {
        if (raw == null)
            return "null";
        if (raw is Texture texture)
            return $"Texture {texture.width}x{texture.height} ({texture.GetType().Name})";
        return raw.GetType().Name;
    }


    // Loads each AssetReferenceTexture2D the region exposes and reports the size that comes back.
    //
    // This is the question that decides whether a clean full-resolution base map can be exported at
    // all. GetMiniMapTextureAsync returns a 1024x1024 DXT5 texture - measured - while the map panel
    // draws a 2048x2048 image, so the HUD is being handed a different, smaller asset. If loading the
    // reference directly yields the larger asset, the HUD can be given that instead, with no fog to
    // light up and no marker objects to hide, which is what the panel route cannot offer.
    //
    // The reference is an asset, not a texture, so the size only becomes known after the load
    // completes; polling a handle needs a frame, so the results are reported from a scheduled check.
    private void ProbeRegionAssetReferences(RegionSpecification region, string sceneName)
    {
        if (!_settings.DeveloperMode)
            return;
        try
        {
            Type type = region.GetType();
            foreach (System.Reflection.PropertyInfo property in type.GetProperties(
                         System.Reflection.BindingFlags.Public |
                         System.Reflection.BindingFlags.NonPublic |
                         System.Reflection.BindingFlags.Instance))
            {
                string kind = property.PropertyType.Name;
                if (!kind.Contains("AssetReference"))
                    continue;

                object raw = null;
                try { raw = property.GetValue(region); }
                catch (Exception ex)
                {
                    LoggerInstance.Msg($"Region asset probe [{sceneName}]: {property.Name} " +
                                       $"unreadable: {ex.Message}");
                    continue;
                }
                if (raw == null)
                {
                    LoggerInstance.Msg($"Region asset probe [{sceneName}]: {property.Name} = null.");
                    continue;
                }

                // Loading needs the game's Addressables operation; if the shape is not what this
                // expects the property is reported by name and type and left alone.
                //
                // A prefab reference resolves to null when loaded as a texture and comes back as a
                // GameObject instead, which is why m_RegionMap reported null while holding the most
                // promising lead: the map panel builds its 2048x2048 image from this prefab, since
                // neither texture the region owns is bigger than 1024.
                bool prefabish = property.Name.IndexOf("Prefab", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 property.Name.IndexOf("RegionMap", StringComparison.OrdinalIgnoreCase) >= 0;
                try
                {
                    dynamic reference = raw;
                    if (prefabish)
                    {
                        var prefabHandle = reference.LoadAssetAsync<GameObject>();
                        _pendingPrefabProbes.Add(new PrefabProbe
                        {
                            Name = property.Name,
                            Scene = sceneName,
                            Handle = prefabHandle,
                            RequestedUtc = DateTime.UtcNow,
                        });
                        LoggerInstance.Msg($"Region asset probe [{sceneName}]: {property.Name} " +
                                           "load requested as a prefab.");
                        continue;
                    }

                    var handle = reference.LoadAssetAsync<Texture2D>();
                    _pendingAssetProbes.Add(new AssetProbe
                    {
                        Name = property.Name,
                        Scene = sceneName,
                        Handle = handle,
                        RequestedUtc = DateTime.UtcNow,
                    });
                    LoggerInstance.Msg($"Region asset probe [{sceneName}]: {property.Name} " +
                                       "load requested.");
                }
                catch (Exception ex)
                {
                    LoggerInstance.Msg($"Region asset probe [{sceneName}]: {property.Name} " +
                                       $"could not be loaded ({ex.GetType().Name}: {ex.Message}).");
                }
            }
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Region asset probe [{sceneName}] failed: {ex.Message}");
        }
    }


    // Reports each prefab probe once its load settles, and lists every texture it carries.
    //
    // The region owns only 1024x1024 textures, so the panel's 2048x2048 image has to come from
    // somewhere else - and this prefab is the only remaining candidate, since the panel builds its
    // map from a region map object. Listing the textures inside it, at their real sizes, is what
    // decides whether a clean full-resolution base map can be exported without lighting the map.
    private void PollPrefabProbes()
    {
        for (int i = _pendingPrefabProbes.Count - 1; i >= 0; i--)
        {
            PrefabProbe probe = _pendingPrefabProbes[i];
            bool timedOut = (DateTime.UtcNow - probe.RequestedUtc).TotalSeconds > 15.0;
            if (!probe.Handle.IsDone && !timedOut)
                continue;

            _pendingPrefabProbes.RemoveAt(i);
            try
            {
                if (timedOut && !probe.Handle.IsDone)
                {
                    LoggerInstance.Warning(
                        $"Region prefab probe [{probe.Scene}]: {probe.Name} timed out.");
                    continue;
                }

                GameObject prefab = probe.Handle.Result;
                if (ReferenceEquals(prefab, null))
                {
                    LoggerInstance.Msg(
                        $"Region prefab probe [{probe.Scene}]: {probe.Name} resolved to null.");
                    continue;
                }

                LoggerInstance.Msg(
                    $"Region prefab probe [{probe.Scene}]: {probe.Name} = '{prefab.name}'.");

                // Every texture the prefab's widgets reference, with its real size.
                var widgets = prefab.GetComponentsInChildren<UITexture>(true);
                if (widgets == null || widgets.Length == 0)
                {
                    LoggerInstance.Msg(
                        $"Region prefab probe [{probe.Scene}]: {probe.Name} has no UITexture.");
                    continue;
                }

                for (int w = 0; w < widgets.Length; w++)
                {
                    UITexture widget = widgets[w];
                    if (ReferenceEquals(widget, null))
                        continue;
                    Texture texture = widget.mainTexture;
                    string size = ReferenceEquals(texture, null)
                        ? "null"
                        : $"{texture.width}x{texture.height}";
                    LoggerInstance.Msg(
                        $"Region prefab probe [{probe.Scene}]: {probe.Name} widget " +
                        $"'{widget.name}' texture={size} size={widget.width}x{widget.height} " +
                        $"uvRect={widget.uvRect}.");

                    if (!ReferenceEquals(texture, null) && texture.width > 1024)
                    {
                        Texture2D owned = CaptureTexture(texture);
                        string path = Path.Combine(_modDirectory,
                            $"prefab_{SanitizeFileName(probe.Name)}_" +
                            $"{SanitizeFileName(widget.name)}.png");
                        if (!File.Exists(path))
                        {
                            WriteTextureToPng(owned, path);
                            LoggerInstance.Msg($"Exported prefab texture: {path}");
                        }
                        UnityEngine.Object.Destroy(owned);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning(
                    $"Region prefab probe [{probe.Scene}]: {probe.Name} failed: {ex.Message}");
            }
            finally
            {
                try { UnityEngine.AddressableAssets.Addressables.Release(probe.Handle); }
                catch { }
            }
        }
    }


    // Reports each asset probe once its load settles, exports the texture, and releases the handle.
    private void PollRegionAssetProbes()
    {
        for (int i = _pendingAssetProbes.Count - 1; i >= 0; i--)
        {
            AssetProbe probe = _pendingAssetProbes[i];
            bool timedOut = (DateTime.UtcNow - probe.RequestedUtc).TotalSeconds > 15.0;
            if (!probe.Handle.IsDone && !timedOut)
                continue;

            _pendingAssetProbes.RemoveAt(i);
            try
            {
                if (timedOut && !probe.Handle.IsDone)
                {
                    LoggerInstance.Warning(
                        $"Region asset probe [{probe.Scene}]: {probe.Name} timed out.");
                    continue;
                }

                Texture2D texture = probe.Handle.Result;
                if (ReferenceEquals(texture, null))
                {
                    LoggerInstance.Msg(
                        $"Region asset probe [{probe.Scene}]: {probe.Name} resolved to null.");
                    continue;
                }

                LoggerInstance.Msg(
                    $"Region asset probe [{probe.Scene}]: {probe.Name} = " +
                    $"{texture.width}x{texture.height}, format={texture.format}.");

                // Only worth exporting when it beats what the HUD already gets, otherwise this just
                // writes the same thumbnail again under a second name.
                if (texture.width > 1024 || texture.height > 1024)
                {
                    Texture2D owned = CaptureTexture(texture);
                    string path = Path.Combine(_modDirectory,
                        $"asset_{SanitizeFileName(probe.Name)}_{SanitizeFileName(probe.Scene)}.png");
                    if (!File.Exists(path))
                    {
                        WriteTextureToPng(owned, path);
                        LoggerInstance.Msg($"Exported region asset: {path}");
                    }
                    UnityEngine.Object.Destroy(owned);
                }
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning(
                    $"Region asset probe [{probe.Scene}]: {probe.Name} result failed: {ex.Message}");
            }
            finally
            {
                try { UnityEngine.AddressableAssets.Addressables.Release(probe.Handle); }
                catch { }
            }
        }
    }


    private void ReleasePendingDiagnosticHandles()
    {
        for (int i = 0; i < _pendingPrefabProbes.Count; i++)
        {
            try
            {
                UnityEngine.AddressableAssets.Addressables.Release(
                    _pendingPrefabProbes[i].Handle);
            }
            catch { }
        }
        _pendingPrefabProbes.Clear();

        for (int i = 0; i < _pendingAssetProbes.Count; i++)
        {
            try
            {
                UnityEngine.AddressableAssets.Addressables.Release(
                    _pendingAssetProbes[i].Handle);
            }
            catch { }
        }
        _pendingAssetProbes.Clear();
    }


    private void ProbeMapDetailMembers()
    {
        if (s_mapDetailMembersProbed)
            return;
        s_mapDetailMembersProbed = true;
        try
        {
            Type type = typeof(MapDetail);
            var lines = new List<string>();
            foreach (System.Reflection.MethodInfo method in type.GetMethods(
                         System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance |
                         System.Reflection.BindingFlags.DeclaredOnly))
            {
                if (method.ReturnType != typeof(string))
                    continue;
                lines.Add(method.Name + "(" +
                    string.Join(",", System.Array.ConvertAll(method.GetParameters(),
                        p => p.ParameterType.Name)) + ")");
            }
            LoggerInstance.Msg($"MapDetail string methods: " +
                (lines.Count > 0 ? string.Join(", ", lines) : "<none>"));

            var fields = new List<string>();
            foreach (System.Reflection.FieldInfo field in type.GetFields(
                         System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                fields.Add($"{field.Name}:{field.FieldType.Name}");
            }
            LoggerInstance.Msg($"MapDetail fields: " +
                (fields.Count > 0 ? string.Join(", ", fields) : "<none>"));

            var props = new List<string>();
            foreach (System.Reflection.PropertyInfo property in type.GetProperties(
                         System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                props.Add($"{property.Name}:{property.PropertyType.Name}");
            }
            LoggerInstance.Msg($"MapDetail properties: " +
                (props.Count > 0 ? string.Join(", ", props) : "<none>"));
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"MapDetail member probe failed: {ex.Message}");
        }
    }


    private void TryExportSceneCatalog()
    {
        if (_sceneCatalogExported || DateTime.UtcNow < _sceneCatalogAfterUtc)
            return;

        string path = Path.Combine(_modDirectory, "scene_catalog.csv");
        if (SceneCatalogExporter.TryExport(path, out int count, out string error))
        {
            _sceneCatalogExported = true;
            LoggerInstance.Msg($"Exported {count} scene names: {path}");
            return;
        }

        _sceneCatalogAttempts++;
        if (_sceneCatalogAttempts >= 12)
        {
            _sceneCatalogExported = true;
            LoggerInstance.Warning($"Scene catalog export abandoned after 12 attempts: {error}");
            return;
        }

        _sceneCatalogAfterUtc = DateTime.UtcNow.AddSeconds(10);
        if (_sceneCatalogAttempts == 1)
            LoggerInstance.Warning($"Scene catalog is not ready; retrying. {error}");
    }
}
// — sutanm · 社区HUD地图

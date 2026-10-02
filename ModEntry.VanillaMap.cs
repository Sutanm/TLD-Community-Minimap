// 社区HUD地图 · sutanm — 原版地图数据：底图请求、面板捕获、图标表
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

    private bool CaptureVanillaMap(Panel_Map panel, string sceneName)
    {
        try
        {
            if (MapCatalog.Find(sceneName) == null)
                return false;

            Transform regionMap = FindActiveRegionMap(panel.transform);
            if (regionMap == null)
                return false;

            UITexture main = regionMap.GetComponent<UITexture>();
            if (main == null)
                return false;

            Vector4 drawing = main.drawingDimensions;
            Rect capturedBounds = new(drawing.x, drawing.y,
                drawing.z - drawing.x, drawing.w - drawing.y);
            Rect capturedUv = main.uvRect;
            // The panel is a fact about the region, so every vanilla layer records the same
            // framing; each keeps its own copy so switching one layer's source cannot disturb it.
            for (int i = 0; i < _layers.Length; i++)
            {
                MapLayer target = _layers[i];
                target.VanillaMapLocalBounds = capturedBounds;
                target.VanillaTextureUv = capturedUv;
                target.VanillaProjectionScene = sceneName;
            }
            EnsureUnityUi();
            _vanillaIconSignature = 0;
            _nextVanillaIconRefreshUtc = DateTime.MinValue;

            // The one comparison that has never been made. The panel draws a 2048x2048 texture with
            // its own uvRect, while the base-map path feeds the HUD a different 1024x1024 texture -
            // and the projection derived here is handed to both. If those two images do not cover
            // the same pixels, one of them must be misplaced, which is exactly the report: markers
            // scattered off a map that otherwise looks correct. Measured, not reasoned about.
            MeasureAndExportPanelTexture(main, sceneName);
            // The panel's own label objects only exist while it is open, and they carry the finished
            // display text, so this is the only moment they can be read. It renders text through NGUI
            // rather than UnityEngine.UI, which is why looking for a Text component found nothing.
            CapturePanelLabelTexts();
            // Full component listing of the panel, once per region, so the text renderer it actually
            // uses is read off the objects instead of assumed.
            DumpVanillaMapHierarchy(panel);

            // The region base map normally supplies the terrain on its own. Only when it could
            // not be loaded do we fall back to this surveyed texture, which is the path that
            // requires the player to have opened the panel.
            if (!AnyVanillaLayerReady() && main.mainTexture != null)
            {
                // One independent copy per layer: UseCapturedVanillaMap destroys whatever the
                // layer held before, so handing the same Texture2D to both would leave the second
                // layer pointing at a destroyed texture.
                for (int i = 0; i < _layers.Length; i++)
                {
                    MapLayer target = _layers[i];
                    if (!target.UsingVanilla)
                        continue;
                    Texture2D capturedMain = CaptureTexture(main.mainTexture);
                    UseCapturedVanillaMap(capturedMain, sceneName, target);
                }
            }

            // Some regions store their base map texture rotated and let the widget's own
            // transform turn it back, so the raw texture cannot be compared with anything
            // until this angle is known. Report it rather than assume zero.
            Vector3 widgetEuler = regionMap.localEulerAngles;
            Vector3 widgetScale = regionMap.localScale;
            LoggerInstance.Msg(
                $"Vanilla map panel refreshed from {regionMap.name}; " +
                $"bounds={capturedBounds}, uv={capturedUv}, " +
                $"widget={main.width}x{main.height}, " +
                $"rotation=({widgetEuler.x:F1},{widgetEuler.y:F1},{widgetEuler.z:F1}), " +
                $"scale=({widgetScale.x:F3},{widgetScale.y:F3},{widgetScale.z:F3}), " +
                $"texture={(main.mainTexture != null ? $"{main.mainTexture.width}x{main.mainTexture.height}" : "null")}.");
            return true;
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Failed refreshing vanilla map: {ex}");
            return false;
        }
    }


    private void UseCapturedVanillaMap(Texture2D texture, string sceneName, MapLayer layer)
    {
        EnsureUnityUi();
        RetireTexture(layer);
        layer.Texture = texture;
        layer.LoadedMapId = "__vanilla__" + sceneName;
        layer.VanillaProjectionScene = sceneName;
        layer.UsingVanilla = true;
        layer.TextureReady = true;
        LoggerInstance.Msg(
            $"Captured vanilla map is now active in the HUD: {texture.width}x{texture.height}.");
    }


    private static Transform FindActiveRegionMap(Transform root)
    {
        if (root.gameObject.activeInHierarchy &&
            root.name.EndsWith("_RegionMap", StringComparison.Ordinal) &&
            root.GetComponent<UITexture>() != null)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindActiveRegionMap(root.GetChild(i));
            if (result != null)
                return result;
        }

        return null;
    }


    // Polls the marker containers so collected/searchable markers disappear from the HUD
    // shortly after the game removes them, instead of only refreshing when M is pressed.
    private void TryRefreshVanillaIcons()
    {
        // MapDetail markers carry their resolved hover text. The panel-scrape path only has sprites;
        // allowing it to refresh this mode replaces every named marker with an anonymous copy and
        // makes hover labels disappear after the original map has been opened once.
        if (_settings.MarkerSource == MinimapSettings.MarkerSourceMapDetails)
            return;

        if (!ActiveLayer.UsingVanilla || DateTime.UtcNow < _nextVanillaIconRefreshUtc)
            return;

        _nextVanillaIconRefreshUtc = DateTime.UtcNow.AddSeconds(1);
        try
        {
            Panel_Map panel = InterfaceManager.GetPanel<Panel_Map>();
            if (panel == null)
                return;

            // Markers are only read while the map panel is closed. The open panel lays its
            // elements out at the current wheel-zoom scale (0.33 by default, 1.00 zoomed in),
            // which would change the marker size, and the HUD is hidden while the map is open
            // anyway.
            if (panel.gameObject.activeInHierarchy)
                return;

            Transform mapElements = FindChildByName(panel.transform, "MapElements");
            if (mapElements == null)
                return;

            long signature = ComputeVanillaIconSignature(mapElements);
            if (signature == _vanillaIconSignature)
                return;

            _vanillaIconSignature = signature;
            CaptureVanillaIcons(mapElements, true, true);
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Vanilla icon refresh failed: {ex.Message}");
        }
    }


    private static long ComputeVanillaIconSignature(Transform mapElements)
    {
        string[] containerNames =
        {
            "ActiveElementsBigSprite",
            "ActiveElementsSmallSprite",
            "ActiveElementsDetailEntry"
        };
        long hash = 17;
        for (int i = 0; i < containerNames.Length; i++)
        {
            Transform container = FindChildByName(mapElements, containerNames[i]);
            if (container == null)
                continue;
            hash = hash * 31 + container.childCount;
            for (int child = 0; child < container.childCount; child++)
            {
                Vector3 position = container.GetChild(child).localPosition;
                hash = hash * 31 + (long)Mathf.Round(position.x * 10f);
                hash = hash * 31 + (long)Mathf.Round(position.y * 10f);
            }
        }

        return hash;
    }


    // Asks the game for the region's own base map. This is independent of Panel_Map, so the
    // HUD no longer requires the player to open the game map first.
    private void TryRequestVanillaBaseMap(string sceneName, MapLayer layer)
    {
        if (layer.BaseMapPending || string.Equals(layer.BaseMapRequestedScene, sceneName, StringComparison.Ordinal))
            return;

        try
        {
            RegionSpecification region = GameManager.TryGetCurrentRegion();
            if (region == null)
            {
                // Retried every frame because the region is briefly unavailable during a scene
                // load. If it stays unavailable, say so once: that is the difference between a
                // slow load and a scene the game simply has no map for.
                if (!string.Equals(_baseMapNullScene, sceneName, StringComparison.Ordinal))
                {
                    _baseMapNullScene = sceneName;
                    _baseMapNullSinceUtc = DateTime.UtcNow;
                    _baseMapNullLogged = false;
                }
                else if (!_baseMapNullLogged &&
                         (DateTime.UtcNow - _baseMapNullSinceUtc).TotalSeconds > 3.0)
                {
                    _baseMapNullLogged = true;
                    MarkVanillaUnavailable(sceneName, layer,
                        "GameManager.TryGetCurrentRegion() stayed null for 3 seconds");
                }
                return;
            }
            _baseMapNullScene = "";
            _baseMapNullLogged = false;

            if (!region.HasMiniMapTexture)
            {
                // Genuinely unavailable for this region: stop asking, and let the surveyed
                // texture fallback take over if the player opens the map panel.
                layer.BaseMapRequestedScene = sceneName;
                MarkVanillaUnavailable(sceneName, layer,
                    "the region reports no base map texture");
                return;
            }

            layer.BaseMapRequestedScene = sceneName;
            ProbeRegionTextures(region, sceneName);
            ProbeRegionAssetReferences(region, sceneName);
            layer.BaseMapHandle = region.GetMiniMapTextureAsync();
            layer.BaseMapHandleValid = true;
            layer.BaseMapPending = true;
            layer.BaseMapRequestUtc = DateTime.UtcNow;
            LoggerInstance.Msg($"Requested region base map for {sceneName}.");
        }
        catch (Exception ex)
        {
            layer.BaseMapRequestedScene = sceneName;
            MarkVanillaUnavailable(sceneName, layer,
                $"the base-map request failed ({ex.Message})");
        }
    }


    private void PollVanillaBaseMap(string sceneName, MapLayer layer)
    {
        if (!layer.BaseMapPending)
            return;

        // Never leave the HUD permanently blank if the load silently stalls.
        if ((DateTime.UtcNow - layer.BaseMapRequestUtc).TotalSeconds > 15.0)
        {
            ReleaseBaseMapHandle(layer);
            MarkVanillaUnavailable(sceneName, layer, "the base-map request timed out");
            return;
        }

        try
        {
            if (!layer.BaseMapHandle.IsDone)
                return;
        }
        catch (Exception ex)
        {
            ReleaseBaseMapHandle(layer);
            MarkVanillaUnavailable(sceneName, layer,
                $"the base-map handle failed ({ex.Message})");
            return;
        }

        layer.BaseMapPending = false;
        try
        {
            Texture2D source = layer.BaseMapHandle.Result;
            if (source == null)
            {
                MarkVanillaUnavailable(sceneName, layer,
                    "the base-map request resolved to null");
                return;
            }

            // The loaded asset is not CPU-readable and is owned by Addressables, so keep an
            // owned copy instead of handing the asset itself to the HUD.
            //
            // Measure the ASSET before copying it. The copy is taken at the asset's own size, so a
            // 1024 result means the asset really is 1024 and not that the copy shrank it - and that
            // distinction decides whether the HUD is being handed the map or a downscaled thumbnail
            // of it. The panel draws a 2048x2048 image, so if this is 1024 the two are different
            // assets and the HUD has the smaller one.
            LoggerInstance.Msg(
                $"Region map asset [{sceneName}]: {source.width}x{source.height} " +
                $"({source.GetType().Name}), format={source.format}, " +
                $"mipmaps={source.mipmapCount}.");
            Texture2D owned = CaptureTexture(source);
            LoggerInstance.Msg(
                $"Region map copy [{sceneName}]: {owned.width}x{owned.height}.");

            // Export this asset at full size, once per region. It is a different file from the
            // basemap_* export so the two can be compared rather than one overwriting the other.
            try
            {
                string assetPath = Path.Combine(_modDirectory,
                    $"regionmap_{SanitizeFileName(sceneName)}.png");
                if (!File.Exists(assetPath))
                {
                    WriteTextureToPng(owned, assetPath);
                    LoggerInstance.Msg($"Exported region map asset: {assetPath}");
                }
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning($"Region map asset export failed: {ex.Message}");
            }

            // The capture is UP TO 1024x1024 and letterboxed, not a full-bleed map: measured across
            // all 16 exported regions, the opaque content runs from 745x893 up to 1009x1021 inside
            // that frame, each with different margins. Projecting onto the frame put the player
            // pointer off by a fixed, region-specific amount - the 22:21 session's "pointer position
            // is wrong" after switching to vanilla. The content box is measured from the pixels
            // rather than assumed, and the projection is aimed at it.
            Rect contentUv = MeasureOpaqueUv(owned);
            UseVanillaBaseMap(owned, sceneName, layer, contentUv);
            LoggerInstance.Msg(
                $"Vanilla base map active for {sceneName}: {owned.width}x{owned.height}, " +
                $"content uv=({contentUv.x:F4},{contentUv.y:F4},{contentUv.width:F4}," +
                $"{contentUv.height:F4}) (no map panel needed).");

            // Calibration aid: keep one PNG of each region's base map on disk so the community
            // map can be registered against it instead of picking pixels by eye.
            try
            {
                string baseMapPath = Path.Combine(_modDirectory,
                    $"basemap_{SanitizeFileName(sceneName)}.png");
                if (!File.Exists(baseMapPath))
                {
                    WriteTextureToPng(owned, baseMapPath);
                    LoggerInstance.Msg($"Exported region base map: {baseMapPath}");
                }
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning($"Base map export failed: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            MarkVanillaUnavailable(sceneName, layer,
                $"the base-map load failed ({ex.Message})");
        }
        finally
        {
            // The HUD owns a CPU copy. Keeping the Addressables handle after that copy is made
            // pins the game's source texture across every scene transition.
            ReleaseBaseMapHandle(layer);
        }
    }


    private void ReleaseBaseMapHandle(MapLayer layer)
    {
        layer.BaseMapPending = false;
        if (!layer.BaseMapHandleValid)
            return;
        try
        {
            UnityEngine.AddressableAssets.Addressables.Release(layer.BaseMapHandle);
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Releasing region base map handle failed: {ex.Message}");
        }
        finally
        {
            layer.BaseMapHandleValid = false;
        }
    }


    // Fills the icon table and the atlas, whatever map source the layers are using.
    //
    // The atlas used to arrive as a side effect of the vanilla marker scrape, and that scrape only
    // ran while a layer wanted vanilla. With the community map active - which is what automatic
    // picks whenever the image is installed, so most installs - the atlas stayed null and the icon
    // table stayed empty: the 22:04 session reported "Resolvable: 0 ... atlas present: False" for
    // all 802 markers. Section 25.3's "100% resolvable" was measured on the vanilla source and does
    // not hold in the default configuration.
    //
    // This is the same one-shot panel call the vanilla path used, minus everything that depended on
    // a texture: it needs the atlas, not the base map, so it must not wait for one.
    private void PopulateIconTableOnce(string sceneName)
    {
        // The guard exists because LoadMapElementsForScene APPENDS, so calling it twice on the same
        // scene would duplicate every marker. It must not, however, block rebuilding a list that
        // something emptied: ObserveScene clears the icons on every real scene change (an interior
        // counts), and the 22:21 session showed the result - markers fell from 162 to 0 on the way
        // into GreyMothersHouseA and never came back, because this guard said "already loaded for
        // this scene" while the list it was protecting no longer existed.
        //
        // An empty list is the one case where the containers may also be empty, so the scan runs
        // first and the appending call is only made when the scan finds nothing.
        bool alreadyLoaded = string.Equals(_elementsLoadedForScene, sceneName, StringComparison.Ordinal);
        if (alreadyLoaded && (_vanillaIcons.Count > 0 ||
            (_settings.MarkerSource == MinimapSettings.MarkerSourceMapDetails &&
             !ReferenceEquals(_mapIconAtlas, null))))
            return;
        if (DateTime.UtcNow < _elementLoadAfterUtc)
            return;
        _elementLoadAfterUtc = DateTime.UtcNow.AddSeconds(2);

        try
        {
            // A completely fresh save can have hundreds of MapDetail entries while the panel has
            // instantiated no marker objects at all. In that state LoadMapElementsForScene has
            // nothing to clone, so scraping the empty MapElements container can never discover the
            // icon atlas. Find the already-loaded NGUI atlas by asking each candidate how many of
            // the registered MapDetail sprite names it owns. This is read-only and runs once per
            // scene; it removes the accidental requirement that the player survey one location
            // before HUD markers can be drawn.
            if (TryDiscoverMapIconAtlas(sceneName) &&
                _settings.MarkerSource == MinimapSettings.MarkerSourceMapDetails)
            {
                _elementsLoadedForScene = sceneName;
                return;
            }

            Panel_Map panel = InterfaceManager.GetPanel<Panel_Map>();
            if (panel == null)
                return;

            // Scan what is already there before asking the game to build more.
            Transform mapElements = FindChildByName(panel.transform, "MapElements");
            if (mapElements != null)
            {
                _vanillaIconSignature = ComputeVanillaIconSignature(mapElements);
                CaptureVanillaIcons(mapElements, false, true);
                if (_vanillaIcons.Count > 0)
                {
                    _elementsLoadedForScene = sceneName;
                    LoggerInstance.Msg(
                        $"Icon table [{sceneName}] rebuilt from existing elements: " +
                        $"{_vanillaIcons.Count} markers, {_iconBySpriteName.Count} sprite names, " +
                        $"atlas present: {!ReferenceEquals(_mapIconAtlas, null)}.");
                    return;
                }
            }

            if (alreadyLoaded)
            {
                // The scene was already loaded once and the containers are genuinely empty. Saying
                // so is more useful than silently retrying forever.
                LoggerInstance.Msg($"Icon table [{sceneName}]: containers are empty; rebuilding.");
            }

            try { panel.ForceUpdateRegion(); }
            catch (Exception ex) { LoggerInstance.Warning($"ForceUpdateRegion failed: {ex.Message}"); }

            try { panel.LoadMapElementsForScene(sceneName); }
            catch (Exception ex) { LoggerInstance.Warning($"LoadMapElementsForScene failed: {ex.Message}"); }

            _elementsLoadedForScene = sceneName;
            mapElements = FindChildByName(panel.transform, "MapElements");
            if (mapElements == null)
            {
                LoggerInstance.Warning($"Icon table [{sceneName}]: no MapElements container.");
                return;
            }

            _vanillaIconSignature = ComputeVanillaIconSignature(mapElements);
            CaptureVanillaIcons(mapElements, false, true);
            LoggerInstance.Msg(
                $"Icon table [{sceneName}]: {_vanillaIcons.Count} markers, " +
                $"{_iconBySpriteName.Count} sprite names, " +
                $"atlas present: {!ReferenceEquals(_mapIconAtlas, null)}.");
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Icon table [{sceneName}] failed: {ex.Message}");
        }
    }


    // Panel_Map exposes public entry points that build the marker objects. Calling them with
    // the panel closed populates the same containers the open panel would, which lets markers
    // appear without the player opening the map.
    private void TryLoadVanillaElementsWithoutPanel(string sceneName, MapLayer layer)
    {
        // LoadMapElementsForScene appends to the existing containers, so calling it repeatedly
        // duplicates every marker. Load each scene's elements exactly once.
        if (string.Equals(_elementsLoadedForScene, sceneName, StringComparison.Ordinal))
            return;
        if (DateTime.UtcNow < _elementLoadAfterUtc)
            return;
        _elementLoadAfterUtc = DateTime.UtcNow.AddSeconds(2);

        try
        {
            Panel_Map panel = InterfaceManager.GetPanel<Panel_Map>();
            if (panel == null)
                return;

            bool requested = false;
            try { panel.ForceUpdateRegion(); requested = true; }
            catch (Exception ex) { LoggerInstance.Warning($"ForceUpdateRegion failed: {ex.Message}"); }

            try { panel.LoadMapElementsForScene(sceneName); requested = true; }
            catch (Exception ex) { LoggerInstance.Warning($"LoadMapElementsForScene failed: {ex.Message}"); }

            if (!requested)
                return;

            _elementsLoadedForScene = sceneName;
            layer.ElementsLoadedForScene = sceneName;
            Transform mapElements = FindChildByName(panel.transform, "MapElements");
            if (mapElements == null)
                return;

            _vanillaIconSignature = ComputeVanillaIconSignature(mapElements);
            CaptureVanillaIcons(mapElements, true, true);
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Closed-panel marker load failed: {ex.Message}");
        }
    }


    // The uv rectangle of the opaque pixels inside a texture. The region base map is delivered on a
    // fixed-size canvas with transparent margins whose size differs per region, so the projection has
    // to aim at the content or every marker and the player pointer land off by a region-specific
    // amount. Returns the full texture when the measurement is unusable, which is the same
    // behaviour as before rather than a guess.
    private Rect MeasureOpaqueUv(Texture2D texture)
    {
        var full = new Rect(0f, 0f, 1f, 1f);
        if (ReferenceEquals(texture, null))
            return full;
        try
        {
            int width = texture.width;
            int height = texture.height;
            if (width <= 1 || height <= 1)
                return full;

            Il2CppStructArray<Color32> pixels = texture.GetPixels32();
            if (pixels == null || pixels.Length < width * height)
                return full;

            int minX = width, maxX = -1, minY = height, maxY = -1;
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (pixels[row + x].a <= 8)
                        continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < minX || maxY < minY)
                return full;

            // GetPixels32 is bottom-up, so row 0 is the bottom of the image and no flip is needed
            // to express the result in texture uv space.
            var content = new Rect(
                (float)minX / width,
                (float)minY / height,
                (float)(maxX - minX + 1) / width,
                (float)(maxY - minY + 1) / height);

            // A box that covers essentially everything is not letterboxed, so report the full
            // texture and keep the numbers clean.
            if (content.width > 0.995f && content.height > 0.995f)
                return full;
            return content;
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Measuring base map content failed: {ex.Message}");
            return full;
        }
    }


    private void UseVanillaBaseMap(Texture2D texture, string sceneName, MapLayer layer,
        Rect textureUv, Rect? mapLocalBounds = null)
    {
        EnsureUnityUi();
        RetireTexture(layer);
        layer.Texture = texture;
        layer.LoadedMapId = "__basemap__" + sceneName;
        layer.UsingVanilla = true;
        layer.TextureReady = true;
        // Captured maps carry the panel's exact framing in their sidecar. For the built-in base-map
        // fallback, use the measured family defaults: legacy regions are 650 square, Far Territory
        // regions are 600 square, and Ravine is 650x325. The texture uv is independent of those
        // local bounds because the 1024 asset can be letterboxed.
        layer.VanillaProjectionScene = sceneName;
        layer.VanillaMapLocalBounds = mapLocalBounds ?? DefaultVanillaMapBounds(sceneName);
        layer.VanillaTextureUv = textureUv;
    }


    private static Rect DefaultVanillaMapBounds(string sceneName)
    {
        if (string.Equals(sceneName, "RavineTransitionZone", StringComparison.Ordinal))
            return new Rect(-325f, -162.5f, 650f, 325f);

        switch (sceneName)
        {
            case "AirfieldRegion":
            case "HubRegion":
            case "TransferPass":
            case "TransferPassRegion":
            case "LongRailTransitionZone":
            case "MiningRegion":
            case "ZoneOfContaminationRegion":
            case "MountainPassRegion":
            case "SunderedPassRegion":
                return new Rect(-300f, -300f, 600f, 600f);
            default:
                return new Rect(-325f, -325f, 650f, 650f);
        }
    }


    private bool TryDiscoverMapIconAtlas(string sceneName)
    {
        if (!ReferenceEquals(_mapIconAtlas, null))
            return true;

        try
        {
            var details = MapDetailManager.s_MapDetails;
            if (details == null || details.Count == 0)
                return false;

            var spriteNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < details.Count; i++)
            {
                MapDetail detail = details[i];
                if (detail == null || string.IsNullOrEmpty(detail.m_SpriteName))
                    continue;
                spriteNames.Add(detail.m_SpriteName);
            }
            if (spriteNames.Count == 0)
                return false;

            var atlases = Resources.FindObjectsOfTypeAll<UIAtlas>();
            UIAtlas best = null;
            int bestScore = 0;
            foreach (UIAtlas atlas in atlases)
            {
                if (ReferenceEquals(atlas, null))
                    continue;

                int score = 0;
                try
                {
                    if (ReferenceEquals(atlas.texture, null))
                        continue;
                    foreach (string spriteName in spriteNames)
                    {
                        if (!ReferenceEquals(atlas.GetSprite(spriteName), null))
                            score++;
                    }
                }
                catch
                {
                    continue;
                }

                if (score > bestScore)
                {
                    best = atlas;
                    bestScore = score;
                }
            }

            // A single generic name is not enough evidence to select a shared UI atlas. The real
            // map atlas resolves many independently registered marker sprites.
            if (ReferenceEquals(best, null) || bestScore < 3)
            {
                LoggerInstance.Msg(
                    $"Icon atlas discovery [{sceneName}]: no match among {atlases.Length} loaded atlases " +
                    $"for {spriteNames.Count} registered sprite names (best {bestScore}).");
                return false;
            }

            _mapIconAtlas = best;
            LoggerInstance.Msg(
                $"Icon atlas discovery [{sceneName}]: selected '{best.name}' with " +
                $"{bestScore}/{spriteNames.Count} registered sprite names.");
            return true;
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Icon atlas discovery [{sceneName}] failed: {ex.Message}");
            return false;
        }
    }
}
// — sutanm · 社区HUD地图

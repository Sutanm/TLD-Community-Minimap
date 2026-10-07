// 社区HUD地图 · sutanm — 图层与图源：每层的定义、贴图、投影基准
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

    private bool CategoryEnabled(MarkerCategory category) => category switch
    {
        MarkerCategory.Resources => _settings.ShowMarkerResources,
        MarkerCategory.Structures => _settings.ShowMarkerStructures,
        MarkerCategory.Corpses => _settings.ShowMarkerCorpses,
        MarkerCategory.RockCaches => _settings.ShowMarkerRockCaches,
        MarkerCategory.SprayMarks => _settings.ShowMarkerSprayMarks,
        // Never filtered out: an unrecognised name must not vanish because we could not place it.
        _ => true,
    };


    // Indexed by the LayerId constants below. Two fixed slots: this is a two-layer feature, not a
    // collection, and the identifiers are used directly as array indices.
    private const int LayerMini = 0;

    private const int LayerFull = 1;

    private readonly MapLayer[] _layers = { new MapLayer(), new MapLayer() };


    // Which layer the single UI object is currently showing. The full map is a modal overlay, so
    // while it is up it is the only thing on screen; otherwise the corner map owns the object.
    // Everything that reads map state to draw or project goes through here rather than naming a
    // field, because those reads are exactly what the split has to keep straight.
    private int ActiveLayerId => _fullMapOn ? LayerFull : LayerMini;

    private MapLayer ActiveLayer => _layers[ActiveLayerId];


    private void ObserveScene(int handle, string sceneName, bool forceReload = false)
    {
        bool sceneChanged = !string.Equals(_observedSceneName, sceneName, StringComparison.Ordinal);
        _observedSceneHandle = handle;
        _observedSceneName = sceneName;
        SetUiVisible(false);

        // Additive scene loads (TracksRegion_WILDLIFE, _SANDBOX, ...) re-fire scene
        // initialisation for the same region. Rebuilding the map there would drop the loaded
        // texture and markers, so only a genuine scene change resets state.
        if (!forceReload && !sceneChanged && ActiveLayer.Definition != null)
            return;

        ClearVanillaIcons();
        ClearLabels();
        // The marker set belongs to the region that just went away.
        _markersBuiltForScene = "";
        // A new region invalidates the framing outright, so no layer may keep the old one.
        ClearVanillaProjection();
        _layersDirty = true;
        _warmUpDone = false;
        _warmUpAfterUtc = DateTime.UtcNow.AddSeconds(4);
        _resourceFilterAfterUtc = DateTime.MaxValue;
        _resourceFilterProbeStartedUtc = DateTime.MaxValue;
        _resourceFilterFallbackUtc = DateTime.MaxValue;
        _nextResourceFilterProbeUtc = DateTime.MinValue;
        _resourceFilterPending = false;
        _nextResourceMarkerRefreshUtc = DateTime.MinValue;

        MapDefinition definition = FindRuntimeMap(sceneName);
        for (int i = 0; i < _layers.Length; i++)
        {
            MapLayer layer = _layers[i];
            ReleaseBaseMapHandle(layer);
            RetireTexture(layer);
            layer.Texture = null;
            layer.LoadedMapId = "";
            layer.UsingVanilla = false;
            layer.TextureReady = false;
            layer.VanillaUnavailable = false;
            layer.BaseMapRequestedScene = "";
            layer.BaseMapPending = false;
            layer.ElementsLoadedForScene = "";
            layer.LastCommunityLoadError = "";
            layer.RequestedSource = -1;
            // Both layers follow the same region: there is one map per scene, and which source to
            // draw it from is the per-layer part, not which map it is.
            layer.Definition = definition;
        }

        if (definition == null)
        {
            // Never carry a modal full-map state into an indoor/cave scene. There is no texture to
            // explain why the cursor was released, and reopening outdoors should start from the
            // normal corner-map state rather than resurrecting an invisible old modal state.
            if (_fullMapOn)
                ApplyViewState(_miniMapOn, false);
            _loadAfterUtc = DateTime.MaxValue;
            LoggerInstance.Msg($"Active scene has no map definition: {sceneName} (handle {handle}).");
            return;
        }

        _elementLoadAfterUtc = DateTime.UtcNow;
        _vanillaIconSignature = 0;
        _nextVanillaIconRefreshUtc = DateTime.MinValue;
        _loadAfterUtc = DateTime.MaxValue;
        // The main region becomes active before its _SANDBOX additive scene has finished selecting
        // random parent groups. Probe the game's own RandomSpawnObject.m_Inited flags instead of
        // guessing that this always takes three seconds. The old delay survives only as a fallback
        // for unusual scenes that contain no discoverable controller.
        _resourceFilterProbeStartedUtc = DateTime.UtcNow;
        _resourceFilterFallbackUtc = DateTime.UtcNow.AddSeconds(3);
        _nextResourceFilterProbeUtc = DateTime.MinValue;
        _resourceFilterPending = true;

        LoggerInstance.Msg(
            $"Active scene mapped: {sceneName} -> {definition.DisplayName} " +
            $"({definition.FileName}, calibrated={definition.IsCalibrated}).");
        if (definition.ProbeOnly)
        {
            LoggerInstance.Msg(
                $"Interior map probe active: scene={sceneName}, player transform will be " +
                "recorded with F11; the uncalibrated probe intentionally has no pointer.");
            ShowStatusToast($"洞穴地图探针已识别：{sceneName}（按 F11 记录）", 4f);
        }
    }


    private MapDefinition FindRuntimeMap(string sceneName)
    {
        MapDefinition definition = MapCatalog.Find(sceneName);
        if (definition != null || _settings == null || !_settings.DeveloperMode)
            return definition;

        Transform player = GameManager.GetPlayerTransform();
        if (!ReferenceEquals(player, null))
        {
            MapDefinition spatial = MapCatalog.FindSpatialVariant(sceneName, player.position.y);
            if (spatial != null)
                return spatial;
        }
        return ProbeMapStore.Find(sceneName) ?? MapCatalog.Find(sceneName, true);
    }


    private void RefreshSpatialMapVariant(int sceneHandle, string sceneName)
    {
        if (_settings == null || !_settings.DeveloperMode)
            return;

        Transform player = GameManager.GetPlayerTransform();
        if (ReferenceEquals(player, null))
            return;

        MapDefinition desired = MapCatalog.FindSpatialVariant(sceneName, player.position.y);
        MapDefinition current = _layers[LayerMini].Definition;
        if (desired == null || current == null ||
            string.Equals(desired.Id, current.Id, StringComparison.OrdinalIgnoreCase))
            return;

        LoggerInstance.Msg(
            $"Spatial map variant changed: {sceneName} Y={player.position.y:F3} -> " +
            $"{desired.DisplayName} ({desired.FileName}).");
        ObserveScene(sceneHandle, sceneName, true);
    }


    private bool CommunityMapExists(MapLayer layer) =>
        layer.Definition != null &&
        File.Exists(Path.Combine(_mapsDirectory, layer.Definition.FileName));


    // This returns the EFFECTIVE source rather than rewriting the requested setting. Automatic
    // mode prefers an installed community image. An explicitly requested source also falls back
    // when it cannot exist here: a missing community file falls back to vanilla immediately, and
    // a vanilla-less region falls back after the game reports that fact.
    private bool LayerWantsCommunity(MapLayer layer)
    {
        bool communityAvailable = CommunityMapExists(layer);
        if (layer.Definition?.ProbeOnly == true)
            return communityAvailable;
        if (layer.Source == 1)
            return _settings.DisableSourceFallback || communityAvailable;
        if (layer.Source == 2)
            return !_settings.DisableSourceFallback && layer.VanillaUnavailable &&
                   communityAvailable;
        if (layer.Definition == null)
            return false;
        return communityAvailable;
    }


    // Kept for the callers that only ask about the layer being drawn.
    private bool ShouldUseCommunityMap() => LayerWantsCommunity(ActiveLayer);


    // Stage two of the split: each layer reads its own setting, so the corner map and the full map
    // can show different sources at the same time. Both default to automatic, which keeps an
    // untouched install behaving exactly as it did before the pair existed.
    private void ReadLayerSettings()
    {
        int fallbackPolicy = _settings.DisableSourceFallback ? 1 : 0;
        if (_sourceFallbackPolicy != fallbackPolicy)
        {
            _sourceFallbackPolicy = fallbackPolicy;
            for (int i = 0; i < _layers.Length; i++)
                _layers[i].RequestedSource = -1;
            LoggerInstance.Msg(_settings.DisableSourceFallback
                ? "Explicit map sources will not fall back."
                : "Explicit map sources may temporarily fall back when unavailable.");
        }

        for (int i = 0; i < _layers.Length; i++)
        {
            int source = i == LayerMini ? _settings.MiniMapSource : _settings.FullMapSource;
            MapLayer layer = _layers[i];
            if (layer.Source == source)
                continue;
            layer.Source = source;
            layer.VanillaUnavailable = false;
            LoggerInstance.Msg($"Layer '{LayerName(i)}' source set to {DescribeSource(source)}.");
        }
    }


    private static string DescribeSource(int source) => source switch
    {
        1 => "community",
        2 => "vanilla",
        _ => "automatic",
    };


    // Re-applies a layer's source after the setting changed. Kept per-layer because each one has to
    // drop its own loaded map and re-request; with two layers on one setting this runs for both,
    // which is what the old single-layer version did.
    private void ApplyMapSourceSelection(string sceneName, MapLayer layer, int layerId,
        bool preferCommunity)
    {
        layer.RequestedSource = layer.Source;
        // The other layer may now be the one that needs warming, or may already hold the right map.
        _warmUpDone = false;
        _warmUpAfterUtc = DateTime.UtcNow.AddSeconds(2);

        // Whichever source we are leaving, the texture it produced is destroyed when the other one
        // loads, so the "already requested this scene" marker becomes a lie and has to be cleared
        // or the base map request is skipped on the way back.
        //
        // The marker set is deliberately NOT touched here. Clearing it forced a re-capture, and a
        // re-capture after the map panel has ever been opened only returns the markers inside the
        // surveyed area - CaptureVanillaIconsRecursive drops sprites whose enabled flag the game's
        // RefreshIconVisibility has turned off, which is how the game draws its fog. The log shows
        // it plainly: 162 markers on scene load, 28 after the panel was opened once.
        // The markers also stay valid across a switch because they are only drawn while the
        // vanilla source is active, and LoadMapElementsForScene appends rather than replaces, so
        // keeping ElementsLoadedForScene also avoids duplicating every marker.
        layer.BaseMapRequestedScene = "";
        ReleaseBaseMapHandle(layer);
        layer.BaseMapPending = false;

        if (preferCommunity)
        {
            layer.UsingVanilla = false;
            bool alreadyLoaded = layer.Definition != null &&
                                 layer.LoadedMapId == layer.Definition.Id &&
                                 !ReferenceEquals(layer.Texture, null);
            layer.TextureReady = alreadyLoaded;
            RequestMapLoad(alreadyLoaded
                ? DateTime.MaxValue
                : DateTime.UtcNow);
            bool vanillaFallback = layer.Source == 2 && layer.VanillaUnavailable;
            LoggerInstance.Msg(vanillaFallback
                ? $"Map source selected [{LayerName(layerId)}]: community map " +
                  "(temporary fallback; vanilla is unavailable in this scene)."
                : $"Map source selected [{LayerName(layerId)}]: community map.");
        }
        else
        {
            bool capturedForScene = layer.UsingVanilla &&
                                    string.Equals(layer.VanillaProjectionScene, sceneName,
                                        StringComparison.Ordinal) &&
                                    !ReferenceEquals(layer.Texture, null);
            layer.TextureReady = capturedForScene;
            RequestMapLoad(DateTime.MaxValue);
            bool communityFallback = layer.Source == 1 && !CommunityMapExists(layer);
            LoggerInstance.Msg(communityFallback
                ? $"Map source selected [{LayerName(layerId)}]: vanilla map " +
                  "(temporary fallback; community image is missing)."
                : capturedForScene
                    ? $"Map source selected [{LayerName(layerId)}]: vanilla surveyed map."
                    : $"Map source selected [{LayerName(layerId)}]: vanilla surveyed map; " +
                      "open the game map once to refresh it.");
        }
    }


    // Records a fact about this scene, not a preference. Re-applying source selection on the next
    // frame moves an explicit vanilla request onto the community image when one is installed.
    // The flag is cleared on scene changes and setting changes, so a later region gets a fresh
    // vanilla request and the ModSettings value is never touched.
    private void MarkVanillaUnavailable(string sceneName, MapLayer layer, string reason)
    {
        if (layer.VanillaUnavailable)
            return;

        layer.VanillaUnavailable = true;
        layer.RequestedSource = -1;
        LoggerInstance.Warning($"Vanilla map unavailable for {sceneName}: {reason}");

        if (_settings.DisableSourceFallback || !CommunityMapExists(layer))
            return;

        RequestMapLoad(DateTime.UtcNow);
        if (ReferenceEquals(layer, ActiveLayer))
            ShowStatusToast("原版地图不可用，已临时切换至社区地图");
    }


    // Whether pressing a view key can currently produce a map for this layer. Unknown vanilla
    // availability remains attemptable until the game's request definitively fails; after that we
    // can distinguish "loading" from "there is nothing to show" instead of opening a blank modal.
    private bool CanAttemptLayerSource(MapLayer layer)
    {
        bool communityAvailable = CommunityMapExists(layer);
        if (!_settings.DisableSourceFallback || layer.Source == 0)
            return communityAvailable || !layer.VanillaUnavailable;
        if (layer.Source == 1)
            return communityAvailable;
        return !layer.VanillaUnavailable;
    }


    private string UnavailableLayerMessage(MapLayer layer)
    {
        if (_settings.DisableSourceFallback && layer.Source == 1)
            return "所选社区地图不可用，图源回退已禁用";
        if (_settings.DisableSourceFallback && layer.Source == 2)
            return "所选原版地图不可用，图源回退已禁用";
        return "当前场景没有可用的 HUD 地图";
    }


    private static string LayerName(int layerId) => layerId == LayerMini ? "mini" : "full";


    // Drops every layer's vanilla projection.
    //
    // These values are a fact about the REGION, not about a layer: both layers ask the same
    // Panel_Map about the same scene, and every measurement of both paths has reported the same
    // numbers. Storing them per layer was an attempt to stop one layer's source switch disturbing
    // the other, but it bought nothing - neither layer writes a different value - and it cost a
    // real bug: a base map loads on whichever layer is active, so the OTHER layer keeps whatever
    // framing it last saw. The 22:08 session shows it, a community layer still carrying the vanilla
    // bounds (-325,-325,650x650).
    //
    // Clearing is therefore by region, not by source switch. A layer that switches back to vanilla
    // re-requests its own base map and repopulates this on arrival, which is the sequence the
    // request markers already enforce.
    private void ClearVanillaProjection()
    {
        for (int i = 0; i < _layers.Length; i++)
        {
            MapLayer layer = _layers[i];
            layer.VanillaProjectionScene = "";
            layer.VanillaMapLocalBounds = new Rect(-1024f, -1024f, 2048f, 2048f);
            layer.VanillaTextureUv = new Rect(0f, 0f, 1f, 1f);
        }
    }


    // _loadAfterUtc is one global clock shared by both layers, because only the layer on screen is
    // ever loaded by the update loop. Letting a layer push it later re-arms a load for a texture
    // that is already on disk in memory: that is what decoded 山间小镇 4360x4198 twice, four and a
    // half seconds apart, the second time while the full map was open and visible.
    private void RequestMapLoad(DateTime when)
    {
        if (when < _loadAfterUtc)
            _loadAfterUtc = when;
    }


    // True while any layer that wants the vanilla source already holds a texture. Used by the
    // panel-fallback path, which must not overwrite a good base map with the surveyed one.
    private bool AnyVanillaLayerReady()
    {
        for (int i = 0; i < _layers.Length; i++)
        {
            if (_layers[i].UsingVanilla && _layers[i].TextureReady)
                return true;
        }
        return false;
    }


    private static bool TryGetOpenVanillaMap(out Panel_Map panel)
    {
        panel = null;
        try
        {
            panel = InterfaceManager.GetPanel<Panel_Map>();
            return panel != null && panel.gameObject.activeInHierarchy;
        }
        catch
        {
            panel = null;
            return false;
        }
    }


    private string DescribeView() => FullMapVisible ? "FullMap"
        : MiniMapVisible ? "MiniMap" : "None";

    private bool FullMapPrefersOverview() =>
        _layers[LayerFull].Definition?.PreferFullMapOverview == true;

    private float InitialFullMapZoom() =>
        FullMapPrefersOverview() ? 1f : FullMapCoverZoom();


    private void ApplyViewState(bool mini, bool full)
    {
        // Opening resets it onto the player: a map that reopens wherever it was last dragged is
        // disorienting, and the player is the one thing on it that moved.
        if (full && !_fullMapOn)
        {
            _miniMapBeforeFull = _miniMapOn;
            // Match the original map: open on the useful screen-filling/local stop. Scrolling out
            // once returns to 1.0, where the complete sheet is visible in the centre.
            _fullMapZoom = InitialFullMapZoom();
            _fullMapCenterValid = false;
        }
        if (!full && _fullMapOn)
        {
            // A tooltip is parented to the shared map object. Without explicit cleanup it survives
            // the same-frame switch back to the corner layer until the pointer leaves its old icon.
            if (!ReferenceEquals(_hoverLabelRoot, null))
                _hoverLabelRoot.SetActive(false);
            if (!ReferenceEquals(_hoverPlateRoot, null))
                _hoverPlateRoot.SetActive(false);
        }
        _miniMapOn = mini;
        _fullMapOn = full;
    }


    // The next state comes from what is on screen rather than from a remembered step, because the
    // game's map key moves between the same states: a stored index would go stale and skip one.
    // While the full map is up the corner map is still switched on underneath, so the full map is
    // what decides which step we are on.
    private void CycleView()
    {
        var scene = UnitySceneManager.GetActiveScene();
        if (FindRuntimeMap(scene.name) == null)
        {
            ShowStatusToast("当前场景没有可用的 HUD 地图");
            return;
        }
        if (!CanAttemptLayerSource(ActiveLayer))
        {
            ShowStatusToast(UnavailableLayerMessage(ActiveLayer));
            return;
        }

        List<(bool Mini, bool Full)> steps = BuildCycle();
        if (steps.Count < 2)
            return;

        int current = -1;
        for (int i = 0; i < steps.Count; i++)
        {
            bool matches = _fullMapOn
                ? steps[i].Full
                : _miniMapOn
                    ? steps[i].Mini && !steps[i].Full
                    : !steps[i].Mini && !steps[i].Full;
            if (matches)
            {
                current = i;
                break;
            }
        }

        (bool Mini, bool Full) next = steps[(current + 1) % steps.Count];
        ApplyViewState(next.Mini, next.Full);
        LoggerInstance.Msg($"View cycle: {DescribeView()} ({current + 2}/{steps.Count}).");
    }


    private void OpenFullMap()
    {
        if (!_settings.RedirectGameMap)
            return;
        ApplyViewState(_miniMapOn, true);
    }


    private void CloseFullMap()
    {
        ApplyViewState(_miniMapBeforeFull, false);
    }
}
// — sutanm · 社区HUD地图

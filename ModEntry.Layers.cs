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


    private void ObserveScene(int handle, string sceneName)
    {
        bool sceneChanged = !string.Equals(_observedSceneName, sceneName, StringComparison.Ordinal);
        _observedSceneHandle = handle;
        _observedSceneName = sceneName;
        SetUiVisible(false);

        // Additive scene loads (TracksRegion_WILDLIFE, _SANDBOX, ...) re-fire scene
        // initialisation for the same region. Rebuilding the map there would drop the loaded
        // texture and markers, so only a genuine scene change resets state.
        if (!sceneChanged && ActiveLayer.Definition != null)
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

        MapDefinition definition = MapCatalog.Find(sceneName);
        for (int i = 0; i < _layers.Length; i++)
        {
            MapLayer layer = _layers[i];
            ReleaseBaseMapHandle(layer);
            RetireTexture(layer);
            layer.Texture = null;
            layer.LoadedMapId = "";
            layer.UsingVanilla = false;
            layer.TextureReady = false;
            layer.BaseMapRequestedScene = "";
            layer.BaseMapPending = false;
            layer.ElementsLoadedForScene = "";
            layer.RequestedSource = -1;
            // Both layers follow the same region: there is one map per scene, and which source to
            // draw it from is the per-layer part, not which map it is.
            layer.Definition = definition;
        }

        if (definition == null)
        {
            _loadAfterUtc = DateTime.MaxValue;
            LoggerInstance.Msg($"Active scene has no map definition: {sceneName} (handle {handle}).");
            return;
        }

        _elementLoadAfterUtc = DateTime.UtcNow;
        _vanillaIconSignature = 0;
        _nextVanillaIconRefreshUtc = DateTime.MinValue;
        _loadAfterUtc = DateTime.MaxValue;

        LoggerInstance.Msg(
            $"Active scene mapped: {sceneName} -> {definition.DisplayName} " +
            $"({definition.FileName}, calibrated={definition.IsCalibrated}).");
    }


    // Automatic mode prefers the community map but only when the image is actually on disk, so the
    // answer depends on which definition the layer is holding rather than on a global one.
    private bool LayerWantsCommunity(MapLayer layer)
    {
        if (layer.Source == 1)
            return true;
        if (layer.Source == 2 || layer.Definition == null)
            return false;
        return File.Exists(Path.Combine(_mapsDirectory, layer.Definition.FileName));
    }


    // Kept for the callers that only ask about the layer being drawn.
    private bool ShouldUseCommunityMap() => LayerWantsCommunity(ActiveLayer);


    // Stage two of the split: each layer reads its own setting, so the corner map and the full map
    // can show different sources at the same time. Both default to automatic, which keeps an
    // untouched install behaving exactly as it did before the pair existed.
    private void ReadLayerSettings()
    {
        for (int i = 0; i < _layers.Length; i++)
        {
            int source = i == LayerMini ? _settings.MiniMapSource : _settings.FullMapSource;
            MapLayer layer = _layers[i];
            if (layer.Source == source)
                continue;
            layer.Source = source;
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
            LoggerInstance.Msg($"Map source selected [{LayerName(layerId)}]: community map.");
        }
        else
        {
            bool capturedForScene = layer.UsingVanilla &&
                                    string.Equals(layer.VanillaProjectionScene, sceneName,
                                        StringComparison.Ordinal) &&
                                    !ReferenceEquals(layer.Texture, null);
            layer.TextureReady = capturedForScene;
            RequestMapLoad(DateTime.MaxValue);
            LoggerInstance.Msg(capturedForScene
                ? $"Map source selected [{LayerName(layerId)}]: vanilla surveyed map."
                : $"Map source selected [{LayerName(layerId)}]: vanilla surveyed map; " +
                  "open the game map once to refresh it.");
        }
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


    // The HUD is worth showing while either layer has something to draw. The visible layer is
    // still the one that decides what is drawn; this only decides whether to unhide the canvas.
    private bool AnyLayerReady()
    {
        for (int i = 0; i < _layers.Length; i++)
        {
            if (_layers[i].TextureReady)
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


    private void ApplyViewState(bool mini, bool full)
    {
        // Opening resets it onto the player: a map that reopens wherever it was last dragged is
        // disorienting, and the player is the one thing on it that moved.
        if (full && !_fullMapOn)
        {
            // Match the original map: open on the useful screen-filling/local stop. Scrolling out
            // once returns to 1.0, where the complete sheet is visible in the centre.
            _fullMapZoom = FullMapCoverZoom();
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
        ApplyViewState(_miniMapOn, false);
    }
}
// — sutanm · 社区HUD地图

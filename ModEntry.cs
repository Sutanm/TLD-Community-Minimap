// 社区HUD地图 · sutanm — 主模组：HUD、图层、标记、校准
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

public sealed class ModEntry : MelonMod
{
    private static bool s_fullMapActive;
    private static DateTime s_lastMapRedirectUtc = DateTime.MinValue;
    private bool _mapContextPushed;
    private float _fullMapZoom = 1f;
    private Vector2 _fullMapCenter = new(0.5f, 0.5f);
    private bool _fullMapCenterValid;
    private bool _fullMapDragging;
    private Vector2 _fullMapDragLast;
    internal static ModEntry s_instance;
    private static int s_suppressEscapeThroughFrame = -1;

    // Stamped by the MapDetail.Surveyed patch. A charcoal survey marks each revealed location, and
    // the forced map panel follows within a second or so; that gap is the only way to tell the
    // survey popup apart from the player opening the map from the inventory, because both arrive
    // as the same Panel_Map.Enable call.
    internal static DateTime s_lastSurveyUtc = DateTime.MinValue;
    private const double SurveyPopupWindowSeconds = 3.0;

    // The corner HUD and the full-screen map are two independent layers: the corner map sits
    // behind a plain on/off setting, the full map is a modal overlay that the game's own map key
    // always opens. Hiding one never takes the other with it, which is what the single two-state
    // DisplayMode used to do.
    private bool _miniMapOn = true;
    private bool _fullMapOn;

    // State for polling the game's own map key while the full map is open; see PollOpenMapKey.
    private bool _openMapKeyHeld;
    private DateTime _nextOpenMapKeyLogUtc = DateTime.MinValue;

    // Remote-diagnosis support: one compact state line on a timer, plus a census of the game's map
    // data taken whenever the vanilla panel opens or closes. Both exist so a single play session
    // answers the open questions - whether the harvestable links are populated at all, and whether
    // the fog machinery exists while the panel is shut - without asking the player to do anything
    // special or to read a wall of log.
    private DateTime _nextHeartbeatUtc = DateTime.MinValue;
    private DateTime _nextCensusUtc = DateTime.MinValue;
    private bool _censusPanelWasOpen;

    // Framing probe for the layer split (section 37.1). The vanilla base map and the vanilla map
    // panel are two different textures reached by two different paths, and the base-map path
    // hard-codes the local bounds on the assumption that both share one framing. That assumption
    // is load-bearing once each layer can hold a different texture, so it gets measured rather
    // than assumed. Bounded to a handful of attempts per scene and a few lines of log.
    private string _framingScene = "";
    private int _framingAttempts;
    private DateTime _framingAfterUtc = DateTime.MinValue;
    private bool _framingDone;

    private readonly MinimapSettings _settings = new();
    private string _modDirectory = "";
    private string _mapsDirectory = "";
    private string _calibrationPath = "";
    private DateTime _calibrationLastWriteUtc = DateTime.MinValue;
    private DateTime _nextCalibrationCheckUtc = DateTime.MinValue;
    private bool _temporarilyHidden;
    private bool _uiVisible;
    private int _observedSceneHandle = int.MinValue;
    private DateTime _loadAfterUtc = DateTime.MaxValue;
    private DateTime _sceneCatalogAfterUtc = DateTime.MaxValue;
    private int _sceneCatalogAttempts;
    private bool _sceneCatalogExported;
    private bool _capturedThisVanillaMapOpen;
    private bool _vanillaMapWasOpen;
    private DateTime _vanillaCaptureAfterUtc = DateTime.MaxValue;
    // Set when the scene changes so both layers re-resolve their source on the next update even
    // though their setting did not change.
    private bool _layersDirty;
    // The marker category switches as of the last draw, so the marker loop can tell in one
    // comparison whether it needs to re-resolve every icon's category.
    private int _markerCategoryState = -1;
    // Set once the layer that is not on screen has been loaded too, so the first switch to it does
    // not pay for the decode. Reset per scene.
    private bool _warmUpDone;
    private DateTime _warmUpAfterUtc = DateTime.MinValue;
    // Textures replaced this frame, freed at the start of the next one. See RetireTexture.
    private List<Texture2D> _retiredTextures;
    // Global, not per-layer: LoadMapElementsForScene APPENDS to the panel's marker containers, so
    // calling it once per layer would duplicate every marker. The per-layer marker in MapLayer is a
    // separate fact ("this layer already has its markers") and never licenses a second call.
    private string _elementsLoadedForScene = "";

    // Some scenes get no base map at all. We cannot tell "the game has no region here" from
    // "the region just is not ready yet" without saying so out loud, so track how long the
    // lookup has been failing and report it once.
    private string _baseMapNullScene = "";
    private DateTime _baseMapNullSinceUtc = DateTime.MinValue;
    private bool _baseMapNullLogged;
    private DateTime _elementLoadAfterUtc = DateTime.MinValue;
    private string _observedSceneName = "";
    // Panel_Map lays its marker objects out at a hard-coded 0.33 root scale. Elements created
    // through LoadMapElementsForScene never go through that layout, so their measured bounds
    // come out exactly 3x too large; measured 52.0 vs 17.3, 32.0 vs 10.7, 47.8 vs 15.9.
    private const float PanelFreeIconScale = 1f / 3f;

    // Region-wide, not per-layer: both layers ask the same Panel_Map the same question about the
    // same scene, so the vanilla projection is one fact about the region rather than one per layer.
    // It is held per layer inside MapLayer so one layer's source switch cannot disturb the framing
    // the other one is already projecting with.

    private GameObject _uiRoot;
    private GameObject _backgroundObject;
    private Image _backgroundImage;
    private RectTransform _mapRect;
    private RawImage _mapImage;
    private Text _hintLabel;
    private Font _hintFont;
    private GameObject _markerRoot;
    private RectTransform _markerRect;
    private RawImage _markerImage;
    private readonly Texture2D[] _markerTextures = new Texture2D[6];
    private readonly List<VanillaIcon> _vanillaIcons = new();
    private readonly List<VanillaIcon> _pendingVanillaIcons = new();
    private DateTime _nextVanillaIconRefreshUtc = DateTime.MinValue;
    private long _vanillaIconSignature;

    // The game names its map icons as strings; the texture behind a name only exists on the
    // panel's sprite objects. Collect the mapping while scraping the UI so the marker list can
    // later be driven from MapDetail data instead of from the sprites themselves.
    private sealed class IconRef
    {
        public Texture Texture;
        public Rect Uv;
        // Only filled by the atlas path; the scraped path derives size from widget corners instead.
        public int PixelWidth;
        public int PixelHeight;
    }

    // Counters for the last MapDetail-driven build, reported once so a session says how many
    // markers could not be placed rather than leaving it to be guessed from the total.
    private int _mapDetailSkipped;
    private int _mapDetailUnresolved;
    // scene|source of the last marker build, so a rebuild happens when either changes and not
    // otherwise.
    private string _markersBuiltForScene = "";
    // Rate limit for rebuilding the marker set while the region's entry count is still growing.
    private DateTime _nextMarkerRebuildUtc = DateTime.MinValue;
    // Place names, which the marker build skips because they carry no sprite name.
    private readonly List<MapLabel> _mapLabels = new();
    // Label entries whose text could not be resolved, reported once so the reason is visible.
    private readonly List<string> _labelMisses = new();
    // Region whose panel texture has already been measured and exported, so the diagram is written
    // once instead of on every panel refresh.
    private string _panelTextureMeasuredForScene = "";

    private readonly Dictionary<string, IconRef> _iconBySpriteName = new(StringComparer.Ordinal);
    private UIAtlas _mapIconAtlas;
    private float _vanillaIconMaxUv = 1f;
    private DateTime _nextMarkerCleanupUtc = DateTime.MinValue;

    // The five buckets the game's own map filter offers, so a player already knows what they mean.
    // They are SEMANTIC, and deliberately not the MapIconType enum: that one says how big an icon
    // is drawn (small entry / detail / top), and DetailEntry alone holds both cattails and deer
    // carcasses, which belong in different buckets here.
    //
    // Classified by sprite name. Anything not recognised lands in Unclassified and is always drawn:
    // guessing a bucket for a name we cannot place would hide a category of marker silently, which
    // is the failure this whole rewrite exists to fix.
    internal enum MarkerCategory
    {
        Resources,
        Structures,
        Corpses,
        RockCaches,
        SprayMarks,
        Unclassified,
    }

    internal static MarkerCategory CategorizeSprite(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName))
            return MarkerCategory.Unclassified;

        switch (spriteName)
        {
            // Harvestable plants and animals lying in the world.
            case "icoMap_cattails":
            case "icoMap_rosehips":
            case "icoMap_oldmansbeard":
            case "icoMap_reishi":
            case "icoMap_burdock":
            case "icoMap_sapling":
            case "icoMap_limb":          // a limb is a harvestable branch pile
            case "icoMap_ptarmiganNest":
            case "icoMap_rabbit":
            case "icoMap_deerCarcass":
            case "icoMap_corpse":
                return MarkerCategory.Resources;

            // Buildings and the man-made things on the map.
            case "icoMap_churchMilton":
            case "icoMap_farmhouseMilton":
            case "icoMap_greyMother":
            case "icoMap_willAirplane":
            case "icoMap_radioTower":
            case "icoMap_orcaGas":
            case "icoMap_trailer":
            case "icoMap_bus":
            case "icoMap_car":
            case "icoMap_bridge":
            case "icoMap_crossroads":
            case "icoMap_burntHusk":
            case "icoMap_rope":
            case "icoMap_spences":
            case "icoMap_3Strikes":
            case "icoMap_hatch":
            case "icoMap_cave":
            case "map_transition_map":
                return MarkerCategory.Structures;

            // Not placed yet. `icoMap_container`, `icoMap_Generic`, `ico_Radial_pack` and
            // `ico_collections_polaroids` are the candidates but there is no evidence which, if any,
            // is a rock cache or a spray mark. Reported by the marker census so the table can be
            // completed from a real session instead of a guess.
            default:
                return MarkerCategory.Unclassified;
        }
    }

    internal static string CategoryName(MarkerCategory category) => category switch
    {
        MarkerCategory.Resources => "资源",
        MarkerCategory.Structures => "结构",
        MarkerCategory.Corpses => "尸骸",
        MarkerCategory.RockCaches => "岩石贮藏处",
        MarkerCategory.SprayMarks => "油漆喷罐标记",
        _ => "未分类",
    };

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


    private sealed class VanillaIcon
    {
        public GameObject Root;
        public RectTransform Rect;
        public Vector2 MapUv;
        public Vector2 MapUvSize;
        // Resolved once when the icon is built. The category comes from the sprite name, and the
        // switch that hides it is read from the settings - both are stable for the icon's lifetime
        // and looking either up per frame would cost more than the draw call it guards.
        public MarkerCategory Category;
        public bool CategoryEnabled;
    }

    // The corner HUD and the full-screen map are two independent layers, so each carries its own
    // map source, its own texture and its own in-flight request markers. They share one UI object
    // (the layout only changes its anchors and size) and one region-wide vanilla projection.
    //
    // Source: the requested setting, 0 automatic / 1 community / 2 vanilla.
    // UsingVanilla: whether the texture actually in Texture came from the game rather than from the
    // maps folder. In automatic mode that is discovered, not chosen, so it is stored rather than
    // recomputed from Source.
    private sealed class MapLayer
    {
        public int Source;
        public int RequestedSource = -1;
        public MapDefinition Definition;
        public string LoadedMapId = "";
        public Texture2D Texture;
        public bool UsingVanilla;
        public bool TextureReady;
        public AsyncOperationHandle<Texture2D> BaseMapHandle;
        public bool BaseMapPending;
        public string BaseMapRequestedScene = "";
        public DateTime BaseMapRequestUtc = DateTime.MinValue;
        public string ElementsLoadedForScene = "";
        // The vanilla projection this layer is holding. The values are region-wide and both layers
        // compute the same ones, but they are stored per layer on purpose: the panel path writes
        // bounds and uv, the base-map path writes different defaults, and switching ONE layer's
        // source must not change the framing the other layer is already projecting with.
        public string VanillaProjectionScene = "";
        public Rect VanillaMapLocalBounds = new(-1024f, -1024f, 2048f, 2048f);
        public Rect VanillaTextureUv = new(0f, 0f, 1f, 1f);
    }

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

    public override void OnInitializeMelon()
    {
        s_instance = this;
        HarmonyInstance.PatchAll();
        _settings.AddToModSettings("社区HUD地图", MenuType.Both);
        // The settings GUI does not exist yet, so the visibility rules have to be applied once by
        // hand or the developer-only rows show up for everyone until something changes.
        _settings.ApplyVisibility(null, null, null);
        _modDirectory = Path.Combine(MelonEnvironment.ModsDirectory, "CommunityMinimap");
        _mapsDirectory = Path.Combine(_modDirectory, "maps");
        _calibrationPath = Path.Combine(_modDirectory, "calibrations.json");
        Directory.CreateDirectory(_mapsDirectory);
        CalibrationStore.Load(_calibrationPath,
            message => LoggerInstance.Msg(message),
            message => LoggerInstance.Warning(message));
        _calibrationLastWriteUtc = File.GetLastWriteTimeUtc(_calibrationPath);
        _sceneCatalogAfterUtc = DateTime.UtcNow.AddSeconds(5);
        LoggerInstance.Msg("社区HUD地图 0.7.0 initialized.");
        LoggerInstance.Msg($"Map directory: {_mapsDirectory}");
    }

    public override void OnUpdate()
    {
        // First thing, so the previous frame has definitely finished drawing with them. There are
        // several early returns below and none of them may skip this.
        SweepRetiredTextures();
        PollRegionAssetProbes();
        PollPrefabProbes();
        TryExportSceneCatalog();
        TryReloadCalibrations();

        // Capture the game's own map image on demand, while it is on screen.
        //
        // The mod cannot light a region up, so the reveal has to happen in the game. What it CAN do
        // is take the panel's full-resolution image at the moment the player says it is ready, apply
        // it to the HUD immediately, and keep a PNG for calibration. That is one key press in the
        // session where the player revealed the map, instead of a file that only refreshes on the
        // first panel open of a session and then has to be carried back to the workspace by hand.
        if (Input.GetKeyDown(KeyCode.F7))
            CaptureGameMapImage();

        if (Input.GetKeyDown(_settings.ToggleKey))
        {
            // Only the corner map: the full map is a modal overlay and must keep working while
            // the corner map is hidden, which is the whole point of the two being separate.
            _temporarilyHidden = !_temporarilyHidden;
            LoggerInstance.Msg($"Mini map {(_temporarilyHidden ? "hidden" : "shown")} temporarily.");
        }
        // The author's mark, and the only part of it that ever runs: hold both modifiers and press
        // the view key with the full map open. Nothing is drawn and nothing else can reach it -
        // plain presses of that key are handled by the branch below - so it stays out of the game
        // and out of the way until somebody deliberately looks for it.
        if (FullMapVisible && _settings.CycleViewKey != KeyCode.None &&
            Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftShift) &&
            Input.GetKeyDown(_settings.CycleViewKey))
        {
            LoggerInstance.Msg("社区HUD地图 · sutanm · 2026 — 有些东西是留给翻代码的人的。");
        }
        else if (_settings.EnableCycleKey && _settings.CycleViewKey != KeyCode.None &&
                 Input.GetKeyDown(_settings.CycleViewKey))
        {
            CycleView();
        }
        if (FullMapVisible && Input.GetKeyDown(KeyCode.Escape))
            LeaveFullMap();
        if (Input.GetKeyDown(_settings.RecordPointKey))
        {
            RecordCalibrationPoint();
            DumpMapDetails();
        }
        // The full map can be switched on in a scene that has no map at all, and the UI is only
        // built once a region with a map loads, so this has to tolerate a missing UI. Without the
        // guard, left-clicking while the view sat on FullMap in a map-less scene threw a
        // NullReferenceException straight out of OnUpdate.
        if (FullMapVisible && !ReferenceEquals(_mapRect, null))
        {
            HandleFullMapInput();
            // While the full map is open our input context stops the game from dispatching its own
            // open-map action, so the same key has to be read through the game's own query instead.
            PollOpenMapKey();
        }

        // The full map owns the cursor and the input context, but only while it is actually on
        // screen: turning a layer off in the settings has to hand the input back as well, and
        // there is nothing to push as a context until the UI exists.
        bool showFullMap = FullMapVisible && !ReferenceEquals(_backgroundImage, null);
        if (s_fullMapActive != showFullMap)
        {
            s_fullMapActive = showFullMap;
            if (showFullMap)
                ApplyMapInputContext();
            else
                ReleaseMapInputContext();
        }
        if (DateTime.UtcNow >= _nextMarkerCleanupUtc)
        {
            _nextMarkerCleanupUtc = DateTime.UtcNow.AddSeconds(2);
            CleanHarvestedMapMarkers();
        }

        var scene = UnitySceneManager.GetActiveScene();
        if (scene.handle != _observedSceneHandle)
            ObserveScene(scene.handle, scene.name);

        // Resolve both layers' sources from the settings, then let each one notice on its own that
        // its answer changed. Per-layer rather than one global check, because a layer that is not
        // being drawn still has to end up with the right texture ready before it is shown.
        ReadLayerSettings();
        for (int i = 0; i < _layers.Length; i++)
        {
            MapLayer layer = _layers[i];
            bool layerPreferCommunity = LayerWantsCommunity(layer);
            if (layer.RequestedSource != layer.Source || _layersDirty)
                ApplyMapSourceSelection(scene.name, layer, i, layerPreferCommunity);
        }
        _layersDirty = false;

        bool preferCommunity = ShouldUseCommunityMap();
        MapLayer active = ActiveLayer;

        // A scene change clears each layer's loaded map, so the community image has to be re-read
        // from disk. The original code waited a second before that first attempt to let the scene
        // finish coming up; preserve that, because reading a 4400px JPEG during the load tail is
        // exactly the stall it was there to avoid.
        if (preferCommunity && !active.TextureReady && _loadAfterUtc == DateTime.MaxValue)
            RequestMapLoad(DateTime.UtcNow.AddSeconds(1));

        bool playerReady = GameManager.m_Instance != null &&
                           !GameManager.IsMainMenuActive() &&
                           GameManager.GetPlayerTransform() != null;

        Panel_Map vanillaPanel = null;
        bool vanillaMapOpen = TryGetOpenVanillaMap(out vanillaPanel);

        // One census per panel transition. This is the measurement that tells apart "the
        // harvestable links are filled in only once the panel has built its elements" from "they
        // are never filled in at all" - section 20 recorded the latter, but MapIconFix works from
        // exactly those fields, so one of the two readings has to be wrong.
        if (vanillaMapOpen != _censusPanelWasOpen)
        {
            _censusPanelWasOpen = vanillaMapOpen;
            _nextCensusUtc = DateTime.MinValue;      // transitions must never be throttled away
            CensusMapDetails(vanillaMapOpen ? "panel opened" : "panel closed");
        }

        // Runs off the same transition but deliberately outside the source switch: the answer is
        // needed even while the community map is the active source, which is the state the tester
        // is in.
        if (vanillaMapOpen)
            ProbeVanillaFraming(scene.name);

        // Before the early returns below, so a session always leaves a readable trail even when
        // there is no map for the scene and the HUD never appears.
        LogStateHeartbeat();

        if (!vanillaMapOpen)
        {
            _vanillaMapWasOpen = false;
            _capturedThisVanillaMapOpen = false;
            _vanillaCaptureAfterUtc = DateTime.MaxValue;
        }
        else if (!_vanillaMapWasOpen)
        {
            _vanillaMapWasOpen = true;
            _capturedThisVanillaMapOpen = false;
            _vanillaCaptureAfterUtc = DateTime.UtcNow.AddSeconds(1);
        }
        else if (!preferCommunity && !_capturedThisVanillaMapOpen &&
                 DateTime.UtcNow >= _vanillaCaptureAfterUtc)
        {
            if (CaptureVanillaMap(vanillaPanel, scene.name))
                _capturedThisVanillaMapOpen = true;
            else
                _vanillaCaptureAfterUtc = DateTime.UtcNow.AddMilliseconds(250);
        }

        // The region's own base map is available without opening the game map panel, so the
        // HUD appears on scene load exactly like the community-map source.
        if (!preferCommunity && active.Definition != null && playerReady)
        {
            // A previously captured map beats the region's own 1024x1024 texture: it is the image the
            // game actually draws, at twice the resolution, and it exists because the player lit the
            // region by hand. Reading it here means the capture survives the session it was made in.
            if (!active.TextureReady || !active.UsingVanilla)
                TryLoadCapturedMap(scene.name, active);
            if (!active.TextureReady)
            {
                TryRequestVanillaBaseMap(scene.name, active);
                PollVanillaBaseMap(scene.name, active);
            }
            if (!vanillaMapOpen && active.TextureReady)
                TryLoadVanillaElementsWithoutPanel(scene.name, active);
        }

        // Fill the icon table and atlas whatever the source is. In the vanilla path above this has
        // already happened and the global one-shot marker makes this a no-op; in the community path
        // - the default whenever the images are installed - this is the only thing that fills them.
        if (active.Definition != null && playerReady && !vanillaMapOpen)
            PopulateIconTableOnce(scene.name);

        // The MapDetail-driven marker set. Rebuilt when the scene changes or the source setting
        // changes, never per frame: it can run to hundreds of objects, and recreating those every
        // frame is what the draw-time visibility rule exists to avoid.
        if (active.Definition != null && playerReady && !vanillaMapOpen)
        {
            // The projection is part of the key because marker uv is computed at BUILD time from
            // whichever layer is active then. Without the layer in the key, the markers built while
            // the vanilla source was up stay in vanilla uv and land in the wrong place the moment
            // the community map is on screen, even though both are "the same scene".
            string projection = active.UsingVanilla ? "vanilla" : "community";
            // The entry count is part of the key because s_MapDetails keeps growing after a scene
            // starts: the same region was logged at 769 entries right after load and at 816 once it
            // had settled, and an earlier session climbed 709, 711, 712, 717, 723. A build that ran
            // once at 769 therefore misses everything that streams in afterwards, which is where the
            // place names live.
            int detailCount = 0;
            try
            {
                var details = MapDetailManager.s_MapDetails;
                if (!ReferenceEquals(details, null))
                    detailCount = details.Count;
            }
            catch { }

            string markerScene = scene.name + "|" + _settings.MarkerSource + "|" + projection +
                                 "|" + detailCount;
            if (_markersBuiltForScene != markerScene)
            {
                // Rebuilding is expensive - hundreds of objects - and the entry count can wobble by a
                // few as the region streams, so a build is only repeated after the count has settled
                // for a moment, or when it has grown enough to matter.
                if (_markersBuiltForScene.EndsWith("|" + detailCount, StringComparison.Ordinal))
                {
                    // Same count as last time: nothing to do.
                }
                else if (DateTime.UtcNow < _nextMarkerRebuildUtc)
                {
                    // Too soon; leave the current set in place and re-check on a later frame.
                }
                else
                {
                    _nextMarkerRebuildUtc = DateTime.UtcNow.AddSeconds(2);
                    BuildMarkersAndLabels(scene.name, markerScene);
                }
            }
        }

        TryRefreshVanillaIcons();

        if (preferCommunity && !active.TextureReady && active.Definition != null && playerReady &&
            DateTime.UtcNow >= _loadAfterUtc)
        {
            if (LoadCurrentMapIntoUnityUi(active))
                _loadAfterUtc = DateTime.MaxValue;
            else
                _loadAfterUtc = DateTime.UtcNow.AddSeconds(5);
        }

        // Warm the layer that is NOT on screen once the one on screen is done. Without this the
        // other layer decodes on the frame its view is first opened, which is a visible freeze:
        // opening the full map showed a blank map for about two seconds while 4360x4198 decoded.
        // One layer per frame, and only after the visible one is ready, so this can never delay
        // what the player is looking at.
        if (preferCommunity && playerReady && active.TextureReady && !_warmUpDone)
        {
            MapLayer other = _layers[ActiveLayerId == LayerMini ? LayerFull : LayerMini];
            if (other.TextureReady)
            {
                _warmUpDone = true;
            }
            else if (other.Definition != null && DateTime.UtcNow >= _warmUpAfterUtc &&
                     LoadCurrentMapIntoUnityUi(other))
            {
                _warmUpDone = true;
            }
        }

        // The temporary hide key is already folded into MiniMapVisible; testing it again here
        // would also hide the full map, which is exactly what the two layers were split to avoid.
        bool shouldShow = AnyLayerReady() && active.Definition != null && playerReady &&
                          (MiniMapVisible || FullMapVisible) && !vanillaMapOpen;
        SetUiVisible(shouldShow);
        if (!shouldShow)
            return;

        UpdateUnityUi(GameManager.GetPlayerTransform());
    }

    private void TryReloadCalibrations()
    {
        DateTime now = DateTime.UtcNow;
        if (now < _nextCalibrationCheckUtc)
            return;

        _nextCalibrationCheckUtc = now.AddSeconds(1);
        DateTime writeUtc = File.GetLastWriteTimeUtc(_calibrationPath);
        if (writeUtc == _calibrationLastWriteUtc)
            return;

        CalibrationStore.Load(_calibrationPath,
            message => LoggerInstance.Msg(message),
            message => LoggerInstance.Warning(message));
        _calibrationLastWriteUtc = writeUtc;
        LoggerInstance.Msg("Reloaded calibrations.json after file change.");
    }

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
        SetUiVisible(false);
        _observedSceneHandle = int.MinValue;
        LoggerInstance.Msg($"Scene initialized callback: {sceneName}.");
    }

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

    // Two independent layers. The corner map answers to its own setting, to the view cycle and to
    // the temporary hide key; the full map answers to the game's map key and to the view cycle.
    private bool MiniMapVisible => _miniMapOn && _settings.Enabled && !_temporarilyHidden;
    private bool FullMapVisible => _fullMapOn;

    private string DescribeView() => FullMapVisible ? "FullMap"
        : MiniMapVisible ? "MiniMap" : "None";

    private void ApplyViewState(bool mini, bool full)
    {
        // Opening resets it onto the player: a map that reopens wherever it was last dragged is
        // disorienting, and the player is the one thing on it that moved.
        if (full && !_fullMapOn)
        {
            _fullMapZoom = 1f;
            _fullMapCenterValid = false;
        }
        _miniMapOn = mini;
        _fullMapOn = full;
    }

    // Corner map -> full map -> nothing -> corner map.
    //
    // The next state is derived from the current one rather than from a stored index, because
    // the game's map key moves between the same states: an index would go stale and skip one.
    // Going to the full map deliberately leaves the corner map switched on underneath, so
    // closing the full map with the game key returns to the corner map instead of to nothing.
    private void CycleView()
    {
        if (_fullMapOn)
            ApplyViewState(false, false);
        else if (_miniMapOn)
            ApplyViewState(true, true);
        else
            ApplyViewState(_settings.Enabled, !_settings.Enabled);

        LoggerInstance.Msg($"View cycle: {DescribeView()}.");
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

    // The game's own map key lives in muscle memory, so the game's "open map" action is
    // intercepted and our full map is shown instead. That never touches Panel_Map: the action is
    // skipped, so the game's panel is not opened at all. Note this hooks the action rather than
    // a key, so a player who rebinds the map key is followed automatically.
    internal static bool TryRedirectGameMap()
    {
        ModEntry mod = s_instance;
        if (mod == null || !mod._settings.RedirectGameMap)
            return false;
        if (mod.ActiveLayer.Definition == null)
            return false;                 // no map for this scene; leave the game alone

        // The game fires this action twice for a single press - the log showed FullMap and
        // MiniMap in the same millisecond - so a plain toggle opened and closed our map at once
        // and nothing appeared to happen. Collapse the repeat.
        DateTime now = DateTime.UtcNow;
        if ((now - s_lastMapRedirectUtc).TotalMilliseconds < 120.0)
        {
            // Not a fault: the game raises this action twice for one press, and both the key
            // patch and the survey patch can arrive for a single survey.
            mod.LoggerInstance.Msg("Duplicate map action within 120 ms; collapsed into one.");
            return true;
        }
        s_lastMapRedirectUtc = now;

        if (mod._fullMapOn)
            mod.CloseFullMap();
        else
            mod.OpenFullMap();
        mod.LoggerInstance.Msg($"Map key: view is now {mod.DescribeView()}.");
        return true;
    }

    // Cartography forces a map open after a survey. It reaches Panel_Map directly instead of going
    // through InputManager, which is why patching the objective action did nothing for a whole
    // session; see InputPatches for the measurement that established the real path.
    //
    // Panel_Map.Enable(bool, bool) is NOT survey-specific, though - the inventory's map button
    // calls the same overload. Answering it unconditionally locked the built-in map away behind
    // the "suppress" and "our map" settings, so the setting only answers for a panel that opens
    // shortly after a survey. Everything else is the player deliberately asking for the game's
    // map, and is left alone.
    internal static bool HandleSurveyMapPopup(string source)
    {
        ModEntry mod = s_instance;
        if (mod == null)
            return false;

        double sinceSurvey = (DateTime.UtcNow - s_lastSurveyUtc).TotalSeconds;
        if (sinceSurvey > SurveyPopupWindowSeconds)
        {
            mod.LoggerInstance.Msg(
                $"Map panel opened from the UI ({source}); no survey in the last " +
                $"{SurveyPopupWindowSeconds:F0}s, so the game's map is left alone.");
            return false;
        }

        switch (mod._settings.SurveyPopup)
        {
            case 0:
                // Same de-duplication window as the key path, so one survey cannot both open the
                // game's panel and be collapsed into the open-map action.
                s_lastMapRedirectUtc = DateTime.UtcNow;
                mod.LoggerInstance.Msg($"Survey popup ({source}): handing the map to the game.");
                return false;
            case 2:
                s_lastMapRedirectUtc = DateTime.UtcNow;
                mod.LoggerInstance.Msg($"Survey popup ({source}): suppressed.");
                return true;
            default:
                // Deliberately not OpenFullMap: that defers to the map-key takeover setting, and
                // the survey answer is its own decision. Otherwise turning the takeover off would
                // leave this branch swallowing the panel and showing nothing at all.
                mod.ApplyViewState(mod._miniMapOn, true);
                mod.LoggerInstance.Msg($"Survey popup ({source}): showing our full map.");
                return true;
        }
    }

    private void LeaveFullMap()
    {
        s_suppressEscapeThroughFrame = Time.frameCount + 1;
        CloseFullMap();
        LoggerInstance.Msg("Full map closed with Escape.");
    }

    // The game locks the mouse and keeps player input live during play, so a map drawn by the
    // mod cannot be scrolled or dragged until both are handed over. The game's own panels do
    // this through the input context list and the cursor helper, so we use the same two calls
    // rather than inventing a mechanism.
    // Closing the full map goes through the game's own map action query rather than a key of ours.
    //
    // Opening is intercepted at ExecuteOpenMapAction, so it already follows whatever the player
    // bound the map to, including a rebind. Closing could not use that path: the input context we
    // push to stop the player walking around while reading the map also stops the game dispatching
    // the action, which is why this used to need a separate hardcoded key - one that ignored
    // rebinding and had to be kept in sync by hand.
    //
    // Measured 2026-09-28 with the map rebound to N: GetOpenMapPressed returned true on every
    // press of N while our map was open, and false for the old M. The query does work inside our
    // own input context, so the manual key is gone.
    private void PollOpenMapKey()
    {
        bool pressed;
        try
        {
            pressed = InputManager.GetOpenMapPressed(_backgroundImage);
        }
        catch (Exception ex)
        {
            if (DateTime.UtcNow >= _nextOpenMapKeyLogUtc)
            {
                _nextOpenMapKeyLogUtc = DateTime.UtcNow.AddSeconds(5);
                LoggerInstance.Warning($"Could not poll the game's map key: {ex.Message}");
            }
            return;
        }

        bool rising = pressed && !_openMapKeyHeld;
        _openMapKeyHeld = pressed;
        if (!rising)
            return;

        // The very press that opened the map also reads as pressed here, so this shares the
        // opening path's de-duplication window. Without that the map would close on the frame it
        // opened, which is exactly the failure this query could have caused.
        TryRedirectGameMap();
    }

    private void HandleFullMapInput()
    {
        if (!_settings.ReleaseMouseOnFullMap)
            return;

        // InputManager.ShowCursor(true) alone left the cursor locked, so drive the Unity
        // state directly as well and keep doing it: the game re-applies its own lock.
        if (Cursor.lockState != CursorLockMode.None)
            Cursor.lockState = CursorLockMode.None;
        if (!Cursor.visible)
            Cursor.visible = true;
        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.01f)
        {
            _fullMapZoom = Mathf.Clamp(_fullMapZoom * Mathf.Exp(wheel * 0.18f), 1f, 24f);
            _fullMapCenterValid = true;
        }

        Vector2 mouse = Input.mousePosition;
        if (Input.GetMouseButtonDown(0))
        {
            _fullMapDragging = true;
            _fullMapDragLast = mouse;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            _fullMapDragging = false;
        }

        if (!_fullMapDragging)
            return;

        Vector2 delta = mouse - _fullMapDragLast;
        _fullMapDragLast = mouse;
        if (delta.sqrMagnitude < 0.01f)
            return;

        // one widget pixel is span/size of the visible window in uv, and the window follows the
        // pointer, so dragging right reveals what is to the left
        Vector2 widget = _mapRect.rect.size;
        if (widget.x < 1f || widget.y < 1f)
            return;
        float span = Mathf.Clamp(1f / Mathf.Max(1f, _fullMapZoom), 0.05f, 1f);
        _fullMapCenter += new Vector2(-delta.x / widget.x * span, -delta.y / widget.y * span);
        _fullMapCenterValid = true;
    }

    // We invented the wheel-zoom and drag interactions, so the full map has to say so: nothing
    // in the game tells the player they exist. The label is built lazily and reports which font
    // it managed to find, because a font that does not exist in this Unity build would
    // otherwise fail silently and just leave the bar blank.
    private void UpdateFullMapHints(bool fullMap)
    {
        if (!fullMap || !_settings.ShowKeyHints)
        {
            if (!ReferenceEquals(_hintLabel, null) && _hintLabel.gameObject.activeSelf)
                _hintLabel.gameObject.SetActive(false);
            return;
        }

        EnsureHintLabel();
        if (ReferenceEquals(_hintLabel, null))
            return;

        if (!_hintLabel.gameObject.activeSelf)
        {
            _hintLabel.gameObject.SetActive(true);
            _hintLabel.transform.SetAsLastSibling();
        }

        RectTransform rect = _hintLabel.rectTransform;
        float width = Mathf.Max(320f, Screen.width - 64f);
        if (!Mathf.Approximately(rect.sizeDelta.x, width))
            rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);

        string text = BuildHintText();
        if (!string.Equals(_hintLabel.text, text, StringComparison.Ordinal))
            _hintLabel.text = text;
    }

    private string BuildHintText()
    {
        string text = _settings.ReleaseMouseOnFullMap
            ? "滚轮 缩放      左键拖动 平移      "
            : "";
        if (_settings.EnableCycleKey && _settings.CycleViewKey != KeyCode.None)
            text += $"{_settings.CycleViewKey} 切换视图      ";
        return $"{text}地图键 / Esc 关闭";
    }

    private void EnsureHintLabel()
    {
        if (!ReferenceEquals(_hintLabel, null) || !_settings.ShowKeyHints)
            return;

        Font font = ResolveHintFont();
        if (ReferenceEquals(font, null))
            return;

        GameObject hintObject = CreateUiObject("KeyHints",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        hintObject.transform.SetParent(_uiRoot.transform, false);
        RectTransform rect = hintObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 14f);
        rect.sizeDelta = new Vector2(Mathf.Max(320f, Screen.width - 64f), 30f);

        _hintLabel = hintObject.GetComponent<Text>();
        _hintLabel.font = font;
        _hintLabel.fontSize = 17;
        _hintLabel.alignment = TextAnchor.MiddleCenter;
        _hintLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        _hintLabel.verticalOverflow = VerticalWrapMode.Overflow;
        _hintLabel.color = new Color(0.88f, 0.91f, 0.95f, 0.92f);
        _hintLabel.raycastTarget = false;
        _hintLabel.text = BuildHintText();
        LoggerInstance.Msg($"Key hint bar created: \"{_hintLabel.text}\".");
    }

    private Font ResolveHintFont()
    {
        if (!ReferenceEquals(_hintFont, null))
            return _hintFont;

        // The bar's text is Chinese, and Unity's built-in font has no CJK glyphs on most
        // builds, so try an OS font first. CreateDynamicFontFromOSFont falls back to a default
        // family rather than failing, so the first name that exists on the machine wins.
        string[] osNames = { "Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "PingFang SC", "Arial" };
        foreach (string name in osNames)
        {
            try
            {
                Font font = Font.CreateDynamicFontFromOSFont(name, 17);
                if (!ReferenceEquals(font, null))
                {
                    _hintFont = font;
                    LoggerInstance.Msg($"Key hint font: OS font \"{name}\".");
                    return _hintFont;
                }
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning($"OS font \"{name}\" unavailable: {ex.Message}");
            }
        }

        // LegacyRuntime.ttf is the built-in font's name from Unity 2022.2 on; Arial.ttf is what
        // it was called before. Try both rather than assume which build this is.
        string[] builtInNames = { "LegacyRuntime.ttf", "Arial.ttf" };
        foreach (string name in builtInNames)
        {
            try
            {
                Font font = Resources.GetBuiltinResource<Font>(name);
                if (!ReferenceEquals(font, null))
                {
                    _hintFont = font;
                    LoggerInstance.Msg($"Key hint font: built-in \"{name}\".");
                    return _hintFont;
                }
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning($"Built-in font \"{name}\" unavailable: {ex.Message}");
            }
        }

        LoggerInstance.Warning(
            "Key hint bar skipped: no OS or built-in font could be resolved. The map still works.");
        return null;
    }

    private void ApplyMapInputContext()
    {        if (!_settings.ReleaseMouseOnFullMap)
            return;
        try
        {
            InputManager.PushContext(_backgroundImage);
            InputManager.ShowCursor(true);
            _mapContextPushed = true;
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Could not take over input for the full map: {ex.Message}");
        }
    }

    private void ReleaseMapInputContext()
    {
        if (!_mapContextPushed)
            return;
        _mapContextPushed = false;
        try
        {
            InputManager.ShowCursor(false);
            InputManager.PopContext(_backgroundImage);
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Could not hand input back: {ex.Message}");
        }
    }

    internal static bool ShouldSuppressGameEscape()
    {
        return Input.GetKeyDown(KeyCode.Escape) &&
               (s_fullMapActive || Time.frameCount <= s_suppressEscapeThroughFrame);
    }

    // Swaps a layer's texture out and leaves the old one alive until the frame is over. The
    // shared RawImage keeps pointing at the old texture until UpdateUnityUi rebinds, so destroying
    // it here draws a blank white rectangle for as long as it takes the new one to arrive.
    private void RetireTexture(MapLayer layer)
    {
        if (ReferenceEquals(layer.Texture, null))
            return;
        _retiredTextures ??= new List<Texture2D>();
        _retiredTextures.Add(layer.Texture);
    }

    private void SweepRetiredTextures()
    {
        if (_retiredTextures == null || _retiredTextures.Count == 0)
            return;
        for (int i = 0; i < _retiredTextures.Count; i++)
        {
            Texture2D texture = _retiredTextures[i];
            if (!ReferenceEquals(texture, null))
                UnityEngine.Object.Destroy(texture);
        }
        _retiredTextures.Clear();
    }

    private bool LoadCurrentMapIntoUnityUi(MapLayer layer)
    {
        string mapPath = Path.Combine(_mapsDirectory, layer.Definition.FileName);
        if (!File.Exists(mapPath))
        {
            LoggerInstance.Warning($"Map image not found: {mapPath}");
            return false;
        }

        try
        {
            EnsureUnityUi();
            byte[] bytes = File.ReadAllBytes(mapPath);
            var il2CppBytes = new Il2CppStructArray<byte>(bytes);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, il2CppBytes, true))
                throw new InvalidOperationException("Unity ImageConversion.LoadImage returned false.");

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.hideFlags = HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
            UnityEngine.Object.DontDestroyOnLoad(texture);

            // Retire the old texture only once the UI has stopped pointing at it. Destroying it
            // here would be a frame too early whenever this layer is the one on screen: the shared
            // RawImage still references the old texture until UpdateUnityUi rebinds, and a widget
            // pointing at a destroyed texture draws as a blank white rectangle. That is the
            // two-second white full map, and it only shows up on a RELOAD of the visible layer.
            RetireTexture(layer);
            layer.Texture = texture;
            layer.LoadedMapId = layer.Definition.Id;
            layer.TextureReady = true;

            // Do not bind this texture to the shared UI here: it belongs to the layer, and that
            // layer may not be the one on screen. UpdateUnityUi binds whichever layer is active.
            LoggerInstance.Msg(
                $"Loaded {layer.Definition.DisplayName} for layer " +
                $"'{LayerName(layer == _layers[LayerMini] ? LayerMini : LayerFull)}': " +
                $"{texture.width}x{texture.height}.");
            GC.KeepAlive(texture);
            return true;
        }
        catch (Exception ex)
        {
            LoggerInstance.Error($"Failed loading map into Unity UI: {ex}");
            return false;
        }
    }

    private void EnsureUnityUi()
    {
        if (!ReferenceEquals(_uiRoot, null))
            return;

        _uiRoot = CreateUiObject("CommunityMinimapCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        UnityEngine.Object.DontDestroyOnLoad(_uiRoot);
        Canvas canvas = _uiRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2000;
        CanvasScaler scaler = _uiRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        _backgroundObject = CreateUiObject("FullMapBackground",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        _backgroundObject.transform.SetParent(_uiRoot.transform, false);
        RectTransform backgroundRect = _backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        _backgroundImage = _backgroundObject.GetComponent<Image>();
        _backgroundImage.color = new Color(0.015f, 0.025f, 0.035f,
            _settings.FullMapBackgroundOpacity);
        _backgroundImage.raycastTarget = false;

        GameObject mapObject = CreateUiObject("Map",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage),
            typeof(RectMask2D));
        mapObject.transform.SetParent(_uiRoot.transform, false);
        _mapRect = mapObject.GetComponent<RectTransform>();
        _mapImage = mapObject.GetComponent<RawImage>();
        _mapImage.raycastTarget = false;

        _markerRoot = CreateUiObject("PlayerPointer",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        _markerRoot.transform.SetParent(mapObject.transform, false);
        _markerRect = _markerRoot.GetComponent<RectTransform>();
        _markerRect.anchorMin = new Vector2(0.5f, 0.5f);
        _markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        _markerRect.pivot = new Vector2(0.5f, 0.5f);
        _markerImage = _markerRoot.GetComponent<RawImage>();
        _markerImage.raycastTarget = false;
        _markerImage.color = Color.white;
        for (int i = 0; i < _markerTextures.Length; i++)
            _markerTextures[i] = CreatePointerTexture(i);
        ApplyPointerPalette();

        _backgroundObject.SetActive(false);
        _uiRoot.SetActive(false);
        LoggerInstance.Msg("Created persistent two-mode Canvas/RawImage map UI.");
    }

    private static GameObject CreateUiObject(string name, params Type[] componentTypes)
    {
        var il2CppTypes = new Il2CppReferenceArray<Il2CppSystem.Type>(componentTypes.Length);
        for (int i = 0; i < componentTypes.Length; i++)
            il2CppTypes[i] = Il2CppType.From(componentTypes[i]);
        return new GameObject(name, il2CppTypes);
    }

    private static Texture2D CreatePointerTexture(int paletteIndex)
    {
        const int size = 128;
        Color accent = GetPointerAccent(paletteIndex);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Color accumulated = Color.clear;
                for (int sy = 0; sy < 2; sy++)
                {
                    for (int sx = 0; sx < 2; sx++)
                    {
                        Vector2 point = new(x + (sx + 0.5f) * 0.5f,
                            y + (sy + 0.5f) * 0.5f);
                        accumulated += SamplePointer(point, accent) * 0.25f;
                    }
                }
                pixels[y * size + x] = accumulated;
            }
        }

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.SetPixels32(new Il2CppStructArray<Color32>(pixels));
        texture.Apply(false, true);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.hideFlags = HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
        UnityEngine.Object.DontDestroyOnLoad(texture);
        return texture;
    }

    private static Color SamplePointer(Vector2 point, Color accent)
    {
        Vector2 center = new(64f, 42f);
        Color result = Color.clear;

        if (PointInTriangle(point, new Vector2(67f, 115f),
                new Vector2(52f, 43f), new Vector2(80f, 43f)))
            result = AlphaOver(result, new Color(0.02f, 0.025f, 0.03f, 0.58f));

        float distance = Vector2.Distance(point, center);
        if (distance >= 22f && distance <= 31f)
            result = AlphaOver(result, new Color(0.02f, 0.025f, 0.03f, 0.78f));
        if (distance >= 24.5f && distance <= 28.5f)
            result = AlphaOver(result, new Color(accent.r, accent.g, accent.b, 0.88f));

        if (PointInTriangle(point, new Vector2(64f, 112f),
                new Vector2(54f, 43f), new Vector2(74f, 43f)))
            result = AlphaOver(result, new Color(accent.r, accent.g, accent.b, 0.96f));

        if (distance <= 8f)
            result = AlphaOver(result, new Color(0.02f, 0.025f, 0.03f, 0.90f));
        if (distance <= 4.5f)
            result = AlphaOver(result, new Color(0.93f, 0.88f, 0.76f, 0.98f));
        return result;
    }

    private static Color GetPointerAccent(int paletteIndex)
    {
        return paletteIndex switch
        {
            1 => new Color(0.76f, 0.25f, 0.29f, 1f),
            2 => new Color(0.72f, 0.52f, 0.91f, 1f),
            3 => new Color(0.21f, 0.74f, 0.72f, 1f),
            4 => new Color(0.85f, 0.66f, 0.24f, 1f),
            5 => new Color(0.94f, 0.075f, 0.05f, 1f),
            _ => new Color(0.88f, 0.35f, 0.28f, 1f)
        };
    }

    private static bool PointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Sign(point, a, b);
        float d2 = Sign(point, b, c);
        float d3 = Sign(point, c, a);
        bool hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
        bool hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(hasNegative && hasPositive);
    }

    private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3) =>
        (p1.x - p3.x) * (p2.y - p3.y) -
        (p2.x - p3.x) * (p1.y - p3.y);

    private static Color AlphaOver(Color background, Color foreground)
    {
        float alpha = foreground.a + background.a * (1f - foreground.a);
        if (alpha <= 0f)
            return Color.clear;
        return new Color(
            (foreground.r * foreground.a + background.r * background.a * (1f - foreground.a)) / alpha,
            (foreground.g * foreground.a + background.g * background.a * (1f - foreground.a)) / alpha,
            (foreground.b * foreground.a + background.b * background.a * (1f - foreground.a)) / alpha,
            alpha);
    }

    private void SetUiVisible(bool visible)
    {
        if (ReferenceEquals(_uiRoot, null) || _uiVisible == visible)
            return;
        _uiVisible = visible;
        _uiRoot.SetActive(visible);
    }

    private void UpdateUnityUi(Transform player)
    {
        bool fullMap = FullMapVisible;
        // The single UI object carries whichever layer is on screen, so the binding happens here
        // rather than at load time: loading a layer must not steal the object from the other one.
        _mapImage.texture = ActiveLayer.Texture;
        // Re-enabled every frame; the corner map switches it off below while it has no projection.
        _mapImage.enabled = true;
        UpdateFullMapHints(fullMap);
        _backgroundObject.SetActive(fullMap);
        _backgroundImage.color = new Color(0.015f, 0.025f, 0.035f,
            _settings.FullMapBackgroundOpacity);
        _mapImage.color = new Color(1f, 1f, 1f, fullMap ? 1f : _settings.Opacity);

        Vector2 mapSize = fullMap ? ApplyFullMapLayout() : ApplyMiniMapLayout();

        bool hasPosition = TryPlayerToMapUv(player.position, out Vector2 uv);
        if (!hasPosition && !fullMap)
        {
            // The corner map centres on the player, so until there is a projection it has nothing
            // to show. Drawing the whole image here was a visible glitch on every scene change: for
            // the moment before the new region's calibration or base map is ready, the corner map
            // flashed the entire region and then snapped to the player. Hide the image instead and
            // restore it as soon as a projection exists.
            //
            // The full map is deliberately not hidden: its zoom and pan are plain uv maths, so it
            // works on an uncalibrated region, and hiding it was what made scrolling do nothing.
            _mapImage.enabled = false;
            _markerRoot.SetActive(false);
            return;
        }


        Rect visibleUv;
        if (fullMap)
        {
            // The full map starts centred on the player and can be zoomed and dragged, so it
            // shares the visible-window maths with the corner map instead of always showing
            // the whole image.
            float span = Mathf.Clamp(1f / Mathf.Max(1f, _fullMapZoom), 0.05f, 1f);
            float half = span * 0.5f;
            if (!_fullMapCenterValid)
            {
                _fullMapCenter = hasPosition ? uv : new Vector2(0.5f, 0.5f);
                _fullMapCenterValid = true;
            }
            _fullMapCenter = new Vector2(
                Mathf.Clamp(_fullMapCenter.x, half, 1f - half),
                Mathf.Clamp(_fullMapCenter.y, half, 1f - half));
            visibleUv = new Rect(_fullMapCenter.x - half, _fullMapCenter.y - half, span, span);
            _mapImage.uvRect = visibleUv;
        }
        else
        {
            float span = 1f / _settings.Zoom;
            float half = span * 0.5f;
            float centerU = Mathf.Clamp(uv.x, half, 1f - half);
            float centerV = Mathf.Clamp(uv.y, half, 1f - half);
            visibleUv = new Rect(centerU - half, centerV - half, span, span);
            _mapImage.uvRect = visibleUv;
        }

        UpdateVanillaIcons(visibleUv, mapSize);
        UpdateMapLabels(visibleUv, mapSize);

        // The markers live in the texture's own uv space, so they are still worth drawing on an
        // uncalibrated map. Only the player pointer needs a projection.
        if (!hasPosition)
        {
            _markerRoot.SetActive(false);
            return;
        }

        bool markerVisible = uv.x >= visibleUv.xMin && uv.x <= visibleUv.xMax &&
                             uv.y >= visibleUv.yMin && uv.y <= visibleUv.yMax;
        if (!markerVisible)
        {
            _markerRoot.SetActive(false);
            return;
        }

        _markerRect.anchoredPosition = new Vector2(
            ((uv.x - visibleUv.x) / visibleUv.width - 0.5f) * mapSize.x,
            ((uv.y - visibleUv.y) / visibleUv.height - 0.5f) * mapSize.y);
        float markerSize = Mathf.Max(44f, _settings.MarkerSize) * (fullMap ? 1.15f : 1f);
        _markerRect.sizeDelta = new Vector2(markerSize, markerSize);
        ApplyPointerPalette();

        TryPlayerToMapUv(player.position + player.forward * 2f, out Vector2 aheadUv);
        float angle = Mathf.Atan2(aheadUv.x - uv.x, aheadUv.y - uv.y) * Mathf.Rad2Deg;
        _markerRect.localEulerAngles = new Vector3(0f, 0f, -angle);
        _markerRoot.SetActive(true);
    }

    private void UpdateVanillaIcons(Rect visibleUv, Vector2 mapSize)
    {
        // Markers used to be drawn only while the vanilla source was active, and that was the whole
        // of the reason they could not appear on the community map. The data does not care which
        // texture is behind it: every marker has already been converted to this layer's texture uv
        // by the same projection the player pointer uses. Drawing them on the community map is
        // therefore a switch, not a feature - and it is the only path whose projection has been
        // fitted against real landmarks.
        bool show = ActiveLayer.UsingVanilla || _settings.MarkersOnCommunityMap;

        bool categoriesChanged = _markerCategoryState != _settings.MarkerCategoryState();
        if (categoriesChanged)
            _markerCategoryState = _settings.MarkerCategoryState();

        for (int i = 0; i < _vanillaIcons.Count; i++)
        {
            VanillaIcon icon = _vanillaIcons[i];
            // The category switch is read once when it changes rather than every frame: a marker
            // set can run to hundreds, and this loop already runs every frame.
            if (categoriesChanged)
                icon.CategoryEnabled = CategoryEnabled(icon.Category);

            bool visible = show && icon.CategoryEnabled &&
                           icon.MapUv.x + icon.MapUvSize.x * 0.5f >= visibleUv.xMin &&
                           icon.MapUv.x - icon.MapUvSize.x * 0.5f <= visibleUv.xMax &&
                           icon.MapUv.y + icon.MapUvSize.y * 0.5f >= visibleUv.yMin &&
                           icon.MapUv.y - icon.MapUvSize.y * 0.5f <= visibleUv.yMax;
            icon.Root.SetActive(visible);
            if (!visible)
                continue;

            icon.Rect.anchoredPosition = new Vector2(
                ((icon.MapUv.x - visibleUv.x) / visibleUv.width - 0.5f) * mapSize.x,
                ((icon.MapUv.y - visibleUv.y) / visibleUv.height - 0.5f) * mapSize.y);
            float markerScale = _settings.MarkerIconSize / Mathf.Max(1e-6f, _vanillaIconMaxUv);
            float ratioX = Mathf.Clamp(icon.MapUvSize.x * markerScale / _settings.MarkerIconSize,
                0.6f, 1.8f);
            float ratioY = Mathf.Clamp(icon.MapUvSize.y * markerScale / _settings.MarkerIconSize,
                0.6f, 1.8f);
            icon.Rect.sizeDelta = new Vector2(
                ratioX * _settings.MarkerIconSize,
                ratioY * _settings.MarkerIconSize);
        }
    }

    // The one place a panel map position becomes a texture uv. Both the live conversion below and
    // the framing probe go through it, because a probe that re-derives the maths it is supposed to
    // be checking measures a copy that can drift away from the real path without anything saying
    // so. It is also the single point the layer split has to make layer-aware.
    private static Vector2 VanillaMapPositionToUv(Vector3 mapPosition, Rect localBounds, Rect textureUv)
    {
        float localU = Mathf.InverseLerp(localBounds.xMin, localBounds.xMax, mapPosition.x);
        float localV = Mathf.InverseLerp(localBounds.yMin, localBounds.yMax, mapPosition.y);
        return new Vector2(
            textureUv.x + localU * textureUv.width,
            textureUv.y + localV * textureUv.height);
    }

    // Markers and the player pointer share one conversion, deliberately: a marker whose position is
    // computed differently from the pointer's would drift against it. In the vanilla layer the
    // texture uv is the region's real uvRect, so a marker and the pointer go through identical
    // maths; in the community layer the affine calibration serves both.
    private bool TryWorldToMarkerUv(Vector3 worldPosition, out Vector2 uv) =>
        TryPlayerToMapUv(worldPosition, out uv);

    private bool TryPlayerToMapUv(Vector3 worldPosition, out Vector2 uv)
    {
        uv = default;
        MapLayer layer = ActiveLayer;
        if (!layer.UsingVanilla)
            return layer.Definition != null &&
                   layer.Definition.TryWorldToMap(worldPosition, out uv);

        try
        {
            Panel_Map panel = InterfaceManager.GetPanel<Panel_Map>();
            if (panel == null || ReferenceEquals(layer.Texture, null))
                return false;
            Vector3 mapPosition = panel.WorldPositionToMapPosition(
                layer.VanillaProjectionScene, worldPosition);
            uv = VanillaMapPositionToUv(mapPosition, layer.VanillaMapLocalBounds, layer.VanillaTextureUv);
            return true;
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Vanilla player projection failed: {ex.Message}");
            return false;
        }
    }

    private void ApplyPointerPalette()
    {
        int index = Mathf.Clamp(_settings.PointerPalette, 0, _markerTextures.Length - 1);
        _markerImage.texture = _markerTextures[index];
    }

    private Vector2 ApplyMiniMapLayout()
    {
        float maxDimension = Mathf.Max(180f,
            Mathf.Min(Screen.width, Screen.height) * _settings.MiniMapSizePercent / 100f);
        float aspect = GetTextureAspect();
        Vector2 size = aspect >= 1f
            ? new Vector2(maxDimension, maxDimension / aspect)
            : new Vector2(maxDimension * aspect, maxDimension);

        Vector2 anchor;
        Vector2 offset;
        float margin = _settings.Margin;
        switch (_settings.HudPosition)
        {
            case 1: anchor = new Vector2(0f, 1f); offset = new Vector2(margin, -margin); break;
            case 2: anchor = new Vector2(1f, 0f); offset = new Vector2(-margin, margin); break;
            case 3: anchor = new Vector2(0f, 0f); offset = new Vector2(margin, margin); break;
            default: anchor = new Vector2(1f, 1f); offset = new Vector2(-margin, -margin); break;
        }

        if (_settings.HudPosition == 4)
        {
            // Custom placement: the percentages describe where the centre of the map goes, which
            // is easier to reason about than a corner plus an offset when the goal is "get out of
            // the way of that HUD element".
            anchor = new Vector2(0f, 0f);
            offset = new Vector2(
                Screen.width * Mathf.Clamp(_settings.MiniMapPositionX, 0, 100) / 100f,
                Screen.height * Mathf.Clamp(_settings.MiniMapPositionY, 0, 100) / 100f);
            _mapRect.anchorMin = anchor;
            _mapRect.anchorMax = anchor;
            _mapRect.pivot = new Vector2(0.5f, 0.5f);
            _mapRect.anchoredPosition = offset;
            _mapRect.sizeDelta = size;
            return size;
        }

        _mapRect.anchorMin = anchor;
        _mapRect.anchorMax = anchor;
        _mapRect.pivot = anchor;
        _mapRect.anchoredPosition = offset;
        _mapRect.sizeDelta = size;
        return size;
    }

    private Vector2 ApplyFullMapLayout()
    {
        const float margin = 32f;
        float availableWidth = Mathf.Max(100f, Screen.width - margin * 2f);
        float availableHeight = Mathf.Max(100f, Screen.height - margin * 2f);
        float aspect = GetTextureAspect();
        float width = availableWidth;
        float height = width / aspect;
        if (height > availableHeight)
        {
            height = availableHeight;
            width = height * aspect;
        }

        Vector2 size = new(width, height);
        Vector2 center = new(0.5f, 0.5f);
        _mapRect.anchorMin = center;
        _mapRect.anchorMax = center;
        _mapRect.pivot = center;
        _mapRect.anchoredPosition = Vector2.zero;
        _mapRect.sizeDelta = size;
        return size;
    }

    private float GetTextureAspect()
    {
        Texture2D texture = ActiveLayer.Texture;
        if (ReferenceEquals(texture, null) || texture.height <= 0)
            return 1f;
        return (float)texture.width / texture.height;
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

    private bool CaptureVanillaMap(Panel_Map panel, string sceneName)
    {
        try
        {
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

    private void CaptureVanillaIcons(Transform mapElements, bool keepExistingWhenEmpty = false,
        bool allowInactive = false)
    {
        // The HUD objects must exist before icons are parented to them.
        EnsureUnityUi();
        _pendingVanillaIcons.Clear();
        string[] containerNames =
        {
            "ActiveElementsBigSprite",
            "ActiveElementsSmallSprite",
            "ActiveElementsDetailEntry"
        };
        for (int i = 0; i < containerNames.Length; i++)
        {
            Transform container = FindChildByName(mapElements, containerNames[i]);
            if (container != null)
                CaptureVanillaIconsRecursive(container, mapElements, allowInactive);
        }

        // With the panel closed the sprite tree may report nothing visible even though
        // the markers still exist. Keep what we have rather than blanking the HUD.
        if (_pendingVanillaIcons.Count == 0 && keepExistingWhenEmpty)
            return;

        // A capture smaller than what we already hold is the game's fog, not markers going away:
        // RefreshIconVisibility switches off the sprite's enabled flag outside the surveyed area,
        // and CaptureVanillaIconsRecursive drops anything with a zero alpha. The game never
        // removes a marker at all, so replacing the set would silently shrink the HUD to the lit
        // area - the log showed exactly that, 162 markers dropping to 28 once the panel had been
        // opened. Keeping the larger set costs nothing except stale markers we already had.
        //
        // An empty existing set is exempt: there is nothing to protect, and refusing the capture
        // is what left the HUD with no markers at all after a scene change.
        if (keepExistingWhenEmpty && _vanillaIcons.Count > 0 &&
            _pendingVanillaIcons.Count < _vanillaIcons.Count)
        {
            LoggerInstance.Msg(
                $"Kept {_vanillaIcons.Count} markers: the re-capture returned only " +
                $"{_pendingVanillaIcons.Count}, which is the game's fog rather than markers leaving.");
            return;
        }

        ClearVanillaIcons();
        _vanillaIcons.AddRange(_pendingVanillaIcons);
        _pendingVanillaIcons.Clear();

        // Remember the largest marker so each one can be drawn at a constant on-screen size
        // that keeps the set's proportions. Sizing them as a fraction of the visible span
        // instead made them scale with the zoom, so the same marker looked several times
        // bigger on the zoomed corner map than on the full map.
                // Normalise against the median icon rather than the largest. A single oversized
        // element (a label or an area blob that slipped past the chrome filter) would
        // otherwise set the scale and shrink every real marker to a pixel or two.
        _vanillaIconMaxUv = 0f;
        if (_vanillaIcons.Count > 0)
        {
            var sizes = new List<float>(_vanillaIcons.Count);
            for (int i = 0; i < _vanillaIcons.Count; i++)
            {
                Vector2 size = _vanillaIcons[i].MapUvSize;
                sizes.Add(Mathf.Max(size.x, size.y));
            }
            sizes.Sort();
            _vanillaIconMaxUv = sizes[sizes.Count / 2];
        }
        if (_vanillaIconMaxUv <= 1e-5f)
            _vanillaIconMaxUv = 1f;
        _markerRoot.transform.SetAsLastSibling();
        LoggerInstance.Msg(
            $"Captured {_vanillaIcons.Count} vanilla map marker layers " +
            $"({(allowInactive ? "panel-free" : "panel-open")}).");
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
        if (!_settings.CleanHarvestedMarkers)
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

    private static bool IsMapIconChrome(string name) =>
        string.Equals(name, "HoverWidget", StringComparison.Ordinal) ||
        string.Equals(name, "Label", StringComparison.Ordinal) ||
        string.Equals(name, "LabelBG", StringComparison.Ordinal) ||
        string.Equals(name, "highlight", StringComparison.Ordinal);

    // NGUI only fills drawingUVs while a widget is actually being drawn. With the map panel    // closed nothing is drawn, so rebuild the atlas rectangle from the sprite data instead.
    private static bool TryGetSpriteUv(UISprite sprite, out Vector4 uv)
    {
        uv = default;
        try
        {
            Vector4 drawn = sprite.drawingUVs;
            if (drawn.z > drawn.x && drawn.w > drawn.y)
            {
                uv = drawn;
                return true;
            }
        }
        catch
        {
            // fall through to the atlas lookup
        }

        try
        {
            if (sprite.atlas == null)
                return false;
            var data = sprite.atlas.GetSprite(sprite.spriteName);
            Texture texture = sprite.atlas.texture;
            if (data == null || texture == null || texture.width <= 0 || texture.height <= 0)
                return false;

            uv = new Vector4(
                (float)data.x / texture.width,
                1f - (float)(data.y + data.height) / texture.height,
                (float)(data.x + data.width) / texture.width,
                1f - (float)data.y / texture.height);
            return uv.z > uv.x && uv.w > uv.y;
        }
        catch
        {
            return false;
        }
    }

    // The texture and atlas rectangle behind a sprite name. The scraped table only ever holds the
    // handful of sprites the game happened to instantiate, so the atlas is the real source: section
    // 25.3 measured 205 from the table plus 597 through the atlas for 802 of 802, and the 22:08
    // session reproduced exactly that.
    private bool TryResolveIcon(string spriteName, out IconRef icon)
    {
        icon = null;
        if (string.IsNullOrEmpty(spriteName))
            return false;

        if (_iconBySpriteName.TryGetValue(spriteName, out IconRef known) &&
            !ReferenceEquals(known.Texture, null))
        {
            icon = known;
            return true;
        }

        if (ReferenceEquals(_mapIconAtlas, null))
            return false;
        try
        {
            UISpriteData data = _mapIconAtlas.GetSprite(spriteName);
            if (data == null)
                return false;
            Texture texture = _mapIconAtlas.texture;
            if (ReferenceEquals(texture, null))
                return false;

            var resolved = new IconRef
            {
                Texture = texture,
                Uv = new Rect(data.x, data.y, data.width, data.height),
                PixelWidth = data.width,
                PixelHeight = data.height,
            };
            // Cached so a rebuild does not walk the atlas once per marker.
            _iconBySpriteName[spriteName] = resolved;
            icon = resolved;
            return true;
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Atlas lookup failed for '{spriteName}': {ex.Message}");
            return false;
        }
    }

    // Builds one marker from data rather than from a UI widget. The scraped path derives position
    // and size from the sprite's world corners; this one is handed both, which is the point of the
    // rewrite: position comes from GetWorldPosition, the same coordinate system the calibration
    // uses, instead of from where the game happened to lay a widget out.
    private VanillaIcon BuildMarkerIcon(IconRef icon, Vector2 mapUv, Vector2 uvSize, Color color)
    {
        GameObject iconObject = CreateUiObject("VanillaMapIcon",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        iconObject.transform.SetParent(_mapRect, false);
        RectTransform rect = iconObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        RawImage image = iconObject.GetComponent<RawImage>();
        image.texture = icon.Texture;
        image.uvRect = icon.Uv;
        image.color = color;
        image.raycastTarget = false;
        return new VanillaIcon
        {
            Root = iconObject,
            Rect = rect,
            MapUv = mapUv,
            MapUvSize = uvSize,
        };
    }

    // Draws every marker the game has registered for this region, from MapDetail data.
    //
    // This is the rewrite section 37.2 describes. The panel only instantiates 162 of the region's
    // 802 registered markers - measured, not assumed - so scraping could never show more than that
    // however well it worked. Reading s_MapDetails shows all of them, and the icon for each comes
    // from the atlas, which resolves every one of the 802.
    //
    // Visibility is NOT decided here (section 35.3): everything registered is built, and whether it
    // is drawn is settled per frame in UpdateVanillaIcons against the live category switches. That
    // is also the groundwork the fog mode needs.
    private void RebuildMarkersFromMapDetails()
    {
        EnsureUnityUi();
        _pendingVanillaIcons.Clear();
        _mapDetailSkipped = 0;
        _mapDetailUnresolved = 0;

        try
        {
            var details = MapDetailManager.s_MapDetails;
            if (ReferenceEquals(details, null))
            {
                LoggerInstance.Warning("Marker build: s_MapDetails is null.");
                return;
            }

            for (int i = 0; i < details.Count; i++)
            {
                MapDetail detail = details[i];
                if (ReferenceEquals(detail, null))
                    continue;

                // Labels and area blobs carry no sprite name (section 37.2 point 3).
                string spriteName = detail.m_SpriteName;
                if (string.IsNullOrEmpty(spriteName))
                {
                    _mapDetailSkipped++;
                    continue;
                }

                // One monster resolves the texture; the atlas covers the rest.
                if (!TryResolveIcon(spriteName, out IconRef icon))
                {
                    _mapDetailUnresolved++;
                    continue;
                }

                Vector2 mapUv;
                try
                {
                    if (!TryWorldToMarkerUv(detail.GetWorldPosition(), out mapUv))
                    {
                        _mapDetailSkipped++;
                        continue;
                    }
                }
                catch
                {
                    _mapDetailSkipped++;
                    continue;
                }

                // Markers are drawn at a constant on-screen size that keeps the set's proportions,
                // exactly as the scraped path does; the icon's own pixel size only sets the ratio.
                float iconUv = icon.Texture.width > 0
                    ? Mathf.Max(icon.Uv.width, icon.Uv.height) / icon.Texture.width
                    : 0.05f;
                var uvSize = new Vector2(iconUv, iconUv);

                MarkerCategory category = CategorizeSprite(spriteName);
                VanillaIcon built = BuildMarkerIcon(icon, mapUv, uvSize, Color.white);
                built.Category = category;
                built.CategoryEnabled = CategoryEnabled(category);
                _pendingVanillaIcons.Add(built);
            }

            ClearVanillaIcons();
            _vanillaIcons.AddRange(_pendingVanillaIcons);
            _pendingVanillaIcons.Clear();

            // The same median normalisation the scraped path uses, so a deliberately oversized icon
            // cannot set the scale for everything else.
            _vanillaIconMaxUv = 0f;
            if (_vanillaIcons.Count > 0)
            {
                var sizes = new List<float>(_vanillaIcons.Count);
                for (int i = 0; i < _vanillaIcons.Count; i++)
                {
                    Vector2 size = _vanillaIcons[i].MapUvSize;
                    sizes.Add(Mathf.Max(size.x, size.y));
                }
                sizes.Sort();
                _vanillaIconMaxUv = sizes[sizes.Count / 2];
            }
            if (_vanillaIconMaxUv <= 1e-5f)
                _vanillaIconMaxUv = 1f;
            _markerRoot.transform.SetAsLastSibling();

            LoggerInstance.Msg(
                $"Markers rebuilt from MapDetail: {_vanillaIcons.Count} drawn, " +
                $"{_mapDetailSkipped} skipped (labels, off-map), " +
                $"{_mapDetailUnresolved} unresolved.");
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Marker rebuild from MapDetail failed: {ex.Message}");
        }
    }

    private void CaptureVanillaIconsRecursive(Transform transform, Transform mapElements,
        bool allowInactive)
    {
        // With the panel closed, NGUI's own visibility test is unavailable, so the hover label
        // chrome has to be skipped explicitly. Otherwise each marker's label background is
        // captured as if it were a map icon.
        if (allowInactive && IsMapIconChrome(transform.name))
            return;

        // With the map panel closed the whole subtree is inactive in the hierarchy, yet the
        // element objects and their sprites still exist with valid transforms and atlases.
        if (allowInactive)
        {
            if (!transform.gameObject.activeSelf)
                return;
        }
        else if (!transform.gameObject.activeInHierarchy)
        {
            return;
        }

        UISprite sprite = transform.GetComponent<UISprite>();
        if (sprite != null && sprite.mainTexture != null)
        {
            float alpha;
            if (allowInactive)
            {
                // NGUI's isVisible is unusable here, but the widget's own enabled flag still
                // reflects whether it would be drawn. Ignoring it pulls in glow/backdrop
                // sprites that swell the markers.
                try
                {
                    alpha = sprite.enabled ? sprite.alpha * sprite.color.a : 0f;
                }
                catch
                {
                    alpha = 0f;
                }
            }
            else
            {
                alpha = sprite.isVisible ? sprite.CalculateFinalAlpha(Time.frameCount) : 0f;
            }

            if (alpha > 0.01f && TryGetSpriteUv(sprite, out Vector4 sourceUv))
            {
                if (ReferenceEquals(_mapIconAtlas, null) && !ReferenceEquals(sprite.atlas, null))
                    _mapIconAtlas = sprite.atlas;
                string spriteName = sprite.spriteName;
                if (!string.IsNullOrEmpty(spriteName) && !_iconBySpriteName.ContainsKey(spriteName))
                {
                    _iconBySpriteName[spriteName] = new IconRef
                    {
                        Texture = sprite.mainTexture,
                        Uv = new Rect(sourceUv.x, sourceUv.y,
                            sourceUv.z - sourceUv.x, sourceUv.w - sourceUv.y),
                    };
                }
                var corners = sprite.worldCorners;
                if (corners != null && corners.Length >= 4)
                {
                    Vector3 first = mapElements.InverseTransformPoint(corners[0]);
                    float minX = first.x;
                    float maxX = first.x;
                    float minY = first.y;
                    float maxY = first.y;
                    for (int i = 1; i < corners.Length; i++)
                    {
                        Vector3 point = mapElements.InverseTransformPoint(corners[i]);
                        minX = Mathf.Min(minX, point.x);
                        maxX = Mathf.Max(maxX, point.x);
                        minY = Mathf.Min(minY, point.y);
                        maxY = Mathf.Max(maxY, point.y);
                    }

                    Vector2 minUv = VanillaLocalToTextureUv(minX, minY);
                    Vector2 maxUv = VanillaLocalToTextureUv(maxX, maxY);
                    var sizeUv = new Vector2(Mathf.Abs(maxUv.x - minUv.x),
                        Mathf.Abs(maxUv.y - minUv.y));
                    if (allowInactive)
                        sizeUv *= PanelFreeIconScale;
                    GameObject iconObject = CreateUiObject("VanillaMapIcon",
                        typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                    iconObject.transform.SetParent(_mapRect, false);
                    RectTransform rect = iconObject.GetComponent<RectTransform>();
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    RawImage image = iconObject.GetComponent<RawImage>();
                    image.texture = sprite.mainTexture;
                    image.uvRect = new Rect(sourceUv.x, sourceUv.y,
                        sourceUv.z - sourceUv.x, sourceUv.w - sourceUv.y);
                    Color color = sprite.color;
                    color.a = alpha;
                    image.color = color;
                    image.raycastTarget = false;
                    MarkerCategory category = CategorizeSprite(spriteName);
                    _pendingVanillaIcons.Add(new VanillaIcon
                    {
                        Root = iconObject,
                        Rect = rect,
                        MapUv = (minUv + maxUv) * 0.5f,
                        MapUvSize = sizeUv,
                        Category = category,
                        CategoryEnabled = CategoryEnabled(category)
                    });
                }
            }
        }

        for (int i = 0; i < transform.childCount; i++)
            CaptureVanillaIconsRecursive(transform.GetChild(i), mapElements, allowInactive);
    }

    private Vector2 VanillaLocalToTextureUv(float x, float y) =>
        VanillaMapPositionToUv(new Vector3(x, y, 0f), ActiveLayer.VanillaMapLocalBounds, ActiveLayer.VanillaTextureUv);

    private void ClearVanillaIcons()
    {
        for (int i = 0; i < _vanillaIcons.Count; i++)
        {
            if (!ReferenceEquals(_vanillaIcons[i].Root, null))
                UnityEngine.Object.Destroy(_vanillaIcons[i].Root);
        }
        _vanillaIcons.Clear();
    }

    // Takes the game's own map image and makes the HUD use it, right now.
    //
    // The region's own texture is 1024x1024 while the panel draws a 2048x2048 image, so the panel is
    // the better source whenever the player has it on screen. It carries the reveal state, which is
    // why this is a key press rather than something automatic: only the player knows whether they
    // have lit the region yet.
    //
    // The framing is shared with the panel path deliberately. The panel reports its bounds and uvRect
    // through the same objects CaptureVanillaMap already reads, so the captured image and the
    // projection that places the player pointer and the markers come from one source. Anything else
    // risks reintroducing the mismatch that made markers land off the map.
    private void CaptureGameMapImage()
    {
        try
        {
            Panel_Map panel = InterfaceManager.GetPanel<Panel_Map>();
            if (panel == null || !panel.gameObject.activeInHierarchy)
            {
                LoggerInstance.Msg("F7: the game map is not open, so there is nothing to capture.");
                return;
            }

            Transform regionMap = FindActiveRegionMap(panel.transform);
            if (regionMap == null)
            {
                LoggerInstance.Msg("F7: no active region map in the panel.");
                return;
            }

            UITexture main = regionMap.GetComponent<UITexture>();
            if (main == null || ReferenceEquals(main.mainTexture, null))
            {
                LoggerInstance.Msg("F7: the region map widget has no texture yet.");
                return;
            }

            string sceneName = _observedSceneName;
            var bounds = new Rect(main.drawingDimensions.x, main.drawingDimensions.y,
                main.drawingDimensions.z - main.drawingDimensions.x,
                main.drawingDimensions.w - main.drawingDimensions.y);
            Rect textureUv = main.uvRect;

            Texture2D owned = CaptureTexture(main.mainTexture);
            LoggerInstance.Msg(
                $"F7 captured the game map for {sceneName}: {owned.width}x{owned.height}, " +
                $"bounds={bounds}, uv={textureUv}.");

            // Keep a separate copy for the file, so the one handed to a layer can be destroyed by
            // that layer later without the export depending on its lifetime.
            Texture2D forFile = CaptureTexture(main.mainTexture);

            // Hand it to every layer that is showing the vanilla source: the capture is region-wide,
            // so a layer that wants vanilla wants this image. Each layer takes its own texture, and
            // UseVanillaBaseMap retires the previous one safely.
            int applied = 0;
            for (int i = 0; i < _layers.Length; i++)
            {
                MapLayer layer = _layers[i];
                if (!layer.UsingVanilla || layer.Definition == null)
                    continue;
                Texture2D copy = applied == 0 ? owned : CaptureTexture(main.mainTexture);
                UseVanillaBaseMap(copy, sceneName, layer, textureUv);
                applied++;
            }

            if (applied == 0)
                UnityEngine.Object.Destroy(owned);
            else
                LoggerInstance.Msg($"F7 applied the game map to {applied} vanilla layer(s).");

            // The projection fields the panel path would have set, so the two routes agree.
            for (int i = 0; i < _layers.Length; i++)
            {
                MapLayer target = _layers[i];
                target.VanillaProjectionScene = sceneName;
                target.VanillaMapLocalBounds = bounds;
                target.VanillaTextureUv = textureUv;
            }
            _panelTextureMeasuredForScene = sceneName;

            string path = Path.Combine(_modDirectory,
                $"panelmap_{SanitizeFileName(sceneName)}.png");
            WriteTextureToPng(forFile, path);
            // The copy the loader reads next session, so this capture only has to be made once.
            SaveCapturedMap(forFile, sceneName);
            WriteFramingSidecar(sceneName, bounds, textureUv);
            UnityEngine.Object.Destroy(forFile);
            LoggerInstance.Msg($"F7 wrote {path} for calibration.");
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"F7 capture failed: {ex.Message}");
        }
    }

    // Captures the panel's own map texture, measures where its opaque content actually sits, and
    // writes one PNG per region for offline comparison against the base map the HUD is given.
    //
    // The panel's uvRect is applied to this image, so the content box here is in the SAME space the
    // projection's output uv indexes. Comparing it with the base map's content box settles whether
    // the two are the same picture; nothing so far has tested that, and every explanation attempted
    // for the misplaced markers assumed it was true.
    private void MeasureAndExportPanelTexture(UITexture main, string sceneName)
    {
        try
        {
            // Once per session per region, not once ever: the file is now overwritten every time,
            // but re-exporting on every panel refresh would write a 2048x2048 PNG repeatedly for no
            // gain. A new session re-captures, which is what makes a freshly revealed map replace a
            // stale one.
            if (string.Equals(_panelTextureMeasuredForScene, sceneName, StringComparison.Ordinal))
                return;

            Texture source = main.mainTexture;
            if (ReferenceEquals(source, null))
            {
                LoggerInstance.Warning($"Panel texture [{sceneName}]: mainTexture is null.");
                return;
            }

            LoggerInstance.Msg(
                $"Panel texture [{sceneName}]: {source.width}x{source.height}, " +
                $"uvRect={main.uvRect}, drawing={main.drawingDimensions}, " +
                $"widget={main.width}x{main.height}.");

            // The asset itself is not CPU-readable and belongs to Addressables, so measure a copy.
            Texture2D owned = CaptureTexture(source);
            Rect content = MeasureOpaqueUv(owned);
            LoggerInstance.Msg(
                $"Panel texture content [{sceneName}]: {owned.width}x{owned.height} " +
                $"uv=({content.x:F4},{content.y:F4},{content.width:F4},{content.height:F4}) " +
                $"pixels={content.width * owned.width:F0}x{content.height * owned.height:F0}.");

            string path = Path.Combine(_modDirectory,
                $"panelmap_{SanitizeFileName(sceneName)}.png");
            // Always overwrite. Skipping when the file exists meant the export kept the FIRST panel
            // state it ever saw, which is the pre-reveal fog, and that stale image was then read as
            // evidence about the map's extent for a whole round of investigation. The panel texture
            // is a snapshot of a reveal state, so the newest one is the only one worth keeping.
            WriteTextureToPng(owned, path);
            LoggerInstance.Msg($"Exported panel map texture: {path}");

            _panelTextureMeasuredForScene = sceneName;
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Panel texture measurement [{sceneName}] failed: {ex.Message}");
        }
    }

    private void UseCapturedVanillaMap(Texture2D texture, string sceneName, MapLayer layer)
    {
        EnsureUnityUi();
        Texture2D previous = layer.Texture;
        layer.Texture = texture;
        layer.LoadedMapId = "__vanilla__" + sceneName;
        layer.VanillaProjectionScene = sceneName;
        layer.UsingVanilla = true;
        layer.TextureReady = true;
        if (!ReferenceEquals(previous, null))
            UnityEngine.Object.Destroy(previous);
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
                    LoggerInstance.Warning(
                        $"No region spec for {sceneName}: GameManager.TryGetCurrentRegion() " +
                        "stayed null for 3s, so there is no vanilla base map here.");
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
                LoggerInstance.Warning($"Region {sceneName} reports no base map texture.");
                return;
            }

            layer.BaseMapRequestedScene = sceneName;
            ProbeRegionTextures(region, sceneName);
            ProbeRegionAssetReferences(region, sceneName);
            layer.BaseMapHandle = region.GetMiniMapTextureAsync();
            layer.BaseMapPending = true;
            layer.BaseMapRequestUtc = DateTime.UtcNow;
            LoggerInstance.Msg($"Requested region base map for {sceneName}.");
        }
        catch (Exception ex)
        {
            layer.BaseMapRequestedScene = sceneName;
            LoggerInstance.Warning($"Region base map request failed for {sceneName}: {ex.Message}");
        }
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

    private sealed class AssetProbe
    {
        public string Name;
        public string Scene;
        public AsyncOperationHandle<Texture2D> Handle;
        public DateTime RequestedUtc;
    }

    private sealed class PrefabProbe
    {
        public string Name;
        public string Scene;
        public AsyncOperationHandle<GameObject> Handle;
        public DateTime RequestedUtc;
    }

    private readonly List<AssetProbe> _pendingAssetProbes = new();
    private readonly List<PrefabProbe> _pendingPrefabProbes = new();

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

    private void PollVanillaBaseMap(string sceneName, MapLayer layer)
    {
        if (!layer.BaseMapPending)
            return;

        // Never leave the HUD permanently blank if the load silently stalls.
        if ((DateTime.UtcNow - layer.BaseMapRequestUtc).TotalSeconds > 15.0)
        {
            layer.BaseMapPending = false;
            LoggerInstance.Warning($"Region base map for {sceneName} timed out.");
            return;
        }

        try
        {
            if (!layer.BaseMapHandle.IsDone)
                return;
        }
        catch (Exception ex)
        {
            layer.BaseMapPending = false;
            LoggerInstance.Warning($"Region base map handle failed: {ex.Message}");
            return;
        }

        layer.BaseMapPending = false;
        try
        {
            Texture2D source = layer.BaseMapHandle.Result;
            if (source == null)
            {
                LoggerInstance.Warning($"Region base map for {sceneName} resolved to NULL.");
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
            LoggerInstance.Warning($"Region base map load failed for {sceneName}: {ex.Message}");
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
        if (alreadyLoaded && _vanillaIcons.Count > 0)
            return;
        if (DateTime.UtcNow < _elementLoadAfterUtc)
            return;
        _elementLoadAfterUtc = DateTime.UtcNow.AddSeconds(2);

        try
        {
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

            try { panel.RefreshIconVisibility(); }
            catch (Exception ex) { LoggerInstance.Warning($"RefreshIconVisibility failed: {ex.Message}"); }

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

    // The captured map, kept on disk and reloaded automatically.
    //
    // Capturing happens once per region by hand, because only the player can light a region up. That
    // work must not be thrown away when the session ends, and it must not depend on the capture
    // being applied live - the first attempt at that did not show up on screen, and a saved image
    // sidesteps the question entirely: the next time the region loads, the file is simply read like
    // any other map image.
    private string CapturedMapPath(string sceneName) =>
        Path.Combine(_modDirectory, "captured", SanitizeFileName(sceneName) + ".png");

    // Turns a localization key such as GAMEPLAY_mtTownCentre into display text.
    //
    // The lookup is tried through a short list of candidate members and the first that works is
    // remembered, because the interop assemblies cannot be reflected outside the game - so the exact
    // member name cannot be confirmed offline and a single guess would be a coin flip. Whatever
    // succeeds is logged once, which is the measurement that replaces the guess.
    private static System.Reflection.MethodInfo s_locStringMethod;
    private static bool s_localizationProbed;

    private string LocalizeLabel(string locId)
    {
        if (string.IsNullOrEmpty(locId))
            return "";

        if (!s_localizationProbed)
            ProbeLocalization();
        if (s_locStringMethod == null)
            return "";

        try
        {
            string result = s_locStringMethod.Invoke(null, new object[] { locId }) as string;
            // A key that does not resolve comes back as the key itself, which is worse than nothing
            // on the map, so treat that as a miss.
            if (!string.IsNullOrEmpty(result) && !string.Equals(result, locId, StringComparison.Ordinal))
                return result;
        }
        catch
        {
            // The method exists but rejected the call; the label is simply omitted.
        }
        return "";
    }

    // Lists what a MapDetail actually exposes for reading a display name.
    //
    // Guessing has failed three times now - "Localization" was not a type name, m_LocalizedName is
    // not in the interop assembly, and Il2Cpp.Locale.GetText answers with the key it was handed - so
    // this enumerates the entry's own members instead. A name that the game puts on its map has to
    // be reachable from the map data somehow, and this is what says how.
    private static bool s_mapDetailMembersProbed;

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

    // Lists what Il2Cpp.Locale actually offers, and tests the candidates.
    //
    // Both value sources for a place name are now ruled out by measurement: m_CustomName is empty for
    // all 14 label entries, and Il2Cpp.Locale.GetText answers with the key it is handed. The key is a
    // real localization key - the game's own map shows 米尔顿小镇 for GAMEPLAY_mtTownCentre - so the
    // translation exists; what is wrong is which member performs it. Guessing that member has failed
    // four times, so this enumerates the class and tries what it finds against a key whose expected
    // answer is known.
    private static bool s_localeMembersProbed;

    private void ProbeLocaleMembers()
    {
        if (s_localeMembersProbed)
            return;
        s_localeMembersProbed = true;
        try
        {
            Type type = null;
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { type = assembly.GetType("Il2Cpp.Locale"); } catch { }
                if (type != null)
                    break;
            }
            if (type == null)
            {
                LoggerInstance.Warning("Locale member probe: Il2Cpp.Locale not found.");
                return;
            }

            LoggerInstance.Msg($"Locale members on {type.FullName}:");
            foreach (System.Reflection.MethodInfo method in type.GetMethods(
                         System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                if (method.IsSpecialName)
                    continue;
                var parameters = method.GetParameters();
                LoggerInstance.Msg($"  {(method.ReturnType == typeof(string) ? "string" : method.ReturnType.Name)} " +
                    $"{method.Name}({string.Join(",", System.Array.ConvertAll(parameters, p => p.ParameterType.Name))})");
            }

            // Try every static string-returning single-string method against a key whose answer is
            // known from the game's own map, so a candidate can be judged rather than assumed.
            const string knownKey = "GAMEPLAY_mtTownCentre";
            foreach (System.Reflection.MethodInfo method in type.GetMethods(
                         System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                if (method.IsSpecialName || method.ReturnType != typeof(string))
                    continue;
                var parameters = method.GetParameters();
                if (parameters.Length != 1 || parameters[0].ParameterType != typeof(string))
                    continue;
                try
                {
                    string answer = method.Invoke(null, new object[] { knownKey }) as string;
                    LoggerInstance.Msg($"  try {method.Name}(\"{knownKey}\") = " +
                        $"{(string.IsNullOrEmpty(answer) ? "<empty>" : $"'{answer}'")}" +
                        (string.Equals(answer, knownKey, StringComparison.Ordinal) ? "  (echoes the key)" : ""));
                }
                catch (Exception ex)
                {
                    LoggerInstance.Msg($"  try {method.Name} threw {ex.GetType().Name}.");
                }
            }
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Locale member probe failed: {ex.Message}");
        }
    }

    private void ProbeLocalization()
    {
        s_localizationProbed = true;
        try
        {
            // Enumerate rather than guess. Looking up the exact name "Localization" failed, which is
            // the second time a name picked by string search turned out not to exist on the type it
            // was assumed to be on. Listing what is actually loaded costs one pass and removes the
            // guess: both the type name and the lookup member are discovered here.
            var candidates = new List<string>();
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch { continue; }

                foreach (Type type in types)
                {
                    string full = type.FullName ?? type.Name;
                    if (full.IndexOf("local", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    candidates.Add(full);
                }
            }

            LoggerInstance.Msg($"Localization: {candidates.Count} loaded types mention 'local': " +
                string.Join(", ", candidates.GetRange(0, Math.Min(12, candidates.Count))) +
                (candidates.Count > 12 ? ", ..." : ""));

            // Now find a static string->string lookup on any of them, preferring names that read
            // like a translation call.
            string[] preferred = { "GetLocString", "Translate", "GetString", "GetText", "Localize" };
            foreach (string wanted in preferred)
            {
                foreach (string fullName in candidates)
                {
                    Type type = null;
                    foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try { type = assembly.GetType(fullName); } catch { }
                        if (type != null)
                            break;
                    }
                    if (type == null)
                        continue;

                    System.Reflection.MethodInfo method = type.GetMethod(wanted,
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                        null, new[] { typeof(string) }, null);
                    if (method != null && method.ReturnType == typeof(string))
                    {
                        s_locStringMethod = method;
                        LoggerInstance.Msg(
                            $"Localization: using {fullName}.{wanted}(string) for map labels.");

                        // Prove the lookup actually returns text before the label build depends on
                        // it. A method that exists but answers with the key, or with nothing, would
                        // otherwise look identical to a build that never ran.
                        try
                        {
                            string probe = method.Invoke(null, new object[] { "GAMEPLAY_mtTownCentre" })
                                as string;
                            LoggerInstance.Msg(
                                $"Localization probe: GetText(\"GAMEPLAY_mtTownCentre\") = " +
                                $"{(string.IsNullOrEmpty(probe) ? "<empty>" : $"'{probe}'")}.");
                        }
                        catch (Exception ex)
                        {
                            LoggerInstance.Warning(
                                $"Localization probe call failed: {ex.GetType().Name}: {ex.Message}");
                        }
                        return;
                    }
                }
            }

            LoggerInstance.Warning(
                "Localization: no static string lookup found; map labels will be omitted.");
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Localization probe failed: {ex.Message}");
        }
    }

    private sealed class MapLabel
    {
        public GameObject Root;
        public RectTransform Rect;
        public Vector2 MapUv;
        // Kept for a later hover test: the game shows these names on hover, and having the key and
        // its text together is what would make that possible without re-reading the map data.
        public string LocId;
        public string Text;
    }

    // Place names, which the marker build deliberately skipped because they carry no sprite name.
    //
    // These are the labels the game's own map shows, and without them the HUD map is missing the one
    // thing that makes a map readable. They are drawn as text at the position each entry carries, so
    // they cost nothing while off screen and there are only a handful per region.
    private void RebuildLabelsFromMapDetails()
    {
        EnsureUnityUi();
        ClearLabels();
        _labelMisses.Clear();

        try
        {
            var details = MapDetailManager.s_MapDetails;
            if (ReferenceEquals(details, null))
                return;

            ProbeMapDetailMembers();
            ProbeLocaleMembers();

            int added = 0;
            for (int i = 0; i < details.Count; i++)
            {
                MapDetail detail = details[i];
                if (ReferenceEquals(detail, null))
                    continue;

                // A label is exactly an entry with no sprite name; anything with a sprite is a
                // marker and is handled by the marker build.
                if (!string.IsNullOrEmpty(detail.m_SpriteName))
                    continue;

                // Order matters. m_CustomName is a plain text override and is used when set;
                // m_LocID is a localization key, which is why the labels came out empty while only
                // the key path was tried. Enumerating the entry's own members found both, after three
                // guesses at a lookup API had failed.
                // Order matters. m_CustomName is a plain text override and is used when set; m_LocID
                // is a localization key, which is why the labels stayed empty while only the key path
                // was tried. Enumerating the entry's own members found both, after three guesses at a
                // lookup API had failed.
                string text = detail.m_CustomName;
                if (string.IsNullOrEmpty(text))
                    text = LocalizeLabel(detail.m_LocID);
                if (string.IsNullOrEmpty(text))
                {
                    _labelMisses.Add($"{detail.m_LocID}|custom='{detail.m_CustomName}'");
                    continue;
                }

                Vector2 mapUv;
                try
                {
                    if (!TryWorldToMarkerUv(detail.GetWorldPosition(), out mapUv))
                        continue;
                }
                catch
                {
                    continue;
                }

                // The hint bar already found a font that can render CJK; reuse it rather than
                // probing for a second one.
                ResolveHintFont();
                if (ReferenceEquals(_hintFont, null))
                    return;

                GameObject labelObject = CreateUiObject("MapLabel",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelObject.transform.SetParent(_mapRect, false);
                RectTransform rect = labelObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(220f, 26f);

                Text label = labelObject.GetComponent<Text>();
                label.text = text;
                label.font = _hintFont;
                label.fontSize = 18;
                label.alignment = TextAnchor.MiddleCenter;
                // Dark text, because the map art is light; a light outline would be needed on the
                // dark community map, but this layer only exists on the vanilla source.
                label.color = new Color(0.10f, 0.08f, 0.06f, 0.92f);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.raycastTarget = false;

                _mapLabels.Add(new MapLabel
                {
                    Root = labelObject,
                    Rect = rect,
                    MapUv = mapUv,
                    LocId = detail.m_LocID,
                    Text = text,
                });
                added++;
            }

            LoggerInstance.Msg($"Map labels: {added} placed from {details.Count} entries " +
                $"(key lookup: {(s_locStringMethod != null ? "yes" : "no")}).");
            if (_labelMisses.Count > 0)
            {
                LoggerInstance.Msg($"Map labels with no text ({_labelMisses.Count}): " +
                    string.Join(", ", _labelMisses.GetRange(0, Math.Min(6, _labelMisses.Count))));
            }
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Map label build failed: {ex.Message}");
        }
    }

    private void ClearLabels()
    {
        for (int i = 0; i < _mapLabels.Count; i++)
        {
            if (!ReferenceEquals(_mapLabels[i].Root, null))
                UnityEngine.Object.Destroy(_mapLabels[i].Root);
        }
        _mapLabels.Clear();
    }

    // Positions each label for the visible window, and hides it when it falls outside.
    private void UpdateMapLabels(Rect visibleUv, Vector2 mapSize)
    {
        if (_mapLabels.Count == 0)
            return;

        bool show = _settings.ShowMapLabels && (ActiveLayer.UsingVanilla || _settings.MarkersOnCommunityMap);
        for (int i = 0; i < _mapLabels.Count; i++)
        {
            MapLabel label = _mapLabels[i];
            bool visible = show &&
                           label.MapUv.x >= visibleUv.xMin && label.MapUv.x <= visibleUv.xMax &&
                           label.MapUv.y >= visibleUv.yMin && label.MapUv.y <= visibleUv.yMax;
            label.Root.SetActive(visible);
            if (!visible)
                continue;

            label.Rect.anchoredPosition = new Vector2(
                ((label.MapUv.x - visibleUv.x) / visibleUv.width - 0.5f) * mapSize.x,
                ((label.MapUv.y - visibleUv.y) / visibleUv.height - 0.5f) * mapSize.y);
        }
    }

    // Builds the marker set and the place names for one scene, and records which build is current.
    private void BuildMarkersAndLabels(string sceneName, string buildKey)
    {
        _markersBuiltForScene = buildKey;
        if (_settings.MarkerSource == MinimapSettings.MarkerSourceMapDetails)
        {
            if (ReferenceEquals(_mapIconAtlas, null) && _iconBySpriteName.Count == 0)
            {
                // No atlas yet: come back once the icon table has been filled.
                _markersBuiltForScene = "";
                return;
            }
            RebuildMarkersFromMapDetails();
            // Place names come from the same map data and are rebuilt on the same trigger, so the
            // two can never disagree about which region they describe.
            RebuildLabelsFromMapDetails();
        }
        else
        {
            // Old path, kept behind the setting so the rewrite can be compared against it. It needs
            // a panel to scrape; with none the icon list stays as it was.
            Panel_Map scrapePanel = InterfaceManager.GetPanel<Panel_Map>();
            Transform scrapeRoot = ReferenceEquals(scrapePanel, null)
                ? null
                : FindChildByName(scrapePanel.transform, "MapElements");
            if (!ReferenceEquals(scrapeRoot, null))
                CaptureVanillaIcons(scrapeRoot, false, true);
        }
    }

    private void SaveCapturedMap(Texture2D texture, string sceneName)
    {
        try
        {
            string path = CapturedMapPath(sceneName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            WriteTextureToPng(texture, path);
            LoggerInstance.Msg($"Saved the captured map for {sceneName}: {path}");
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Saving the captured map for {sceneName} failed: {ex.Message}");
        }
    }

    // Writes the panel's framing next to the captured image. Kept as text rather than folded into
    // the image so a framing that ever needs adjusting can be corrected without re-capturing.
    private void WriteFramingSidecar(string sceneName, Rect bounds, Rect textureUv)
    {
        try
        {
            string sidecar = CapturedMapPath(sceneName) + ".framing";
            File.WriteAllText(sidecar, string.Join(",",
                bounds.x.ToString("R", CultureInfo.InvariantCulture),
                bounds.y.ToString("R", CultureInfo.InvariantCulture),
                bounds.width.ToString("R", CultureInfo.InvariantCulture),
                bounds.height.ToString("R", CultureInfo.InvariantCulture),
                textureUv.x.ToString("R", CultureInfo.InvariantCulture),
                textureUv.y.ToString("R", CultureInfo.InvariantCulture),
                textureUv.width.ToString("R", CultureInfo.InvariantCulture),
                textureUv.height.ToString("R", CultureInfo.InvariantCulture)));
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Writing framing for {sceneName} failed: {ex.Message}");
        }
    }

    // Reads a previously captured map, if one exists, into the layer.
    //
    // The framing recorded with it is the panel's own bounds and uvRect, because that is what the
    // game lays its markers out with. The sidecar is a plain text file so it can be inspected and
    // corrected by hand if a capture ever needs adjusting.
    private bool TryLoadCapturedMap(string sceneName, MapLayer layer)
    {
        try
        {
            string path = CapturedMapPath(sceneName);
            if (!File.Exists(path))
                return false;

            Rect bounds = new(-325f, -325f, 650f, 650f);
            Rect textureUv = new(0f, 0f, 1f, 1f);
            string sidecar = path + ".framing";
            if (File.Exists(sidecar))
            {
                string[] parts = File.ReadAllText(sidecar).Split(',');
                if (parts.Length >= 8 &&
                    float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float bx) &&
                    float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float by) &&
                    float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float bw) &&
                    float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float bh) &&
                    float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float ux) &&
                    float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out float uy) &&
                    float.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out float uw) &&
                    float.TryParse(parts[7], NumberStyles.Float, CultureInfo.InvariantCulture, out float uh))
                {
                    bounds = new Rect(bx, by, bw, bh);
                    textureUv = new Rect(ux, uy, uw, uh);
                }
            }

            byte[] bytes = File.ReadAllBytes(path);
            var il2CppBytes = new Il2CppStructArray<byte>(bytes);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, il2CppBytes, true))
            {
                LoggerInstance.Warning($"Captured map for {sceneName} failed to decode.");
                return false;
            }
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.hideFlags = HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
            UnityEngine.Object.DontDestroyOnLoad(texture);

            UseVanillaBaseMap(texture, sceneName, layer, textureUv);
            LoggerInstance.Msg(
                $"Using the captured map for {sceneName}: {texture.width}x{texture.height}, " +
                $"bounds={bounds}, uv={textureUv}.");
            return true;
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Loading the captured map for {sceneName} failed: {ex.Message}");
            return false;
        }
    }

    private void UseVanillaBaseMap(Texture2D texture, string sceneName, MapLayer layer,
        Rect textureUv)
    {
        EnsureUnityUi();
        Texture2D previous = layer.Texture;
        layer.Texture = texture;
        layer.LoadedMapId = "__basemap__" + sceneName;
        layer.UsingVanilla = true;
        layer.TextureReady = true;
        // The world bounds are the surveyed map's framing, confirmed by the framing probe: the panel
        // path reported (-325,-325,650x650) and both paths produced the same final uv for the player
        // and 600 world units away. The TEXTURE uv is a different matter - this capture is
        // letterboxed, so it is the measured content box rather than the whole frame. Recording the
        // frame's identity rect here is what put the vanilla pointer in the wrong place.
        for (int i = 0; i < _layers.Length; i++)
        {
            MapLayer target = _layers[i];
            target.VanillaProjectionScene = sceneName;
            target.VanillaMapLocalBounds = new Rect(-325f, -325f, 650f, 650f);
            target.VanillaTextureUv = textureUv;
        }
        if (!ReferenceEquals(previous, null) && !ReferenceEquals(previous, texture))
            UnityEngine.Object.Destroy(previous);
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (string.Equals(child.name, name, StringComparison.Ordinal))
                return child;
            Transform result = FindChildByName(child, name);
            if (result != null)
                return result;
        }

        return null;
    }

    private static Texture2D CaptureTexture(Texture source)
    {
        var target = new RenderTexture(source.width, source.height, 0,
            RenderTextureFormat.ARGB32);
        var readback = new Texture2D(source.width, source.height,
            TextureFormat.RGBA32, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            target.Create();
            Graphics.Blit(source, target);
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0,
                false);
            readback.Apply(false, false);
            readback.wrapMode = TextureWrapMode.Clamp;
            readback.filterMode = FilterMode.Bilinear;
            readback.hideFlags = HideFlags.HideAndDontSave |
                                 HideFlags.DontUnloadUnusedAsset;
            UnityEngine.Object.DontDestroyOnLoad(readback);
            return readback;
        }
        finally
        {
            RenderTexture.active = previous;
            target.Release();
            UnityEngine.Object.Destroy(target);
        }
    }

    private static void WriteTextureToPng(Texture2D texture, string path)
    {
        Il2CppStructArray<byte> encoded = ImageConversion.EncodeToPNG(texture);
        int encodedLength = checked((int)encoded.Length);
        var bytes = new byte[encodedLength];
        for (int i = 0; i < encodedLength; i++)
            bytes[i] = encoded[i];
        File.WriteAllBytes(path, bytes);
    }

    private static void AppendTransformDiagnostics(Transform transform, string parentPath,
        StringBuilder output)
    {
        string currentPath = string.IsNullOrEmpty(parentPath)
            ? transform.name
            : parentPath + "/" + transform.name;
        output.Append(currentPath)
            .Append(" | activeSelf=").Append(transform.gameObject.activeSelf)
            .Append(" activeInHierarchy=").Append(transform.gameObject.activeInHierarchy)
            .Append(" localPosition=").Append(transform.localPosition)
            .Append(" localScale=").Append(transform.localScale)
            .Append(" | components=");

        Component[] components = transform.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            if (i > 0)
                output.Append(';');
            Component component = components[i];
            output.Append(component == null ? "<null>" : component.GetType().FullName);
        }

        UITexture uiTexture = transform.GetComponent<UITexture>();
        if (uiTexture != null && uiTexture.mainTexture != null)
        {
            Texture texture = uiTexture.mainTexture;
            output.Append(" | UITexture=").Append(texture.name)
                .Append(' ').Append(texture.width).Append('x').Append(texture.height);
        }

        Renderer renderer = transform.GetComponent<Renderer>();
        if (renderer != null && renderer.sharedMaterial != null &&
            renderer.sharedMaterial.mainTexture != null)
        {
            Texture texture = renderer.sharedMaterial.mainTexture;
            output.Append(" | RendererTexture=").Append(texture.name)
                .Append(' ').Append(texture.width).Append('x').Append(texture.height);
        }

        Camera camera = transform.GetComponent<Camera>();
        if (camera != null)
        {
            output.Append(" | Camera orthographic=").Append(camera.orthographic)
                .Append(" size=").Append(camera.orthographicSize)
                .Append(" target=")
                .Append(camera.targetTexture == null ? "<screen>" : camera.targetTexture.name);
        }

        output.AppendLine();
        for (int i = 0; i < transform.childCount; i++)
            AppendTransformDiagnostics(transform.GetChild(i), currentPath, output);
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

    private static string SanitizeFileName(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
            value = value.Replace(invalid, '_');
        return string.IsNullOrWhiteSpace(value) ? "unknown_scene" : value;
    }

    private static string EscapeCsv(string value) => '"' + value.Replace("\"", "\"\"") + '"';
}
// — sutanm · 社区HUD地图

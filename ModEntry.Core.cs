// 社区HUD地图 · sutanm — 入口点与共享状态：字段、嵌套类型、OnUpdate 调度
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
    private static bool s_fullMapActive;

    private static DateTime s_lastMapRedirectUtc = DateTime.MinValue;

    private bool _fullMapCursorOwned;

    // Full-screen mode temporarily replaces the corner view. Keep the state that existed before
    // it opened so closing with the map key, Escape, or a game-panel shortcut returns to the view
    // the player recognises instead of unexpectedly landing on a completely blank HUD.
    private bool _miniMapBeforeFull;

    private const float FullMapMinZoom = 0.35f;
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

    private string _probeMapsPath = "";

    private DateTime _calibrationLastWriteUtc = DateTime.MinValue;

    private DateTime _nextCalibrationCheckUtc = DateTime.MinValue;

    private DateTime _probeMapsLastWriteUtc = DateTime.MinValue;

    private DateTime _nextProbeMapsCheckUtc = DateTime.MinValue;

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

    // Cached separately from each layer because one setting changes the effective source policy
    // of both layers at once. -1 guarantees that a saved non-default value is applied on startup.
    private int _sourceFallbackPolicy = -1;

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

    private Canvas _mapCanvas;

    private Camera _mapCanvasCamera;

    private bool _mapCanvasBehindHud;

    private GameObject _backgroundObject;

    private Image _backgroundImage;

    private RectTransform _mapRect;

    private RawImage _mapImage;

    private Text _hintLabel;

    private GameObject _statusToastRoot;

    private Image _statusToastPlate;

    private Text _statusToastLabel;

    private float _statusToastFadeAt;

    private float _statusToastHideAt;

    private Font _hintFont;

    private bool _hintFontOwned;

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

    // Resource markers whose linked random spawn is absent or fully harvested. Kept
    // separate from malformed/off-map skips so a log immediately exposes this filter's effect.
    private int _mapDetailInactiveResources;

    // scene|source of the last marker build, so a rebuild happens when either changes and not
    // otherwise.
    private string _markersBuiltForScene = "";

    // Rate limit for rebuilding the marker set while the region's entry count is still growing.
    private DateTime _nextMarkerRebuildUtc = DateTime.MinValue;

    // Random resource parents are activated by RandomSpawnObject after the additive SANDBOX scene
    // arrives. Until every loaded controller reports m_Inited, activeInHierarchy is a loading
    // signal rather than a spawn result. _resourceFilterAfterUtc remains the single flag consumed
    // by the marker builder; the other fields drive the readiness probe and its defensive fallback.
    private DateTime _resourceFilterAfterUtc = DateTime.MaxValue;

    private DateTime _resourceFilterProbeStartedUtc = DateTime.MaxValue;

    private DateTime _resourceFilterFallbackUtc = DateTime.MaxValue;

    private DateTime _nextResourceFilterProbeUtc = DateTime.MinValue;

    private bool _resourceFilterPending;

    // Live resource visibility is cheap compared with rebuilding a thousand marker GameObjects.
    // Poll only the MapDetail entries that actually link to harvestables, and only while a HUD map
    // is visible. This makes a picked plant disappear from both views within a fraction of a second.
    private DateTime _nextResourceMarkerRefreshUtc = DateTime.MinValue;

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


    internal static string CategoryName(MarkerCategory category) => category switch
    {
        MarkerCategory.Resources => "资源",
        MarkerCategory.Structures => "结构",
        MarkerCategory.Corpses => "尸骸",
        MarkerCategory.RockCaches => "岩石贮藏处",
        MarkerCategory.SprayMarks => "油漆喷罐标记",
        _ => "未分类",
    };



    private sealed class VanillaIcon
    {
        public GameObject Root;
        public RectTransform Rect;
        // The dark outlined copy drawn behind this marker, when the outline setting is on. Kept so it
        // can be positioned with the icon and destroyed with it.
        public GameObject Backing;
        public Vector2 MapUv;
        public Vector2 MapUvSize;
        // Resolved once when the icon is built. The category comes from the sprite name, and the
        // switch that hides it is read from the settings - both are stable for the icon's lifetime
        // and looking either up per frame would cost more than the draw call it guards.
        public MarkerCategory Category;
        public bool CategoryEnabled;
        // Resources < structures < scene-transition arrows. The player pointer is a separate
        // object and is always placed above the entire marker stack.
        public int DrawPriority;
        // The name shown on hover, resolved once at build time. Markers with no name simply never
        // win the hover pick.
        public string Text;
        // Filled for MapDetail markers whose visibility is tied to harvestables or a live animal.
        // The same marker objects serve the mini and full views, so changing this one flag updates
        // both without rebuilding either map.
        public MapDetail ResourceDetail;
        public string SpriteName;
        public bool ResourceAvailable = true;
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
        // A requested vanilla source can be genuinely absent in caves, transition scenes and
        // community-only regions. This is runtime state for the current scene, not a setting:
        // it lets the layer fall back without changing the player's preferred source.
        public bool VanillaUnavailable;
        public AsyncOperationHandle<Texture2D> BaseMapHandle;
        public bool BaseMapHandleValid;
        public bool BaseMapPending;
        public string BaseMapRequestedScene = "";
        public DateTime BaseMapRequestUtc = DateTime.MinValue;
        public string ElementsLoadedForScene = "";
        public string LastCommunityLoadError = "";
        // The vanilla projection this layer is holding. The values are region-wide and both layers
        // compute the same ones, but they are stored per layer on purpose: the panel path writes
        // bounds and uv, the base-map path writes different defaults, and switching ONE layer's
        // source must not change the framing the other layer is already projecting with.
        public string VanillaProjectionScene = "";
        public Rect VanillaMapLocalBounds = new(-1024f, -1024f, 2048f, 2048f);
        public Rect VanillaTextureUv = new(0f, 0f, 1f, 1f);
    }


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
        _probeMapsPath = Path.Combine(_modDirectory, "probe-maps.json");
        Directory.CreateDirectory(_mapsDirectory);
        CalibrationStore.Load(_calibrationPath,
            message => LoggerInstance.Msg(message),
            message => LoggerInstance.Warning(message));
        _calibrationLastWriteUtc = File.GetLastWriteTimeUtc(_calibrationPath);
        ProbeMapStore.Load(_probeMapsPath,
            message => LoggerInstance.Msg(message),
            message => LoggerInstance.Warning(message));
        _probeMapsLastWriteUtc = File.GetLastWriteTimeUtc(_probeMapsPath);
        _sceneCatalogAfterUtc = DateTime.UtcNow.AddSeconds(5);
        LoggerInstance.Msg("社区HUD地图 0.7.0 initialized.");
        LoggerInstance.Msg($"Map directory: {_mapsDirectory}");
    }


    public override void OnDeinitializeMelon()
    {
        // The UI and decoded maps are DontDestroyOnLoad by design, so a hot unload must explicitly
        // return every resource and input context. A normal process exit would reclaim them, but
        // relying on that made development reloads stack canvases and textures.
        s_fullMapActive = false;
        try { ReleaseFullMapCursor(); }
        catch (Exception ex) { LoggerInstance.Warning($"Input cleanup failed: {ex.Message}"); }
        try { ReleasePendingDiagnosticHandles(); }
        catch (Exception ex) { LoggerInstance.Warning($"Diagnostic handle cleanup failed: {ex.Message}"); }
        try { ReleasePersistentResources(); }
        catch (Exception ex) { LoggerInstance.Warning($"Persistent resource cleanup failed: {ex.Message}"); }
        s_instance = null;
        LoggerInstance.Msg("社区HUD地图 resources released.");
    }


    public override void OnUpdate()
    {
        // First thing, so the previous frame has definitely finished drawing with them. There are
        // several early returns below and none of them may skip this.
        SweepRetiredTextures();
        UpdateStatusToast();
        PollRegionAssetProbes();
        PollPrefabProbes();
        TryExportSceneCatalog();
        TryReloadCalibrations();
        TryReloadProbeMaps();

        // Capture the game's own map image on demand, while it is on screen.
        //
        // The mod cannot light a region up, so the reveal has to happen in the game. What it CAN do
        // is take the panel's full-resolution image at the moment the player says it is ready, apply
        // it to the HUD immediately, and keep a PNG for calibration. That is one key press in the
        // session where the player revealed the map, instead of a file that only refreshes on the
        // first panel open of a session and then has to be carried back to the workspace by hand.
        if (_settings.DeveloperMode && _settings.CaptureMapKey != KeyCode.None &&
            NoShortcutModifierHeld() && Input.GetKeyDown(_settings.CaptureMapKey))
            CaptureGameMapImage();

        if (NoShortcutModifierHeld() && Input.GetKeyDown(_settings.ToggleKey))
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
                 NoShortcutModifierHeld() && Input.GetKeyDown(_settings.CycleViewKey))
        {
            CycleView();
        }
        if (FullMapVisible && Input.GetKeyDown(KeyCode.Escape))
            LeaveFullMap();
        if (_settings.DeveloperMode && NoShortcutModifierHeld() &&
            Input.GetKeyDown(_settings.RecordPointKey))
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
        var inputScene = UnitySceneManager.GetActiveScene();
        bool activeSceneObserved = inputScene.handle == _observedSceneHandle;
        bool fullMapStateAvailable = FullMapVisible && activeSceneObserved &&
                                     ActiveLayer.Definition != null;
        bool showFullMap = fullMapStateAvailable && ActiveLayer.TextureReady &&
                           !ReferenceEquals(ActiveLayer.Texture, null) &&
                           !ReferenceEquals(_backgroundImage, null);
        // Escape still belongs to our modal state while a valid map is finishing its first load,
        // but mouse takeover waits until there is a visible texture to interact with.
        s_fullMapActive = fullMapStateAvailable;

        // The setting can be changed while the full map is already open. Tying the input context
        // only to the visibility transition left the cursor behind when the option was turned off,
        // and failed to claim it when the option was turned on. Reconcile the desired state every
        // frame; the helpers themselves remain transition-only through _fullMapCursorOwned.
        bool shouldOwnMapInput = showFullMap && _settings.ReleaseMouseOnFullMap;
        if (shouldOwnMapInput && !_fullMapCursorOwned)
            ApplyFullMapCursor();
        else if (!shouldOwnMapInput && _fullMapCursorOwned)
            ReleaseFullMapCursor();
        if (DateTime.UtcNow >= _nextMarkerCleanupUtc)
        {
            _nextMarkerCleanupUtc = DateTime.UtcNow.AddSeconds(2);
            ReportHarvestedMapMarkers();
        }

        var scene = UnitySceneManager.GetActiveScene();
        if (scene.handle != _observedSceneHandle)
            ObserveScene(scene.handle, scene.name);
        RefreshSpatialMapVariant(scene.handle, scene.name);

        ProbeRandomResourceReadiness(scene.name);
        RefreshResourceMarkerAvailability();

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
        else if (active.Definition != null && !preferCommunity && !_capturedThisVanillaMapOpen &&
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
            bool resourceStateSettled = DateTime.UtcNow >= _resourceFilterAfterUtc;
            string projection = (active.UsingVanilla ? "vanilla" : "community") +
                                (resourceStateSettled ? "+settled" : "+loading");
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
                // few as the region streams, so one rebuild is allowed every couple of seconds.
                //
                // The rate limit must not swallow a change of PROJECTION, which is what the first
                // version of this guard did: it compared only the trailing entry count, so switching
                // the map source from vanilla to community produced a different key that still ended
                // in the same "|816" and was treated as "nothing to do". The markers then stayed in
                // vanilla uv and were drawn across the community map, offset - reported by the user
                // as "the markers are all shifted, it is not using the community coordinates".
                // A projection change is not a streaming wobble; it invalidates every position, so it
                // rebuilds immediately and only the same-projection case is rate limited.
                string lastProjection = LastProjection(_markersBuiltForScene);
                bool sameProjection = string.Equals(lastProjection, projection, StringComparison.Ordinal);
                bool sameCount = _markersBuiltForScene.EndsWith("|" + detailCount, StringComparison.Ordinal);

                if (sameProjection && sameCount)
                {
                    // Same projection and entry count: nothing to do.
                }
                else if (sameProjection && DateTime.UtcNow < _nextMarkerRebuildUtc)
                {
                    // Same projection but the count moved; wait for it to settle.
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
        // Readiness belongs to the layer being drawn. Letting the other layer satisfy this test
        // exposes the shared Canvas with a null/stale texture during first load or a source switch,
        // which is the "pointer only" frame reported in testing.
        bool shouldShow = active.TextureReady && !ReferenceEquals(active.Texture, null) &&
                          active.Definition != null && playerReady &&
                          (MiniMapVisible || FullMapVisible) && !vanillaMapOpen;
        SetUiVisible(shouldShow);
        if (!shouldShow)
            return;

        UpdateUnityUi(GameManager.GetPlayerTransform());
    }

    // A configurable single-key shortcut must not steal the character from an operating-system or
    // game chord. Unity reports GetKeyDown(V) for Ctrl+V too, which made pasting text unexpectedly
    // cycle the map. The deliberate Ctrl+Shift+view-key signature above is checked first and is the
    // sole modified chord owned by this mod.
    private static bool NoShortcutModifierHeld()
    {
        return !Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl) &&
               !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt) &&
               !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift) &&
               !Input.GetKey(KeyCode.LeftCommand) && !Input.GetKey(KeyCode.RightCommand);
    }

    // Reads the projection part out of a marker build key of the form
    // "scene|markerSource|projection|entryCount".
    //
    // Its own method because the key is assembled in one place and parsed in another, and a change
    // to the format that only updates one of them would quietly reintroduce the bug this parsing
    // exists to fix: markers staying in vanilla uv while the community map is drawn.
    private static string LastProjection(string buildKey)
    {
        if (string.IsNullOrEmpty(buildKey))
            return "";
        string[] parts = buildKey.Split('|');
        return parts.Length >= 3 ? parts[2] : "";
    }

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
        SetUiVisible(false);
        _observedSceneHandle = int.MinValue;
        LoggerInstance.Msg($"Scene initialized callback: {sceneName}.");
    }


    // Two independent layers. The corner map answers to its own setting, to the view cycle and to
    // the temporary hide key; the full map answers to the game's map key and to the view cycle.
    private bool MiniMapVisible => _miniMapOn && _settings.Enabled && !_temporarilyHidden;

    private bool FullMapVisible => _fullMapOn;


    // The orders the cycle key can walk. A state is "which of the two layers is on screen": the
    // corner map alone, the full map, or neither. Two-step entries are the plain toggles.
    private static readonly (bool Mini, bool Full)[] CycleMiniFull =
        { (true, false), (false, true) };

    private static readonly (bool Mini, bool Full)[] CycleMiniFullNone =
        { (true, false), (false, true), (false, false) };

    private static readonly (bool Mini, bool Full)[] CycleMiniOnly =
        { (true, false), (false, false) };

    private static readonly (bool Mini, bool Full)[] CycleFullOnly =
        { (false, true), (false, false) };


    private List<(bool Mini, bool Full)> BuildCycle()
    {
        // A genuinely map-less scene cannot enter an invisible full-map state. Developer-only
        // indoor probes count as maps here so their independent image can be exercised normally.
        var scene = UnitySceneManager.GetActiveScene();
        if (FindRuntimeMap(scene.name) == null)
            return new List<(bool Mini, bool Full)>();

        (bool Mini, bool Full)[] source = _settings.CyclePreset switch
        {
            0 => CycleMiniFull,
            2 => CycleMiniOnly,
            3 => CycleFullOnly,
            _ => CycleMiniFullNone,
        };

        // A step that reaches a layer the player switched off would just show nothing, so those
        // steps are dropped. If that leaves fewer than two, the key has nothing to cycle and the
        // caller does nothing rather than sitting on one state.
        var steps = new List<(bool Mini, bool Full)>(source.Length);
        foreach ((bool mini, bool full) in source)
        {
            if (mini && !_settings.Enabled)
                continue;
            steps.Add((mini, full));
        }
        return steps;
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


    private Vector2 ApplyMiniMapLayout()
    {
        float maxDimension = Mathf.Max(180f,
            Mathf.Min(Screen.width, Screen.height) * _settings.MiniMapSizePercent / 100f);
        // This is a local navigation viewport, not a whole-sheet preview. Inheriting the source
        // image's aspect ratio made long transition maps almost unusable: Ravine is roughly 3:1,
        // so a nominal 24% HUD map collapsed into a very short strip. Keep the viewport square and
        // compensate in the UV window below; that preserves the artwork's geometry without making
        // the HUD's usable height depend on how the cartographer cropped the source sheet.
        Vector2 size = new(maxDimension, maxDimension);

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

        // Zooming the original map scales the parchment itself: its default/local view covers the
        // display and crops the excess, while the overview scale fits the whole sheet in the middle.
        // Keeping the texture at uv 0..1 also preserves its real aspect ratio; stretching a square
        // capture across a widescreen rectangle was the tempting but visibly wrong alternative.
        // Let players pull back beyond the complete-sheet stop. This is especially useful for
        // narrow cave diagrams where even a perfectly fitted sheet can feel pressed against the
        // screen edges. Keep a non-zero floor so an accidental wheel spin cannot lose the map.
        float zoom = Mathf.Max(FullMapMinZoom, _fullMapZoom);
        Vector2 size = new(width * zoom, height * zoom);
        Vector2 center = new(0.5f, 0.5f);
        _mapRect.anchorMin = center;
        _mapRect.anchorMax = center;
        _mapRect.pivot = center;

        float halfVisibleX = Mathf.Min(0.5f, Screen.width * 0.5f / Mathf.Max(1f, size.x));
        float halfVisibleY = Mathf.Min(0.5f, Screen.height * 0.5f / Mathf.Max(1f, size.y));
        Vector2 focus = _fullMapCenterValid ? _fullMapCenter : center;
        focus = new Vector2(
            Mathf.Clamp(focus.x, halfVisibleX, 1f - halfVisibleX),
            Mathf.Clamp(focus.y, halfVisibleY, 1f - halfVisibleY));
        _fullMapCenter = focus;
        _mapRect.anchoredPosition = new Vector2(
            (0.5f - focus.x) * size.x,
            (0.5f - focus.y) * size.y);
        _mapRect.sizeDelta = size;
        return size;
    }


    // Scale at which an aspect-fitted sheet grows just enough to cover the whole screen. This is
    // the game's useful first zoom stop; 1.0 remains the complete-map overview stop.
    private float FullMapCoverZoom()
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
        return Mathf.Max(1f,
            Mathf.Max(Screen.width / Mathf.Max(1f, width),
                      Screen.height / Mathf.Max(1f, height)));
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


    // Memoised localization.
    //
    // Resolving a key goes through reflection because the member had to be discovered rather than
    // compiled against, and a reflected call under IL2CPP is expensive. A region's marker set runs to
    // hundreds of entries that share a handful of distinct keys - 251 cattails, 127 rosehips - so
    // resolving per entry hammered that cost once per marker. That is what made the map stall after
    // the hover names were added: the place names were fine because there are only fourteen of them.
    private readonly Dictionary<string, string> _localizedCache = new(StringComparer.Ordinal);


    // Lists what a MapDetail actually exposes for reading a display name.
    //
    // Guessing has failed three times now - "Localization" was not a type name, m_LocalizedName is
    // not in the interop assembly, and Il2Cpp.Locale.GetText answers with the key it was handed - so
    // this enumerates the entry's own members instead. A name that the game puts on its map has to
    // be reachable from the map data somehow, and this is what says how.
    private static bool s_mapDetailMembersProbed;


    // The members that resolve a location key, found by scanning rather than by naming them.
    //
    // A scan of every static (string)->string member, called against a key whose answer was known to
    // exist on the panel's own text objects, produced these two:
    //   Il2Cpp.Localization.Get                    "GAMEPLAY_mtTownCentre" -> 米尔顿小镇
    //   Il2Cpp.Localization.GetForFallbackLanguage "GAMEPLAY_mtTownCentre" -> Town of Milton
    // Four hand-picked names had been wrong before this - m_MapTex, a type named Localization, a
    // property m_LocalizedName, and Il2Cpp.Locale.GetText - which is why the member is discovered
    // and verified here instead of assumed.
    private static System.Reflection.MethodInfo s_localizationGet;

    private static System.Reflection.MethodInfo s_localizationFallback;

    private static bool s_localizationResolved;



    private sealed class MapLabel
    {
        public GameObject Root;
        public RectTransform Rect;
        public Vector2 MapUv;
        // Kept so a hover tooltip can name the same thing without re-reading the map data.
        public string LocId;
        public string Text;
    }


    // The single hover tooltip, reused instead of one per marker. Hundreds of markers exist, so a
    // label object each would be the same allocation problem the marker set already has.
    private GameObject _hoverLabelRoot;

    private RectTransform _hoverLabelRect;

    private Text _hoverLabelText;

    // The dark plate is a separate object behind the text, because adding Image to the text object
    // stopped its Text component from being created at all.
    private GameObject _hoverPlateRoot;

    // The plate is sized from the text, so it is re-measured whenever the shown name changes.
    private bool _plateWidthDirty = true;

    // Font size in force on the existing label objects, so a settings change is applied once.
    private int _appliedLabelFontSize = -1;

    // Set when the tooltip could not be constructed, so the failure costs one log line rather than an
    // exception every frame the pointer moves.
    private bool _hoverTooltipDisabled;


    private static Texture2D CaptureTexture(Texture source)
    {
        var target = new RenderTexture(source.width, source.height, 0,
            RenderTextureFormat.ARGB32);
        var readback = new Texture2D(source.width, source.height,
            TextureFormat.RGBA32, false);
        bool completed = false;
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
            completed = true;
            return readback;
        }
        finally
        {
            RenderTexture.active = previous;
            target.Release();
            UnityEngine.Object.Destroy(target);
            if (!completed && !ReferenceEquals(readback, null))
                UnityEngine.Object.Destroy(readback);
        }
    }
}
// — sutanm · 社区HUD地图

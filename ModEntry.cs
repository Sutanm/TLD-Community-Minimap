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
    private static int s_suppressEscapeThroughFrame = -1;

    private enum DisplayMode
    {
        MiniMap,
        FullMap
    }

    private readonly MinimapSettings _settings = new();
    private string _modDirectory = "";
    private string _mapsDirectory = "";
    private string _calibrationPath = "";
    private DateTime _calibrationLastWriteUtc = DateTime.MinValue;
    private DateTime _nextCalibrationCheckUtc = DateTime.MinValue;
    private bool _temporarilyHidden;
    private bool _textureReady;
    private bool _uiVisible;
    private int _observedSceneHandle = int.MinValue;
    private DateTime _loadAfterUtc = DateTime.MaxValue;
    private DateTime _sceneCatalogAfterUtc = DateTime.MaxValue;
    private int _sceneCatalogAttempts;
    private bool _sceneCatalogExported;
    private int _observedMapSource = -1;
    private bool _observedPreferCommunity;
    private bool _capturedThisVanillaMapOpen;
    private bool _vanillaMapWasOpen;
    private DateTime _vanillaCaptureAfterUtc = DateTime.MaxValue;
    private AsyncOperationHandle<Texture2D> _baseMapHandle;
    private bool _baseMapPending;
    private string _baseMapRequestedScene = "";
    private DateTime _baseMapRequestUtc = DateTime.MinValue;
    private DateTime _elementLoadAfterUtc = DateTime.MinValue;
    private string _elementsLoadedForScene = "";
    private string _observedSceneName = "";
    // Panel_Map lays its marker objects out at a hard-coded 0.33 root scale. Elements created
    // through LoadMapElementsForScene never go through that layout, so their measured bounds
    // come out exactly 3x too large; measured 52.0 vs 17.3, 32.0 vs 10.7, 47.8 vs 15.9.
    private const float PanelFreeIconScale = 1f / 3f;
    private DisplayMode _displayMode = DisplayMode.MiniMap;
    private MapDefinition _currentDefinition;
    private string _loadedMapId = "";
    private Texture2D _currentTexture;
    private bool _usingVanillaMap;
    private string _vanillaProjectionScene = "";
    private Rect _vanillaMapLocalBounds = new(-1024f, -1024f, 2048f, 2048f);
    private Rect _vanillaTextureUv = new(0f, 0f, 1f, 1f);

    private GameObject _uiRoot;
    private GameObject _backgroundObject;
    private Image _backgroundImage;
    private RectTransform _mapRect;
    private RawImage _mapImage;
    private GameObject _markerRoot;
    private RectTransform _markerRect;
    private RawImage _markerImage;
    private readonly Texture2D[] _markerTextures = new Texture2D[6];
    private readonly List<VanillaIcon> _vanillaIcons = new();
    private readonly List<VanillaIcon> _pendingVanillaIcons = new();
    private DateTime _nextVanillaIconRefreshUtc = DateTime.MinValue;
    private long _vanillaIconSignature;

    private sealed class VanillaIcon
    {
        public GameObject Root;
        public RectTransform Rect;
        public Vector2 MapUv;
        public Vector2 MapUvSize;
    }

    public override void OnInitializeMelon()
    {
        HarmonyInstance.PatchAll();
        _settings.AddToModSettings("社区HUD地图", MenuType.Both);
        _modDirectory = Path.Combine(MelonEnvironment.ModsDirectory, "CommunityMinimap");
        _mapsDirectory = Path.Combine(_modDirectory, "maps");
        _calibrationPath = Path.Combine(_modDirectory, "calibrations.json");
        Directory.CreateDirectory(_mapsDirectory);
        CalibrationStore.Load(_calibrationPath,
            message => LoggerInstance.Msg(message),
            message => LoggerInstance.Warning(message));
        _calibrationLastWriteUtc = File.GetLastWriteTimeUtc(_calibrationPath);
        _sceneCatalogAfterUtc = DateTime.UtcNow.AddSeconds(5);
        LoggerInstance.Msg("社区HUD地图 0.6.2 initialized.");
        LoggerInstance.Msg($"Map directory: {_mapsDirectory}");
    }

    public override void OnUpdate()
    {
        TryExportSceneCatalog();
        TryReloadCalibrations();

        if (Input.GetKeyDown(_settings.ToggleKey))
            _temporarilyHidden = !_temporarilyHidden;
        bool leaveFullMap = _displayMode == DisplayMode.FullMap &&
                            Input.GetKeyDown(KeyCode.Escape);
        if (leaveFullMap)
            LeaveFullMap();
        else if (Input.GetKeyDown(_settings.MapModeKey))
            ToggleDisplayMode();
        if (Input.GetKeyDown(_settings.RecordPointKey))
            RecordCalibrationPoint();

        var scene = UnitySceneManager.GetActiveScene();
        if (scene.handle != _observedSceneHandle)
            ObserveScene(scene.handle, scene.name);

        bool preferCommunity = ShouldUseCommunityMap();
        if (_settings.MapSource != _observedMapSource ||
            preferCommunity != _observedPreferCommunity)
        {
            ApplyMapSourceSelection(scene.name, preferCommunity);
        }

        bool playerReady = GameManager.m_Instance != null &&
                           !GameManager.IsMainMenuActive() &&
                           GameManager.GetPlayerTransform() != null;

        Panel_Map vanillaPanel = null;
        bool vanillaMapOpen = TryGetOpenVanillaMap(out vanillaPanel);
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
        if (!preferCommunity && _currentDefinition != null && playerReady)
        {
            TryRequestVanillaBaseMap(scene.name);
            PollVanillaBaseMap(scene.name);
            if (!vanillaMapOpen && _textureReady)
                TryLoadVanillaElementsWithoutPanel(scene.name);
        }

        TryRefreshVanillaIcons();

        if (preferCommunity && !_textureReady && _currentDefinition != null && playerReady &&
            DateTime.UtcNow >= _loadAfterUtc)
        {
            if (LoadCurrentMapIntoUnityUi())
                _loadAfterUtc = DateTime.MaxValue;
            else
                _loadAfterUtc = DateTime.UtcNow.AddSeconds(5);
        }

        bool shouldShow = _textureReady && _currentDefinition != null && playerReady &&
                          _settings.Enabled && !_temporarilyHidden && !vanillaMapOpen;
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
        if (!sceneChanged && _currentDefinition != null)
            return;

        ClearVanillaIcons();
        _usingVanillaMap = false;
        _vanillaProjectionScene = "";
        _currentDefinition = MapCatalog.Find(sceneName);

        if (_currentDefinition == null)
        {
            _textureReady = false;
            _loadAfterUtc = DateTime.MaxValue;
            _baseMapRequestedScene = "";
            LoggerInstance.Msg($"Active scene has no map definition: {sceneName} (handle {handle}).");
            return;
        }

        bool preferCommunity = ShouldUseCommunityMap();
        _observedMapSource = _settings.MapSource;
        _observedPreferCommunity = preferCommunity;
        if (preferCommunity && _loadedMapId == _currentDefinition.Id &&
            !ReferenceEquals(_currentTexture, null))
        {
            _textureReady = true;
            _loadAfterUtc = DateTime.MaxValue;
        }
        else if (preferCommunity)
        {
            _textureReady = false;
            _loadAfterUtc = DateTime.UtcNow.AddSeconds(1);
        }
        else
        {
            _textureReady = false;
            _loadAfterUtc = DateTime.MaxValue;
        }

        _baseMapRequestedScene = "";
        _elementsLoadedForScene = "";
        _elementLoadAfterUtc = DateTime.UtcNow;
        _vanillaIconSignature = 0;
        _nextVanillaIconRefreshUtc = DateTime.MinValue;

        LoggerInstance.Msg(
            $"Active scene mapped: {sceneName} -> {_currentDefinition.DisplayName} " +
            $"({_currentDefinition.FileName}, calibrated={_currentDefinition.IsCalibrated}).");
    }

    private bool ShouldUseCommunityMap()
    {
        if (_settings.MapSource == 1)
            return true;
        if (_settings.MapSource == 2 || _currentDefinition == null)
            return false;
        return File.Exists(Path.Combine(_mapsDirectory, _currentDefinition.FileName));
    }

    private void ApplyMapSourceSelection(string sceneName, bool preferCommunity)
    {
        _observedMapSource = _settings.MapSource;
        _observedPreferCommunity = preferCommunity;
        if (preferCommunity)
        {
            ClearVanillaIcons();
            _usingVanillaMap = false;
            _vanillaProjectionScene = "";
            bool alreadyLoaded = _currentDefinition != null &&
                                 _loadedMapId == _currentDefinition.Id &&
                                 !ReferenceEquals(_currentTexture, null);
            _textureReady = alreadyLoaded;
            _loadAfterUtc = alreadyLoaded
                ? DateTime.MaxValue
                : DateTime.UtcNow;
            LoggerInstance.Msg("Map source selected: community map.");
        }
        else
        {
            bool capturedForScene = _usingVanillaMap &&
                                    string.Equals(_vanillaProjectionScene, sceneName,
                                        StringComparison.Ordinal) &&
                                    !ReferenceEquals(_currentTexture, null);
            _textureReady = capturedForScene;
            _loadAfterUtc = DateTime.MaxValue;
            LoggerInstance.Msg(capturedForScene
                ? "Map source selected: vanilla surveyed map."
                : "Map source selected: vanilla surveyed map; open the game map once to refresh it.");
        }
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

    private void ToggleDisplayMode()
    {
        _displayMode = _displayMode == DisplayMode.MiniMap
            ? DisplayMode.FullMap
            : DisplayMode.MiniMap;
        s_fullMapActive = _displayMode == DisplayMode.FullMap;
        LoggerInstance.Msg($"Map display mode: {_displayMode}.");
    }

    private void LeaveFullMap()
    {
        _displayMode = DisplayMode.MiniMap;
        s_fullMapActive = false;
        s_suppressEscapeThroughFrame = Time.frameCount + 1;
        LoggerInstance.Msg("Full map closed with Escape.");
    }

    internal static bool ShouldSuppressGameEscape()
    {
        return Input.GetKeyDown(KeyCode.Escape) &&
               (s_fullMapActive || Time.frameCount <= s_suppressEscapeThroughFrame);
    }

    private bool LoadCurrentMapIntoUnityUi()
    {
        string mapPath = Path.Combine(_mapsDirectory, _currentDefinition.FileName);
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

            Texture2D previousTexture = _currentTexture;
            _currentTexture = texture;
            _mapImage.texture = texture;
            _loadedMapId = _currentDefinition.Id;
            _textureReady = true;

            if (!ReferenceEquals(previousTexture, null))
                UnityEngine.Object.Destroy(previousTexture);

            LoggerInstance.Msg(
                $"Loaded {_currentDefinition.DisplayName}: {texture.width}x{texture.height}.");
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
        bool fullMap = _displayMode == DisplayMode.FullMap;
        _backgroundObject.SetActive(fullMap);
        _backgroundImage.color = new Color(0.015f, 0.025f, 0.035f,
            _settings.FullMapBackgroundOpacity);
        _mapImage.color = new Color(1f, 1f, 1f, fullMap ? 1f : _settings.Opacity);

        Vector2 mapSize = fullMap ? ApplyFullMapLayout() : ApplyMiniMapLayout();
        if (fullMap)
            _mapImage.uvRect = new Rect(0f, 0f, 1f, 1f);

        bool hasPosition = TryPlayerToMapUv(player.position, out Vector2 uv);
        if (!hasPosition)
        {
            if (!fullMap)
                _mapImage.uvRect = new Rect(0f, 0f, 1f, 1f);
            _markerRoot.SetActive(false);
            return;
        }

        Rect visibleUv;
        if (fullMap)
        {
            visibleUv = new Rect(0f, 0f, 1f, 1f);
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

        bool markerVisible = uv.x >= visibleUv.xMin && uv.x <= visibleUv.xMax &&
                             uv.y >= visibleUv.yMin && uv.y <= visibleUv.yMax;
        UpdateVanillaIcons(visibleUv, mapSize);
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
        bool show = _usingVanillaMap;
        for (int i = 0; i < _vanillaIcons.Count; i++)
        {
            VanillaIcon icon = _vanillaIcons[i];
            bool visible = show &&
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
            icon.Rect.sizeDelta = new Vector2(
                icon.MapUvSize.x / visibleUv.width * mapSize.x,
                icon.MapUvSize.y / visibleUv.height * mapSize.y);
        }
    }

    private bool TryPlayerToMapUv(Vector3 worldPosition, out Vector2 uv)
    {
        uv = default;
        if (!_usingVanillaMap)
            return _currentDefinition != null &&
                   _currentDefinition.TryWorldToMap(worldPosition, out uv);

        try
        {
            Panel_Map panel = InterfaceManager.GetPanel<Panel_Map>();
            if (panel == null || ReferenceEquals(_currentTexture, null))
                return false;
            Vector3 mapPosition = panel.WorldPositionToMapPosition(
                _vanillaProjectionScene, worldPosition);
            float localU = Mathf.InverseLerp(_vanillaMapLocalBounds.xMin,
                _vanillaMapLocalBounds.xMax, mapPosition.x);
            float localV = Mathf.InverseLerp(_vanillaMapLocalBounds.yMin,
                _vanillaMapLocalBounds.yMax, mapPosition.y);
            uv = new Vector2(
                _vanillaTextureUv.x + localU * _vanillaTextureUv.width,
                _vanillaTextureUv.y + localV * _vanillaTextureUv.height);
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
        if (ReferenceEquals(_currentTexture, null) || _currentTexture.height <= 0)
            return 1f;
        return (float)_currentTexture.width / _currentTexture.height;
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
                EscapeCsv(_currentDefinition?.Id ?? "unmapped"),
                EscapeCsv(_currentDefinition?.FileName ?? ""),
                _currentDefinition?.IsCalibrated == true ? "true" : "false",
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
            _vanillaMapLocalBounds = new Rect(drawing.x, drawing.y,
                drawing.z - drawing.x, drawing.w - drawing.y);
            _vanillaTextureUv = main.uvRect;
            EnsureUnityUi();
            _vanillaIconSignature = 0;
            _nextVanillaIconRefreshUtc = DateTime.MinValue;

            // The region base map normally supplies the terrain on its own. Only when it could
            // not be loaded do we fall back to this surveyed texture, which is the path that
            // requires the player to have opened the panel.
            if (!_textureReady && main.mainTexture != null)
            {
                Texture2D capturedMain = CaptureTexture(main.mainTexture);
                UseCapturedVanillaMap(capturedMain, sceneName);
            }

            LoggerInstance.Msg(
                $"Vanilla map panel refreshed from {regionMap.name}; " +
                $"bounds={_vanillaMapLocalBounds}, uv={_vanillaTextureUv}, " +
                $"widget={main.width}x{main.height}.");
            return true;
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Failed refreshing vanilla map: {ex}");
            return false;
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

        ClearVanillaIcons();
        _vanillaIcons.AddRange(_pendingVanillaIcons);
        _pendingVanillaIcons.Clear();
        _markerRoot.transform.SetAsLastSibling();
        LoggerInstance.Msg(
            $"Captured {_vanillaIcons.Count} vanilla map marker layers " +
            $"({(allowInactive ? "panel-free" : "panel-open")}).");
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
                    _pendingVanillaIcons.Add(new VanillaIcon
                    {
                        Root = iconObject,
                        Rect = rect,
                        MapUv = (minUv + maxUv) * 0.5f,
                        MapUvSize = sizeUv
                    });
                }
            }
        }

        for (int i = 0; i < transform.childCount; i++)
            CaptureVanillaIconsRecursive(transform.GetChild(i), mapElements, allowInactive);
    }

    private Vector2 VanillaLocalToTextureUv(float x, float y)
    {
        float localU = Mathf.InverseLerp(_vanillaMapLocalBounds.xMin,
            _vanillaMapLocalBounds.xMax, x);
        float localV = Mathf.InverseLerp(_vanillaMapLocalBounds.yMin,
            _vanillaMapLocalBounds.yMax, y);
        return new Vector2(
            _vanillaTextureUv.x + localU * _vanillaTextureUv.width,
            _vanillaTextureUv.y + localV * _vanillaTextureUv.height);
    }

    private void ClearVanillaIcons()
    {
        for (int i = 0; i < _vanillaIcons.Count; i++)
        {
            if (!ReferenceEquals(_vanillaIcons[i].Root, null))
                UnityEngine.Object.Destroy(_vanillaIcons[i].Root);
        }
        _vanillaIcons.Clear();
    }

    private void UseCapturedVanillaMap(Texture2D texture, string sceneName)
    {
        EnsureUnityUi();
        Texture2D previous = _currentTexture;
        _currentTexture = texture;
        _mapImage.texture = texture;
        _loadedMapId = "__vanilla__" + sceneName;
        _vanillaProjectionScene = sceneName;
        _usingVanillaMap = true;
        _textureReady = true;
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
        if (!_usingVanillaMap || DateTime.UtcNow < _nextVanillaIconRefreshUtc)
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
    private void TryRequestVanillaBaseMap(string sceneName)
    {
        if (_baseMapPending || string.Equals(_baseMapRequestedScene, sceneName, StringComparison.Ordinal))
            return;

        try
        {
            RegionSpecification region = GameManager.TryGetCurrentRegion();
            if (region == null)
                return;   // transient during scene load; try again next frame

            if (!region.HasMiniMapTexture)
            {
                // Genuinely unavailable for this region: stop asking, and let the surveyed
                // texture fallback take over if the player opens the map panel.
                _baseMapRequestedScene = sceneName;
                LoggerInstance.Warning($"Region {sceneName} reports no base map texture.");
                return;
            }

            _baseMapRequestedScene = sceneName;
            _baseMapHandle = region.GetMiniMapTextureAsync();
            _baseMapPending = true;
            _baseMapRequestUtc = DateTime.UtcNow;
            LoggerInstance.Msg($"Requested region base map for {sceneName}.");
        }
        catch (Exception ex)
        {
            _baseMapRequestedScene = sceneName;
            LoggerInstance.Warning($"Region base map request failed for {sceneName}: {ex.Message}");
        }
    }

    private void PollVanillaBaseMap(string sceneName)
    {
        if (!_baseMapPending)
            return;

        // Never leave the HUD permanently blank if the load silently stalls.
        if ((DateTime.UtcNow - _baseMapRequestUtc).TotalSeconds > 15.0)
        {
            _baseMapPending = false;
            LoggerInstance.Warning($"Region base map for {sceneName} timed out.");
            return;
        }

        try
        {
            if (!_baseMapHandle.IsDone)
                return;
        }
        catch (Exception ex)
        {
            _baseMapPending = false;
            LoggerInstance.Warning($"Region base map handle failed: {ex.Message}");
            return;
        }

        _baseMapPending = false;
        try
        {
            Texture2D source = _baseMapHandle.Result;
            if (source == null)
            {
                LoggerInstance.Warning($"Region base map for {sceneName} resolved to NULL.");
                return;
            }

            // The loaded asset is not CPU-readable and is owned by Addressables, so keep an
            // owned copy instead of handing the asset itself to the HUD.
            Texture2D owned = CaptureTexture(source);
            UseVanillaBaseMap(owned, sceneName);
            LoggerInstance.Msg(
                $"Vanilla base map active for {sceneName}: {owned.width}x{owned.height} " +
                "(no map panel needed).");

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

    // Panel_Map exposes public entry points that build the marker objects. Calling them with
    // the panel closed populates the same containers the open panel would, which lets markers
    // appear without the player opening the map.
    private void TryLoadVanillaElementsWithoutPanel(string sceneName)
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

    private void UseVanillaBaseMap(Texture2D texture, string sceneName)    {
        EnsureUnityUi();
        Texture2D previous = _currentTexture;
        _currentTexture = texture;
        _mapImage.texture = texture;
        _loadedMapId = "__basemap__" + sceneName;
        _vanillaProjectionScene = sceneName;
        // The base map shares the surveyed map's framing, verified by direct overlay of the
        // two textures, so the same projection bounds apply.
        _vanillaMapLocalBounds = new Rect(-325f, -325f, 650f, 650f);
        _vanillaTextureUv = new Rect(0f, 0f, 1f, 1f);
        _usingVanillaMap = true;
        _textureReady = true;
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

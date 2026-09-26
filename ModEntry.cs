using System;
using System.Globalization;
using System.IO;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using MelonLoader.Utils;
using ModSettings;
using UnityEngine;
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
    private DisplayMode _displayMode = DisplayMode.MiniMap;
    private MapDefinition _currentDefinition;
    private string _loadedMapId = "";
    private Texture2D _currentTexture;

    private GameObject _uiRoot;
    private GameObject _backgroundObject;
    private Image _backgroundImage;
    private RectTransform _mapRect;
    private RawImage _mapImage;
    private GameObject _markerRoot;
    private RectTransform _markerRect;
    private RawImage _markerImage;
    private readonly Texture2D[] _markerTextures = new Texture2D[6];

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
        LoggerInstance.Msg("社区HUD地图 0.5.2 initialized.");
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

        bool playerReady = GameManager.m_Instance != null &&
                           !GameManager.IsMainMenuActive() &&
                           GameManager.GetPlayerTransform() != null;

        if (!_textureReady && _currentDefinition != null && playerReady &&
            DateTime.UtcNow >= _loadAfterUtc)
        {
            if (LoadCurrentMapIntoUnityUi())
                _loadAfterUtc = DateTime.MaxValue;
            else
                _loadAfterUtc = DateTime.UtcNow.AddSeconds(5);
        }

        bool shouldShow = _textureReady && _currentDefinition != null && playerReady &&
                          _settings.Enabled && !_temporarilyHidden;
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
        _observedSceneHandle = handle;
        SetUiVisible(false);
        _currentDefinition = MapCatalog.Find(sceneName);

        if (_currentDefinition == null)
        {
            _textureReady = false;
            _loadAfterUtc = DateTime.MaxValue;
            LoggerInstance.Msg($"Active scene has no map definition: {sceneName} (handle {handle}).");
            return;
        }

        if (_loadedMapId == _currentDefinition.Id && !ReferenceEquals(_currentTexture, null))
        {
            _textureReady = true;
            _loadAfterUtc = DateTime.MaxValue;
        }
        else
        {
            _textureReady = false;
            _loadAfterUtc = DateTime.UtcNow.AddSeconds(1);
        }

        LoggerInstance.Msg(
            $"Active scene mapped: {sceneName} -> {_currentDefinition.DisplayName} " +
            $"({_currentDefinition.FileName}, calibrated={_currentDefinition.IsCalibrated}).");
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
            typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
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

        bool hasPosition = _currentDefinition.TryWorldToMap(player.position, out Vector2 uv);
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

        _currentDefinition.TryWorldToMap(player.position + player.forward * 2f, out Vector2 aheadUv);
        float angle = Mathf.Atan2(aheadUv.x - uv.x, aheadUv.y - uv.y) * Mathf.Rad2Deg;
        _markerRect.localEulerAngles = new Vector3(0f, 0f, -angle);
        _markerRoot.SetActive(true);
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
            LoggerInstance.Msg(
                $"Calibration point recorded: {sceneName} " +
                $"({position.x:F3}, {position.y:F3}, {position.z:F3}), capture={captureId}.");
        }
        catch (Exception ex)
        {
            LoggerInstance.Error($"Failed recording calibration point: {ex}");
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

    private static string SanitizeFileName(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
            value = value.Replace(invalid, '_');
        return string.IsNullOrWhiteSpace(value) ? "unknown_scene" : value;
    }

    private static string EscapeCsv(string value) => '"' + value.Replace("\"", "\"\"") + '"';
}

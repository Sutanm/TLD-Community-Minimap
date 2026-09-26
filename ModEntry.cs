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
    private enum DisplayMode
    {
        MiniMap,
        FullMap
    }

    private readonly MinimapSettings _settings = new();
    private string _modDirectory = "";
    private string _mapsDirectory = "";
    private bool _temporarilyHidden;
    private bool _textureReady;
    private bool _uiVisible;
    private int _observedSceneHandle = int.MinValue;
    private DateTime _loadAfterUtc = DateTime.MaxValue;
    private DisplayMode _displayMode = DisplayMode.MiniMap;
    private MapDefinition _currentDefinition;
    private string _loadedMapId = "";
    private Texture2D _currentTexture;
    private Rect _fullMapUv = new(0f, 0f, 1f, 1f);
    private bool _dragging;
    private Vector3 _lastMousePosition;

    private GameObject _uiRoot;
    private GameObject _backgroundObject;
    private RectTransform _mapRect;
    private RawImage _mapImage;
    private GameObject _markerRoot;
    private RectTransform _markerRect;

    public override void OnInitializeMelon()
    {
        _settings.AddToModSettings("Community Minimap / 民间小地图", MenuType.Both);
        _modDirectory = Path.Combine(MelonEnvironment.ModsDirectory, "CommunityMinimap");
        _mapsDirectory = Path.Combine(_modDirectory, "maps");
        Directory.CreateDirectory(_mapsDirectory);
        LoggerInstance.Msg("Community Minimap 0.4.0 initialized.");
        LoggerInstance.Msg($"Map directory: {_mapsDirectory}");
    }

    public override void OnUpdate()
    {
        if (Input.GetKeyDown(_settings.ToggleKey))
            _temporarilyHidden = !_temporarilyHidden;
        if (Input.GetKeyDown(_settings.MapModeKey))
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

        if (_displayMode == DisplayMode.FullMap)
            UpdateFullMapInput();
        UpdateUnityUi(GameManager.GetPlayerTransform());
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
        _fullMapUv = new Rect(0f, 0f, 1f, 1f);
        _dragging = false;

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
        _fullMapUv = new Rect(0f, 0f, 1f, 1f);
        _dragging = false;
        LoggerInstance.Msg($"Map display mode: {_displayMode}.");
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
            _fullMapUv = new Rect(0f, 0f, 1f, 1f);

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
        Image background = _backgroundObject.GetComponent<Image>();
        background.color = new Color(0.015f, 0.025f, 0.035f, 0.97f);
        background.raycastTarget = false;

        GameObject mapObject = CreateUiObject("Map",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        mapObject.transform.SetParent(_uiRoot.transform, false);
        _mapRect = mapObject.GetComponent<RectTransform>();
        _mapImage = mapObject.GetComponent<RawImage>();
        _mapImage.raycastTarget = false;

        _markerRoot = CreateUiObject("PlayerArrow", typeof(RectTransform));
        _markerRoot.transform.SetParent(mapObject.transform, false);
        _markerRect = _markerRoot.GetComponent<RectTransform>();
        _markerRect.anchorMin = new Vector2(0.5f, 0.5f);
        _markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        _markerRect.pivot = new Vector2(0.5f, 0.5f);

        CreateArrowPart("Shaft", _markerRoot.transform,
            new Vector2(0f, -2f), new Vector2(3f, 13f), 0f);
        CreateArrowPart("HeadLeft", _markerRoot.transform,
            new Vector2(-3.5f, 4.5f), new Vector2(3f, 10f), -45f);
        CreateArrowPart("HeadRight", _markerRoot.transform,
            new Vector2(3.5f, 4.5f), new Vector2(3f, 10f), 45f);

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

    private static void CreateArrowPart(
        string name, Transform parent, Vector2 position, Vector2 size, float rotation)
    {
        GameObject part = CreateUiObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        part.transform.SetParent(parent, false);
        RectTransform rect = part.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localEulerAngles = new Vector3(0f, 0f, rotation);
        Image image = part.GetComponent<Image>();
        image.color = new Color(1f, 0.05f, 0.02f, 1f);
        image.raycastTarget = false;
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
        _mapImage.color = new Color(1f, 1f, 1f, fullMap ? 1f : _settings.Opacity);

        Vector2 mapSize = fullMap ? ApplyFullMapLayout() : ApplyMiniMapLayout();
        if (fullMap)
            _mapImage.uvRect = _fullMapUv;

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
            visibleUv = _fullMapUv;
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
        float markerSize = _settings.MarkerSize * (fullMap ? 1.2f : 1f);
        _markerRect.sizeDelta = new Vector2(markerSize * 1.2f, markerSize * 1.2f);

        _currentDefinition.TryWorldToMap(player.position + player.forward * 2f, out Vector2 aheadUv);
        float angle = Mathf.Atan2(aheadUv.x - uv.x, aheadUv.y - uv.y) * Mathf.Rad2Deg;
        _markerRect.localEulerAngles = new Vector3(0f, 0f, -angle);
        _markerRoot.SetActive(true);
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
        switch (_settings.Position)
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

    private void UpdateFullMapInput()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            float oldSpan = _fullMapUv.width;
            float newSpan = Mathf.Clamp(oldSpan * Mathf.Pow(0.82f, scroll), 0.10f, 1f);
            Vector2 center = _fullMapUv.center;
            _fullMapUv.width = newSpan;
            _fullMapUv.height = newSpan;
            _fullMapUv.center = center;
            ClampFullMapUv();
        }

        if (Input.GetMouseButtonDown(0))
        {
            _dragging = true;
            _lastMousePosition = Input.mousePosition;
        }
        if (Input.GetMouseButtonUp(0))
            _dragging = false;
        if (!_dragging || !Input.GetMouseButton(0))
            return;

        Vector3 mousePosition = Input.mousePosition;
        Vector3 delta = mousePosition - _lastMousePosition;
        _lastMousePosition = mousePosition;
        float width = Mathf.Max(1f, _mapRect.rect.width);
        float height = Mathf.Max(1f, _mapRect.rect.height);
        _fullMapUv.x -= delta.x / width * _fullMapUv.width;
        _fullMapUv.y -= delta.y / height * _fullMapUv.height;
        ClampFullMapUv();
    }

    private void ClampFullMapUv()
    {
        _fullMapUv.x = Mathf.Clamp(_fullMapUv.x, 0f, 1f - _fullMapUv.width);
        _fullMapUv.y = Mathf.Clamp(_fullMapUv.y, 0f, 1f - _fullMapUv.height);
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
            string sceneName = UnitySceneManager.GetActiveScene().name;
            Vector3 position = player.position;
            float heading = player.eulerAngles.y;
            string path = Path.Combine(_modDirectory, "calibration_points.csv");
            if (!File.Exists(path))
                File.AppendAllText(path, "timestamp,scene,map_id,x,y,z,heading,note\r\n");
            string line = string.Join(",",
                DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                EscapeCsv(sceneName),
                EscapeCsv(_currentDefinition?.Id ?? "unmapped"),
                position.x.ToString("F3", CultureInfo.InvariantCulture),
                position.y.ToString("F3", CultureInfo.InvariantCulture),
                position.z.ToString("F3", CultureInfo.InvariantCulture),
                heading.ToString("F2", CultureInfo.InvariantCulture),
                "填写地标名称");
            File.AppendAllText(path, line + "\r\n");
            LoggerInstance.Msg($"Calibration point recorded: {line}");
        }
        catch (Exception ex)
        {
            LoggerInstance.Error($"Failed recording calibration point: {ex}");
        }
    }

    private static string EscapeCsv(string value) => '"' + value.Replace("\"", "\"\"") + '"';
}

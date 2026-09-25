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
    private const string SupportedScene = "LakeRegion";
    // Global base transform established from the long south-railway run. Around the
    // Forlorn Muskeg entrance the community map is approximately one pixel per metre:
    // world +X points right and world +Z points toward the top of the map.
    private const float MapWidth = 2048f;
    private const float MapHeight = 2028f;
    private const float MapOffsetX = 190f;
    private const float MapOffsetZ = 244f;
    private const float SouthCarX = 783.618f;
    private const float SouthCarZ = -49.576f;
    private const float SouthCorrectionRadius = 500f;
    private const float SouthCorrectionPixelsX = -6f;
    private const float SouthCorrectionPixelsY = -54f;

    private readonly MinimapSettings _settings = new();
    private string _modDirectory = "";
    private string _mapPath = "";
    private bool _temporarilyHidden;
    private bool _textureReady;
    private bool _uiVisible;
    private int _observedSceneHandle = int.MinValue;
    private DateTime _supportedSceneSinceUtc = DateTime.MinValue;

    private GameObject _uiRoot;
    private RectTransform _mapRect;
    private RawImage _mapImage;
    private GameObject _markerRoot;
    private RectTransform _markerRect;

    public override void OnInitializeMelon()
    {
        _settings.AddToModSettings("Community Minimap / 民间小地图", MenuType.Both);
        _modDirectory = Path.Combine(MelonEnvironment.ModsDirectory, "CommunityMinimap");
        _mapPath = Path.Combine(_modDirectory, "maps", "神秘湖.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(_mapPath)!);
        LoggerInstance.Msg("Community Minimap uGUI prototype initialized.");
        LoggerInstance.Msg($"Map asset: {_mapPath}");
    }

    public override void OnUpdate()
    {
        if (Input.GetKeyDown(_settings.ToggleKey))
            _temporarilyHidden = !_temporarilyHidden;
        if (Input.GetKeyDown(_settings.RecordPointKey))
            RecordCalibrationPoint();

        var scene = UnitySceneManager.GetActiveScene();
        if (scene.handle != _observedSceneHandle)
        {
            _observedSceneHandle = scene.handle;
            _supportedSceneSinceUtc = string.Equals(scene.name, SupportedScene, StringComparison.OrdinalIgnoreCase)
                ? DateTime.UtcNow
                : DateTime.MinValue;
            SetUiVisible(false);
            LoggerInstance.Msg($"Active scene detected: {scene.name} (handle {scene.handle}).");
        }

        bool inSupportedScene = string.Equals(scene.name, SupportedScene, StringComparison.OrdinalIgnoreCase);
        bool playerReady = GameManager.m_Instance != null &&
                           !GameManager.IsMainMenuActive() &&
                           GameManager.GetPlayerTransform() != null;

        if (!_textureReady && inSupportedScene && playerReady &&
            _supportedSceneSinceUtc != DateTime.MinValue &&
            (DateTime.UtcNow - _supportedSceneSinceUtc).TotalSeconds >= 1.0)
        {
            if (LoadMapIntoUnityUi())
                _supportedSceneSinceUtc = DateTime.MinValue;
        }

        bool shouldShow = _textureReady && inSupportedScene && playerReady &&
                          _settings.Enabled && !_temporarilyHidden;
        SetUiVisible(shouldShow);
        if (shouldShow)
            UpdateUnityUi(GameManager.GetPlayerTransform());
    }

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
        SetUiVisible(false);
        _observedSceneHandle = int.MinValue;
        LoggerInstance.Msg($"Scene initialized callback: {sceneName}.");
    }

    private bool LoadMapIntoUnityUi()
    {
        if (!File.Exists(_mapPath))
        {
            LoggerInstance.Warning($"Map image not found: {_mapPath}");
            return false;
        }

        try
        {
            EnsureUnityUi();
            byte[] bytes = File.ReadAllBytes(_mapPath);
            var il2CppBytes = new Il2CppStructArray<byte>(bytes);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, il2CppBytes, true))
                throw new InvalidOperationException("Unity ImageConversion.LoadImage returned false.");

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.hideFlags = HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
            UnityEngine.Object.DontDestroyOnLoad(texture);

            _mapImage.texture = texture;
            _textureReady = true;
            LoggerInstance.Msg($"Loaded map texture into persistent RawImage: {texture.width}x{texture.height}.");
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

        _uiRoot = CreateUiObject("CommunityMinimapCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        UnityEngine.Object.DontDestroyOnLoad(_uiRoot);
        var canvas = _uiRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2000;
        var scaler = _uiRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        GameObject mapObject = CreateUiObject("Map", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
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

        CreateArrowPart("Shaft", _markerRoot.transform, new Vector2(0f, -2f), new Vector2(3f, 13f), 0f);
        CreateArrowPart("HeadLeft", _markerRoot.transform, new Vector2(-3.5f, 4.5f), new Vector2(3f, 10f), -45f);
        CreateArrowPart("HeadRight", _markerRoot.transform, new Vector2(3.5f, 4.5f), new Vector2(3f, 10f), 45f);

        _uiRoot.SetActive(false);
        LoggerInstance.Msg("Created persistent Canvas/RawImage minimap UI.");
    }

    private static GameObject CreateUiObject(string name, params Type[] componentTypes)
    {
        var il2CppTypes = new Il2CppReferenceArray<Il2CppSystem.Type>(componentTypes.Length);
        for (int i = 0; i < componentTypes.Length; i++)
            il2CppTypes[i] = Il2CppType.From(componentTypes[i]);
        return new GameObject(name, il2CppTypes);
    }

    private static void CreateArrowPart(string name, Transform parent, Vector2 position, Vector2 size, float rotation)
    {
        GameObject part = CreateUiObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
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
        ApplyLayout();
        _mapImage.color = new Color(1f, 1f, 1f, _settings.Opacity);

        bool local = TryGetLocalMapPosition(player, out float u, out float v);
        if (!local)
        {
            _mapImage.uvRect = new Rect(0f, 0f, 1f, 1f);
            _markerRoot.SetActive(false);
            return;
        }

        float span = 1f / _settings.Zoom;
        float half = span * 0.5f;
        float centerU = Mathf.Clamp(u, half, 1f - half);
        float centerV = Mathf.Clamp(v, half, 1f - half);
        Rect uv = new Rect(centerU - half, centerV - half, span, span);
        _mapImage.uvRect = uv;

        float size = _settings.Size;
        _markerRect.anchoredPosition = new Vector2(
            ((u - uv.x) / uv.width - 0.5f) * size,
            ((v - uv.y) / uv.height - 0.5f) * size);
        float markerSize = _settings.MarkerSize;
        _markerRect.sizeDelta = new Vector2(markerSize * 1.2f, markerSize * 1.2f);

        Vector3 forward = player.forward;
        WorldToMap(player.position + forward * 2f, out float aheadU, out float aheadV);
        float mapDu = aheadU - u;
        float mapDv = aheadV - v;
        float angle = Mathf.Atan2(mapDu, mapDv) * Mathf.Rad2Deg;
        _markerRect.localEulerAngles = new Vector3(0f, 0f, -angle);
        _markerRoot.SetActive(true);
    }

    private void ApplyLayout()
    {
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
        _mapRect.sizeDelta = new Vector2(_settings.Size, _settings.Size);
    }

    private bool TryGetLocalMapPosition(Transform player, out float u, out float v)
    {
        u = 0f;
        v = 0f;
        if (player == null)
            return false;
        Vector3 pos = player.position;
        WorldToMap(pos, out u, out v);
        return u >= 0f && u <= 1f && v >= 0f && v <= 1f;
    }

    private static void WorldToMap(Vector3 pos, out float u, out float v)
    {
        float dx = pos.x - SouthCarX;
        float dz = pos.z - SouthCarZ;
        float normalizedDistance = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dz * dz) / SouthCorrectionRadius);
        float southWeight = 1f - Mathf.SmoothStep(0f, 1f, normalizedDistance);

        float pixelX = pos.x + MapOffsetX + SouthCorrectionPixelsX * southWeight;
        float pixelYFromTop = (MapHeight - MapOffsetZ) - pos.z + SouthCorrectionPixelsY * southWeight;
        u = pixelX / MapWidth;
        v = 1f - pixelYFromTop / MapHeight;
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
                File.AppendAllText(path, "timestamp,scene,x,y,z,heading,note\r\n");
            string line = string.Join(",",
                DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                EscapeCsv(sceneName),
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

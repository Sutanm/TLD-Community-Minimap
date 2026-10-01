// 社区HUD地图 · sutanm — 渲染：Canvas/RawImage、指针、投影换算、通用工具
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

        bool hasPosition = TryPlayerToMapUv(player.position, out Vector2 uv);
        if (fullMap && !_fullMapCenterValid)
        {
            bool followPlayer = hasPosition && _settings.ShowFullMapPlayerPointer;
            _fullMapCenter = followPlayer ? uv : new Vector2(0.5f, 0.5f);
            // Use one predictable screen-filling scale. Near a map edge ApplyFullMapLayout clamps
            // the focus instead of magnifying until the player can sit exactly at screen centre;
            // that keeps enough surrounding geography visible to make the full map useful.
            _fullMapZoom = FullMapCoverZoom();
            _fullMapCenterValid = true;
        }

        Vector2 mapSize = fullMap ? ApplyFullMapLayout() : ApplyMiniMapLayout();

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
            // Labels too: without a projection their positions are meaningless and leaving them on
            // would strand text over a map that is not being drawn.
            for (int i = 0; i < _mapLabels.Count; i++)
            {
                if (_mapLabels[i].Root.activeSelf)
                    _mapLabels[i].Root.SetActive(false);
            }
            return;
        }


        Rect visibleUv;
        if (fullMap)
        {
            // The full texture stays intact and the parchment RectTransform itself grows. This is
            // what lets a square original map fill a widescreen display without distorting it.
            _mapImage.uvRect = new Rect(0f, 0f, 1f, 1f);
            float halfVisibleX = Mathf.Min(0.5f,
                Screen.width * 0.5f / Mathf.Max(1f, mapSize.x));
            float halfVisibleY = Mathf.Min(0.5f,
                Screen.height * 0.5f / Mathf.Max(1f, mapSize.y));
            visibleUv = new Rect(
                _fullMapCenter.x - halfVisibleX,
                _fullMapCenter.y - halfVisibleY,
                halfVisibleX * 2f,
                halfVisibleY * 2f);
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
        if (!hasPosition || (fullMap && !_settings.ShowFullMapPlayerPointer))
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

        _markerRect.anchoredPosition = MapUvToLocal(uv, visibleUv, mapSize);
        float markerSize = Mathf.Max(44f, _settings.MarkerSize) * (fullMap ? 1.15f : 1f);
        _markerRect.sizeDelta = new Vector2(markerSize, markerSize);
        ApplyPointerPalette();

        TryPlayerToMapUv(player.position + player.forward * 2f, out Vector2 aheadUv);
        float angle = Mathf.Atan2(aheadUv.x - uv.x, aheadUv.y - uv.y) * Mathf.Rad2Deg;
        _markerRect.localEulerAngles = new Vector3(0f, 0f, -angle);
        _markerRoot.SetActive(true);
    }


    private Vector2 MapUvToLocal(Vector2 uv, Rect visibleUv, Vector2 mapSize)
    {
        if (FullMapVisible)
            return new Vector2((uv.x - 0.5f) * mapSize.x, (uv.y - 0.5f) * mapSize.y);
        return new Vector2(
            ((uv.x - visibleUv.x) / visibleUv.width - 0.5f) * mapSize.x,
            ((uv.y - visibleUv.y) / visibleUv.height - 0.5f) * mapSize.y);
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


    private float GetTextureAspect()
    {
        Texture2D texture = ActiveLayer.Texture;
        if (ReferenceEquals(texture, null) || texture.height <= 0)
            return 1f;
        return (float)texture.width / texture.height;
    }


    private Vector2 VanillaLocalToTextureUv(float x, float y) =>
        VanillaMapPositionToUv(new Vector3(x, y, 0f), ActiveLayer.VanillaMapLocalBounds, ActiveLayer.VanillaTextureUv);


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

        // NGUI draws its text through UILabel, not UnityEngine.UI.Text, which is why searching for a
        // Text component on the panel found nothing. The text itself is the whole point here, so the
        // dump carries the string rather than just the component type.
        UILabel uiLabel = transform.GetComponent<UILabel>();
        if (uiLabel != null)
        {
            output.Append(" | UILabel='").Append(uiLabel.text).Append('\'');
        }

        Text uguiLabel = transform.GetComponent<Text>();
        if (uguiLabel != null)
        {
            output.Append(" | Text='").Append(uguiLabel.text).Append('\'');
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


    private static string SanitizeFileName(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
            value = value.Replace(invalid, '_');
        return string.IsNullOrWhiteSpace(value) ? "unknown_scene" : value;
    }


    private static string EscapeCsv(string value) => '"' + value.Replace("\"", "\"\"") + '"';
}
// — sutanm · 社区HUD地图

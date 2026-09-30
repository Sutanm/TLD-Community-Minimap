// 社区HUD地图 · sutanm — 标记：从 MapDetail 构建、分类筛选、绘制
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


    // Re-reads the category switches onto the marker set when any of them changed.
    //
    // Called from both the draw loop and the hover pick rather than only the draw loop, because the
    // draw loop can return early - an uncalibrated region, or a corner map with no projection - and a
    // stale flag there would let a hidden category still answer a hover, which reads as a bug.
    private void RefreshMarkerCategoryFlags()
    {
        int state = _settings.MarkerCategoryState();
        if (state == _markerCategoryState)
            return;
        _markerCategoryState = state;
        for (int i = 0; i < _vanillaIcons.Count; i++)
            _vanillaIcons[i].CategoryEnabled = CategoryEnabled(_vanillaIcons[i].Category);
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
        RefreshMarkerCategoryFlags();

        for (int i = 0; i < _vanillaIcons.Count; i++)
        {
            VanillaIcon icon = _vanillaIcons[i];
            bool visible = show && icon.CategoryEnabled &&
                           icon.MapUv.x + icon.MapUvSize.x * 0.5f >= visibleUv.xMin &&
                           icon.MapUv.x - icon.MapUvSize.x * 0.5f <= visibleUv.xMax &&
                           icon.MapUv.y + icon.MapUvSize.y * 0.5f >= visibleUv.yMin &&
                           icon.MapUv.y - icon.MapUvSize.y * 0.5f <= visibleUv.yMax;
            if (icon.Root.activeSelf != visible)
                icon.Root.SetActive(visible);
            if (!visible)
                continue;

            // Every RectTransform write marks the layout dirty, and this loop runs over the whole
            // marker set each frame - hundreds of entries - so a value is only written when it
            // actually changed. Writing unconditionally made dragging the map feel a beat behind,
            // because each frame queued a rebuild of the whole icon layer.
            var position = new Vector2(
                ((icon.MapUv.x - visibleUv.x) / visibleUv.width - 0.5f) * mapSize.x,
                ((icon.MapUv.y - visibleUv.y) / visibleUv.height - 0.5f) * mapSize.y);
            if (icon.Rect.anchoredPosition != position)
                icon.Rect.anchoredPosition = position;

            float markerScale = _settings.MarkerIconSize / Mathf.Max(1e-6f, _vanillaIconMaxUv);
            float ratioX = Mathf.Clamp(icon.MapUvSize.x * markerScale / _settings.MarkerIconSize,
                0.6f, 1.8f);
            float ratioY = Mathf.Clamp(icon.MapUvSize.y * markerScale / _settings.MarkerIconSize,
                0.6f, 1.8f);
            var size = new Vector2(
                ratioX * _settings.MarkerIconSize,
                ratioY * _settings.MarkerIconSize);
            if (icon.Rect.sizeDelta != size)
                icon.Rect.sizeDelta = size;
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
                VanillaIcon built = BuildMarkerIcon(icon, mapUv, uvSize, MarkerColourForActiveLayer());
                built.Category = category;
                built.CategoryEnabled = CategoryEnabled(category);
                // The name the game shows when the pointer rests on this icon. Resolved once here
                // rather than on hover, because hover runs every frame and this walks a localization
                // lookup.
                built.Text = !string.IsNullOrEmpty(detail.m_CustomName)
                    ? detail.m_CustomName
                    : LocalizeLabelCached(detail.m_LocID);
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


    private void ClearVanillaIcons()
    {
        for (int i = 0; i < _vanillaIcons.Count; i++)
        {
            if (!ReferenceEquals(_vanillaIcons[i].Root, null))
                UnityEngine.Object.Destroy(_vanillaIcons[i].Root);
        }
        _vanillaIcons.Clear();
    }


    // Builds the marker set and the place names for one scene, and records which build is current.
    // The colour the marker atlas is drawn in.
    //
    // The atlas itself is white artwork: the game tints it when it draws its own map, which is why
    // the panel's icons come out dark brown while ours came out white. On the vanilla map - pale
    // parchment crossed by dark lines - white icons are nearly invisible, which is exactly what was
    // reported. The default therefore follows the layer: white over the community map, whose art is
    // busy and mid-toned, and dark over the pale vanilla map.
    private Color MarkerColourForActiveLayer()
    {
        return _settings.MarkerTint switch
        {
            1 => Color.white,
            2 => new Color(0.12f, 0.10f, 0.09f, 1f),          // dark, for the pale vanilla map
            3 => new Color(0.35f, 0.16f, 0.10f, 1f),          // the brown the game's own map uses
            _ => ActiveLayer.UsingVanilla
                ? new Color(0.12f, 0.10f, 0.09f, 1f)
                : Color.white,
        };
    }

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
}
// — sutanm · 社区HUD地图

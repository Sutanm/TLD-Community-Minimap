// 社区HUD地图 · sutanm — 地名与悬停：标签构建、本地化、悬停提示
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

    private string LocalizeLabelCached(string locId)
    {
        if (string.IsNullOrEmpty(locId))
            return "";
        if (_localizedCache.TryGetValue(locId, out string cached))
            return cached;
        string resolved = LocalizeLabel(locId);
        _localizedCache[locId] = resolved;
        return resolved;
    }


    // Turns a localization key such as GAMEPLAY_mtTownCentre into display text.
    //
    // The member that does this was found by scanning, not by naming it: see
    // ResolveLocalizationMembers for what the scan produced and why.
    private string LocalizeLabel(string locId)
    {
        if (string.IsNullOrEmpty(locId))
            return "";

        ResolveLocalizationMembers();
        if (s_localizationGet == null)
            return "";

        try
        {
            string result = s_localizationGet.Invoke(null, new object[] { locId }) as string;
            // A key that does not resolve comes back as the key itself, which is worse than nothing
            // on the map, so treat that as a miss.
            if (!string.IsNullOrEmpty(result) && !string.Equals(result, locId, StringComparison.Ordinal))
                return result;

            // The fallback language answers in English when the chosen language has no entry, which
            // still beats showing the raw key.
            if (s_localizationFallback != null)
            {
                string fallback = s_localizationFallback.Invoke(null, new object[] { locId }) as string;
                if (!string.IsNullOrEmpty(fallback) &&
                    !string.Equals(fallback, locId, StringComparison.Ordinal))
                    return fallback;
            }
        }
        catch
        {
            // The method exists but rejected the call; the label is simply omitted.
        }
        return "";
    }


    // Reads the place names straight off the game's own map objects.
    //
    // The localization class has three GetText overloads and none of them translates these keys, so
    // the lookup route is a dead end - but the panel already shows the names, which means it has
    // already resolved them into text. The panel is NGUI, so that text lives on UILabel components
    // rather than UnityEngine.UI.Text; searching for the latter is why the first attempt found
    // nothing.
    private void CapturePanelLabelTexts()
    {
        try
        {
            Panel_Map panel = InterfaceManager.GetPanel<Panel_Map>();
            if (panel == null)
                return;

            Transform mapElements = FindChildByName(panel.transform, "MapElements");
            if (mapElements == null)
                return;

            var labels = mapElements.GetComponentsInChildren<UILabel>(true);
            if (labels == null || labels.Length == 0)
            {
                LoggerInstance.Msg("Panel label text: MapElements has no UILabel components.");
                return;
            }

            int withText = 0;
            var sample = new List<string>();
            for (int i = 0; i < labels.Length; i++)
            {
                UILabel label = labels[i];
                if (ReferenceEquals(label, null) || string.IsNullOrEmpty(label.text))
                    continue;
                withText++;
                if (sample.Count < 10)
                    sample.Add($"'{label.text}'");
            }

            LoggerInstance.Msg($"Panel label text: {withText} non-empty of {labels.Length} UILabel " +
                $"components. Sample: {string.Join(", ", sample)}");
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Panel label text probe failed: {ex.Message}");
        }
    }


    private static void ResolveLocalizationMembers()
    {
        if (s_localizationResolved)
            return;
        s_localizationResolved = true;

        try
        {
            Type type = null;
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { type = assembly.GetType("Il2Cpp.Localization"); } catch { }
                if (type != null)
                    break;
            }
            if (type == null)
                return;

            s_localizationGet = type.GetMethod("Get",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null, new[] { typeof(string) }, null);
            s_localizationFallback = type.GetMethod("GetForFallbackLanguage",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null, new[] { typeof(string) }, null);
        }
        catch
        {
            s_localizationGet = null;
            s_localizationFallback = null;
        }
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

            // The members that resolve a location key were found by scanning every static
            // (string)->string candidate and testing it; see ResolveLocalizationMembers.
            ResolveLocalizationMembers();

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
                    text = LocalizeLabelCached(detail.m_LocID);
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
                label.fontSize = _settings.LabelFontSize;
                label.alignment = TextAnchor.MiddleCenter;
                // Dark text, because the map art is light; a light outline would be needed on the
                // dark community map, but this layer only exists on the vanilla source.
                label.color = new Color(0.10f, 0.08f, 0.06f, 0.92f);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.raycastTarget = false;
                // Deliberately no Outline here. Outline duplicates every glyph four times, and a
                // canvas full of them multiplies the vertex count until the UI rebuilds stall - which
                // is exactly what happened when it was added. A Shadow is one extra copy and gives
                // most of the legibility for a fifth of the cost.
                AddTextShadow(labelObject.GetComponent<RectTransform>());

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
                $"(localization: {(s_localizationGet != null ? "found" : "missing")}).");
            // How many markers ended up with a hover name, reported separately from the place names
            // because they are resolved by the same lookup but used by a different feature.
            int named = 0;
            for (int i = 0; i < _vanillaIcons.Count; i++)
            {
                if (!string.IsNullOrEmpty(_vanillaIcons[i].Text))
                    named++;
            }
            LoggerInstance.Msg($"Marker hover names: {named} of {_vanillaIcons.Count}.");
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
    // Positions each label for the visible window, and decides which of them to show.
    //
    // In always-on mode every label inside the window is drawn, which is what the user saw - including
    // names the game only shows in story mode. In hover mode nothing is drawn until the pointer is
    // near something, matching what the game's own map does: one name at a time, under the cursor.
    private void UpdateMapLabels(Rect visibleUv, Vector2 mapSize)
    {
        bool show = _settings.ShowMapLabels && (ActiveLayer.UsingVanilla || _settings.MarkersOnCommunityMap);
        if (!show)
        {
            for (int i = 0; i < _mapLabels.Count; i++)
                _mapLabels[i].Root.SetActive(false);
            _hoverLabelRoot?.SetActive(false);
            return;
        }

        // Font size is a setting and the label set is not rebuilt for it, so it is pushed onto the
        // existing labels when it changes.
        if (_appliedLabelFontSize != _settings.LabelFontSize)
        {
            _appliedLabelFontSize = _settings.LabelFontSize;
            for (int i = 0; i < _mapLabels.Count; i++)
            {
                Text existing = _mapLabels[i].Root.GetComponent<Text>();
                if (!ReferenceEquals(existing, null))
                    existing.fontSize = _appliedLabelFontSize;
            }
        }

        // The pointer only means something while the cursor is actually free to move over the map, and
        // only the full map takes the cursor. On the corner map the labels still need placing, which is
        // why the hover work is gated here rather than the whole method being skipped.
        bool hoverMode = _settings.HoverMapLabels && FullMapVisible &&
                         _settings.ReleaseMouseOnFullMap;
        Vector2 hoverUv = default;
        bool haveHover = false;
        if (hoverMode)
        {
            // Invert the projection UpdateUnityUi used: anchored position measured from the map
            // centre, as a fraction of the visible window.
            Vector2 mapScreenCenter = new(
                Screen.width * 0.5f + _mapRect.anchoredPosition.x,
                Screen.height * 0.5f + _mapRect.anchoredPosition.y);
            Vector2 local = new(
                (Input.mousePosition.x - mapScreenCenter.x) / Mathf.Max(1f, mapSize.x),
                (Input.mousePosition.y - mapScreenCenter.y) / Mathf.Max(1f, mapSize.y));
            hoverUv = FullMapVisible
                ? new Vector2(local.x + 0.5f, local.y + 0.5f)
                : new Vector2(
                    local.x * visibleUv.width + visibleUv.x + visibleUv.width * 0.5f,
                    local.y * visibleUv.height + visibleUv.y + visibleUv.height * 0.5f);
            haveHover = hoverUv.x >= visibleUv.xMin && hoverUv.x <= visibleUv.xMax &&
                        hoverUv.y >= visibleUv.yMin && hoverUv.y <= visibleUv.yMax;
        }

        // The hover pick reads CategoryEnabled, and the draw loop that normally keeps it current can
        // have returned early, so it is refreshed here as well - otherwise a category the player just
        // hid would still answer a hover.
        RefreshMarkerCategoryFlags();

        // Nearest ICON to the pointer, in uv. Place names are deliberately not candidates: their text
        // is already on the map, so repeating it in a tooltip says nothing new - which is what made a
        // tooltip appear over a name that was already visible. A radius rather than exact hit testing,
        // because markers are small on screen and exact overlap would be frustrating.
        float best = float.MaxValue;
        string bestText = null;
        Vector2 bestUv = default;
        if (haveHover)
        {
            const float radius = 0.02f;
            for (int i = 0; i < _vanillaIcons.Count; i++)
            {
                VanillaIcon icon = _vanillaIcons[i];
                if (!icon.CategoryEnabled || string.IsNullOrEmpty(icon.Text))
                    continue;
                float distance = Vector2.Distance(icon.MapUv, hoverUv);
                if (distance < best && distance <= radius)
                {
                    best = distance;
                    bestText = icon.Text;
                    bestUv = icon.MapUv;
                }
            }
        }

        // Place names are drawn whether or not the pointer is over anything: they are part of the map,
        // not a tooltip. An earlier version hid them in hover mode, which was a misreading - the two
        // are independent, and the game shows its own place names permanently.
        for (int i = 0; i < _mapLabels.Count; i++)
        {
            MapLabel label = _mapLabels[i];
            bool visible = label.MapUv.x >= visibleUv.xMin && label.MapUv.x <= visibleUv.xMax &&
                           label.MapUv.y >= visibleUv.yMin && label.MapUv.y <= visibleUv.yMax;
            if (label.Root.activeSelf != visible)
                label.Root.SetActive(visible);
            if (!visible)
                continue;

            // Assigned only when it moved: a RectTransform write marks the layout dirty even when the
            // value is unchanged, and that happens on every frame the map is up.
            Vector2 position = MapUvToLocal(label.MapUv, visibleUv, mapSize);
            if (label.Rect.anchoredPosition != position)
                label.Rect.anchoredPosition = position;
        }

        // The tooltip is the only part that depends on the pointer.
        if (hoverMode)
            ShowHoverLabel(bestText, bestUv, visibleUv, mapSize);
        else if (!ReferenceEquals(_hoverLabelRoot, null))
            _hoverLabelRoot.SetActive(false);

        // The plate follows the tooltip exactly: shown only when there is a name to show, and
        // suppressible on its own because the text is legible either way.
        if (!ReferenceEquals(_hoverPlateRoot, null))
        {
            bool plateWanted = hoverMode && !string.IsNullOrEmpty(bestText) &&
                               _settings.ShowHoverPlate;
            if (_hoverPlateRoot.activeSelf != plateWanted)
                _hoverPlateRoot.SetActive(plateWanted);
        }
    }


    // One reusable tooltip, created on first use so an idle map allocates nothing.
    private void ShowHoverLabel(string text, Vector2 mapUv, Rect visibleUv, Vector2 mapSize)
    {
        if (_hoverTooltipDisabled)
            return;

        if (string.IsNullOrEmpty(text))
        {
            if (!ReferenceEquals(_hoverLabelRoot, null))
                _hoverLabelRoot.SetActive(false);
            if (!ReferenceEquals(_hoverPlateRoot, null))
                _hoverPlateRoot.SetActive(false);
            return;
        }

        if (ReferenceEquals(_hoverLabelRoot, null))
        {
            ResolveHintFont();
            if (ReferenceEquals(_hintFont, null))
                return;

            try
            {
                CreateHoverTooltip();
            }
            catch (Exception ex)
            {
                // The full exception, once, because a bare message with no line number cost a round of
                // guessing: the stack named the method but not the statement. A per-frame throw is far
                // more expensive than the tooltip is worth, so one failure disables it.
                LoggerInstance.Warning($"Hover tooltip creation failed: {ex}");
                _hoverTooltipDisabled = true;
                if (!ReferenceEquals(_hoverLabelRoot, null))
                {
                    UnityEngine.Object.Destroy(_hoverLabelRoot);
                    _hoverLabelRoot = null;
                }
                return;
            }

            if (ReferenceEquals(_hoverLabelRoot, null))
                return;
        }

        // Font size is a setting, but assigning it rebuilds the text mesh, so it is only written when
        // it actually differs. The tooltip is positioned every frame while the pointer moves, and a
        // rebuild per frame is what made dragging feel like the map was following a second behind.
        if (!ReferenceEquals(_hoverLabelText, null) &&
            _hoverLabelText.fontSize != _settings.LabelFontSize)
        {
            _hoverLabelText.fontSize = _settings.LabelFontSize;
            // The preferred width depends on the font size, so the plate has to be re-measured.
            _plateWidthDirty = true;
        }

        // The text is set before the plate is measured, because the plate is sized to the text.
        // A fixed plate looked wrong: a four-character name sat in a wide empty box.
        if (!string.Equals(_hoverLabelText.text, text, StringComparison.Ordinal))
        {
            _hoverLabelText.text = text;
            _plateWidthDirty = true;
        }

        // Sits just above the thing it names, the way the game's own hover label does. Written only
        // when it moved, for the same reason as everywhere else: a RectTransform write dirties the
        // layout even when the value is identical.
        Vector2 tooltipPosition = MapUvToLocal(mapUv, visibleUv, mapSize);
        tooltipPosition.y += _settings.LabelFontSize + 6f;
        if (_hoverLabelRect.anchoredPosition != tooltipPosition)
            _hoverLabelRect.anchoredPosition = tooltipPosition;
        if (!_hoverLabelRoot.activeSelf)
            _hoverLabelRoot.SetActive(true);

        // The plate is a separate object, so it is positioned and sized alongside the text.
        if (!ReferenceEquals(_hoverPlateRoot, null))
        {
            RectTransform plateRect = _hoverPlateRoot.GetComponent<RectTransform>();
            if (!ReferenceEquals(plateRect, null))
            {
                if (plateRect.anchoredPosition != tooltipPosition)
                    plateRect.anchoredPosition = tooltipPosition;

                // Sized from the text itself rather than guessed per character count, so the same
                // call is right for a short name and a long one. preferredWidth is only meaningful
                // after the text has been set, hence the dirty flag.
                if (_plateWidthDirty)
                {
                    _plateWidthDirty = false;
                    float width = _hoverLabelText.preferredWidth + 16f;
                    float height = _settings.LabelFontSize + 8f;
                    var size = new Vector2(width, height);
                    if (plateRect.sizeDelta != size)
                        plateRect.sizeDelta = size;
                }
            }
            if (!_hoverPlateRoot.activeSelf)
                _hoverPlateRoot.SetActive(true);
        }

        // The tooltip is created once, early, while the marker objects are added afterwards - and
        // uGUI draws in hierarchy order, so anything created later covers it. The markers are laid on
        // top of each other in places, so the tooltip was regularly hidden behind an icon. Moving it
        // to the end of the sibling list each time it is shown keeps it above the whole marker set
        // without having to re-order anything when markers are rebuilt. The plate goes last of the two
        // so the text draws over its own background.
        if (!ReferenceEquals(_hoverPlateRoot, null))
            _hoverPlateRoot.transform.SetAsLastSibling();
        _hoverLabelRoot.transform.SetAsLastSibling();
    }


    // Builds the one reusable tooltip.
    //
    // The component list here is exactly the one that worked before: RectTransform, CanvasRenderer,
    // Text. Adding Image to it stopped Text from being created at all, and neither adding it
    // afterwards nor adding via Il2CppType recovered it - measured three times, with the diagnostics
    // reporting text=false each time. So the text object is created exactly as it was, and the dark
    // plate is a separate sibling placed behind it.
    private void CreateHoverTooltip()
    {
        // The plate first, so it sits behind the text in the draw order.
        _hoverPlateRoot = CreateUiObject("MapHoverPlate",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        _hoverPlateRoot.transform.SetParent(_mapRect, false);
        RectTransform plateRect = _hoverPlateRoot.GetComponent<RectTransform>();
        plateRect.anchorMin = new Vector2(0.5f, 0.5f);
        plateRect.anchorMax = new Vector2(0.5f, 0.5f);
        plateRect.pivot = new Vector2(0.5f, 0.5f);
        plateRect.sizeDelta = new Vector2(268f, 32f);
        Image plate = _hoverPlateRoot.GetComponent<Image>();
        if (!ReferenceEquals(plate, null))
        {
            plate.color = new Color(0.06f, 0.05f, 0.04f, 0.80f);
            plate.raycastTarget = false;
        }

        _hoverLabelRoot = CreateUiObject("MapHoverLabel",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        _hoverLabelRoot.transform.SetParent(_mapRect, false);
        _hoverLabelRect = _hoverLabelRoot.GetComponent<RectTransform>();
        _hoverLabelRect.anchorMin = new Vector2(0.5f, 0.5f);
        _hoverLabelRect.anchorMax = new Vector2(0.5f, 0.5f);
        _hoverLabelRect.pivot = new Vector2(0.5f, 0.5f);
        _hoverLabelRect.sizeDelta = new Vector2(260f, 30f);

        _hoverLabelText = _hoverLabelRoot.GetComponent<Text>();
        if (ReferenceEquals(_hoverLabelRect, null) || ReferenceEquals(_hoverLabelText, null))
            throw new InvalidOperationException(
                $"tooltip components missing (rect={_hoverLabelRect != null}, " +
                $"text={_hoverLabelText != null}, plate={plate != null})");

        _hoverLabelText.font = _hintFont;
        _hoverLabelText.fontSize = _settings.LabelFontSize;
        _hoverLabelText.alignment = TextAnchor.MiddleCenter;
        _hoverLabelText.color = new Color(0.98f, 0.96f, 0.92f, 1f);
        _hoverLabelText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _hoverLabelText.verticalOverflow = VerticalWrapMode.Overflow;
        _hoverLabelText.raycastTarget = false;
    }


    // A single offset copy behind the text, to lift it off the map's dark linework.
    //
    // Outline was tried first and was a mistake: it draws four extra copies of every glyph, which on
    // hundreds of CJK labels multiplies the canvas vertex count until the UI stops keeping up. One
    // shadow copy is a fifth of that cost and keeps the text readable.
    private void AddTextShadow(RectTransform rect)
    {
        try
        {
            var effect = rect.gameObject.AddComponent<Shadow>();
            if (ReferenceEquals(effect, null))
                return;
            effect.effectColor = new Color(0.97f, 0.95f, 0.90f, 0.85f);
            effect.effectDistance = new Vector2(1.2f, -1.2f);
            effect.useGraphicAlpha = false;
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Text shadow unavailable: {ex.Message}");
        }
    }
}
// — sutanm · 社区HUD地图

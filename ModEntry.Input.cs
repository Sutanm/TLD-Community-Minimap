// 社区HUD地图 · sutanm — 输入与视图：地图键接管、全屏地图输入、按键提示
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
            // No snapping between overview and local view. The player-centred opening scale can be
            // several times larger near a map edge, and jumping between it and 1x in one notch was
            // visually jarring. Exponential steps keep both directions continuous.
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

        // The parchment itself is scaled, so one widget pixel is exactly 1/size in map uv. The
        // focus follows the pointer: dragging right reveals what is to the left.
        Vector2 widget = _mapRect.rect.size;
        if (widget.x < 1f || widget.y < 1f)
            return;
        _fullMapCenter += new Vector2(-delta.x / widget.x, -delta.y / widget.y);
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
}
// — sutanm · 社区HUD地图

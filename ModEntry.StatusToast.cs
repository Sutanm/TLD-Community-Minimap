// 社区HUD地图 · sutanm — 独立状态提示：无地图与图源回退反馈
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CommunityMinimap;

public sealed partial class ModEntry
{
    private static readonly Color StatusToastPlateColour =
        new(0.015f, 0.025f, 0.035f, 0.82f);

    private static readonly Color StatusToastTextColour =
        new(0.95f, 0.92f, 0.84f, 1f);


    // This canvas is intentionally independent from the map canvas. Map-less indoor scenes turn
    // the latter off completely, which is precisely where a short explanation is needed most.
    // The widgets never receive raycasts, so the toast cannot take input away from either game UI.
    private void EnsureStatusToastUi()
    {
        if (!ReferenceEquals(_statusToastRoot, null))
            return;

        Font font = ResolveHintFont();
        if (ReferenceEquals(font, null))
            return;

        _statusToastRoot = CreateUiObject("CommunityMinimapStatusCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        UnityEngine.Object.DontDestroyOnLoad(_statusToastRoot);
        Canvas canvas = _statusToastRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2100;
        CanvasScaler scaler = _statusToastRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        GameObject plateObject = CreateUiObject("StatusPlate",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        plateObject.transform.SetParent(_statusToastRoot.transform, false);
        RectTransform plateRect = plateObject.GetComponent<RectTransform>();
        plateRect.anchorMin = new Vector2(0.5f, 1f);
        plateRect.anchorMax = new Vector2(0.5f, 1f);
        plateRect.pivot = new Vector2(0.5f, 1f);
        plateRect.anchoredPosition = new Vector2(0f, -82f);
        plateRect.sizeDelta = new Vector2(Mathf.Min(760f, Screen.width - 40f), 52f);
        _statusToastPlate = plateObject.GetComponent<Image>();
        _statusToastPlate.color = StatusToastPlateColour;
        _statusToastPlate.raycastTarget = false;

        GameObject labelObject = CreateUiObject("StatusText",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelObject.transform.SetParent(plateObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(18f, 6f);
        labelRect.offsetMax = new Vector2(-18f, -6f);
        _statusToastLabel = labelObject.GetComponent<Text>();
        _statusToastLabel.font = font;
        _statusToastLabel.fontSize = 21;
        _statusToastLabel.alignment = TextAnchor.MiddleCenter;
        _statusToastLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        _statusToastLabel.verticalOverflow = VerticalWrapMode.Overflow;
        _statusToastLabel.color = StatusToastTextColour;
        _statusToastLabel.raycastTarget = false;

        _statusToastRoot.SetActive(false);
    }


    private void ShowStatusToast(string message, float seconds = 2f)
    {
        EnsureStatusToastUi();
        if (ReferenceEquals(_statusToastRoot, null) || ReferenceEquals(_statusToastLabel, null))
            return;

        _statusToastLabel.text = message;
        _statusToastPlate.color = StatusToastPlateColour;
        _statusToastLabel.color = StatusToastTextColour;
        _statusToastFadeAt = Time.unscaledTime + Mathf.Max(0.2f, seconds - 0.35f);
        _statusToastHideAt = Time.unscaledTime + Mathf.Max(0.55f, seconds);
        if (!_statusToastRoot.activeSelf)
            _statusToastRoot.SetActive(true);
    }


    private void UpdateStatusToast()
    {
        if (ReferenceEquals(_statusToastRoot, null) || !_statusToastRoot.activeSelf)
            return;

        float now = Time.unscaledTime;
        if (now >= _statusToastHideAt)
        {
            _statusToastRoot.SetActive(false);
            return;
        }

        float alpha = now <= _statusToastFadeAt
            ? 1f
            : Mathf.Clamp01((_statusToastHideAt - now) /
                            Mathf.Max(0.01f, _statusToastHideAt - _statusToastFadeAt));
        Color plate = StatusToastPlateColour;
        plate.a *= alpha;
        Color text = StatusToastTextColour;
        text.a *= alpha;
        _statusToastPlate.color = plate;
        _statusToastLabel.color = text;
    }
}
// — sutanm · 社区HUD地图

using ModSettings;
using UnityEngine;

namespace CommunityMinimap;

internal sealed class MinimapSettings : JsonModSettings
{
    [Section("HUD")]

    [Name("启用小地图")]
    [Description("显示或隐藏民间地图 HUD。")]
    public bool Enabled = true;

    [Name("HUD 位置")]
    [Description("选择小地图在屏幕上的位置。")]
    [Choice("右上", "左上", "右下", "左下")]
    public int Position = 0;

    [Name("小地图 UI 大小")]
    [Description("小地图占屏幕短边的百分比，与地图局部缩放分开计算。")]
    [Slider(15, 40, 26, NumberFormat = "{0}%")]
    public int MiniMapSizePercent = 24;

    [Name("地图透明度")]
    [Description("调整地图图片的不透明度。")]
    [Slider(0.25f, 1f, 16, NumberFormat = "{0:P0}")]
    public float Opacity = 0.9f;

    [Name("全屏背景不透明度")]
    [Description("调整完整地图周围的深色背景。0% 完全透明，100% 完全不透明。")]
    [Slider(0f, 1f, 21, NumberFormat = "{0:P0}")]
    public float FullMapBackgroundOpacity = 0.97f;

    [Name("边距")]
    [Description("小地图与屏幕边缘的距离。")]
    [Slider(0, 100, 21)]
    public int Margin = 24;

    [Name("局部缩放")]
    [Description("玩家处于已校准范围内时，小地图的放大倍数。")]
    [Slider(1.5f, 12f, 22, NumberFormat = "{0:F1}x")]
    public float Zoom = 5f;

    [Name("玩家指针尺寸")]
    [Description("圆环位置标记与方向短针的显示尺寸。")]
    [Slider(32, 64, 17)]
    public int MarkerSize = 42;

    [Name("显示坐标诊断")]
    [Description("在小地图下方显示场景名、玩家世界坐标和朝向，供地图配准使用。")]
    public bool ShowDiagnostics = true;

    [Section("快捷键与校准")]

    [Name("切换小地图/完整地图")]
    [Description("在角落小地图和全屏完整地图之间切换；全屏时可按 Esc 返回。")]
    public KeyCode MapModeKey = KeyCode.Tab;

    [Name("快速显示/隐藏")]
    [Description("不改变保存设置，仅临时显示或隐藏 HUD。")]
    public KeyCode ToggleKey = KeyCode.F8;

    [Name("记录校准点")]
    [Description("站在地图上易识别的地标后按此键，将世界坐标追加到 calibration_points.csv。")]
    public KeyCode RecordPointKey = KeyCode.F9;
}

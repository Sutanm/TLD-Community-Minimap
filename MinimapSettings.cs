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

    [Name("HUD 尺寸")]
    [Description("小地图方框的像素尺寸。")]
    [Slider(220, 600, 20)]
    public int Size = 360;

    [Name("地图透明度")]
    [Description("调整地图图片的不透明度。")]
    [Slider(0.25f, 1f, 16, NumberFormat = "{0:P0}")]
    public float Opacity = 0.9f;

    [Name("边距")]
    [Description("小地图与屏幕边缘的距离。")]
    [Slider(0, 100, 21)]
    public int Margin = 24;

    [Name("局部缩放")]
    [Description("玩家处于已校准范围内时，小地图的放大倍数。")]
    [Slider(2f, 10f, 17, NumberFormat = "{0:F1}x")]
    public float Zoom = 5f;

    [Name("玩家箭头尺寸")]
    [Description("带尾部玩家箭头的像素高度。")]
    [Slider(12, 36, 13)]
    public int MarkerSize = 20;

    [Name("显示坐标诊断")]
    [Description("在小地图下方显示场景名、玩家世界坐标和朝向，供地图配准使用。")]
    public bool ShowDiagnostics = true;

    [Section("快捷键与校准")]

    [Name("快速显示/隐藏")]
    [Description("不改变保存设置，仅临时显示或隐藏 HUD。")]
    public KeyCode ToggleKey = KeyCode.F8;

    [Name("记录校准点")]
    [Description("站在地图上易识别的地标后按此键，将世界坐标追加到 calibration_points.csv。")]
    public KeyCode RecordPointKey = KeyCode.F9;
}

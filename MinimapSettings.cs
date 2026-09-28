using ModSettings;
using UnityEngine;

namespace CommunityMinimap;

internal sealed class MinimapSettings : JsonModSettings
{
    [Section("地图来源")]

    [Name("地图来源")]
    [Description("自动模式优先使用已安装的民间高清地图；缺少图片时使用原版制图。原版模式会在正常打开游戏地图时自动刷新。")]
    [Choice("自动", "民间高清", "原版制图")]
    public int MapSource = 0;

    [Section("HUD")]

    [Name("启用小地图")]
    [Description("显示或隐藏地图 HUD。")]
    public bool Enabled = true;

    [Name("HUD 位置")]
    [Description("选择小地图在屏幕上的位置。")]
    [Choice("右上", "左上", "右下", "左下")]
    public int HudPosition = 1;

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
    [Description("玩家处于已校准范围内时，小地图的放大倍数。原版地图标记较密集时可调高。")]
    [Slider(1.5f, 30f, 58, NumberFormat = "{0:F1}x")]
    public float Zoom = 5f;

    [Name("玩家指针尺寸")]
    [Description("圆环位置标记与方向短针的显示尺寸。")]
    [Slider(40, 80, 21)]
    public int MarkerSize = 54;

    [Name("清理已采集的标记（仅报告）")]
    [Description("扫描已采集完的资源标记并写入日志，但不删除。用于确认判据是否正确。")]
    public bool CleanHarvestedMarkers = true;

    [Name("真正删除已采集标记")]
    [Description("确认上一项的日志数量正确后才打开。会从游戏数据里移除这些标记；若标记大面积消失，关掉并重载场景即可恢复。")]
    public bool RemoveHarvestedMarkers = false;

    [Name("地图标记尺寸")]
    [Description("原版地图标记（物资、建筑等图标）的屏幕像素大小。固定尺寸，不随缩放变大，因此小地图和全屏地图上看起来一样大。")]
    [Slider(10, 120, 111)]
    public int MarkerIconSize = 44;

    [Name("玩家指针配色")]
    [Description("切换方向针和内环的强调色；深色外环与暖白中心点保持不变。")]
    [Choice("珊瑚红", "酒红", "淡紫", "青绿色", "琥珀金", "鲜红")]
    public int PointerPalette = 0;

    [Name("显示坐标诊断")]
    [Description("在小地图下方显示场景名、玩家世界坐标和朝向，供地图配准使用。")]
    public bool ShowDiagnostics = true;

    [Section("快捷键与校准")]

    [Name("接管游戏地图键")]
    [Description("按下游戏原本的“打开地图”键（默认 M）时显示本模组的全屏地图，而不是游戏自带的地图界面。不会修改游戏地图模块本身；关掉此项即恢复游戏原行为。")]
    public bool RedirectGameMap = true;

    [Name("全屏地图释放鼠标")]
    [Description("打开全屏地图时释放鼠标并接管输入，这样才能用滚轮缩放、按住左键拖动地图。关掉后鼠标仍由游戏锁定，地图只能查看不能移动。")]
    public bool ReleaseMouseOnFullMap = true;

    [Name("切换小地图/完整地图")]
    [Description("在角落小地图和全屏完整地图之间切换；全屏时可按 Esc 返回。若与其他 mod 的按键冲突，改这里。")]
    public KeyCode MapModeKey = KeyCode.Tab;

    [Name("快速显示/隐藏")]
    [Description("不改变保存设置，仅临时显示或隐藏 HUD。若与其他 mod 的按键冲突，改这里。")]
    public KeyCode ToggleKey = KeyCode.F8;

    [Name("记录校准点")]
    [Description("站在地图上易识别的地标后按此键，将坐标写入 calibration_points_v2.csv，并保存对应截图。若与其他 mod 的按键冲突，改这里。")]
    public KeyCode RecordPointKey = KeyCode.F9;
}

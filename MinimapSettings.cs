// 社区HUD地图 · sutanm — 设置界面与条件显示
using System.Reflection;
using ModSettings;
using UnityEngine;

namespace CommunityMinimap;

internal sealed class MinimapSettings : JsonModSettings
{
    // Public field names are persisted JSON keys. Keep them stable when reorganising the menu so
    // an update never resets a player's existing choices.

    [Section("通用")]

    [Name("社区地图显示游戏标记")]
    [Description("在已校准的社区地图上显示游戏标记。")]
    public bool MarkersOnCommunityMap = false;

    [Name("启用洞穴与室内地图")]
    [Description("在洞穴、矿井和室内场景启用本模组地图。")]
    public bool EnableInteriorMaps = true;

    [Name("禁用图源回退")]
    [Description("所选图源不可用时不切换到另一图源。")]
    public bool DisableSourceFallback = false;


    [Section("小地图")]

    [Name("显示小地图")]
    [Description("显示角落小地图，不影响大地图。")]
    public bool Enabled = true;

    [Name("小地图图源")]
    [Description("选择小地图使用的地图。")]
    [Choice("自动", "社区地图", "原版地图")]
    public int MiniMapSource = 0;

    [Name("小地图标记大小")]
    [Description("调整小地图上的游戏标记大小。")]
    [Slider(10, 120, 111)]
    public int MiniMapMarkerIconSize = 44;

    [Name("小地图始终置顶")]
    [Description("让小地图显示在游戏 HUD 上方。")]
    public bool MiniMapAlwaysOnTop = false;

    [Name("小地图位置")]
    [Description("选择小地图位置。")]
    [Choice("右上", "左上", "右下", "左下", "自定义")]
    public int HudPosition = 1;

    [Name("自定义位置 X")]
    [Description("小地图中心的横向位置。")]
    [Slider(0, 100, 101, NumberFormat = "{0}%")]
    public int MiniMapPositionX = 50;

    [Name("自定义位置 Y")]
    [Description("小地图中心的纵向位置。")]
    [Slider(0, 100, 101, NumberFormat = "{0}%")]
    public int MiniMapPositionY = 50;

    [Name("边距")]
    [Description("小地图与屏幕边缘的距离。")]
    [Slider(0, 100, 21)]
    public int Margin = 24;

    [Name("小地图大小")]
    [Description("调整小地图占屏幕短边的比例。")]
    [Slider(15, 40, 26, NumberFormat = "{0}%")]
    public int MiniMapSizePercent = 24;

    [Name("小地图透明度")]
    [Description("调整小地图的不透明度。")]
    [Slider(0.25f, 1f, 16, NumberFormat = "{0:P0}")]
    public float Opacity = 0.9f;

    [Name("小地图缩放")]
    [Description("调整已校准地图的局部缩放。")]
    [Slider(1.5f, 30f, 58, NumberFormat = "{0:F1}x")]
    public float Zoom = 5f;

    [Section("大地图")]

    [Name("使用模组大地图")]
    [Description("使用游戏地图键打开本模组大地图。")]
    public bool RedirectGameMap = true;

    [Name("大地图图源")]
    [Description("选择大地图使用的地图。")]
    [Choice("自动", "社区地图", "原版地图")]
    public int FullMapSource = 0;

    [Name("大地图标记大小")]
    [Description("调整大地图上的游戏标记大小。")]
    [Slider(10, 120, 111)]
    public int FullMapMarkerIconSize = 44;

    [Name("大地图始终置顶")]
    [Description("让大地图显示在游戏 HUD 上方。")]
    public bool FullMapAlwaysOnTop = false;

    [Name("显示玩家指针")]
    [Description("在大地图上显示玩家位置和朝向。")]
    public bool ShowFullMapPlayerPointer = true;

    [Name("背景不透明度")]
    [Description("调整大地图周围的背景不透明度。")]
    [Slider(0f, 1f, 21, NumberFormat = "{0:P0}")]
    public float FullMapBackgroundOpacity = 0.97f;

    [Name("启用鼠标操作")]
    [Description("允许使用滚轮缩放、左键拖动和标记悬停。")]
    public bool ReleaseMouseOnFullMap = true;

    [Name("允许打开时移动")]
    [Description("打开大地图时允许角色移动，仍会阻止转向和其他操作。")]
    public bool AllowMovementOnFullMap = false;

    [Name("显示操作提示")]
    [Description("在大地图底部显示操作提示。")]
    public bool ShowKeyHints = true;


    [Section("玩家指针")]

    [Name("玩家指针尺寸")]
    [Description("调整玩家指针大小。")]
    [Slider(40, 80, 21)]
    public int MarkerSize = 54;

    [Name("玩家指针配色")]
    [Description("选择玩家指针的强调色。")]
    [Choice("珊瑚红", "酒红", "淡紫", "青绿色", "琥珀金", "鲜红")]
    public int PointerPalette = 0;

    [Section("地图标记与文字")]

    [Name("标记来源")]
    [Description("地图数据读取完整标记；游戏面板只读取已创建的标记。")]
    [Choice("地图数据", "游戏面板")]
    public int MarkerSource = 0;

    [Name("标记颜色")]
    [Description("选择游戏标记的颜色。")]
    [Choice("白色", "深色")]
    public int MarkerTint = 0;

    [Name("标记描边")]
    [Description("为游戏标记添加深色轮廓。")]
    public bool MarkerOutline = true;

    [Name("地名标签")]
    [Description("在地图上显示地名。")]
    public bool ShowMapLabels = true;

    [Name("标签字号")]
    [Description("调整地名和悬停提示的字号。")]
    [Slider(12, 40, 29)]
    public int LabelFontSize = 20;

    [Name("悬停显示标记名称")]
    [Description("鼠标悬停时显示标记名称。")]
    public bool HoverMapLabels = true;

    [Name("悬停提示背景")]
    [Description("为悬停提示添加深色背景。")]
    public bool ShowHoverPlate = true;


    [Section("地图标记筛选")]

    [Name("资源")]
    [Description("显示可采集资源标记。")]
    public bool ShowMarkerResources = true;

    [Name("结构")]
    [Description("显示建筑、车辆、桥梁、绳点和洞穴标记。")]
    public bool ShowMarkerStructures = true;

    [Name("尸骸")]
    [Description("显示尸骸标记。")]
    public bool ShowMarkerCorpses = true;

    [Name("岩石贮藏处")]
    [Description("显示岩石贮藏处标记。")]
    public bool ShowMarkerRockCaches = true;

    [Name("油漆喷罐标记")]
    [Description("显示玩家留下的喷漆标记。")]
    public bool ShowMarkerSprayMarks = true;


    [Section("按键与游戏联动")]

    [Name("临时隐藏小地图")]
    [Description("临时隐藏或恢复小地图。")]
    public KeyCode ToggleKey = KeyCode.X;

    [Name("启用视图轮换按键")]
    [Description("启用独立的地图视图轮换按键。")]
    public bool EnableCycleKey = true;

    [Name("视图轮换按键")]
    [Description("在选定的地图视图之间轮换。")]
    public KeyCode CycleViewKey = KeyCode.V;

    [Name("轮换内容")]
    [Description("选择参与轮换的地图视图。")]
    [Choice("小地图 → 大地图", "小地图 → 大地图 → 无", "小地图 → 无", "大地图 → 无")]
    public int CyclePreset = 1;

    [Name("木炭勘测后弹图")]
    [Description("选择木炭勘测后打开的地图。")]
    [Choice("原版地图", "模组大地图", "不打开")]
    public int SurveyPopup = 0;


    [Section("高级与开发")]

    [Name("开发者模式")]
    [Description("显示地图抓取、校准和诊断选项。")]
    public bool DeveloperMode = false;

    [Name("抓取原版地图")]
    [Description("保存当前原版地图供校准使用。")]
    public KeyCode CaptureMapKey = KeyCode.P;

    [Name("显示坐标诊断")]
    [Description("显示场景、世界坐标和朝向。")]
    public bool ShowDiagnostics = false;

    [Name("记录校准点")]
    [Description("记录当前位置和截图供校准使用。")]
    public KeyCode RecordPointKey = KeyCode.F11;

    [Name("清理已采集的标记（仅报告）")]
    [Description("扫描已采集完的资源标记并写入日志。仅用于诊断；HUD 会自动隐藏已采集资源，不会修改游戏地图数据。")]
    public bool ReportHarvestedMarkers = false;


    internal const int MarkerSourceMapDetails = 0;
    internal const int MarkerSourcePanel = 1;

    internal int MarkerCategoryState()
    {
        int state = 0;
        if (ShowMarkerResources) state |= 1;
        if (ShowMarkerStructures) state |= 2;
        if (ShowMarkerCorpses) state |= 4;
        if (ShowMarkerRockCaches) state |= 8;
        if (ShowMarkerSprayMarks) state |= 16;
        return state;
    }


    protected override void OnChange(FieldInfo field, object oldValue, object newValue)
    {
        base.OnChange(field, oldValue, newValue);

        // ModSettings keeps tentative GUI values separately, so use the value delivered here.
        if (field.Name == nameof(DeveloperMode) && newValue is bool developerMode)
            ApplyVisibility(developerMode, null, null);
        else if (field.Name == nameof(HudPosition) && newValue is int position)
            ApplyVisibility(null, position, null);
        else if (field.Name == nameof(EnableCycleKey) && newValue is bool cycleEnabled)
            ApplyVisibility(null, null, cycleEnabled);
    }


    protected override void OnConfirm()
    {
        base.OnConfirm();
        ApplyVisibility(null, null, null);
    }


    internal void ApplyVisibility(bool? developerMode, int? hudPosition, bool? cycleKeyEnabled)
    {
        bool developer = developerMode ?? DeveloperMode;
        bool customPosition = (hudPosition ?? HudPosition) == 4;
        bool cycleKey = cycleKeyEnabled ?? EnableCycleKey;

        try
        {
            SetFieldVisible(nameof(CaptureMapKey), developer);
            SetFieldVisible(nameof(ShowDiagnostics), developer);
            SetFieldVisible(nameof(RecordPointKey), developer);
            // Retain the legacy field for existing config files and diagnostics, but do not expose
            // a stale report-only switch in the player-facing settings panel.
            SetFieldVisible(nameof(ReportHarvestedMarkers), false);

            SetFieldVisible(nameof(MiniMapPositionX), customPosition);
            SetFieldVisible(nameof(MiniMapPositionY), customPosition);
            SetFieldVisible(nameof(Margin), !customPosition);

            SetFieldVisible(nameof(CycleViewKey), cycleKey);
            SetFieldVisible(nameof(CyclePreset), cycleKey);

            RefreshGUI();
        }
        catch (System.Exception ex)
        {
            MelonLoader.MelonLogger.Warning(
                $"[CommunityMinimap] Could not update settings visibility: {ex.Message}");
        }
    }
}
// — sutanm · 社区HUD地图

// 社区HUD地图 · sutanm — 设置界面与条件显示
using System.Reflection;
using ModSettings;
using UnityEngine;

namespace CommunityMinimap;

internal sealed class MinimapSettings : JsonModSettings
{
    // Public field names are persisted JSON keys. Keep them stable when reorganising the menu so
    // an update never resets a player's existing choices.

    [Section("基本与图源")]

    [Name("显示小地图")]
    [Description("角落小地图的总开关。关闭不会影响通过游戏地图键打开的全屏地图。")]
    public bool Enabled = true;

    [Name("接管游戏地图键")]
    [Description("按游戏自己的「打开地图」动作时显示本模组全屏地图。它跟随游戏内的键位绑定；关闭即恢复游戏原行为。")]
    public bool RedirectGameMap = true;

    [Name("小地图图源")]
    [Description("角落小地图使用哪套地图。自动模式优先使用已安装的民间高清地图，缺少图片时使用原版制图。")]
    [Choice("自动", "民间高清", "原版制图")]
    public int MiniMapSource = 0;

    [Name("全屏地图图源")]
    [Description("通过游戏地图键打开的全屏地图使用哪套地图。自动模式优先使用已安装的民间高清地图。")]
    [Choice("自动", "民间高清", "原版制图")]
    public int FullMapSource = 0;

    [Name("禁用图源回退")]
    [Description("默认关闭。打开后，明确选择「民间高清」或「原版制图」时严格使用该图源，不可用时不再临时改用另一套图。选择「自动」时仍会自动选择可用图源。")]
    public bool DisableSourceFallback = false;


    [Section("小地图")]

    [Name("HUD 位置")]
    [Description("小地图贴在屏幕的哪个角；需要自由放置时选择「自定义」。")]
    [Choice("右上", "左上", "右下", "左下", "自定义")]
    public int HudPosition = 1;

    [Name("自定义位置 X")]
    [Description("小地图中心所在的横向位置，占屏幕宽度的百分比。仅在 HUD 位置选「自定义」时生效。")]
    [Slider(0, 100, 101, NumberFormat = "{0}%")]
    public int MiniMapPositionX = 50;

    [Name("自定义位置 Y")]
    [Description("小地图中心所在的纵向位置，占屏幕高度的百分比。仅在 HUD 位置选「自定义」时生效。")]
    [Slider(0, 100, 101, NumberFormat = "{0}%")]
    public int MiniMapPositionY = 50;

    [Name("边距")]
    [Description("小地图与屏幕边缘的距离。仅在 HUD 位置选四角时生效。")]
    [Slider(0, 100, 21)]
    public int Margin = 24;

    [Name("小地图 UI 大小")]
    [Description("小地图占屏幕短边的百分比，与地图局部缩放分开计算。")]
    [Slider(15, 40, 26, NumberFormat = "{0}%")]
    public int MiniMapSizePercent = 24;

    [Name("小地图透明度")]
    [Description("小地图图片的不透明度。全屏地图不受此项影响。")]
    [Slider(0.25f, 1f, 16, NumberFormat = "{0:P0}")]
    public float Opacity = 0.9f;

    [Name("小地图始终置顶")]
    [Description("开启后让小地图覆盖游戏原版 HUD，恢复旧版显示层级。默认关闭时，原版状态、地点与交互 HUD 会显示在小地图上方。全屏地图不受影响。")]
    public bool MiniMapAlwaysOnTop = false;

    [Name("局部缩放")]
    [Description("玩家处于已校准范围内时，小地图的放大倍数。")]
    [Slider(1.5f, 30f, 58, NumberFormat = "{0:F1}x")]
    public float Zoom = 5f;

    [Name("小地图标记尺寸")]
    [Description("角落小地图中物资、建筑等游戏地图标记的屏幕像素大小。固定尺寸，不随地图缩放变大。")]
    [Slider(10, 120, 111)]
    public int MiniMapMarkerIconSize = 44;


    [Section("全屏地图")]

    [Name("全屏背景不透明度")]
    [Description("调整完整地图周围的深色背景。0% 完全透明，100% 完全不透明。")]
    [Slider(0f, 1f, 21, NumberFormat = "{0:P0}")]
    public float FullMapBackgroundOpacity = 0.97f;

    [Name("释放鼠标（缩放与拖动）")]
    [Description("打开全屏地图时释放鼠标并接管输入，以便使用滚轮缩放和左键拖动。关闭后地图只能查看。")]
    public bool ReleaseMouseOnFullMap = true;

    [Name("按键提示")]
    [Description("在全屏地图底部显示缩放、拖动、切换和关闭操作提示。")]
    public bool ShowKeyHints = true;

    [Name("全屏地图标记尺寸")]
    [Description("全屏地图中物资、建筑等游戏地图标记的屏幕像素大小。固定尺寸，不随地图缩放变大。")]
    [Slider(10, 120, 111)]
    public int FullMapMarkerIconSize = 44;


    [Section("玩家指针")]

    [Name("玩家指针尺寸")]
    [Description("圆环位置标记与方向短针的显示尺寸。")]
    [Slider(40, 80, 21)]
    public int MarkerSize = 54;

    [Name("玩家指针配色")]
    [Description("切换方向针和内环的强调色；深色外环与暖白中心点保持不变。")]
    [Choice("珊瑚红", "酒红", "淡紫", "青绿色", "琥珀金", "鲜红")]
    public int PointerPalette = 0;

    [Name("全屏地图显示玩家指针")]
    [Description("在全屏地图上显示玩家当前位置与朝向。关闭后小地图仍显示指针，全屏地图从整张地图中央打开。")]
    public bool ShowFullMapPlayerPointer = true;


    [Section("地图标记与文字")]

    [Name("标记来源")]
    [Description("地图数据＝读取游戏登记的完整标记；游戏面板＝只读取游戏实际创建出的部分。若完整标记显示异常可切回游戏面板。")]
    [Choice("地图数据", "游戏面板")]
    public int MarkerSource = 0;

    [Name("社区地图上也画标记")]
    [Description("打开后，民间高清地图上也会绘制游戏地图标记；关闭后只有原版制图带这些标记。")]
    public bool MarkersOnCommunityMap = true;

    [Name("标记颜色")]
    [Description("标记图标的颜色。白色通常在原版羊皮纸和社区地图上都更清楚。")]
    [Choice("白色", "深色")]
    public int MarkerTint = 0;

    [Name("标记描边")]
    [Description("在标记背后绘制深色轮廓，使白色图标在明暗不同的地图区域保持可见。")]
    public bool MarkerOutline = true;

    [Name("地名标签")]
    [Description("在地图上常显米尔顿小镇、盆地、教堂等游戏地名。")]
    public bool ShowMapLabels = true;

    [Name("标记字号")]
    [Description("地名与悬停提示的文字大小。")]
    [Slider(12, 40, 29)]
    public int LabelFontSize = 20;

    [Name("悬停显示图标名字")]
    [Description("打开全屏地图后，鼠标停在标记上时显示香蒲、车、洞穴等名称。需要开启「释放鼠标」。")]
    public bool HoverMapLabels = true;

    [Name("悬停提示的深色底片")]
    [Description("悬停提示文字背后的深色方块。关闭后只显示带浅色描边的文字。")]
    public bool ShowHoverPlate = true;


    [Section("地图标记筛选")]

    [Name("资源")]
    [Description("可采集植物、树枝、鸟巢与动物尸体等资源标记。")]
    public bool ShowMarkerResources = true;

    [Name("结构")]
    [Description("建筑物、车辆、桥梁、绳点、洞穴等人工地物标记。")]
    public bool ShowMarkerStructures = true;

    [Name("尸骸")]
    [Description("尸体标记。")]
    public bool ShowMarkerCorpses = true;

    [Name("岩石贮藏处")]
    [Description("岩石贮藏处标记。")]
    public bool ShowMarkerRockCaches = true;

    [Name("油漆喷罐标记")]
    [Description("玩家用油漆喷罐在地图上留下的标记。")]
    public bool ShowMarkerSprayMarks = true;


    [Section("按键与游戏联动")]

    [Name("临时隐藏小地图")]
    [Description("游戏中按此键临时收起小地图，再按一次恢复。不改变保存的设置，也不影响全屏地图。")]
    public KeyCode ToggleKey = KeyCode.X;

    [Name("启用视图轮换按键")]
    [Description("允许用一个独立按键在小地图、全屏地图和不显示之间轮换。关闭后不占用按键。")]
    public bool EnableCycleKey = true;

    [Name("视图轮换按键")]
    [Description("按此键在「轮换内容」选定的视图之间循环。默认 V，该键未被游戏默认键位占用。")]
    public KeyCode CycleViewKey = KeyCode.V;

    [Name("轮换内容")]
    [Description("小地图＝角落 HUD；大地图＝全屏地图；无＝两个都不显示。")]
    [Choice("小地图 → 大地图", "小地图 → 大地图 → 无", "小地图 → 无", "大地图 → 无")]
    public int CyclePreset = 1;

    [Name("木炭勘测后弹图")]
    [Description("游戏内置＝保持游戏原样；当前图源＝显示本模组全屏地图；不弹＝阻止木炭勘测后自动打开地图。")]
    [Choice("游戏内置", "当前图源", "不弹")]
    public int SurveyPopup = 1;


    [Section("高级与开发")]

    [Name("开发者模式")]
    [Description("显示并启用地图抓取、配准和标记诊断选项。关闭时隐藏这些项目并保留其设置值。 · sutanm 制作")]
    public bool DeveloperMode = false;

    [Name("抓取游戏地图的键")]
    [Description("开发工具。打开游戏原版地图后按此键，将高分辨率地图抓取到 Mods\\CommunityMinimap，供采集与校准使用。")]
    public KeyCode CaptureMapKey = KeyCode.P;

    [Name("显示坐标诊断")]
    [Description("在小地图下方显示场景名、玩家世界坐标和朝向，供地图配准使用。")]
    public bool ShowDiagnostics = false;

    [Name("记录校准点")]
    [Description("站在容易识别的地标后按此键，将坐标和截图写入校准文件。")]
    public KeyCode RecordPointKey = KeyCode.F11;

    [Name("清理已采集的标记（仅报告）")]
    [Description("扫描已采集完的资源标记并写入日志，但不删除。用于确认判据是否正确。")]
    public bool ReportHarvestedMarkers = false;

    [Name("真正删除已采集标记")]
    [Description("确认上一项的报告正确后才打开。会从游戏数据中移除已采集标记；关闭并重载场景即可恢复。")]
    public bool RemoveHarvestedMarkers = false;


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
            SetFieldVisible(nameof(ReportHarvestedMarkers), developer);
            SetFieldVisible(nameof(RemoveHarvestedMarkers), developer);

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

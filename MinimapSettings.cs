using System.Reflection;
using ModSettings;
using UnityEngine;

namespace CommunityMinimap;

internal sealed class MinimapSettings : JsonModSettings
{
    // Field names are the keys in Loader.cfg, so renaming one silently resets it for everyone
    // who already has a config. Only the [Name] display strings are free to change.

    [Section("小地图")]

    [Name("显示小地图")]
    [Description("角落小地图的总开关。关掉它只是不画小地图，全屏地图不受影响，随时可以用游戏地图键（默认 M）打开。")]
    public bool Enabled = true;

    [Name("临时隐藏小地图")]
    [Description("游戏中按此键临时收起小地图，再按一次恢复。不改变保存的设置，也不影响全屏地图。")]
    public KeyCode ToggleKey = KeyCode.F8;

    [Name("HUD 位置")]
    [Description("小地图贴在屏幕的哪个角；玩家 HUD 那个角落被占用时选「自定义」自己摆。")]
    [Choice("右上", "左上", "右下", "左下", "自定义")]
    public int HudPosition = 1;

    [Name("自定义位置 X")]
    [Description("小地图中心所在的横向位置，占屏幕宽度的百分比。50% 是屏幕正中。仅在 HUD 位置选「自定义」时生效。")]
    [Slider(0, 100, 101, NumberFormat = "{0}%")]
    public int MiniMapPositionX = 50;

    [Name("自定义位置 Y")]
    [Description("小地图中心所在的纵向位置，占屏幕高度的百分比。50% 是屏幕正中。仅在 HUD 位置选「自定义」时生效。")]
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
    [Description("小地图图片的不透明度。调低可以让底下的游戏 HUD 透出来。全屏地图不受此项影响。")]
    [Slider(0.25f, 1f, 16, NumberFormat = "{0:P0}")]
    public float Opacity = 0.9f;

    [Name("局部缩放")]
    [Description("玩家处于已校准范围内时，小地图的放大倍数。原版地图标记较密集时可调高。")]
    [Slider(1.5f, 30f, 58, NumberFormat = "{0:F1}x")]
    public float Zoom = 5f;

    [Name("玩家指针尺寸")]
    [Description("圆环位置标记与方向短针的显示尺寸。")]
    [Slider(40, 80, 21)]
    public int MarkerSize = 54;

    [Name("玩家指针配色")]
    [Description("切换方向针和内环的强调色；深色外环与暖白中心点保持不变。")]
    [Choice("珊瑚红", "酒红", "淡紫", "青绿色", "琥珀金", "鲜红")]
    public int PointerPalette = 0;

    [Name("地图标记尺寸")]
    [Description("原版地图标记（物资、建筑等图标）的屏幕像素大小，小地图和全屏地图共用。固定尺寸，不随缩放变大。")]
    [Slider(10, 120, 111)]
    public int MarkerIconSize = 44;

    [Section("全屏地图")]

    [Name("全屏背景不透明度")]
    [Description("调整完整地图周围的深色背景。0% 完全透明，100% 完全不透明。")]
    [Slider(0f, 1f, 21, NumberFormat = "{0:P0}")]
    public float FullMapBackgroundOpacity = 0.97f;

    [Name("释放鼠标（缩放与拖动）")]
    [Description("打开全屏地图时释放鼠标并接管输入，这样才能用滚轮缩放、按住左键拖动地图。关掉后鼠标仍由游戏锁定，地图只能查看不能移动。")]
    public bool ReleaseMouseOnFullMap = true;

    [Name("按键提示")]
    [Description("在全屏地图底部显示一行操作提示（缩放 / 拖动 / 关闭）。找不到可用字体会自动隐藏，并写入日志。")]
    public bool ShowKeyHints = true;

    [Name("木炭勘测后弹图")]
    [Description("用木炭点亮地图时，游戏会强制打开一次地图。游戏内置＝保持游戏原样；当前图源＝改弹本模组的全屏地图，用你上面选的图源；不弹＝直接吃掉这次弹窗（我们的地图本来就全亮，信息量相同，少一次打扰）。")]
    [Choice("游戏内置", "当前图源", "不弹")]
    public int SurveyPopup = 1;

    [Section("地图来源")]

    [Name("地图来源")]
    [Description("自动模式优先使用已安装的民间高清地图；缺少图片时使用原版制图。原版模式会在正常打开游戏地图时自动刷新。")]
    [Choice("自动", "民间高清", "原版制图")]
    public int MapSource = 0;

    [Section("高级")]

    [Name("开发者模式")]
    [Description("打开后显示地图配准、标记诊断等开发用选项。关掉则隐藏它们，值不会丢失。")]
    public bool DeveloperMode = false;

    [Name("接管游戏地图键")]
    [Description("按游戏自己的「打开地图」动作时显示本模组的全屏地图，而不是游戏内置的地图界面。它拦的是游戏的动作而不是某个按键，所以你在游戏里改过绑定也照样跟随。不会修改游戏地图模块本身；关掉此项即恢复游戏原行为。")]
    public bool RedirectGameMap = true;

    [Name("显示坐标诊断")]
    [Description("在小地图下方显示场景名、玩家世界坐标和朝向，供地图配准使用。")]
    public bool ShowDiagnostics = false;

    [Name("记录校准点")]
    [Description("站在地图上易识别的地标后按此键，将坐标写入 calibration_points_v2.csv，并保存对应截图。")]
    public KeyCode RecordPointKey = KeyCode.F9;

    // Provisional: whether this can be removed entirely depends on InputManager.GetOpenMapPressed
    // working inside our own input context. Until that is measured it stays as the manual
    // fallback, and it stays out of the player's way in the developer section.
    [Name("关闭全屏地图的按键")]
    [Description("暂时保留。全屏地图打开时，按此键关闭。理想情况下这一项应该消失——打开和关闭都跟随游戏自己的地图绑定——但需要先实测游戏的 GetOpenMapPressed 在我们的输入上下文里是否可用。")]
    public KeyCode GameMapKey = KeyCode.M;

    [Name("清理已采集的标记（仅报告）")]
    [Description("扫描已采集完的资源标记并写入日志，但不删除。用于确认判据是否正确。")]
    public bool CleanHarvestedMarkers = true;

    [Name("真正删除已采集标记")]
    [Description("确认上一项的日志数量正确后才打开。会从游戏数据里移除这些标记；若标记大面积消失，关掉并重载场景即可恢复。")]
    public bool RemoveHarvestedMarkers = false;

    // ModSettings draws every public field as its own row, so the developer-only knobs would
    // otherwise sit in the middle of the player's list. Hiding them keeps the player's menu
    // short; the values are untouched, so switching developer mode back on restores them.
    protected override void OnChange(FieldInfo field, object oldValue, object newValue)
    {
        base.OnChange(field, oldValue, newValue);

        // Use the value the GUI hands us rather than re-reading the field: ModSettings keeps a
        // separate confirmed-value map so a change can be cancelled, so the field it exposes
        // during a change is not necessarily the new one.
        if (field.Name == nameof(DeveloperMode) && newValue is bool developerMode)
            ApplyVisibility(developerMode, null);
        else if (field.Name == nameof(HudPosition) && newValue is int position)
            ApplyVisibility(null, position);
    }

    // OnChange is driven by the settings GUI, which does not exist yet when the mod loads, so
    // apply the same rules once by hand at startup as well.
    protected override void OnConfirm()
    {
        base.OnConfirm();
        ApplyVisibility(null, null);
    }

    internal void ApplyVisibility(bool? developerMode, int? hudPosition)
    {
        bool developer = developerMode ?? DeveloperMode;
        bool customPosition = (hudPosition ?? HudPosition) == 4;

        try
        {
            SetFieldVisible(nameof(RedirectGameMap), developer);
            SetFieldVisible(nameof(ShowDiagnostics), developer);
            SetFieldVisible(nameof(RecordPointKey), developer);
            SetFieldVisible(nameof(GameMapKey), developer);
            SetFieldVisible(nameof(CleanHarvestedMarkers), developer);
            SetFieldVisible(nameof(RemoveHarvestedMarkers), developer);

            // Only one of the two positioning systems is meaningful at a time: the corner margin
            // does nothing in custom mode, and the custom coordinates do nothing in corner mode.
            SetFieldVisible(nameof(MiniMapPositionX), customPosition);
            SetFieldVisible(nameof(MiniMapPositionY), customPosition);
            SetFieldVisible(nameof(Margin), !customPosition);

            RefreshGUI();
        }
        catch (System.Exception ex)
        {
            MelonLoader.MelonLogger.Warning(
                $"[CommunityMinimap] Could not update settings visibility: {ex.Message}");
        }
    }
}

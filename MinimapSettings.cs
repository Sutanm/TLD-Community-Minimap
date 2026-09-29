// 社区HUD地图 · sutanm — 设置界面与条件显示
using System.Reflection;
using ModSettings;
using UnityEngine;

namespace CommunityMinimap;

internal sealed class MinimapSettings : JsonModSettings
{
    // Field names are the keys in Loader.cfg, so renaming one silently resets it for everyone
    // who already has a config. Only the [Name] display strings are free to change.

    // First in the menu on purpose: which map you are looking at is the one choice that changes
    // what everything below it means, so it is answered before the tuning knobs.
    [Section("地图来源")]

    // Two layers, two sources. They start on the same value so an existing install looks exactly
    // the way it did before the split; the point of the pair is that they can now be told apart.
    [Name("小地图图源")]
    [Description("左上角小地图用哪套图。自动模式优先使用已安装的民间高清地图；缺少图片时使用原版制图。原版制图全区都能用，但只有它带游戏自己的地图标记。")]
    [Choice("自动", "民间高清", "原版制图")]
    public int MiniMapSource = 0;

    [Name("全屏地图图源")]
    [Description("按游戏地图键打开的全屏地图用哪套图。自动模式优先使用已安装的民间高清地图；缺少图片时使用原版制图。")]
    [Choice("自动", "民间高清", "原版制图")]
    public int FullMapSource = 0;

    [Section("小地图")]

    [Name("显示小地图")]
    [Description("角落小地图的总开关，关掉它不会影响全屏地图。全屏地图请按游戏自己的地图键打开；打开后用滚轮缩放、按住左键拖动。")]
    public bool Enabled = true;

    [Name("临时隐藏小地图")]
    [Description("游戏中按此键临时收起小地图，再按一次恢复。不改变保存的设置，也不影响全屏地图。默认 X —— 游戏自己占用了 F5/F6/F8/F9/F10，原来的 F8 正好是调试截屏。")]
    public KeyCode ToggleKey = KeyCode.X;

    [Name("HUD 位置")]
    [Description("小地图贴在屏幕的哪个角；玩家 HUD 那个角落被占用时选「自定义」。")]
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

    // The section header is the only text in this menu that is always on screen - the
    // per-setting descriptions only appear once the player selects a row - so the one thing a
    // new player cannot guess goes here rather than only in a description.
    [Section("全屏地图（按游戏地图键打开，默认 M）")]

    [Name("全屏背景不透明度")]
    [Description("调整完整地图周围的深色背景。0% 完全透明，100% 完全不透明。")]
    [Slider(0f, 1f, 21, NumberFormat = "{0:P0}")]
    public float FullMapBackgroundOpacity = 0.97f;

    [Name("接管游戏地图键")]
    [Description("按游戏自己的「打开地图」动作时显示本模组的全屏地图，而不是游戏内置的地图界面。它拦的是游戏的动作而不是某个按键，所以你在游戏里改过绑定也照样跟随。不会修改游戏地图模块本身；关掉此项即恢复游戏原行为。")]
    public bool RedirectGameMap = true;

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

    [Section("高级")]

    [Name("开发者模式")]
    [Description("打开后显示地图配准、标记诊断等开发用选项。关掉则隐藏它们，值不会丢失。 · sutanm 制作")]
    public bool DeveloperMode = false;

    // The cycle key is deliberately not a developer-only row: it is a convenience the player may
    // want. It is kept in this section only because most players never need it, and the key row
    // hides when the switch is off rather than being left there to be rebound by accident.
    [Name("启用视图轮换按键")]
    [Description("打开后，下面那个键可以在 小地图 → 全屏地图 → 不显示 之间轮换。不需要就关掉这一项，完全不占用键盘，按键提示条里也不会出现。")]
    public bool EnableCycleKey = true;

    [Name("视图轮换按键")]
    [Description("在 小地图 → 全屏地图 → 不显示 之间轮换。游戏自己的默认键位里 Tab 是生存面板（显示时间），如果你把这里也设成 Tab，两者会同时响应——冲突就换成别的键。")]
    public KeyCode CycleViewKey = KeyCode.Tab;

    [Name("显示坐标诊断")]
    [Description("在小地图下方显示场景名、玩家世界坐标和朝向，供地图配准使用。")]
    public bool ShowDiagnostics = false;

    [Name("记录校准点")]
    [Description("站在地图上易识别的地标后按此键，将坐标写入 calibration_points_v2.csv，并保存对应截图。默认 F11 —— 游戏自己占用了 F5/F6/F8/F9/F10，原来的 F9 正好是截屏。")]
    public KeyCode RecordPointKey = KeyCode.F11;

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
            ApplyVisibility(developerMode, null, null);
        else if (field.Name == nameof(HudPosition) && newValue is int position)
            ApplyVisibility(null, position, null);
        else if (field.Name == nameof(EnableCycleKey) && newValue is bool cycleEnabled)
            ApplyVisibility(null, null, cycleEnabled);
    }

    // OnChange is driven by the settings GUI, which does not exist yet when the mod loads, so
    // apply the same rules once by hand at startup as well.
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
            SetFieldVisible(nameof(ShowDiagnostics), developer);
            SetFieldVisible(nameof(RecordPointKey), developer);
            SetFieldVisible(nameof(CleanHarvestedMarkers), developer);
            SetFieldVisible(nameof(RemoveHarvestedMarkers), developer);

            // Only one of the two positioning systems is meaningful at a time: the corner margin
            // does nothing in custom mode, and the custom coordinates do nothing in corner mode.
            SetFieldVisible(nameof(MiniMapPositionX), customPosition);
            SetFieldVisible(nameof(MiniMapPositionY), customPosition);
            SetFieldVisible(nameof(Margin), !customPosition);

            SetFieldVisible(nameof(CycleViewKey), cycleKey);

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

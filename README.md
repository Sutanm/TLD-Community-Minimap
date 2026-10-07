# 社区 HUD 地图

一个面向《漫漫长夜》的 MelonLoader 模组：在角落小地图或可缩放的全屏地图中显示社区地图、
原版制图底图、玩家位置以及游戏登记的地图标记。

当前目标环境：**The Long Dark 2.55 / Unity 6**、MelonLoader 0.7.2、ModSettings 2.2.5。

## 主要功能

- 小地图与全屏地图可分别选择“自动 / 社区地图 / 原版制图”图源。
- 可接管游戏自己的地图键；没有可用地图时会给出明确提示，并可配置是否允许图源回退。
- 全屏地图支持滚轮平滑缩放、左键拖动、悬停名称和独立的玩家指针开关。
- 小地图可选屏幕四角或自定义位置，并可选择是否始终覆盖原版 HUD。
- 地图标记直接读取游戏登记数据，按资源、结构、尸骸、岩石贮藏处和喷漆标记筛选。
- 随机资源组、已采集资源、可再生枝干和大型动物尸骸会跟随当前场景状态更新。
- 可捕获游戏面板的 2048 原版底图；已完成 22 个有效室外场景的捕获。
- 已完成 22 张室外社区地图的粗校准；洞穴、矿井和大型室内图已经完成场景配对，等待粗校准。

## 安装

1. 安装 MelonLoader 与 ModSettings。
2. 将 `CommunityMinimap.dll` 放入游戏的 `Mods` 目录。
3. 创建 `Mods/CommunityMinimap/maps`。
4. 按 [MAPS.md](MAPS.md) 中的内部文件名放入地图图片。

地图图片不随源码仓库分发。重新发布地图素材前请取得原制图者许可并保留署名。

## 默认操作

所有独立按键都可在 ModSettings 中修改；游戏地图键跟随游戏自己的绑定。

| 操作 | 默认 |
|---|---|
| 打开/关闭全屏地图 | 游戏地图键（通常为 `M`） |
| 临时隐藏小地图 | `X` |
| 轮换小地图 / 大地图 / 不显示 | `V` |
| 关闭全屏地图 | `Esc` |
| 缩放 / 平移 | 鼠标滚轮 / 左键拖动 |
| 捕获原版地图（开发者模式） | `P` |
| 记录校准点（开发者模式） | `F11` |

组合键不会误触发单键视图轮换；例如 `Ctrl+V` 不会被当作按下 `V`。

## 精度说明

原版图源使用游戏自身的世界坐标换算，位置精确。社区地图是手工绘制图，不同区域可能存在
非均匀拉伸；模组使用多点仿射拟合，只能保证地标附近达到可用精度，无法把局部绘图形变彻底消除。
需要精确定位时请选择原版图源。

## 文档

当前文档入口是 [docs/INDEX.md](docs/INDEX.md)。地图文件、场景和校准状态见：

- [MAPS.md](MAPS.md)：运行时地图目录
- [docs/STATUS.md](docs/STATUS.md)：项目状态与剩余工作
- [docs/COARSE-CALIBRATION-CHECKLIST.md](docs/COARSE-CALIBRATION-CHECKLIST.md)：下一阶段粗校准清单
- [docs/SETTINGS.md](docs/SETTINGS.md)：设置项参考
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)：实现结构
- [docs/PITFALLS.md](docs/PITFALLS.md)：实测陷阱与硬性纪律

## 构建

先启动一次游戏以生成 IL2CPP 程序集，并安装 ModSettings，然后执行：

```powershell
./build.ps1 -GameDirectory 'D:\SteamLibrary\steamapps\common\TheLongDark'
```

构建脚本会在游戏进程 `tld` 运行时拒绝安装。成功构建后会备份旧 DLL，并把新 DLL 和运行时
JSON 数据安装到游戏的 `Mods` 目录。

## 致谢

- 社区地图素材属于各自的原制图者，不属于本仓库。
- 持久 Unity UI 的实现参考了 MotionTracker 的 Unity 6 场景生命周期处理。
- ModSettings 由 DigitalzombieTLD 及其贡献者维护。

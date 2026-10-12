# 地图文件与可用性

> 更新：2026-10-12。静态室外目录以 `MapDefinition.cs` 为准；洞穴/室内绑定以 `probe-maps.json` 为准。`probe_` ID 与文件名为兼容旧校准保留的内部名称，不代表玩家可见的“探针模式”。

所有图片放在 `<游戏目录>\Mods\CommunityMinimap\maps`。运行时使用 ASCII 文件名，界面显示中文名。

## 室外地图（22/22）

| 地图 | 文件 | Unity 场景 |
|---|---|---|
| 神秘湖 | `mystery_lake.jpg` | `LakeRegion` |
| 孤寂沼地 | `forlorn_muskeg.jpg` | `MarshRegion` |
| 沿海公路 | `coastal_highway.jpg` | `CoastalRegion` |
| 怡人山谷 | `pleasant_valley.jpg` | `RuralRegion` |
| 山间小镇 | `mountain_town.jpg` | `MountainTownRegion` |
| 林狼雪岭 | `timberwolf_mountain.jpg` | `CrashMountainRegion` |
| 灰烬峡谷 | `ash_canyon.jpg` | `AshCanyonRegion` |
| 寂静河谷 | `hushed_river_valley.jpg` | `RiverValleyRegion` |
| 荒凉水湾 | `bleak_inlet.jpg` | `CanneryRegion` |
| 荒芜据点 | `desolation_point.jpg` | `WhalingStationRegion` |
| 黑岩地区 | `blackrock.jpg` | `BlackrockRegion` |
| 断开的铁路 | `broken_railroad.jpg` | `TracksRegion` |
| 废弃机场 | `forsaken_airfield.jpg` | `AirfieldRegion` |
| 污染区 | `zone_of_contamination.jpg` | `MiningRegion` / `ZoneOfContaminationRegion` |
| 破碎山道 | `sundered_pass.jpg` | `MountainPassRegion` / `SunderedPassRegion` |
| 中转通道 | `transfer_pass.jpg` | `HubRegion` / `TransferPassRegion` |
| 公路废墟 | `crumbling_highway.jpg` | `HighwayTransitionZone` |
| 深谷 | `ravine.jpg` | `RavineTransitionZone` |
| 蜿蜒河流 | `winding_river_dam.jpg` | `DamRiverTransitionZoneB` |
| 守山人山隘北侧 | `keepers_pass_north.jpg` | `BlackrockTransitionZone` |
| 守山人山隘南侧 | `keepers_pass_south.jpg` | `CanyonRoadTransitionZone` |
| 远境支路 | `far_range_branch_line.jpg` | `LongRailTransitionZone` |

22 个室外 `mapId` 都有粗校准。粗校准代表出口和主要地标可用，不代表手绘道路每个转角都精确。

## 原版图源

原版地图图片不随发布包分发。每个室外区域按以下顺序加载：

1. 本机 `Mods/CommunityMinimap/captured/<场景>.png` 中捕获的完整 2048 面板图；
2. 游戏提供的 1024 区域纹理。

1024 纹理是可用回退，但内容少于完整面板图，并非仅仅分辨率较低。首次使用原版图源时，建议暂时关闭“使用模组大地图”，逐区域打开一次原版地图面板并停留约一秒。模组会自动保存面板图与对应 `.framing`，之后无需重复操作。发布包不能预置这些从游戏生成的图片。

## 洞穴、矿井与大型室内图（44/44）

全部空间变体都已完成场景绑定和发布处置，并由“启用洞穴与室内地图”统一控制，不再依赖开发者模式。完整逐项结果见 [粗校准清单](docs/COARSE-CALIBRATION-CHECKLIST.md)。

- 已校准空间：显示社区底图、玩家指针；开启“社区地图显示游戏标记”后可显示当前场景标记。
- 参考模式：完整显示底图，不显示玩家指针和动态标记。用于朗斯顿矿场、远境大型连接洞穴等无法可靠拟合的地图。
- 多楼层组合图：按高度与空间范围选择局部校准；楼梯边界采用容差和降级，避免指针飞出地图。
- 污染区集中器：已完成实机复查。

`MineTransitionZone` 的沿海公路下层与煤渣山矿洞上层共用 Unity 场景，运行时根据空间位置自动切换；该行为已实机确认。

## 素材边界

社区地图图片不提交到源码仓库。发布者必须确认分发授权、保留图片内署名，并随包附带 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。全世界拼接总览图只作参考，不由模组加载。

# 原版地图捕获结论（2026-10-01）

## 有效底图

已实测并捕获 **22 个可独立用于 HUD 的室外场景**：

- 16 个主区域：`LakeRegion`、`MountainTownRegion`、`TracksRegion`、`MarshRegion`、
  `CoastalRegion`、`RuralRegion`、`WhalingStationRegion`、`CrashMountainRegion`、
  `RiverValleyRegion`、`CanneryRegion`、`AshCanyonRegion`、`BlackrockRegion`、
  `HubRegion`、`AirfieldRegion`、`MiningRegion`、`MountainPassRegion`。
- 6 个室外过渡区：`RavineTransitionZone`、`DamRiverTransitionZoneB`、
  `HighwayTransitionZone`、`BlackrockTransitionZone`、`CanyonRoadTransitionZone`、
  `LongRailTransitionZone`。

捕获边界有三类，加载时必须读取 `.framing`，不能统一写死：

- 旧区域通常为 `(-325,-325,650,650)`。
- 远境/DLC 区域通常为 `(-300,-300,600,600)`。
- `RavineTransitionZone` 为 `(-325,-162.5,650,325)`。

## 洞穴和室内场景

抽查了连接洞穴、普通洞穴、矿井、卡特大坝、黑岩监狱设施、污染区矿场设施、
泵房和破碎山道建筑。它们全部报告 `Active scene has no map definition`。

在这些场景打开原版 M 页时，游戏只是回退显示所属室外区域的 `*_RegionMap`；玩家指针
使用室内独立坐标后会落在区域图角落或边界外。这不是独立洞穴地图，也不能用作 HUD
底图。模组应在这些场景隐藏地图，除非以后为该场景增加独立社区图和独立校准。

`map_reveal` 在无地图定义的场景会由开发者控制台抛出 `NullReferenceException`，因此不应
用它判断洞穴是否有地图。

## 捕获文件说明

本次测试目录共有 45 份捕获 PNG，其中只有上述 22 个室外场景是有效底图；其余文件是
用于证明原版回退行为的诊断样本，不应打包，也不应自动绑定到场景。

捕获代码现已拒绝为没有 `MapCatalog` 定义的场景保存回退图，避免以后再次产生误绑定。

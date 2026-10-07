# 地图文件与场景目录

> 更新：2026-10-07。代码中的静态目录以 `MapDefinition.cs` 为准；开发期洞穴/室内配对以
> `probe-maps.json` 为准。地图素材不随仓库分发。

所有图片放在 `<游戏目录>\Mods\CommunityMinimap\maps`。运行时使用 ASCII 文件名，界面仍显示中文名。

## 已粗校准的室外地图（22/22）

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

上述 22 个 `mapId` 均已写入 `calibrations.json`。粗校准代表“可用”，不代表手绘图每个道路转角都精确。

## 洞穴、矿井与大型室内图

场景与图片已经全部配对，但仍处于开发探针阶段：默认只有开启“开发者模式”才加载，尚未加入正式
地图目录，也没有玩家指针/标记投影。完整的 32 个场景、33 个空间变体及校准顺序见
[粗校准清单](docs/COARSE-CALIBRATION-CHECKLIST.md)。

特殊情况：`MineTransitionZone` 同一个 Unity 场景包含两张平面图，运行时按玩家高度自动切换：

- `Y >= -70`：`cave_pleasant_valley_coastal.jpg`（煤渣山矿洞上层，沿海公路—怡人山谷）
- `Y < -70`：`interior_coastal_mine_lower.jpg`（沿海公路内部下层）

全世界拼接总览图只作参考，不由模组加载。

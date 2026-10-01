# 全区域传送与地图点亮测试清单

> **状态：本轮测试已完成。** 最终有效底图和无独立地图场景的结论见
> [`VANILLA-MAP-CAPTURE-RESULTS.md`](VANILLA-MAP-CAPTURE-RESULTS.md)。下表保留为以后游戏更新后的回归测试清单。

> 目标：逐一确认每个室外区域、过渡区、洞穴和大型地下子图是否拥有原版地图、是否能用木炭点亮，以及它应绑定哪一张社区地图。
>
> 场景名来自游戏当前导出的 `scene_catalog.csv`；传送语法来自游戏内置开发者控制台的 `scene` 命令。

## 操作规则

1. 使用专门的测试存档，开始前备份存档。
2. 按 `F1` 打开控制台，复制一条 `scene 场景名`，按回车。
3. 载入后可输入 `scene_name`，确认实际场景名。
4. 测试能否打开原版地图；若能打开，再测试木炭制图是否增加迷雾揭示区域。
5. 站在容易从社区图辨认的位置按 `F11`，记录坐标和截图。
6. 只有原版地图已经完整点亮时才在原版地图面板按 `P`。`P` 会覆盖该场景已有的捕获图。
7. 若 `scene` 后出生在无效位置、黑屏或坠落，立即回主菜单，不要覆盖测试存档。

结果建议记为：

- `原版图：有/无`
- `木炭：可/不可`
- `社区图：文件名或主图中的子图名称`
- `备注：出生点、出口、是否与另一个场景共图`

## A. 主区域：16个原版2048候选

这些是目前最可能拥有完整原版区域地图的场景，也是按 `P` 捕获2048底图的第一优先级。

| 完成 | 地区 | 指令 |
|---|---|---|
| [ ] | 神秘湖 | `scene LakeRegion` |
| [ ] | 沿海公路 | `scene CoastalRegion` |
| [ ] | 怡人山谷 | `scene RuralRegion` |
| [ ] | 荒芜据点 | `scene WhalingStationRegion` |
| [ ] | 林狼雪岭 | `scene CrashMountainRegion` |
| [ ] | 孤寂沼地 | `scene MarshRegion` |
| [ ] | 断开的铁路 | `scene TracksRegion` |
| [x] | 山间小镇（已有2048捕获） | `scene MountainTownRegion` |
| [ ] | 寂静河谷 | `scene RiverValleyRegion` |
| [ ] | 荒凉水湾 | `scene CanneryRegion` |
| [ ] | 灰烬峡谷 | `scene AshCanyonRegion` |
| [ ] | 黑岩地区 | `scene BlackrockRegion` |
| [ ] | 中转通道 | `scene HubRegion` |
| [ ] | 废弃机场 | `scene AirfieldRegion` |
| [ ] | 污染区 | `scene MiningRegion` |
| [ ] | 破碎山道 | `scene MountainPassRegion` |

## B. 室外过渡区：6个场景、5张社区图片

守山人山隘南北两侧是两个独立Unity场景，但共用一张社区图片。

| 完成 | 地区 | 指令 | 社区图片 |
|---|---|---|---|
| [ ] | 深谷/乌鸦瀑布铁路 | `scene RavineTransitionZone` | `ravine.jpg` |
| [ ] | 蜿蜒河流 | `scene DamRiverTransitionZoneB` | `winding_river_dam.jpg` |
| [ ] | 公路废墟 | `scene HighwayTransitionZone` | `crumbling_highway.jpg` |
| [ ] | 守山人山隘北侧 | `scene BlackrockTransitionZone` | `keepers_pass.jpg` 左侧 |
| [ ] | 守山人山隘南侧 | `scene CanyonRoadTransitionZone` | `keepers_pass.jpg` 右侧 |
| [ ] | 远境支路 | `scene LongRailTransitionZone` | `far_range_branch_line.jpg` |

## C. 已有独立社区图片的连接洞穴：7张

灰烬峡谷有两个 `AshCave` 场景，当前仅凭文件名无法可靠断定哪个是林狼雪岭连接洞穴，因此两个都要进入一次。

| 完成 | 社区洞穴图 | 指令 | 确定性 |
|---|---|---|---|
| [ ] | 神秘湖—山间小镇 | `scene MountainTownCaveA` | 高 |
| [ ] | 沿海公路—怡人山谷（煤渣山矿洞上层） | `scene HighwayMineTransitionZone` | 高 |
| [ ] | 林狼雪岭—灰烬峡谷候选A | `scene AshCaveA` | 待实测 |
| [ ] | 林狼雪岭—灰烬峡谷候选B | `scene AshCaveB` | 待实测 |
| [ ] | 林狼雪岭—黑岩地区 | `scene BlackrockCaveA` | 高 |
| [ ] | 孤寂沼地—荒凉水湾 | `scene CanneryMarshTransitionCave` | 高 |
| [ ] | 山间小镇—寂静河谷 | `scene RiverValleyTransitionCave` | 高 |
| [ ] | 废弃机场—污染区—破碎山道 | `scene LongTransitionCave` | 高 |

## D. 拼接在大图里的洞穴、矿井和大型建筑

这些不是额外图片文件，而是画在主区域图或连接图边缘的子图。这里的重点是确认场景对应关系以及原版是否为它提供地图。

### 蜿蜒河流与卡特大坝

| 完成 | 子图 | 指令 |
|---|---|---|
| [ ] | 卡特大坝主体候选 | `scene Dam` |
| [ ] | 卡特大坝另一层/过渡部分 | `scene DamTransitionZone` |
| [ ] | 怡人山谷—蜿蜒河流连接洞穴 | `scene DamCaveTransitionZone` |

### 荒芜据点与公路废墟

| 完成 | 子图 | 指令 |
|---|---|---|
| [ ] | 五号废弃矿井 | `scene WhalingMine` |
| [ ] | 三号煤矿厂（公路废墟—荒芜据点） | `scene MineTransitionZone` |
| [ ] | 荒芜据点普通洞穴候选1 | `scene CaveB` |
| [ ] | 荒芜据点普通洞穴候选2 | `scene CaveC` |
| [ ] | 荒芜据点普通洞穴候选3 | `scene CaveD` |

### 林狼雪岭、山间小镇与孤寂沼地子图

| 完成 | 子图 | 指令 |
|---|---|---|
| [ ] | 林狼雪岭山内洞穴候选A | `scene MountainCaveA` |
| [ ] | 林狼雪岭山内洞穴候选B | `scene MountainCaveB` |
| [ ] | 山间小镇内部连接洞穴候选 | `scene MountainTownCaveB` |

> 孤寂沼地主图右下角也有洞穴子图。它很可能属于 `CaveB/C/D` 之一；跑完三个通用洞穴后按形状确定，不重复传送。

### 寂静河谷

| 完成 | 子图 | 指令 |
|---|---|---|
| [ ] | 北部冰洞 | `scene IceCaveA` |
| [ ] | 南部冰洞 | `scene IceCaveB` |

### 灰烬峡谷

| 完成 | 子图 | 指令 |
|---|---|---|
| [ ] | 金矿 | `scene AshMine` |

> `AshCaveA`、`AshCaveB` 中一个应是主图右侧的内部洞穴，另一个应是林狼雪岭连接洞穴；在C组已列出，跑一次即可。

### 黑岩地区

| 完成 | 子图 | 指令 |
|---|---|---|
| [ ] | 黑岩监狱内部 | `scene BlackrockInteriorASurvival` |
| [ ] | 黑岩发电站 | `scene BlackrockPowerplantA` |
| [ ] | 黑岩蒸汽通道 | `scene BlackrockSteamTunnelsASurvival` |
| [ ] | 黑岩废弃矿井 | `scene BlackrockMineA` |

### 守山人山隘与远境支路

| 完成 | 子图 | 指令 |
|---|---|---|
| [ ] | 守山人山隘南北连接洞穴 | `scene CanyonRoadCave` |
| [ ] | 远境支路铁路工人小道 | `scene HubCave` |

### 污染区

| 完成 | 子图 | 指令 |
|---|---|---|
| [ ] | 集中器/选矿建筑 | `scene MineConcentratorBuilding` |
| [ ] | 朗斯顿矿场地下矿区 | `scene MiningRegionMine` |

### 破碎山道

| 完成 | 子图 | 指令 |
|---|---|---|
| [ ] | 破碎山道洞穴候选A | `scene MountainPassCaveA` |
| [ ] | 破碎山道洞穴候选B | `scene MountainPassCaveB` |

## 建议的回报格式

每跑完一个场景，按下面一行回报即可：

```text
LakeRegion：成功进入；原版地图有；木炭可点亮；已按F11；尚未完整点亮，未按P。
```

若场景对应关系不确定，补一句看到的结构，例如：

```text
AshCaveA：两个水池、东西两个出口；对应灰烬峡谷主图右侧子图；原版地图无。
```

这样可以直接把实测结果回填为最终的场景—社区图片绑定表。

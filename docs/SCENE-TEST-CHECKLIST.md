# 全区域传送与地图点亮测试清单（已归档）

> **归档状态（2026-10-07）：场景配对和原版底图测试均已完成。** 本文保留完整实测过程，
> 表格中遗留的 `[ ]` 不再表示当前待办。原版底图结论见
> [`VANILLA-MAP-CAPTURE-RESULTS.md`](VANILLA-MAP-CAPTURE-RESULTS.md)，当前场景—图片目录见
> [`../MAPS.md`](../MAPS.md)，下一阶段实际操作清单见
> [`COARSE-CALIBRATION-CHECKLIST.md`](COARSE-CALIBRATION-CHECKLIST.md)。

> 目标：逐一确认每个室外区域、过渡区、洞穴和大型地下子图是否拥有原版地图、是否能用木炭点亮，以及它应绑定哪一张社区地图。
>
> 场景名来自游戏当前导出的 `scene_catalog.csv`；传送语法来自游戏内置开发者控制台的 `scene` 命令。
>
> **严重警告（2026-10-03 实测）**：`scene` 是破坏存档状态的调试命令，不是正常的跨区传送。
> 它会绕过正常过图和目标区域的存档恢复，可能重置玩家状态、区域探索状态、容器和动态物件；
> 之后进出建筑触发自动保存时，重置后的区域数据可能被写回存档。只允许在一次性测试档中使用，
> 严禁用于社区地图校准或正常游戏存档。社区地图校准必须自然过图进入区域，只能在当前室外场景内用
> `tp` 移动。

## 操作规则

1. 使用一次性的专门测试存档，开始前备份存档；执行过 `scene` 后不得继续正常游玩。
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
| [x] | 三号煤矿厂（公路废墟—荒芜据点） | `scene HighwayMineTransitionZone` | 高 |
| [ ] | 林狼雪岭—灰烬峡谷候选A | `scene AshCaveA` | 待实测 |
| [ ] | 林狼雪岭—灰烬峡谷候选B | `scene AshCaveB` | 待实测 |
| [ ] | 林狼雪岭—黑岩地区 | `scene BlackrockCaveA` | 高 |
| [ ] | 孤寂沼地—荒凉水湾 | `scene CanneryMarshTransitionCave` | 高 |
| [ ] | 山间小镇—寂静河谷 | `scene RiverValleyTransitionCave` | 高 |
| [ ] | 废弃机场—污染区—破碎山道 | `scene LongTransitionCave` | 高 |

## D. 拼接在大图里的洞穴、矿井和大型建筑

> 裁剪规范：所有洞穴/室内子图均以其黑色矩形外框的**外沿像素**为最终边界，
> 四边不得保留坐标轴、刻度或额外白边，也不得切掉标题/徽标所在的框线。
> 遇到外层坐标轴框与内层地图框并存时，以包住该子图完整标题、图例和路线的内层黑框为准。
> `manual-crops/` 中与输出同名的文件优先于清单里的矩形裁剪坐标；手工修好的异形或
> 多楼层组合图因此不会在再次运行 `prepare-maps.ps1` 时被旧裁剪覆盖。
> 黑岩废弃矿地使用白底 PNG 保留原图 L 形内容：缺口区域填充为地图白底，既避免把相邻的
> 蒸汽通道子图带入，也避免全屏 HUD 透出游戏画面形成突兀的黑色缺角。
>
> 多楼层或多栋房屋共用一张图时保留原始组合图，不拆成多份纹理。校准数据按
> `场景 + 楼层/子区域` 分组；运行时优先用玩家高度和所在范围自动选组，边界无法可靠
> 判定时再提供手动切层作为后备。这样既保留原图的连接线，也避免不同楼层共用 X/Z
> 时把玩家指针投到错误楼层。

这些不是额外图片文件，而是画在主区域图或连接图边缘的子图。这里的重点是确认场景对应关系以及原版是否为它提供地图。

### 蜿蜒河流与卡特大坝

| 完成 | 子图 | 指令 |
|---|---|---|
| [x] | 卡特大坝上层 | `scene Dam` |
| [x] | 卡特大坝下层 | `scene DamTransitionZone` |
| [x] | 怡人山谷—蜿蜒河流连接洞穴 | `scene DamCaveTransitionZone` |

### 荒芜据点与公路废墟

| 完成 | 子图 | 指令 |
|---|---|---|
| [x] | 五号废弃矿井 | `scene WhalingMine` |
| [x] | 沿海公路废弃矿地／煤渣山煤矿 | `scene MineTransitionZone` |

> `MineTransitionZone` 同时包含两套平面：`interior_coastal_mine_lower.jpg` 是仅服务沿海公路内部的下层；
> `cave_pleasant_valley_coastal.jpg` 是连接沿海公路—怡人山谷的上层。游戏 Addressables 场景目录中没有独立的上层场景。
> F11 实测上层 Y=0.909、下层 Y=-80.992；运行时以 Y=-70 为界自动切换两张地图，2026-10-07 已实机验证通过。
| [x] | 怡人山谷迷雾瀑布附近洞穴 | `scene CaveB` |
| [x] | 荒芜据点瀑布旁普通洞穴 | `scene CaveC` |
| [x] | 孤寂沼地洞穴 AB（A入口） | `scene CaveD` |

### 林狼雪岭、山间小镇与孤寂沼地子图

| 完成 | 子图 | 指令 |
|---|---|---|
| [x] | 山间小镇本地洞穴 | `scene MountainTownCaveA` |
| [x] | 林狼雪岭山内洞穴 CD（D入口） | `scene MountainCaveA` |
| [x] | 林狼雪岭山内洞穴 AB（B入口） | `scene MountainCaveB` |
| [x] | 山间小镇—神秘湖连接洞穴（B场景别名） | `scene MountainTownCaveB` |

> 孤寂沼地主图右下角也有洞穴子图。它很可能属于 `CaveB/C/D` 之一；跑完三个通用洞穴后按形状确定，不重复传送。

### 寂静河谷

| 完成 | 子图 | 指令 |
|---|---|---|
| [x] | 北部冰洞（F入口实测） | `scene IceCaveB` |
| [x] | 南部冰洞（A入口实测） | `scene IceCaveA` |

### 灰烬峡谷

| 完成 | 子图 | 指令 |
|---|---|---|
| [x] | 金矿 | `scene AshMine` |

> 已实测 `AshCaveA` 是林狼雪岭—灰烬峡谷连接洞穴；`AshCaveB` 是灰烬峡谷内部洞穴 AB，传送落点为 B 入口。

### 黑岩地区

| 完成 | 子图 | 指令 |
|---|---|---|
| [x] | 黑岩监狱内部 | `scene BlackrockInteriorASurvival` |
| [x] | 黑岩监狱场地 | `scene BlackrockPrisonSurvivalZone` |
| [x] | 黑岩发电站内部（社区原图仅标入口，无内部平面图；不绑定） | `scene BlackrockPowerplantA` |
| [x] | 黑岩蒸汽通道 | `scene BlackrockSteamTunnelsASurvival` |
| [x] | 黑岩废弃矿井 | `scene BlackrockMineA` |

### 守山人山隘与远境支路

| 完成 | 子图 | 指令 |
|---|---|---|
| [x] | 守山人山隘南北连接洞穴 | `scene CanyonRoadCave` |
| [x] | 远境三地区连接洞穴（废弃机场—污染区—破碎山道） | `scene HubCave` |
| [x] | 铁路工人小道 | `scene LongTransitionCave` |

> `HubCave` 实测为远境三地区连接洞穴；`LongTransitionCave` 实测为铁路工人小道。

### 污染区

| 完成 | 子图 | 指令 |
|---|---|---|
| [x] | 集中器/选矿建筑 | `scene MineConcentratorBuilding` |
| [x] | 朗斯顿矿场地下矿区 | `scene MiningRegionMine` |

### 破碎山道

| 完成 | 子图 | 指令 |
|---|---|---|
| [x] | 破碎山道洞穴 1/2/3/4（洞口1） | `scene MountainPassCaveA` |
| [x] | 破碎山道废弃矿地 | `scene MountainPassCaveB` |

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

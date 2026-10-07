# 洞穴与室内地图粗校准清单

> 建立：2026-10-07。场景与图片已经全部实机配对；本清单只跟踪尚未建立世界坐标投影的新内容。
> 当前进度：**0 / 33 空间变体**。

## 安全与操作规则

1. `scene` 会绕过正常区域存档恢复，可能重置区域、容器和动态物件。只在一次性测试档使用；正式校准优先自然过图。
2. 开启“开发者模式”和“显示坐标诊断”，确认 HUD 显示的场景名与表格一致。
3. 每张单层图至少选 **4 个**分散锚点；普通面状图建议 5 个，狭长洞穴至少覆盖两端和中段转折。
4. 站在地图中可明确辨认的位置按 `F11`。黄色手工点是事实值；绿色圆圈只是拟合预览，最终应尽量落回黄色点。
5. 不要用巨大社区图标的中心猜位置，优先选出口门槛、绳点、固定楼梯、轨道交叉和房间转角。
6. 校准 JSON 写入后重载场景验证玩家指针；“出口和地标准确、道路局部偏”可记录为手绘图形变。
7. 多楼层组合图在开始前先记录玩家 Y。若同一 X/Z 在不同楼层重叠，停止全局拟合，先设计空间分组。

## 批次 1：单层小图（优先，12 个）

这些图最适合先验证洞穴投影流程，预计每张 4–5 点即可。

| 完成 | 内容 | 场景指令 | mapId | 图片 |
|---|---|---|---|---|
| [ ] | 山间小镇本地洞穴 | `scene MountainTownCaveA` | `probe_interior_mountain_town_cave` | `interior_mountain_town_cave.jpg` |
| [ ] | 神秘湖—山间小镇连接洞穴 | `scene MountainTownCaveB` | `probe_cave_mystery_lake_mountain_town` | `cave_mystery_lake_mountain_town.jpg` |
| [ ] | 三号煤矿厂 | `scene HighwayMineTransitionZone` | `probe_interior_coal_mine_no3` | `interior_coal_mine_no3.jpg` |
| [ ] | 林狼雪岭—灰烬峡谷连接洞穴 | `scene AshCaveA` | `probe_cave_ash_canyon_timberwolf` | `cave_ash_canyon_timberwolf.jpg` |
| [ ] | 灰烬峡谷内部洞穴 AB | `scene AshCaveB` | `probe_interior_ash_local_cave` | `interior_ash_local_cave.jpg` |
| [ ] | 林狼雪岭—黑岩连接洞穴 | `scene BlackrockCaveA` | `probe_cave_timberwolf_blackrock` | `cave_timberwolf_blackrock.jpg` |
| [ ] | 孤寂沼地—荒凉水湾连接洞穴 | `scene CanneryMarshTransitionCave` | `probe_cave_forlorn_bleak_inlet` | `cave_forlorn_bleak_inlet.jpg` |
| [ ] | 山间小镇—寂静河谷连接洞穴 | `scene RiverValleyTransitionCave` | `probe_cave_hrv_mountain_town` | `cave_hrv_mountain_town.jpg` |
| [ ] | 铁路工人小道 | `scene LongTransitionCave` | `probe_interior_railway_worker_tunnel` | `interior_railway_worker_tunnel.jpg` |
| [ ] | 五号废弃矿井 | `scene WhalingMine` | `probe_interior_desolation_mine` | `interior_desolation_mine.jpg` |
| [ ] | 守山人山隘南北连接洞穴 | `scene CanyonRoadCave` | `probe_interior_keepers_pass_cave` | `interior_keepers_pass_cave.jpg` |
| [ ] | 怡人山谷—蜿蜒河流连接洞穴 | `scene DamCaveTransitionZone` | `probe_interior_carter_cave` | `interior_carter_cave.jpg` |

## 批次 2：单层但狭长/复杂（12 个）

| 完成 | 内容 | 场景指令 | mapId | 图片 |
|---|---|---|---|---|
| [ ] | 寂静河谷寒冰洞窟南（A入口） | `scene IceCaveA` | `probe_interior_hrv_ice_cave_south` | `interior_hrv_ice_cave_south.jpg` |
| [ ] | 寂静河谷寒冰洞窟北（F入口） | `scene IceCaveB` | `probe_interior_hrv_ice_cave_north` | `interior_hrv_ice_cave_north.jpg` |
| [ ] | 灰烬峡谷金矿 | `scene AshMine` | `probe_interior_ash_gold_mine` | `interior_ash_gold_mine.jpg` |
| [ ] | 林狼雪岭山内洞穴 CD | `scene MountainCaveA` | `probe_interior_timberwolf_cave_lower` | `interior_timberwolf_cave_lower.jpg` |
| [ ] | 林狼雪岭山内洞穴 AB | `scene MountainCaveB` | `probe_interior_timberwolf_cave_upper` | `interior_timberwolf_cave_upper.jpg` |
| [ ] | 怡人山谷迷雾瀑布洞穴 | `scene CaveB` | `probe_interior_pleasant_valley_cave` | `interior_pleasant_valley_cave.jpg` |
| [ ] | 荒芜据点瀑布旁洞穴 | `scene CaveC` | `probe_interior_desolation_cave` | `interior_desolation_cave.jpg` |
| [ ] | 孤寂沼地洞穴 AB | `scene CaveD` | `probe_interior_forlorn_cave` | `interior_forlorn_cave.jpg` |
| [ ] | 远境三地区连接洞穴 | `scene HubCave` | `probe_cave_airfield_contamination_sundered` | `cave_airfield_contamination_sundered.jpg` |
| [ ] | 黑岩蒸汽通道 | `scene BlackrockSteamTunnelsASurvival` | `probe_interior_blackrock_steam_tunnels` | `interior_blackrock_steam_tunnels.jpg` |
| [ ] | 破碎山道废弃矿地 | `scene MountainPassCaveB` | `probe_interior_sundered_mine` | `interior_sundered_mine.jpg` |
| [ ] | 黑岩监狱场地 | `scene BlackrockPrisonSurvivalZone` | `probe_interior_blackrock_prison_grounds` | `interior_blackrock_prison_grounds.jpg` |

## 批次 3：高度切换与多楼层组合图（9 个空间变体）

这些图不要直接套单一矩阵。先确认楼层/子区域的 Y 范围和空间边界，再决定是否拆成多个 mapId。

| 完成 | 内容 | 场景指令 | 当前 mapId | 图片 / 备注 |
|---|---|---|---|---|
| [ ] | 沿海公路废弃矿地下层 | `scene MineTransitionZone` | `probe_interior_coastal_mine_lower` | `interior_coastal_mine_lower.jpg`，`Y < -70` |
| [ ] | 煤渣山矿洞上层 | `scene MineTransitionZone` | `probe_cave_pleasant_valley_coastal` | `cave_pleasant_valley_coastal.jpg`，`Y >= -70` |
| [ ] | 卡特大坝上层 | `scene Dam` | `probe_interior_carter_dam_upper` | `interior_carter_dam_upper.jpg` |
| [ ] | 卡特大坝下层 | `scene DamTransitionZone` | `probe_interior_carter_dam_lower` | `interior_carter_dam_lower.jpg` |
| [ ] | 黑岩监狱室内 1/2/3 层 | `scene BlackrockInteriorASurvival` | `probe_interior_blackrock_prison` | `interior_blackrock_prison.jpg` |
| [ ] | 黑岩废弃矿地 1/2/3 层 | `scene BlackrockMineA` | `probe_interior_blackrock_mine` | `interior_blackrock_mine.png` |
| [ ] | 污染区集中器建筑 | `scene MineConcentratorBuilding` | `probe_interior_concentrator_complex` | `interior_concentrator_complex.jpg` |
| [ ] | 朗斯顿矿场 1/2/3 层 | `scene MiningRegionMine` | `probe_interior_contamination_mine` | `interior_contamination_mine.jpg` |
| [ ] | 破碎山道洞穴 1/2/3/4 | `scene MountainPassCaveA` | `probe_interior_sundered_cave` | `interior_sundered_cave.jpg` |

## 明确跳过

- `BlackrockPowerplantA`：发电厂内部只是剧情过渡直线，没有对应可用社区平面图，不进入校准清单。
- 黑色外框之外的坐标轴、标尺和相邻子图：已经在手工裁剪阶段处理，不作为校准内容。

## 每张图的完成标准

- 校准器最大误差最好不超过 45 px；超过时先复查手工点，不能只看拟合残差。
- 游戏内至少验证两个出口和一个中段地标；狭长图验证两端。
- HUD 默认总览观感正常，0.35x 最小缩放能看全图，拖拽边界不会切掉出口文字。
- 无连续异常、无错误图源、场景重载后仍能加载。
- 完成后把校准写入 `calibrations.json`，再决定是否从 `ProbeOnly` 提升为正式图。

## 下一项

从批次 1 第一项开始：`scene MountainTownCaveA`（山间小镇本地洞穴）。它图形简单、单层、出口明确，
最适合验证新一轮洞穴粗校准的完整流程。

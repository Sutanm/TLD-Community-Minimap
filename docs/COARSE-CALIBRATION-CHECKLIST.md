# 洞穴与室内地图粗校准清单

> 建立：2026-10-07。场景与图片已经全部实机配对；本清单只跟踪尚未建立世界坐标投影的新内容。
> 当前进度：**44 / 44 空间变体**。

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
| [x] | 山间小镇本地洞穴 | `scene MountainTownCaveA` | `probe_interior_mountain_town_cave` | `interior_mountain_town_cave.jpg`（全局仿射存在明显局部偏移，暂按可用记录） |
| [x] | 神秘湖—山间小镇连接洞穴 | `scene MountainTownCaveB` | `probe_cave_mystery_lake_mountain_town` | `cave_mystery_lake_mountain_town.jpg`（明显局部拉伸，暂按粗校准可用记录） |
| [x] | 三号煤矿厂 | `scene HighwayMineTransitionZone` | `probe_interior_coal_mine_no3` | `interior_coal_mine_no3.jpg` |
| [x] | 林狼雪岭—灰烬峡谷连接洞穴 | `scene AshCaveA` | `probe_cave_ash_canyon_timberwolf` | `cave_ash_canyon_timberwolf.jpg` |
| [x] | 灰烬峡谷内部洞穴 AB | `scene AshCaveB` | `probe_interior_ash_local_cave` | `interior_ash_local_cave.jpg` |
| [x] | 林狼雪岭—黑岩连接洞穴 | `scene BlackrockCaveA` | `probe_cave_timberwolf_blackrock` | `cave_timberwolf_blackrock.jpg` |
| [x] | 孤寂沼地—荒凉水湾连接洞穴 | `scene CanneryMarshTransitionCave` | `probe_cave_forlorn_bleak_inlet` | `cave_forlorn_bleak_inlet.jpg` |
| [x] | 山间小镇—寂静河谷连接洞穴 | `scene RiverValleyTransitionCave` | `probe_cave_hrv_mountain_town` | `cave_hrv_mountain_town.jpg` |
| [x] | 铁路工人小道 | `scene LongTransitionCave` | `probe_interior_railway_worker_tunnel` | `interior_railway_worker_tunnel.jpg` |
| [x] | 五号废弃矿井 | `scene WhalingMine` | `probe_interior_desolation_mine` | `interior_desolation_mine.jpg` |
| [x] | 守山人山隘南北连接洞穴 | `scene CanyonRoadCave` | `probe_interior_keepers_pass_cave` | `interior_keepers_pass_cave.jpg` |
| [x] | 怡人山谷—蜿蜒河流连接洞穴 | `scene DamCaveTransitionZone` | `probe_interior_carter_cave` | `interior_carter_cave.jpg` |

## 批次 2：单层但狭长/复杂（12 个）

| 完成 | 内容 | 场景指令 | mapId | 图片 |
|---|---|---|---|---|
| [x] | 寂静河谷寒冰洞窟南（A入口） | `scene IceCaveA` | `probe_interior_hrv_ice_cave_south` | `interior_hrv_ice_cave_south.jpg` |
| [x] | 寂静河谷寒冰洞窟北（F入口） | `scene IceCaveB` | `probe_interior_hrv_ice_cave_north` | `interior_hrv_ice_cave_north.jpg` |
| [x] | 灰烬峡谷金矿 | `scene AshMine` | `probe_interior_ash_gold_mine` | `interior_ash_gold_mine.jpg` |
| [x] | 林狼雪岭山内洞穴 CD | `scene MountainCaveA` | `probe_interior_timberwolf_cave_lower` | `interior_timberwolf_cave_lower.jpg` |
| [x] | 林狼雪岭山内洞穴 AB | `scene MountainCaveB` | `probe_interior_timberwolf_cave_upper` | `interior_timberwolf_cave_upper.jpg` |
| [x] | 怡人山谷迷雾瀑布洞穴 | `scene CaveB` | `probe_interior_pleasant_valley_cave` | `interior_pleasant_valley_cave.jpg` |
| [x] | 荒芜据点瀑布旁洞穴 | `scene CaveC` | `probe_interior_desolation_cave` | `interior_desolation_cave.jpg` |
| [x] | 孤寂沼地洞穴 AB | `scene CaveD` | `probe_interior_forlorn_cave` | `interior_forlorn_cave.jpg` |
| [x] | 远境三地区连接洞穴 | `scene HubCave` | `probe_cave_airfield_contamination_sundered` | `cave_airfield_contamination_sundered.jpg`；有意不校准：社区图缺少稳定参照且单一仿射严重漂移，角落小地图以无指针、无资源标记的全貌参考图显示 |
| [x] | 黑岩蒸汽通道 | `scene BlackrockSteamTunnelsASurvival` | `probe_interior_blackrock_steam_tunnels` | `interior_blackrock_steam_tunnels.jpg` |
| [x] | 破碎山道废弃矿地 | `scene MountainPassCaveB` | `probe_interior_sundered_mine` | `interior_sundered_mine.jpg`（宽阔直线洞穴，四点拟合已足够） |
| [x] | 黑岩监狱场地 | `scene BlackrockPrisonSurvivalZone` | `probe_interior_blackrock_prison_grounds` | `interior_blackrock_prison_grounds.jpg` |

## 批次 3：高度切换与多楼层组合图（18 个空间变体）

这些图不要直接套单一矩阵。先确认楼层/子区域的 Y 范围和空间边界，再决定是否拆成多个 mapId。

| 完成 | 内容 | 场景指令 | 当前 mapId | 图片 / 备注 |
|---|---|---|---|---|
| [x] | 沿海公路废弃矿地深层 | `scene MineTransitionZone` | `probe_interior_coastal_mine_lower` | `interior_coastal_mine_lower.jpg`；实测 `Y≈-81..-75`，使用 `Y < -66` |
| [x] | 沿海公路废弃矿地电梯上层 | `scene MineTransitionZone` | `probe_interior_coastal_mine_elevator` | 与深层共用 `interior_coastal_mine_lower.jpg`，但使用独立投影；实测 `Y≈-57..-49`，使用 `-66 <= Y < -35` |
| [x] | 煤渣山矿洞上层 | `scene MineTransitionZone` | `probe_cave_pleasant_valley_coastal` | `cave_pleasant_valley_coastal.jpg`，实测从 `Y≈-20` 延伸至 0 以上；使用 `Y >= -35` |
| [x] | 卡特大坝上层区域 A | `scene Dam` | `probe_interior_carter_dam_upper_zone_a` | `interior_carter_dam_upper.jpg` |
| [x] | 卡特大坝上层区域 B | `scene Dam` | `probe_interior_carter_dam_upper_zone_b` | `interior_carter_dam_upper.jpg` |
| [x] | 卡特大坝上层区域 C | `scene Dam` | `probe_interior_carter_dam_upper_zone_c` | `interior_carter_dam_upper.jpg`，`Y=4.70` |
| [x] | 卡特大坝楼梯间负一层 | `scene Dam` | `probe_interior_carter_dam_upper_stairs_b1` | `interior_carter_dam_upper.jpg`，`Y=-4.55..-2.14` |
| [x] | 卡特大坝楼梯间一层 | `scene Dam` | `probe_interior_carter_dam_upper_stairs_f1` | `interior_carter_dam_upper.jpg`，`Y=0.08..2.47` |
| [x] | 卡特大坝楼梯间二层 | `scene Dam` | `probe_interior_carter_dam_upper_stairs_f2` | `interior_carter_dam_upper.jpg`，`Y=4.697..7.042` |
| [x] | 卡特大坝楼梯间三层 | `scene Dam` | `probe_interior_carter_dam_upper_stairs_f3` | `interior_carter_dam_upper.jpg`，`Y=7.042..9.268` |
| [x] | 卡特大坝下层·上部区域 | `scene DamTransitionZone` | `probe_interior_carter_dam_lower_upper` | `interior_carter_dam_lower.jpg`；楼梯过渡带保持上一层和最后有效点 |
| [x] | 卡特大坝下层·中部区域 | `scene DamTransitionZone` | `probe_interior_carter_dam_lower_middle` | `interior_carter_dam_lower.jpg`；稳定层约 `Y=-5.9` |
| [x] | 卡特大坝下层·深部区域 | `scene DamTransitionZone` | `probe_interior_carter_dam_lower_deep` | `interior_carter_dam_lower.jpg`；稳定层约 `Y=-8.7` |
| [x] | 黑岩监狱室内 1/2/3 层 | 一、二层：`scene BlackrockInteriorASurvival`；三层：`scene BlackrockPrisonSurvivalZone` | `probe_interior_blackrock_prison_floor1` / `floor2` / `floor3` | `interior_blackrock_prison.jpg`；一层已用 11 个锚点重做，二层 `Y≈3.9`，三层 `Y≈234.05`。三层仅在 `X=-245..-225 / Z=153..169 / Y=232..236` 覆盖场地图，离开该范围显式切回 `probe_interior_blackrock_prison_grounds` |
| [x] | 黑岩废弃矿地 1/2/3 层 | `scene BlackrockMineA` | `probe_interior_blackrock_mine_floor1` / `floor2` / `floor3` | `interior_blackrock_mine.png`；1F 13 点（`Y=-58.43..-49.24`）、2F 4 点（`Y=-69.22..-62.37`）、3F 5 点（`Y=-91.71..-82.24`）；在 `-62..-59` 与 `-80..-72` 两段保持上一层，离开过渡带后切换；实机复查三层偏差明显但勉强可用 |
| [x] | 污染区集中器建筑 | `scene MineConcentratorBuilding` | `probe_interior_concentrator_mining_floor1..6` / `probe_interior_concentrator_conveyor_floor1..2` / `probe_interior_concentrator_headframe_floor1..2` / `probe_interior_concentrator_headframe_roof_lower` / `roof_upper` | `interior_concentrator_complex.jpg`；全部分区锚点、X/Z+Y 路由及实机复查均已完成。采矿建筑 1F 4 点（`Y=1.048..1.293`）；2F 5 点（`Y=11.489..17.386`，社区图存在偏差且同层有明显高度变化）；3F 为简单转弯、3 点（`Y=22.112..23.903`）；4F 4 点（`Y=30.617..32.192`）；5F 4 点（`Y=44.002..44.343`）；6F 5 点（`Y=54.512..54.608`）。输送建筑 1F 3 点（`Y=-2.397..-2.258`），2F/楼梯上段 3 点（`Y=2.316..5.716`）。井架建筑 1F 4 点（`Y=1.769..3.024`），2F 3 点（`Y=12.186..13.459`），屋顶下段 4 点（`Y=29.655..29.743`），屋顶上段 3 点（`Y=34.927..34.967`） |
| [x] | 朗斯顿矿场 1/2/3 层 | `scene MiningRegionMine` | `probe_interior_contamination_mine` | `interior_contamination_mine.jpg`；有意不校准：连续路线高度变化大且社区图没有稳定参照，角落小地图以无指针、无资源标记的全貌参考图显示 |
| [x] | 破碎山道连续洞穴 | `scene MountainPassCaveA` | `probe_interior_sundered_cave` | `interior_sundered_cave.jpg`；图上的 1/2/3/4 并非叠层。8 个锚点横跨 `Y=-47.866..45.667`，单一全图投影精度有限；实机确认勉强可用并结束复校 |

## 明确跳过

- `BlackrockPowerplantA`：发电厂内部只是剧情过渡直线，没有对应可用社区平面图，不进入校准清单。
- 黑色外框之外的坐标轴、标尺和相邻子图：已经在手工裁剪阶段处理，不作为校准内容。

## 每张图的完成标准

- 校准器最大误差最好不超过 45 px；超过时先复查手工点，不能只看拟合残差。
- 游戏内至少验证两个出口和一个中段地标；狭长图验证两端。
- HUD 默认总览观感正常，0.35x 最小缩放能看全图，拖拽边界不会切掉出口文字。
- 无连续异常、无错误图源、场景重载后仍能加载。
- 完成后把校准写入 `calibrations.json`；无法可靠拟合的地图明确记录为无指针参考图。

校准阶段已经收口。后续只处理实际玩家反馈，不再以增加锚点作为发布前置条件。

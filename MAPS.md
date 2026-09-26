# Map filenames

Map artwork is external to this repository. The mod uses ASCII filenames internally
while retaining Chinese display names in the UI and source artwork.

| Display name | Internal filename | Known scene |
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
| 公路废墟 | `crumbling_highway.jpg` | `HighwayTransitionZone` |
| 深谷 | `ravine.jpg` | `RavineTransitionZone` |
| 守山人山隘北侧 | `keepers_pass.jpg` | `BlackrockTransitionZone` |
| 守山人山隘南侧 | `keepers_pass.jpg` | `CanyonRoadTransitionZone` |
| 蜿蜒河流 | `winding_river_dam.jpg` | `DamRiverTransitionZoneB` |
| 污染区 | `zone_of_contamination.jpg` | provisional aliases |
| 破碎山道 | `sundered_pass.jpg` | provisional aliases |
| 远境支路 | `far_range_branch_line.jpg` | provisional aliases |
| 中转通道 | `transfer_pass.jpg` | provisional aliases |

Connection-cave composite images are stored under descriptive `cave_*.jpg` names.
They are not assigned to a scene until the exact active scene name and the image's
usable sub-rectangle have been recorded in game.

The stitched all-world overview image is reference material only. It is not loaded,
copied, or used by the mod.

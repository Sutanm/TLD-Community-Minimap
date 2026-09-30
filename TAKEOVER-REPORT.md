# 社区HUD地图：接手勘察报告

> # 🗄️ 已归档（2026-09-30）
>
> 这是 2026-09-27 的**接手初期**勘察报告，当时版本 `0.6.1`，依据的是 447 行的旧手册。
> 报告里的结论已被后续工作**大面积覆盖**：标记重写、图源分离、地名、悬停、
> 底图抓取都是它之后做的。
>
> **当前文档见 [`docs/INDEX.md`](docs/INDEX.md)。**
> 本文件保留作历史记录 —— 它记录了"接手时项目长什么样"。

> 勘察时间：2026-09-27（Asia/Shanghai）
> 勘察范围：只读探索 + 构建验证。**本报告未修改任何模组源码、校准数据或场景绑定。**
> 依据文档：`HANDOFF.md`（447 行，已完整阅读）
> 当前版本：`0.6.1`

---

## 0. 结论摘要

项目状态**健康**，可以按 `HANDOFF.md` 的主线直接接手。

| 项目 | 结果 |
|---|---|
| Release 构建 | ✅ **0 警告 / 0 错误** |
| 运行时稳定性 | ✅ 上局日志无崩溃、无 `AccessViolation` |
| 场景绑定正确性 | ✅ **22 / 22 全部有效**，无一处需要修正 |
| 校准覆盖率 | ⚠️ **3 / 22**，剩 19 张室外地图待粗校准 |
| Git 工作区 | ✅ 干净 |
| 与远端同步 | ⚠️ 本地领先 2 个提交，**推送因网络失败**（可重试） |

**没有发现阻塞性缺陷。** 唯一需要你决策的是：从哪张地图继续粗校准。

---

## 1. 环境与构建验证

| 项 | 值 |
|---|---|
| 工作区 | `D:\CommunityMinimap-Workspace` |
| 游戏目录 | `D:\Program Files (x86)\Steam\steamapps\common\TheLongDark` |
| 游戏进程 | 未运行（可安全替换 DLL） |
| .NET SDK | `10.0.401`（工程 `TargetFramework` 为 `net6.0`，可正常构建） |
| 已安装 DLL | `CommunityMinimap.dll`，46592 字节，2026-09-26 22:23:51 |

构建命令（已实测通过）：

```powershell
dotnet build .\CommunityMinimap.csproj -c Release `
  -p:GameDirectory='D:\Program Files (x86)\Steam\steamapps\common\TheLongDark'
```

输出：`已成功生成。0 个警告，0 个错误`。

> 注意：直接用上面的 `dotnet build` **不会**覆盖游戏目录的 DLL（工程未配置拷贝目标），
> 这是安全的。只有 `build.ps1` 会构建后覆盖，因此**禁止在游戏运行时使用 `build.ps1`**
> （与 `HANDOFF.md` §10 一致）。

---

## 2. Git 状态

```
HEAD       1596658  Add selectable vanilla map source
origin/main 28e9347  Probe vanilla map coordinate conversion
```

本地领先远端 **2 个提交**：

| SHA | 说明 |
|---|---|
| `98a7c86` | Prototype vanilla surveyed map HUD |
| `1596658` | Add selectable vanilla map source |

### 推送尝试：失败

已按 `HANDOFF.md` §13 执行 `git push origin main`，结果：

```
fatal: unable to access 'https://github.com/Sutanm/TLD-Community-Minimap.git/':
Recv failure: Connection was reset
[exit code: 128]
```

这是手册 §9.5 已预告的 GitHub 网络问题。**本地提交完好，无需任何回滚**，稍后重试即可。

### 推送前的安全检查（已完成）

- 这 2 个提交只改动 4 个文件：`AssemblyInfo.cs`、`HANDOFF.md`、`MinimapSettings.cs`、`ModEntry.cs`（共 +515 / −25 行）。
- `git ls-files` 确认仓库中**从未追踪任何 jpg/png/webp**。
- `.gitignore` 已覆盖 `maps/`、`prepared-maps/`、`bin/`、`obj/`、`*.dll`。

→ 符合手册 §3 的禁止事项，可以推送。

---

## 3. 代码结构地图

共 7 个源文件（约 1500 行）：

| 文件 | 行数 | 职责 |
|---|---|---|
| `ModEntry.cs` | 1036 | 主入口：场景观察、UI、纹理载入、F9、热重载、原版地图捕获 |
| `CalibrationStore.cs` | 187 | 读 `calibrations.json`，三点及以上最小二乘仿射映射 |
| `MapDefinition.cs` | 122 | 地图 ID / 图片文件 / 场景绑定；神秘湖内置局部修正 |
| `MinimapSettings.cs` | 60 | ModSettings 配置项 |
| `SceneCatalogExporter.cs` | 57 | 导出游戏场景列表 |
| `InputPatches.cs` | 26 | Harmony 补丁，全屏地图时吞掉 Esc / 暂停键 |
| `AssemblyInfo.cs` | 9 | 程序集元数据（版本 0.6.1） |

### `ModEntry.cs` 主循环（`OnUpdate`，行 93–163）

每帧顺序：

1. `TryExportSceneCatalog()` — 启动后 5 秒导出一次场景表
2. `TryReloadCalibrations()` — **每 1 秒**检查 `calibrations.json` 的 `LastWriteTimeUtc`，变化即热重载
3. 按键：`F8` 临时隐藏 / `Esc` 退出全屏 / `Tab` 切换 / `F9` 记录校准点
4. 场景句柄变化 → `ObserveScene()`
5. 地图来源（自动 / 民间 / 原版）变化 → `ApplyMapSourceSelection()`
6. 原版地图面板开启检测 → **延迟 1 秒**后捕获（见 §6 注意事项）
7. 民间模式且纹理未就绪 → `LoadCurrentMapIntoUnityUi()`
8. `UpdateUnityUi()` — 计算玩家 UV、定位标记、更新 uvRect

### 坐标链路（关键）

```
玩家世界坐标 (x, z)
  → MapDefinition.TryWorldToMap          [民间模式]
     ├─ CalibrationStore.TryWorldToMap   （calibrations.json 仿射）
     └─ 或 MysteryLakeV033 内置公式       （仅神秘湖）
  → 归一化 UV (0..1)                      ← 纹理空间，与分辨率无关
  → RawImage.uvRect + 标记 anchoredPosition
```

原版模式走 `Panel_Map.WorldPositionToMapPosition()` + NGUI 局部边界插值（行 604–632）。

---

## 4. 场景绑定核验：22 / 22 全部正确

用运行时导出的 `scene_catalog.csv`（149 个场景）逐条比对 `MapCatalog` 中的绑定，
**没有发现任何错误绑定，也没有发现应绑定却遗漏的室外场景**。

| mapId | 场景 | 状态 |
|---|---|---|
| `mystery_lake` | `LakeRegion` | ✅ |
| `forlorn_muskeg` | `MarshRegion` | ✅ |
| `coastal_highway` | `CoastalRegion` | ✅ |
| `pleasant_valley` | `RuralRegion` | ✅ |
| `mountain_town` | `MountainTownRegion` | ✅ |
| `timberwolf_mountain` | `CrashMountainRegion` | ✅ |
| `ash_canyon` | `AshCanyonRegion` | ✅ |
| `hushed_river_valley` | `RiverValleyRegion` | ✅ |
| `bleak_inlet` | `CanneryRegion` | ✅ |
| `desolation_point` | `WhalingStationRegion` | ✅ |
| `blackrock` | `BlackrockRegion` | ✅ |
| `broken_railroad` | `TracksRegion` | ✅ |
| `forsaken_airfield` | `AirfieldRegion` | ✅ |
| `crumbling_highway` | `HighwayTransitionZone` | ✅ |
| `ravine` | `RavineTransitionZone` | ✅ |
| `keepers_pass_north` | `BlackrockTransitionZone` | ✅ |
| `keepers_pass_south` | `CanyonRoadTransitionZone` | ✅ |
| `winding_river` | `DamRiverTransitionZoneB` | ✅ |
| `zone_of_contamination` | `MiningRegion`（主）/ `ZoneOfContaminationRegion`（别名） | ✅ |
| `sundered_pass` | `MountainPassRegion`（主）/ `SunderedPassRegion`（别名） | ✅ |
| `far_range_branch_line` | `LongRailTransitionZone` | ✅ |
| `transfer_pass` | `HubRegion`（主）/ `TransferPass`、`TransferPassRegion`（别名） | ✅ |

补充确认：

- **无法识别的 127 个场景全部是室内 / 洞穴 / 建筑 / 菜单**（如 `CoastalHouseA`、`PrepperCacheA`、
  `MiningRegionMine`、`MainMenu_DLC01`），隐藏地图是正确行为。
- `Dam` 与 `DamTransitionZone` **确实未被绑定**，符合手册 §6 的要求。
- 三个带别名的地图，其**主绑定均为场景表里的真实名称**，别名不会造成冲突。

> 手册 §11 第 3 项（核验真实场景名）**已经完成，无需再改 `MapDefinition.cs`**。

---

## 5. 校准覆盖状态

当前 `calibrations.json` 只有 2 条仿射数据，加上神秘湖的内置校准，共覆盖 **3 / 22**。

### 已可用（3 张）

| 地图 | 场景 | 来源 | 用户评价 |
|---|---|---|---|
| 神秘湖 | `LakeRegion` | `MapDefinition.cs` 内置 `MysteryLakeV033` + 南部局部修正 | 总体可用 |
| 孤寂沼地 | `MarshRegion` | `calibrations.json` 三点仿射 | 大部分准确，够用 |
| 断开的铁路 | `TracksRegion` | `calibrations.json` 三点仿射 | 准确度够用 |

### 待粗校准（19 张）

按手册 §11 建议，**沿海公路为下一张**。尺寸取自 `prepared-maps.json`，即写入
`calibrations.json` 时要用的 `imageWidth` / `imageHeight`：

| 优先级 | 地图 | mapId | 场景 | 图片 | imageWidth × imageHeight |
|---|---|---|---|---|---|
| ⭐ 建议下一张 | 沿海公路 | `coastal_highway` | `CoastalRegion` | `coastal_highway.jpg` | 4474 × 4473 |
| | 怡人山谷 | `pleasant_valley` | `RuralRegion` | `pleasant_valley.jpg` | 4965 × 4584 |
| | 山间小镇 | `mountain_town` | `MountainTownRegion` | `mountain_town.jpg` | 4360 × 4401 |
| | 林狼雪岭 | `timberwolf_mountain` | `CrashMountainRegion` | `timberwolf_mountain.jpg` | 4380 × 4302 |
| | 灰烬峡谷 | `ash_canyon` | `AshCanyonRegion` | `ash_canyon.jpg` | 4177 × 4212 |
| | 寂静河谷 | `hushed_river_valley` | `RiverValleyRegion` | `hushed_river_valley.jpg` | 4489 × 4251 |
| | 荒凉水湾 | `bleak_inlet` | `CanneryRegion` | `bleak_inlet.jpg` | 4202 × 4381 |
| | 荒芜据点 | `desolation_point` | `WhalingStationRegion` | `desolation_point.jpg` | 2779 × 2249 |
| | 黑岩地区 | `blackrock` | `BlackrockRegion` | `blackrock.jpg` | 4508 × 4269 |
| | 废弃机场 | `forsaken_airfield` | `AirfieldRegion` | `forsaken_airfield.jpg` | 4294 × 4170 |
| | 污染区 | `zone_of_contamination` | `MiningRegion` | `zone_of_contamination.jpg` | 4410 × 4200 |
| | 破碎山道 | `sundered_pass` | `MountainPassRegion` | `sundered_pass.jpg` | 4328 × 4327 |
| | 中转通道 | `transfer_pass` | `HubRegion` | `transfer_pass.jpg` | 6230 × 5739 |
| 过渡区 | 公路废墟 | `crumbling_highway` | `HighwayTransitionZone` | `crumbling_highway.jpg` | 4017 × 3448 |
| 过渡区 | 深谷 | `ravine` | `RavineTransitionZone` | `ravine.jpg` | 3207 × 1054 |
| 过渡区 | 蜿蜒河流 | `winding_river` | `DamRiverTransitionZoneB` | `winding_river_dam.jpg` | 4884 × 4544 |
| 过渡区 | 远境支路 | `far_range_branch_line` | `LongRailTransitionZone` | `far_range_branch_line.jpg` | 7395 × 1995 |
| 过渡区 | 守山人山隘北侧 | `keepers_pass_north` | `BlackrockTransitionZone` | `keepers_pass.jpg` | 5280 × 2495 |
| 过渡区 | 守山人山隘南侧 | `keepers_pass_south` | `CanyonRoadTransitionZone` | `keepers_pass.jpg` | 5280 × 2495 |

> ⚠️ **南北山隘共用同一张 `keepers_pass.jpg`，但必须使用两个不同的 `mapId` 和两套独立校准**
> （两个场景的世界坐标系互相独立）。这与 `HANDOFF.md` §6 的要求一致。

### 已存在 F9 截图但尚未转成校准的 5 个场景

`calibration_points_v2.csv` 与截图目录里已经有下列记录，但 `note` 字段仍是占位符
`填写地标名称`，也没有对应的 `calibrations.json` 条目：

| 场景 | 截图数 | 说明 |
|---|---|---|
| `LongRailTransitionZone` | 2 | 远境支路，**无绑定缺失问题**，可直接复用 |
| `CanyonRoadTransitionZone` | 2 | 守山人山隘南侧，可直接复用 |
| `BlackrockTransitionZone` | 1 | 守山人山隘北侧，仅有 1 点，需再补 2 点 |
| `DamRiverTransitionZoneB` | 1 | 蜿蜒河流，仅有 1 点，需再补 2 点 |
| `DamTransitionZone` | 1 | ⚠️ **已废弃**：该场景已从绑定中移除，此记录不再有用 |

**建议**：前两条各已有 2 点，如能通过截图确认地标，**只需再补 1 点**即可凑齐三点，
比全新开始更省事。但必须逐张看图确认站位，不能凭按键顺序盲填（手册 §9.2）。

---

## 6. 重要发现与澄清

### 6.1 神秘湖 2048 之谜：**不是 Bug**（我最初的怀疑是错的）

`MapDefinition.cs` 的 `MysteryLakeV033` 内置公式硬编码 `mapWidth = 2048f`，
但 `prepared-maps/mystery_lake.jpg` 实际是 **4583 × 4539**。乍看像是缩放错误，**实际不是**：

```csharp
uv = new Vector2(pixelX / mapWidth, 1f - pixelYFromTop / mapHeight);
```

`TryWorldToMap` 返回的是**归一化 UV**，直接喂给 `RawImage.uvRect`——
而 `uvRect` 使用的就是归一化纹理坐标。设高清图是旧图的等比放大 `s = 4583 / 2048`：

```
归一化 x = s·pixelX / (s·2048) = pixelX / 2048     ← s 被约掉
```

**归一化坐标与分辨率无关**，所以硬编码的 2048 只是该线性公式的推导基准，
换任何分辨率的同一张图都成立。佐证：两者宽高比几乎相同
（`2048/2028 = 1.00986`，`4583/4539 = 1.00969`）。

→ **神秘湖不需要修**。最多值得加一行注释说明「2048 是参考基准，非纹理实际宽度」，
避免下一个接手的人重复我的怀疑。

### 6.2 `prepared-maps` 与源图对应关系

- `tools/map-sources.json` + `tools/prepare-maps.ps1` 定义了 28 张源图的
  哈希 / 尺寸 / 裁剪参数，脚本先验证全部源文件再做确定性裁剪与重命名。
- `prepared-maps.json` 记录 `formatVersion`、`sourcePack`、`sourceDirectory`、
  `generatedAt`、`jpegQuality`，以及每张图的 `kind`（`region` / `cave`）、
  `source`、`output`、`sourceSha256`、`outputSha256`、`width`、`height`、`crop`。
- 28 张 = 22 张已绑定地图 + 6 张 **未绑定的 `cave_*.jpg` 连接洞穴合成图**
  （`cave_pleasant_valley_coastal` 等）。合成图按手册暂不绑定，因为需要先记录
  精确场景名和可用子矩形。
- 运行时 `maps/` 目录内容与 `prepared-maps/` **逐字节一致**（28 个文件，文件名与大小均匹配）。

### 6.3 游戏目录里的两处遗留物

1. `maps\神秘湖.jpg`（691005 字节，中文名）——**模组不使用**。
   模组按 `MapDefinition.FileName` 只加载 `mystery_lake.jpg`（2701808 字节）。
   这是旧版遗留文件，保留无害。
2. `maps\backups\mapset-before-v239-original...` —— 替换前的原版地图集备份。
   同上，保留无害。

### 6.4 原版地图模式：原型已验证，可产品化

日志确认自发售版原型全程正常：

```
[22:26:11.070] Captured 16 visible vanilla map icon layers.
[22:26:11.089] Captured vanilla map is now active in the HUD: 2048x2048.
[22:26:11.089] Vanilla map refreshed from TracksRegion_RegionMap;
              bounds=(x:-325.00, y:-325.00, width:650.00, height:650.00),
              uv=(x:0.00, y:0.00, width:1.00, height:1.00), widget=650x650.
[22:27:09.942] Captured 17 visible vanilla map icon layers.
```

- 主纹理 `2048×2048`，NGUI 控件边界 `650×650`，玩家位置用官方
  `WorldPositionToMapPosition` 换算——**原版模式无需逐图校准**。
- 图标记数在不同时刻为 16 / 17，说明图标数随勘测进度动态变化，符合预期。
- ⚠️ **不要移除约 1 秒的捕获延迟**（`_vanillaCaptureAfterUtc = UtcNow.AddSeconds(1)`，
  重试间隔 250ms）：首帧捕获会拿到空纹理和 0 个图标。

---

## 7. 不要倒退的既有约束（来自手册，务必遵守）

1. **不要重新引入 `OnGUI` / `GUI.DrawTextureWithTexCoords` 绘图。**
   早期版本在室内外切换时触发 `System.AccessViolationException` /
   `il2cpp_gchandle_get_target` 崩溃。当前持久的 `Canvas` / `RawImage` 方案已稳定。
2. **所有室外粗校准完成前不要改 `README.md`**（制作首个预发行包时统一更新）。
3. **不要 `git add -f maps/` 或 `prepared-maps/`**，不要把任何 JPG 传到 GitHub。
4. **不要删除旧的 `calibration_points.csv`**（新工作使用 `_v2`）。
5. **不要为了艺术性偏差移动整张地图**（如孤寂沼地偷猎营地「地图画三节车厢、实际四节」）。
6. **不要在游戏运行时用 `build.ps1`**；替换 DLL 前确认 `tld` / `TheLongDark` 已退出并备份。
7. **不要把 `Dam` / `DamTransitionZone` 重新绑定。**
8. **不要重新调查已确认的原版纹理层级**（手册 §14 结论已定）。
9. 粗校准阶段**不要重构 UI、纹理加载、指针绘制或设置系统**（除非出现崩溃或数据丢失）。

---

## 8. 建议的下一步

按 `HANDOFF.md` §11 的授权范围，主线是**继续粗校准**。建议顺序：

### 立即可做（不需要进游戏）

- [ ] **重试 `git push origin main`**，把 2 个本地提交同步到远端。
- [ ] （可选）为神秘湖的 2048 加一行澄清注释（§6.1），防止后续误解。

### 需要你进游戏配合

- [ ] **开第一张：沿海公路 `CoastalRegion`**。手册建议的三个点：
  通往公路废墟的过图口 / 加油站（奎塞特车库）主入口 / 兔子岛或厌世者宅邸房门
  （选与前两点不共线、且地图上更清楚的一处）。
- [ ] 优先复用已有 2 点的 `LongRailTransitionZone`（远境支路）和
  `CanyonRoadTransitionZone`（山隘南侧），每处只需再补 1 点。
- [ ] 全部 19 张完成后，按 §11.1 做回归测试并发布 **GitHub Pre-release** 测试包
  （版本号用 `0.x.y-beta.1`，**不得标记为稳定版或 1.0**，**不得包含任何 JPG**）。

### 明确**不要**现在展开（手册 §12）

局部非线性精修（分区仿射 / 残差控制点 / 薄板样条）、神秘湖铁路桥与车厢的单独修正、
洞穴与室内地图支持、正式版打包与安装器、地图来源授权的最终文案、
是否整合原生地图界面。

---

## 9. 常用命令速查

```powershell
# 构建（不覆盖游戏 DLL，安全性检查用）
dotnet build .\CommunityMinimap.csproj -c Release `
  -p:GameDirectory='D:\Program Files (x86)\Steam\steamapps\common\TheLongDark'

# 校验 calibrations.json 语法
Get-Content .\calibrations.json -Raw | ConvertFrom-Json | Out-Null

# 热重载：游戏运行时只需复制数据文件
Copy-Item -LiteralPath .\calibrations.json `
  -Destination 'D:\Program Files (x86)\Steam\steamapps\common\TheLongDark\Mods\CommunityMinimap\calibrations.json' -Force

# 读取最新的 F9 记录与截图
$modRoot = 'D:\Program Files (x86)\Steam\steamapps\common\TheLongDark\Mods\CommunityMinimap'
Get-Content -LiteralPath (Join-Path $modRoot 'calibration_points_v2.csv') -Tail 10
Get-ChildItem -LiteralPath (Join-Path $modRoot 'calibration_screenshots') -File |
  Sort-Object LastWriteTime -Descending | Select-Object -First 10

# 查看日志中的校准装载情况
Select-String -LiteralPath 'D:\Program Files (x86)\Steam\steamapps\common\TheLongDark\MelonLoader\Latest.log' `
  -Pattern 'Loaded affine calibration|Reloaded calibrations'

# 取地图图片像素尺寸（写 calibrations.json 要用）
Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Image]::FromFile('D:\CommunityMinimap-Workspace\prepared-maps\coastal_highway.jpg')
"{0} x {1}" -f $img.Width, $img.Height; $img.Dispose()
```

---

## 10. 勘察方法与可复核性

本报告所有结论均来自实际命令输出，未作推测性陈述：

- 完整阅读 `HANDOFF.md`、`README.md`、`MAPS.md`、`calibrations.json` 与全部 7 个源文件。
- 实际执行 Release 构建，确认 0 警告 / 0 错误。
- 用游戏导出的 `scene_catalog.csv`（149 行）逐条比对 `MapCatalog` 绑定。
- 用 `System.Drawing` 实测 28 张 `prepared-maps` 图片的真实像素尺寸。
- 核对 `calibration_points_v2.csv`、`vanilla_map_coordinates.csv`、
  `calibration_screenshots`（24 张）与 `vanilla_map_hierarchy.txt`。
- 检查 `git status`、`git log`、`git diff --stat`、`git ls-files`、`.gitignore`。
- 读取 `MelonLoader\Latest.log` 并扫描异常与警告。

> 本报告为新增的**未跟踪文件**，未提交。是否纳入版本库由你决定。

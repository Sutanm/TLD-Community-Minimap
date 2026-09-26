# 社区HUD地图：AI 交接手册

> 更新日期：2026-09-26（Asia/Shanghai）  
> 工作区：`D:\CommunityMinimap-Workspace`  
> 仓库：<https://github.com/Sutanm/TLD-Community-Minimap>  
> 当前本地版本：`0.5.3`

## 给接手 AI 的第一句话

请先完整阅读本文件，再检查 `git status`、`calibrations.json` 和游戏的
`MelonLoader\Latest.log`。不要重新设计已经稳定的 HUD。当前阶段的主线任务是：
**让所有室外地图先完成三点仿射粗校准并可用，然后发布一个不含地图图片的预发行测试包。**
粗校准完成前不要修改 README；制作首个测试包时可以统一更新。

## 1. 项目目标与已经确定的产品方案

这是《漫漫长夜》（The Long Dark）的 MelonLoader 模组，中文名为“社区HUD地图”，
作者字段为 `sutanm`。

最终采用两档显示，而不是三档：

- 左上角局部小地图（默认位置，可在 ModSettings 中调整位置、尺寸、边距、缩放和透明度）。
- 全屏完整地图（按 Tab 切换，Esc 退出；背景不透明度可设置）。

快捷键：

- `Tab`：角落小地图 / 全屏地图。
- `Esc`：退出全屏地图。
- `F8`：临时显示 / 隐藏 HUD。
- `F9`：记录校准点并截图。

玩家标记已经改成圆环加方向短针，并有六种配色预设。默认 HUD 在左上角。

从 v0.5.3 起，F9 还会把游戏原版地图坐标写入 `vanilla_map_coordinates.csv`。

## 2. 用户已经确认的原则

1. 先让所有室外地图完成粗校准并“动起来”，再考虑精修。
2. 大多数地势起伏不大的地图，三点仿射粗校准已经足够，不必精修。
3. 地图作者明确说明，为提高观感曾主观拓宽或收缩部分地形，因此个别局部偏差不是代码错误。
4. 洞穴和建筑内部由游戏划为独立室内场景，目前一律隐藏地图，不做室内校准。
5. 不替换游戏官方内置地图界面；继续使用独立 HUD。
6. **README 在全部室外粗校准完成前不要更新**；制作首个预发行测试包时再统一编写。
7. 民间地图图片不进入仓库，也不捆绑进发行包。未来让玩家自行准备图片，模组只提供放置规则/来源地址。
8. `00 全图拼接 .jpg` 仅供查看，运行时完全不用。
9. 用户明确授权：全部室外地图粗校准完成并通过基本稳定性检查后，接手 AI 可以发布 GitHub 预发行测试包，用户会邀请其他玩家测试。

## 3. 版权和地图文件边界

用户目前只获得部分作者的完整授权，但获得了二次分发许可。考虑到图片体积很大且授权并非完全统一，
仍决定不随模组分发图片。

原始高清地图目录：

`D:\BaiduNetdiskDownload\漫漫长夜v2.39地图高清重制`

运行时地图目录：

`D:\Program Files (x86)\Steam\steamapps\common\TheLongDark\Mods\CommunityMinimap\maps`

本地生成但被 Git 忽略的标准化地图目录：

`D:\CommunityMinimap-Workspace\prepared-maps`

`tools\map-sources.json` 记录了 28 张源图的哈希、尺寸、英文运行时文件名及裁剪参数。
`tools\prepare-maps.ps1` 会先验证全部源文件，再确定性地裁剪和重命名。

禁止事项：

- 不要 `git add -f maps/` 或 `prepared-maps/`。
- 不要把原始/处理后的 JPG 上传到 GitHub。
- 不要在授权和发布方案敲定前写下载器或自动抓取地图。

## 4. 环境与依赖

游戏目录：

`D:\Program Files (x86)\Steam\steamapps\common\TheLongDark`

已验证版本：

- The Long Dark `2.55`
- MelonLoader `0.7.2`
- ModSettings `2.2.5`
- TLD Developer Console `1.8.8`

Developer Console 开源仓库：

<https://github.com/DigitalzombieTLD/TLD-Developer-Console/>

常用控制台命令：

- `scene <SceneName>`：切换场景。
- `scene_name`：显示当前场景。
- `scene_list`：列出场景。
- `pos`：显示坐标。
- `tp x z` 或 `tp x y z`：传送。

不用逆向 Developer Console，也暂时不用 fork；F9 已能把所需数据写入文件。

## 5. 当前稳定实现（不要倒退）

早期版本使用 `OnGUI` 和 `GUI.DrawTextureWithTexCoords`，在室内外切换时曾触发
`System.AccessViolationException` / `il2cpp_gchandle_get_target` 崩溃。

当前实现使用持久的 Unity `Canvas` / `RawImage`，纹理生命周期已稳定。**不要重新引入 OnGUI 绘图。**

核心文件：

- `ModEntry.cs`：场景观察、UI、纹理载入、F9、校准文件热重载。
- `MapDefinition.cs`：地图 ID、图片文件、场景绑定；神秘湖保留旧的内置局部修正。
- `CalibrationStore.cs`：读取 `calibrations.json`，使用三点或更多点做最小二乘仿射映射。
- `MinimapSettings.cs`：ModSettings 配置。
- `SceneCatalogExporter.cs`：导出游戏场景列表。
- `calibrations.json`：应随 DLL 一起发行的小型校准数据，不包含地图图片。

校准文件支持运行时热重载。将新 JSON 复制到游戏目录后，游戏窗口处于前台时通常 1 秒内生效。
游戏切到后台时 Unity 会暂停更新，看不到重载日志属于正常现象；切回游戏即可。

## 6. 当前场景绑定

| mapId | 中文地图 | 图片 | Unity 场景 |
|---|---|---|---|
| `mystery_lake` | 神秘湖 | `mystery_lake.jpg` | `LakeRegion` |
| `forlorn_muskeg` | 孤寂沼地 | `forlorn_muskeg.jpg` | `MarshRegion` |
| `coastal_highway` | 沿海公路 | `coastal_highway.jpg` | `CoastalRegion` |
| `pleasant_valley` | 怡人山谷 | `pleasant_valley.jpg` | `RuralRegion` |
| `mountain_town` | 山间小镇 | `mountain_town.jpg` | `MountainTownRegion` |
| `timberwolf_mountain` | 林狼雪岭 | `timberwolf_mountain.jpg` | `CrashMountainRegion` |
| `ash_canyon` | 灰烬峡谷 | `ash_canyon.jpg` | `AshCanyonRegion` |
| `hushed_river_valley` | 寂静河谷 | `hushed_river_valley.jpg` | `RiverValleyRegion` |
| `bleak_inlet` | 荒凉水湾 | `bleak_inlet.jpg` | `CanneryRegion` |
| `desolation_point` | 荒芜据点 | `desolation_point.jpg` | `WhalingStationRegion` |
| `blackrock` | 黑岩地区 | `blackrock.jpg` | `BlackrockRegion` |
| `broken_railroad` | 断开的铁路 | `broken_railroad.jpg` | `TracksRegion` |
| `forsaken_airfield` | 废弃机场 | `forsaken_airfield.jpg` | `AirfieldRegion` |
| `crumbling_highway` | 公路废墟 | `crumbling_highway.jpg` | `HighwayTransitionZone` |
| `ravine` | 深谷 | `ravine.jpg` | `RavineTransitionZone` |
| `keepers_pass_north` | 守山人山隘北侧 | `keepers_pass.jpg` | `BlackrockTransitionZone` |
| `keepers_pass_south` | 守山人山隘南侧 | `keepers_pass.jpg` | `CanyonRoadTransitionZone` |
| `winding_river` | 蜿蜒河流 | `winding_river_dam.jpg` | `DamRiverTransitionZoneB` |
| `zone_of_contamination` | 污染区 | `zone_of_contamination.jpg` | `MiningRegion`（另有旧别名） |
| `sundered_pass` | 破碎山道 | `sundered_pass.jpg` | `MountainPassRegion`（另有旧别名） |
| `far_range_branch_line` | 远境支路 | `far_range_branch_line.jpg` | `LongRailTransitionZone` |
| `transfer_pass` | 中转通道 | `transfer_pass.jpg` | `HubRegion`（另有旧别名） |

重要：合成图中的不同 Unity 场景必须使用不同 `mapId`，即使它们共享同一张 JPG。
这些场景的世界坐标系互相独立，不能共用一套校准。

`DamTransitionZone` 是大坝内部，已经从地图绑定中移除；`Dam` 同样不要重新绑定。

## 7. 已完成并经用户实测的校准

### 神秘湖

- `LakeRegion`
- 使用 `MapDefinition.cs` 中的旧内置校准和南部局部修正。
- 用户认为总体可用；铁路桥附近存在地图非等比例造成的局部偏差，暂不精修。

### 孤寂沼地

- `MarshRegion`
- 三点数据已写入 `calibrations.json`。
- 用户反馈“大部分准确，够用”。
- 已知艺术性偏差：游戏里的偷猎营地有四节车厢，地图只画三节；玩家实际进入第二节，地图看起来像第一节。
- 不要为了车厢数量差异移动整张地图；将其留作后期局部修正候选。

### 断开的铁路

- `TracksRegion`
- 三点数据已写入 `calibrations.json`。
- 主点：孤寂沼地铁路过图口、维修场正门、猎人小屋正门。
- 额外验证点：维修场入口小屋，预测落点距正门图标约 16 像素。
- 用户反馈“准确度够用”。

运行时热重载已经从日志确认：

```text
Loaded affine calibration: forlorn_muskeg (3 points).
Loaded affine calibration: broken_railroad (3 points).
Reloaded calibrations.json after file change.
```

## 8. F9 数据位置

结构化记录：

`D:\Program Files (x86)\Steam\steamapps\common\TheLongDark\Mods\CommunityMinimap\calibration_points_v2.csv`

截图：

`D:\Program Files (x86)\Steam\steamapps\common\TheLongDark\Mods\CommunityMinimap\calibration_screenshots`

日志：

`D:\Program Files (x86)\Steam\steamapps\common\TheLongDark\MelonLoader\Latest.log`

F9 CSV 字段：

`timestamp,capture_id,scene,scene_handle,map_id,map_file,is_calibrated,x,y,z,heading,screenshot,note`

旧的 `calibration_points.csv` 要保留，不要删除；新工作使用 v2 文件。

原版地图坐标诊断：

`D:\Program Files (x86)\Steam\steamapps\common\TheLongDark\Mods\CommunityMinimap\vanilla_map_coordinates.csv`

该文件通过 `capture_id` 与 v2 记录对应，包含原版地图名称、地图坐标和地图朝向。

## 9. 标准校准工作流（接手 AI 应照此重复）

### 9.1 让用户取点

1. 选择一张尚未校准的室外地图。
2. 告诉用户执行 `scene <SceneName>`。
3. 选择三个容易在民间地图上精确对应、彼此距离较远且不共线的地标。
4. 优先选择门口、过图触发点、铁路端点、桥头等；避免“某片树林”“道路中间”等模糊点。
5. 每个位置只需按一次 F9。多按的记录可作为验证点，不必删除。

### 9.2 读取记录并确认截图

```powershell
$modRoot = 'D:\Program Files (x86)\Steam\steamapps\common\TheLongDark\Mods\CommunityMinimap'
Get-Content -LiteralPath (Join-Path $modRoot 'calibration_points_v2.csv') -Tail 10
Get-ChildItem -LiteralPath (Join-Path $modRoot 'calibration_screenshots') -File |
  Sort-Object LastWriteTime -Descending | Select-Object -First 10
```

逐张查看截图，确认用户确实站在要求的地标。不要仅凭按键顺序盲填。

### 9.3 从地图原图取像素坐标

- 使用 `prepared-maps\<map>.jpg`，不要使用游戏截图左上角的缩略图。
- `mapX` 从图片左边起算。
- `mapY` 从图片顶部起算。
- 取地标图标中心；门口/过图口若图标与道路端点不同，应根据用户实际站位作判断。
- 可以用 PowerShell `System.Drawing` 裁切局部到 `%TEMP%` 后放大查看。
- 先用三点求出的比例和旋转做合理性检查：两个轴的比例通常应相近；巨大剪切、镜像异常或数量级差异通常意味着点配错。

### 9.4 更新 JSON

在工作区根目录 `calibrations.json` 的 `maps` 数组加入：

```json
{
  "mapId": "与 MapDefinition.cs 完全一致",
  "imageWidth": 4000,
  "imageHeight": 4000,
  "points": [
    { "worldX": 0.0, "worldZ": 0.0, "mapX": 500.0, "mapY": 3500.0, "label": "地标一" },
    { "worldX": 1000.0, "worldZ": 0.0, "mapX": 3500.0, "mapY": 3500.0, "label": "地标二" },
    { "worldX": 0.0, "worldZ": 1000.0, "mapX": 500.0, "mapY": 500.0, "label": "地标三" }
  ]
}
```

先验证 JSON：

```powershell
Get-Content .\calibrations.json -Raw | ConvertFrom-Json | Out-Null
```

游戏运行时只需复制数据文件：

```powershell
Copy-Item -LiteralPath .\calibrations.json `
  -Destination 'D:\Program Files (x86)\Steam\steamapps\common\TheLongDark\Mods\CommunityMinimap\calibrations.json' `
  -Force
```

让用户切回游戏，等一秒；再检查 `Latest.log` 是否出现对应的 `Loaded affine calibration` 和
`Reloaded calibrations.json after file change`。

### 9.5 验证与提交

让用户在三个控制点之间移动，并额外观察一个未参与拟合的地标。只要总体方向正确且误差可接受，就继续下一张。

每张或每两张地图提交一次：

```powershell
git diff --check
git add calibrations.json
git commit -m "Calibrate <English map name>"
git push origin main
```

GitHub 网络可能失败。推送失败不能回滚本地提交，也不能重做校准；稍后重试即可。

## 10. 构建与安装规则

仅修改 `calibrations.json` 时不需要重建 DLL，也不需要关闭游戏。

代码有改动时，构建命令必须显式指向 D 盘游戏目录：

```powershell
dotnet build .\CommunityMinimap.csproj -c Release `
  -p:GameDirectory='D:\Program Files (x86)\Steam\steamapps\common\TheLongDark'
```

必须达到 0 警告、0 错误。

替换 `Mods\CommunityMinimap.dll` 前必须确认 `tld` / `TheLongDark` 进程已退出，并备份旧 DLL。
不要在游戏运行时使用 `build.ps1`，因为该脚本构建后会直接覆盖游戏目录中的 DLL。

## 11. 现在可以交给其他 AI 的工作

这些工作低风险、流程重复，适合在本对话额度恢复前交给其他 AI：

1. 按本手册继续完成所有室外主地图的三点仿射粗校准。
2. 校准小型过渡区：公路废墟、深谷、蜿蜒河流、远境支路、中转通道、守山人山隘南北侧。
3. 核验所有真实场景名，并在必要时只修改 `MapDefinition.cs` 的场景绑定。
4. 记录地图画法与游戏实景不一致的位置，但不要立即实现局部扭曲。
5. 定期提交 `calibrations.json`，网络恢复后推送 GitHub。
6. 全部室外粗校准完成后，执行一次基本回归测试并发布 GitHub **Pre-release** 测试包。

建议下一张从沿海公路 (`CoastalRegion`) 开始。可选三个点：

- 通往公路废墟的过图口。
- 加油站/奎塞特车库主入口。
- 兔子岛或厌世者宅邸的房门（选择地图上更清楚、且与前两点不共线的一处）。

若用户更愿意按区域相邻顺序，也可以先做远境支路和中转通道；原则是三点清晰且不共线。

### 11.1 预发行测试包授权与要求

接手 AI 在完成全部室外粗校准后，不必等待本对话额度恢复，可以制作并发布测试包。建议使用
`0.x.y-beta.1` 或类似预发行版本号，不要直接标记为稳定版或 `1.0`。

测试包至少包含：

- `CommunityMinimap.dll`
- `calibrations.json`
- 简明安装说明、依赖版本、地图文件名/放置目录说明
- 已知限制和反馈方法
- 文件哈希或至少记录构建提交 SHA

测试包不得包含任何 JPG 地图。README 可以在这个阶段更新，说明玩家需要自行准备地图，并指向合法来源或用户提供的地址；
在来源/授权文字没有最终确认前，应使用克制、事实性的表述，不代替地图作者作许可承诺。

发布前最低检查：

1. Release 构建为 0 警告、0 错误。
2. 从主菜单载入室外存档、进入室内、返回室外、跨地图切换均不崩溃。
3. Tab、Esc、F8、F9 和 ModSettings 仍正常。
4. `calibrations.json` 能从空白/缺失状态创建，也能读取发行包版本。
5. 至少抽查神秘湖、孤寂沼地、断开的铁路和一个合成图场景。
6. Git 工作区干净，发布产物对应明确提交，GitHub Release 必须勾选为 Pre-release。

给测试玩家的反馈模板应要求：游戏版本、场景名、地标名称、误差方向/大致距离，以及最好附 F9 生成的 CSV 行和截图。

## 12. 建议留到本对话额度恢复后再做的工作

以下任务需要更多整体设计判断，暂时不要让接手 AI 擅自展开：

1. **局部非线性精修方案**：例如分区仿射、残差控制点、薄板样条或局部偏移场。
2. 神秘湖铁路桥、偷猎营地车厢等艺术性失真区域的单独修正。
3. 洞穴/室内地图支持及室内外坐标关联。
4. 稳定版的正式打包、安装器和自动地图准备向导；预发行测试包已授权接手 AI 制作。
5. 地图来源/授权说明的最终文案。
6. 是否与游戏原生地图界面整合；当前明确不做替换。

除非出现崩溃或数据丢失，不要在粗校准阶段重构 UI、纹理加载、指针绘制或设置系统。

## 13. 当前 Git 状态（交接时）

初版交接手册提交后，本地 `main` 与 `origin/main` 已成功同步；孤寂沼地、断开的铁路、热重载和交接手册均已上传。
后续工作仍应以命令输出为准。接手时先执行：

```powershell
git status --short
git log --oneline --decorate -8
git rev-parse HEAD
git rev-parse origin/main
```

如果两个 SHA 不同，先检查本地提交内容，再正常执行 `git push origin main`。不要 reset、rebase 或丢弃已有提交。

## 14. 原版地图小地图：已验证结论与后续方案

用户提出将游戏原版地图作为另一种小地图来源，以便不准备民间 JPG 也能使用。这个方向已经完成第一阶段验证。

游戏 IL2CPP 程序集中的 `Il2Cpp.Panel_Map` 公开包装了以下方法：

- `WorldPositionToMapPosition(string sceneName, Vector3 worldPosition)`
- `MapPositionToWorldPosition(...)`
- `WorldRotationToMapRotation(string sceneName, Quaternion worldRotation)`
- `GetMapNameOfScene(string sceneName)`

v0.5.3 已让 F9 同时调用这些方法。2026-09-26 在未打开原版地图界面的情况下实测成功：

```text
scene=TracksRegion
world=(53.563, 244.785, 472.534), heading=267.21
vanillaMap=(-130.577, 160.787, 0.000), heading=2.79
available=true, error=""
```

日志：

```text
Vanilla map projection: TracksRegion -> TracksRegion (-130.577, 160.787, 0.000), heading=2.79.
```

结论：

1. 原版地图模式的玩家位置和方向可直接使用游戏官方转换，无需逐图手动校准。
2. 该转换不能直接消除民间 JPG 的校准，因为民间图片有独立像素坐标和人为地形拉伸。
3. 可以在 ModSettings 增加“地图来源：民间高清 / 原版制图”选项，两种模式并存。
4. 原版地图是动态拼装的区域地图对象，并非保证存在一张可直接复制的静态 JPG。

推荐实现路径：

1. 不要每帧强制打开或劫持整个 `Panel_Map`，以免影响暂停、输入和存档状态。
2. 找到 `Panel_Map` 通过 Addressables 实例化的 RegionMap 对象或其渲染纹理。
3. 将原版区域地图克隆到独立层，或用单独相机渲染到 `RenderTexture`，再交给现有 HUD 的 `RawImage`。
4. 玩家指针始终使用官方 `WorldPositionToMapPosition` 与 `WorldRotationToMapRotation`。
5. 优先支持“遵循当前存档炭笔勘测进度”；“显示完整未探索底图”只做可选实验，且不得修改存档解锁状态。
6. 原版模式未稳定前不能阻塞民间地图粗校准或预发行测试包。

下一步诊断应检查原版地图界面打开前后 `Panel_Map` 的子对象、RegionMap 实例和纹理来源，确认最安全的离屏渲染入口。

## 15. 当前运行状态与最后一次用户反馈

- v0.5.3 已安装到游戏目录。
- 运行时 `calibrations.json` 与工作区版本一致。
- 热重载已在日志中确认成功。
- 用户已验证孤寂沼地和断开的铁路，评价均为“准确度够用”。
- 最新已知室外场景为 `TracksRegion`；猎人小屋内部场景 `HuntingLodgeA` 会正确隐藏地图。
- 当前没有崩溃或 AccessViolation。
- 原版地图坐标与朝向接口已在地图界面关闭时验证成功。

交接后的首要动作不是写代码，而是确认用户想继续哪张室外地图，然后重复第 9 节流程。

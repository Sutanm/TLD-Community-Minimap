# 社区HUD地图：AI 交接手册

> 更新日期：2026-09-26（Asia/Shanghai）  
> 工作区：`D:\CommunityMinimap-Workspace`  
> 仓库：<https://github.com/Sutanm/TLD-Community-Minimap>  
> 当前本地版本：`0.6.1`

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

用户提出将游戏原版地图作为另一种小地图来源，以便不准备民间 JPG 也能使用。这个方向已经完成可运行原型并由用户实测通过。

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
4. 原版地图的当前勘测地形是一张可直接复制的 `UITexture`，但房屋、动物、车辆等标记是独立 NGUI `UISprite`，不能只复制主纹理。

已验证的运行时结构（`TracksRegion`）：

- `TracksRegion_RegionMap`：主纹理 `2048x2048`，只包含当前存档已经勘测的地形，未探索区域透明。
- `RegionDetailMap`：`1648x1648` 透明细节层，当前只发现少量黑色笔划。
- 主图 NGUI 实际坐标边界：`(-325,-325) - (325,325)`，控件尺寸 `650x650`。不能用纹理像素尺寸 `2048` 换算玩家位置。
- `ActiveElementsBigSprite`、`ActiveElementsSmallSprite`、`ActiveElementsDetailEntry`：已发现地图标记容器。
- 2026-09-26 实测捕获了 16 个可见图标层；房屋、动物和玩家位置均与原版地图准确重合。

当前 v0.6.1 行为：

1. ModSettings 已提供 `自动 / 民间高清 / 原版制图` 三种地图来源；自动模式优先已安装的民间地图，缺图时回退原版。
2. 原版制图模式不再依赖 F9。玩家正常打开 M 后，等待约 1 秒让 NGUI 纹理和图标稳定，再自动捕获；失败时每 0.25 秒重试。
3. 同时复制当前可见 `UISprite` 的图集 UV、颜色、大小和地图坐标，在 HUD 上重建标记层。
4. 原版地图界面打开期间 HUD 自动隐藏；关闭后显示刚刷新的原版勘测图。
5. 同一时刻只显示一种地图来源；Tab 只负责角落/全屏切换。
6. 用户已实测自动捕获修正版正常。不要移除约 1 秒的延迟：首帧捕获会得到空纹理和 0 个图标。

下一步产品化路径：

1. 不要每帧强制打开或劫持整个 `Panel_Map`，以免影响暂停、输入和存档状态。
2. 后续可增加独立来源切换快捷键，但当前 ModSettings 切换已经可用。
3. 不得修改存档的勘测/解锁状态。
5. 玩家指针始终使用官方 `WorldPositionToMapPosition`；原版模式无需逐地图校准。
6. 决定原版细节层是否需要叠加，并验证其他区域、不同勘测进度与过滤器状态。
7. 原版模式产品化不能阻塞民间地图粗校准或预发行测试包。

## 15. 当前运行状态与最后一次用户反馈

- v0.6.1 已安装到游戏目录。
- 运行时 `calibrations.json` 与工作区版本一致。
- 热重载已在日志中确认成功。
- 用户已验证孤寂沼地和断开的铁路，评价均为“准确度够用”。
- 最新已知室外场景为 `TracksRegion`；猎人小屋内部场景 `HuntingLodgeA` 会正确隐藏地图。
- 当前没有崩溃或 AccessViolation。
- 原版地图的主纹理、已发现图标和官方坐标投影均已实机渲染成功；用户反馈“可以了，准确”。

交接后可在两条主线中选择：继续第 9 节的民间地图粗校准，或先把第 14 节原版地图原型产品化。不要重新调查已经确认的原版纹理层级。

## 16. 原版地图免开面板（v0.6.2，2026-09-27 完成）

原版模式此前必须让玩家先按一次 M 才能出现 HUD。该限制**已解除**，根因不是权限问题，
而是模组查错了对象：`FindActiveRegionMap` 要求 `activeInHierarchy`，而面板关闭时恒为 false。

### 16.1 底图：不需要打开地图面板

`Il2Cpp.Panel_Map` 之外还有一条更干净的路，全部为 public：

```csharp
RegionSpecification region = GameManager.TryGetCurrentRegion();   // static，无参
if (region != null && region.HasMiniMapTexture)
{
    AsyncOperationHandle<Texture2D> handle = region.GetMiniMapTextureAsync();
    // handle.IsDone 轮询；handle.Result 是 Texture2D
}
```

- 实测 **~130 ms** 返回 **1024×1024 DXT5**（`readable=false`，需 blit 成可读副本）。
- 内容是**完整区域**的木炭底图（铁路、山脉、湖泊齐全），**与勘测进度无关**。
- 与 `Panel_Map` 的勘测纹理**完全同框**：把勘测图缩到 1024 后，在 ±120 像素范围内
  扫描最佳偏移，结果稳定为 **(0,0)**。因此坐标换算沿用同一组
  `bounds=(-325,-325,650,650)` + `uvRect=(0,0,1,1)`，**不需要第二套参数**。
- 勘测纹理（2048）是**渐进解锁**的：实测某个存档只亮了 **5.7%** 的像素，其余全透明。
  因此它不能当作 HUD 地形来源。

用户已确认采用**底图常驻**：始终显示完整区域，不随勘测进度变化。

### 16.2 标记：可以不开面板生成

`Panel_Map` 暴露了三个 public 入口，面板关闭时调用同样有效：

```csharp
panel.ForceUpdateRegion();
panel.LoadMapElementsForScene(sceneName);   // 参数是 Unity 场景名
panel.RefreshIconVisibility();
```

调用后五个 `ActiveElements*` 容器立刻被填充（实测 `1 / 2 / 10`）。但读出来有四个坑，
每一个都**必须**处理，否则 HUD 会被污染或标记消失：

1. **`LoadMapElementsForScene` 是追加语义**——重复调用会让标记成倍累加
   （实测 26 → 52）。每个场景只能调用一次。
2. **不能依赖 `isVisible`**——面板关闭时它恒为 false（`mIsVisibleByPanel` 不更新）。
   改用 `sprite.enabled && sprite.alpha * sprite.color.a > 0.01`。
3. **`drawingUVs` 恒为无效值**——NGUI 只在绘制时填它。必须自己从图集重建，
   且**必须做 V 轴翻转**（`UISpriteData` 是左上原点，Unity UV 是左下原点）：
   ```csharp
   var d = sprite.atlas.GetSprite(sprite.spriteName);
   uv = new Vector4(d.x / tw, 1f - (d.y + d.height) / th,
                    (d.x + d.width) / tw, 1f - d.y / th);
   ```
4. **尺寸必须乘 1/3**——面板布局时把元素根节点缩放到 **0.33**，而
   `LoadMapElementsForScene` 创建的元素没走这一步。实测同一图标
   `ngui=52.0`（自建）对 `17.3`（面板内），比值 **3.006**；多个图标一致为 3.0。

另外要**跳过 `HoverWidget` / `Label` / `LabelBG` / `highlight` 子树**，
否则每个标记的悬停标签底板都会被当成图标（表现为一堆白色方块）。

### 16.3 0.33 不是常量，是滚轮缩放档位

用户在实机中发现：M 大地图有**两档滚轮缩放**——默认 **0.33**、放大 **1.00**。

**推论：从打开的面板读图标会继承当时的档位**，玩家只要放大过地图，
抓到的尺寸就会偏大 3 倍。因此**只在面板关闭时读图标**，永远走自建元素这条一致路径。
（面板打开时 HUD 本来就被隐藏，此时刷新图标没有意义。）

### 16.4 其他

- `GameManager.TryGetCurrentRegion()` 在场景加载瞬间可能返回 null，需下一帧重试。
- `SaveGameSlotHelper.GetCurrentSaveSlotInfo()` 在游戏内返回 **null**
  （`m_SaveSlotName` / `m_GameId` 都取不到），所以**按存档键控缓存这条路走不通**——
  好在底图方案根本不需要缓存。
- HUD 的地图容器已加 `RectMask2D`；否则边缘标记会画到小地图矩形之外。
- 顺带修好一个老 bug：`OnSceneWasInitialized` 会被 `*_WILDLIFE` / `*_SANDBOX` 等
  附加场景反复触发，导致同一区域重复 `ObserveScene`、已加载纹理被丢弃。
  现在按**场景名**判断，同名不再重置状态。

### 16.5 标记覆盖范围受勘测限制

未勘测区域**不会**出现标记——这是游戏机制。绕过它需要
`UnlockRegionMap` / `ForceUnlock` / `ApplySurvey` 等接口，**会写入存档的勘测/解锁状态**，
与第 14 节第 3 条冲突。用户询问过"一键点亮全图"作弊选项，**尚未实现，待决策**。

**标记尺寸不再需要手工标定**：全部由 16.2 第 4 条的 1/3 得出。

## 17. 社区地图与官方地图成固定比例（2026-09-27 发现，重要）

这一节推翻了第 9 节"每张图都必须取三个分散点"的前提。**校准从此只需一个锚点地标。**

### 17.1 规律

社区高清图与原版地图之间存在**固定的线性关系**，只含**缩放 + 平移**：

    社区图像素 = S * NGUI坐标 + 平移      S ≈ 8.61（社区像素 / NGUI 单位）
    横轴取正、纵轴取负（NGUI 的 v 是下→上，图像 y 是上→下）

**没有旋转，没有镜像。** 社区作者是按原版地图**等比放大**后重新绘制的。

**注意：S 不是全局常量。** 实测各图不同：

| 地图 | S | 备注 |
|---|---|---|
| 断开的铁路 | 8.6144 | 由已知准确的校准反推 |
| 怡人山谷 | **8.8208** | 两个锚点最小二乘拟合 |
| 沿海公路 | 8.6709 | 目视粗测，精度较低 |

各图相差约 2.4%，说明作者对不同地图用了不同的绘制比例。
**因此 S 必须每张图自己拟合——至少取两个锚点，不要套用其他图的值。**
单锚点 + 借用别图的 S，会在离锚点较远处产生百分之几的缩放误差。

### 17.2 证据

| 区域 | 社区图 px/世界单位 | NGUI/世界单位 | 比值 |
|---|---|---|---|
| 断开的铁路（用户实测"够用"） | 2.4251 | 0.28152 | **8.6144** |
| 沿海公路（独立测得） | 1.7486 | 0.20170 | **8.6709** |

两个区域独立测得，**相差仅 0.6%**。

更强的证据：沿海公路上只用**修理站一个锚点** + S=8.6144 预测另外四点，
而这四点**事先没有取过像素**——预测结果全部落在正确的图上特征上
（野兔岛预测 (1993,1926)，独立裁剪时观察到的红色房屋图标在 (1955,1917)）。

### 17.3 新校准流程（替代第 9 节的三点法）

1. 让用户在目标区域走动，逐个按 F9。**点越多越好，不需要分散、不需要不共线。**
2. F9 已同时写入世界坐标和原版 NGUI 坐标（`WorldPositionToMapPosition`）。
3. 用**全部点**做 `world(x,z) → NGUI` 最小二乘拟合。
   实测残差可达 **0.00035 NGUI 单位**——投影是精确仿射，非常干净。
4. **只需在社区图上点准一个地标**得到 `(mapX, mapY)`，即可解出平移量：

       tx = mapX_anchor - S * ngui_u_anchor
       ty = mapY_anchor + S * ngui_v_anchor

5. 其余所有点的 `mapX/mapY` **全部由公式算出**，不再需要目视取点。
6. 把算出的点全部写进 `calibrations.json`（支持任意点数的最小二乘）。

**为什么这比三点法好**：三点法残差恒为 0、无法自我检验；而目视取点误差可达
**±100 像素**，那才是先前漂移的**真正主因**（不是地图失真）。单锚点法把目视参与
压缩到"一个点"，其余全靠精确的游戏投影。

### 17.4 原版投影的各向异性是真实的，且被社区图继承

沿海公路实测：`world → NGUI` 为 `diag(0.19587, 0.20745)`，**各向异性 +5.91%**。

原因是游戏把**非正方形**区域压进**正方形**贴图（NGUI 边界固定 650×650）。
这是游戏自身的拉伸，不是误差，社区图继承同样的拉伸。

所以：**校准后各向异性落在 +5.9% 附近是正常的**。先前目视拟合出的 9.5% 各向异性
与 0.1336 交叉项，全部是取点误差。

### 17.5 沿海公路：已完成，含一处已知局部偏差

- 7 个控制点，全部由 17.3 的模型生成。
- 结果仿射交叉项 ≈ 0，各向异性 +5.91%（与游戏拉伸一致）。
- 用户实测：**除深谷铁路口外全部准确**。

**已知局部偏差（深谷铁路口）**：模型预测 (486, 293)，该处铁轨实际起于 x≈508、
中心线 y≈338，偏差约 **(横向 -22、纵向 -45) 像素**。

已排除校准错误：

- 锚点（修理站）实测图标中心 (3746,1307)，与采用的 (3730,1300) **只差 17 像素**
- 若是锚点误差应为**全局平移**，不可能只有一角偏
- 其余地标全部准确

**结论：这是作者主观拉伸局部地形造成的真实局部失真，单一仿射在数学上无法消除。**
不要为此移动整张图（第 2 节第 3 条）。将来做第 12 节局部精修时，
**这一角是现成的第一个测试用例**。

### 17.6 这张图的坑（下次别再踩）

1. 标签是**「匡西特修理站」**，不是"奎塞特加油站"。
2. **左下角一大块插图（废弃矿地煤矿）加图例不属于世界内容**，任何基于整图归一化的
   换算都会偏，必须逐点看图取像素。
3. 图上大量**带图标图例的标签框**（废弃瞭望台、火车卸货区、活动房、冰钓小屋、沿海房屋…），
   它们**只是标签**；框里的图标图例表示"该处有什么"，**不是地标本身的图标位置**。
4. 红色**双箭头只是方向标识**，与真正的过图触发点（道路/铁路出图处）可差 100~150 像素。
   这是最初 P1/P3 取错的直接原因——**改用 17.3 的模型后此问题自然消失**。

### 17.7 辅助工具

模组会把每个区域的原版底图导出到 `Mods\CommunityMinimap\basemap_<场景名>.png`
（每个区域只写一次），供把社区图与原版底图做配准/叠加核对使用。

> `SaveGameSlotHelper.GetCurrentSaveSlotInfo()` 在游戏内返回 null，
> 所以"按存档键控缓存"这条路走不通；好在底图方案不需要缓存。
### 17.8 怡人山谷（`RuralRegion`）：已完成，精度最好的一张

5 个控制点，两个锚点最小二乘拟合（教堂 + 心碎桥）。

- **world→NGUI 残差 2.85e-04 NGUI 单位**（投影依旧精确）
- **拟合出 S = 8.8208**，与断桥的 8.6144 相差 2.4%——见 17.1 的修正
- **各向异性仅 +0.82%**（对比沿海公路 +5.91%）——这张图几乎没被拉伸，
  所以是目前精度最好的一张
- 交叉验证：模型预测的农庄 (1471,2212) **正落在红色农舍图标上**，
  信号山坡 (849,2917) **正落在无线电天线塔底部**——两点都未参与锚定

锚点像素（`pleasant_valley.jpg`，4965×4584）：

| 地标 | 像素 |
|---|---|
| 怡人山谷教堂 | (3396, 3469) |
| 心碎桥 | (1636, 2733) |

F9 顺序 → 地标（本次）：

| 顺序 | 截图内容 | 地标 |
|---|---|---|
| 1 | 两层木屋正面、门廊 | 怡人山谷农庄 |
| 2 | 红白无线电塔 + 铁丝网 | 信号山坡（无线电控制站） |
| 3 | 砖石教堂、尖拱窗 | 怡人山谷教堂 |
| 4 | 木桁架桥 + 路面 | 心碎桥 |
| 5 | 白桦林中的木架小棚（地图未标注） | 孤独家宅附近 |

> 经验：**未校准的地图不显示玩家标记，小地图会直接铺满整张图**，
> 所以校准期间的小地图截图**无法用于判断站位**，必须看 3D 画面。
### 17.9 校准工具实测有效（2026-09-27）

用户实测反馈：用工具点锚点得到的校准"**比之前准多了**"。

**工具流程（推荐，替代 17.3 的纯 AI 估算）**

1. 用户走动逐个按 F9（每张图 **≥2 个能明确认出的地标**，越多越好）
2. 打开 `tools/calibrate.html`，载入地图 JPG + 两个 CSV，选场景
3. 用户**把图放大到像素级**，逐个点锚点
4. 工具最小二乘拟合 S、tx、ty，并给出每个锚点误差
5. 把输出的 JSON 写进 `calibrations.json`

**为什么必须由用户点，而不是 AI 目视估算**

AI 在 4000~5000 像素宽、布满标签框的图上目视取像素，误差是 **±30~50 像素**——
和校准本身的误差**同一量级**。这一轮 AI 估的锚点比用户点位的**系统性偏左偏上 40~75 像素**，
导致 13~41 像素的可见偏差（低缩放看不出，高缩放很明显）。
用户能把图放大到像素级且知道自己站在哪栋建筑，精度远高于 AI。

**怡人山谷最终数据**

- 5 个锚点，工具拟合 **S = 8.6598**，各向异性 **+0.83%**
- 结果仿射：`mapX = 0.0000825·wx + 1.51461·wz - 0.03`，
  `mapY = 1.50220·wx - 0.0000087·wz + 54.01`
- **注意这张图世界轴转了 90°**：`mapX ∝ 世界z`、`mapY ∝ 世界x`。
  不同区域是独立 Unity 场景，世界朝向各不相同，**不能假设所有图轴对齐方式一致**。

**工具的一个诊断缺陷（待改进）**

面板里的"最大残差"**不能用来判断锚点点得准不准**——因为非锚点全部由模型生成，
仿射拟合必然严丝合缝。**真正有意义的是"锚点误差"那一行**。
建议后续改进：直接在锚点表格里逐行标出误差。
---

## 18. 当前进度与下一步（2026-09-27 夜 记录）

### 18.1 已完成

| 项 | 状态 |
|---|---|
| 原版免M HUD（不按 M 就显示地图与标记） | ✅ 用户已验收 |
| 面板无关的底图加载 / 标记采集 | ✅ |
| 底图导出（`basemap_<场景>.png`） | ✅ 16 个区域已导出 |
| 场景目录导出（`scene_catalog.csv`） | ✅ 149 个场景名 |
| 已校准地图 | **5 / 22**：神秘湖、孤寂沼地、断开的铁路、沿海公路、怡人山谷 |

### 18.2 两个工具

| 工具 | 用途 | 状态 |
|---|---|---|
| `tools/calibrate.html` | 导入地图 + 两个 F9 CSV，**在图上点锚点**，最小二乘拟合**相似变换（含旋转）**，逐行显示锚点误差 | ✅ 怡人山谷用它做成功 |
| `tools/align.html` | 导入两张图手动对齐（拖角点拉伸、拖圆点旋转、对数滑块、3 组对应点解完整仿射） | ✅ 可用，但**结论是这条路对本项目无用** |

**align.html 为什么无用**：原版底图 PNG 是**旋转状态存储**的（见 18.4），拿它当配准基准等于在拟合"底图的存储旋转"，而不是拟合校准。**校准链路全程不读原版地图图片**：

```
世界坐标 ──F9采集──► 原版 NGUI 坐标 ──仿射──► 社区图像素
```

所以**只需在社区图上点出 F9 位置**，不要用底图做蒙版配准。

### 18.3 已安装的构建（重要）

```
Mods\CommunityMinimap.dll   时间 2026-09-27 23:11:16   (52224 字节)
备份  Mods\CommunityMinimap\backups\CommunityMinimap_20260927_23*.dll
```

含：底图导出、场景目录导出，以及**新增的 RegionMap 控件旋转诊断日志**（`ModEntry.cs:921`）。

### 18.4 待查：原版底图贴图疑似旋转存储

**证据链**

1. 用户报告：社区图相对**内置地图**转了 90°；神秘湖社区图上的指北针是「北朝左、东朝上」
2. 用户手动对齐：把底图 PNG 转 **-78.85°** 才凑上社区图，而**拉伸比 0.9930:1（几乎完全等比）**
3. 我用两锚点算「社区图 ↔ NGUI」只有 **-1.6°**（近 0°）
4. 相关性搜索的次峰在 **265~275°**，与 90° 吻合

**推论**：游戏把 `RegionMap` 贴图**旋转存储**，靠控件 transform 转回来。我们的导出是原始贴图，**没应用那个旋转**。

**影响**：若成立，**原版 HUD 在那些区域显示的底图是歪的**——这影响已验收的免M功能，不只是校准。

**下一步**：在**原版制图**模式下进入若干区域，**各按一次 M**，日志会出现：

```
Vanilla map panel refreshed from <场景>_RegionMap; ... rotation=(0.0,0.0,90.0), scale=(...), texture=1024x1024.
```

读 `rotation.z` 即可。**建议区域**：LakeRegion、TracksRegion（对照）、CoastalRegion、RuralRegion。

### 18.5 待办清单（按优先级）

| # | 事项 | 说明 |
|---|---|---|
| 1 | **神秘湖补 3+ 锚点** | 现只有 2 个可用锚点（营地办公室、捕兽者之家），**两点恰定、残差恒为 0，等于未验证**。F9 点里那个冰钓小屋有 6 间相同建筑，**不能当锚点**。需补 1~2 个独一无二的地标（林业瞭望塔 / 卡特大坝外观 / 伐木营地 / 无名池塘） |
| 2 | **查底图旋转**（见 18.4） | 影响原版 HUD 正确性 |
| 3 | **沿海公路重做** | 它用的是从断桥**借来的 S=8.6144**，而 S 实测每张图不同（8.34 / 8.61 / 8.66 / 8.82，跨度 5.8%）。借用值会让**离锚点越远误差越大**（最远处可能 ~95 px）。应用工具取 ≥2 锚点重拟合 |
| 4 | 孤寂沼地 / 断开的铁路 重做 | 3 点手工目视取点，**残差恒为 0、无法自检**；且像素由 AI 目视所取（±30~50 px） |
| 5 | 剩余 15 张新图 | 走 F9 → 工具点锚点流程 |

**注意**：S 不是全局常量，**每张图必须自己拟合**（≥2 个锚点）。实测：

| 地图 | S |
|---|---|
| 神秘湖 | 8.339 |
| 断开的铁路 | 8.614 |
| 怡人山谷 | 8.660 |
| （沿海公路借用断桥值） | 8.614 |
| 怡人山谷（AI 目视锚点，作废） | 8.820 |

### 18.6 各区域底图情况

- **16 个区域有原版底图**：LakeRegion、MarshRegion、CoastalRegion、RuralRegion、MountainTownRegion、CrashMountainRegion、AshCanyonRegion、RiverValleyRegion、CanneryRegion、WhalingStationRegion、BlackrockRegion、TracksRegion、AirfieldRegion、MiningRegion、MountainPassRegion、HubRegion
- **6 个过渡区没有**：HighwayTransitionZone、RavineTransitionZone、BlackrockTransitionZone、CanyonRoadTransitionZone、DamRiverTransitionZoneB、LongRailTransitionZone

  原因：`GameManager.TryGetCurrentRegion()` 在这些场景**返回 null**（游戏没有为过渡区提供 `RegionSpecification`）。**这是游戏侧限制，不是映射 bug**（22 个场景的映射全部正确）。
  **后果：过渡区在原版模式下永远拿不到底图 → 没有 HUD，只有民间图模式可用。**

- **RiverValleyRegion 的底图是 1024×512（非方形）**，其余 15 张都是 1024×1024。我们的 `UseVanillaBaseMap` 假设方形 650×650 边界，**这张可能显示不正确，待查**。

### 18.7 未决事项

- `TAKEOVER-REPORT.md`（AI 最初的勘察报告）仍未提交。内容部分已被本手册 §16/§17 取代。**待决定：删除，还是精简后并入手册。**
- 地图来源的三态（自动 / 民间高清 / 原版制图）在过渡区的降级行为，尚未在手册中说明。

### 18.8 明天的第一步

**二选一，看你想先解决哪个：**

- **A（推荐，快）**：原版制图模式下跑 4 个区域各按一次 M，把 `MelonLoader\Latest.log` 日志给我 → 我读出每张底图的旋转 → 若确认旋转 bug，我修 HUD 显示，顺带把导出也修正
- **B（主线推进）**：神秘湖补 1~2 个 F9，然后在 `calibrate.html` 里点 3~4 个锚点，一次把神秘湖做对

两者不冲突，A 更快且有定论，B 是校准主线。
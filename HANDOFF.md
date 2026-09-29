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
---

## 19. 追加记录：两个待做功能 + 一条明确的"不做"（2026-09-27 深夜）

### 19.1 重要澄清：标记的来源与限制

用户实测确认：**原版模式下未勘测的区域，底图会显示，但没有标记。**

| 图层 | 未勘测时 | 来源 |
|---|---|---|
| 底图 | ✅ 显示（完整地形，无迷雾） | `RegionSpecification.GetMiniMapTextureAsync()` |
| 标记 | ❌ 为空 | 游戏的 `MapElements` |

**标记读自游戏自己的 `MapElements`，游戏没扫描到的物件就没有条目。我们无法凭空生成标记**，这是数据源限制，不是 bug。

所以"免M"的真正含义是：**地图永远显示，标记随探索自动出现，不需要玩家按 M 去激活它**。断桥那版能看到标记，是因为那张图已探索过。

### 19.2 待做：全屏地图（Tab）的居中 / 缩放 / 拖拽

**现状**：`Tab` 打开的全屏地图固定显示整图（`uvRect = (0,0,1,1)`），不能居中于玩家、不能缩放、不能拖拽。

**结论：不需要碰游戏的内置地图模块。** 全屏地图是我们自己实现的 UI 树：

```
CommunityMinimapCanvas        (ModEntry.cs:404, sortingOrder 2000)
├── FullMapBackground         (:413)
├── Map                       (:426, RawImage + RectMask2D)
│   └── PlayerPointer         (:434)
```

代码里所有 `texture =` 赋值**只作用于我们自己的 RawImage**，从未写过游戏的 `Panel_Map`。所以"换成自己实现"这件事**已经完成了**，只差交互。

**要做的功能**

| 功能 | 现状 | 做法 |
|---|---|---|
| 以玩家为中心 | ❌ 固定整图 | 打开时把 `visibleUv` 中心设为玩家 UV |
| 滚轮缩放 | ❌ | 全屏时也按 `_settings.Zoom` 计算 `visibleUv`，滚轮改倍数 |
| 拖拽平移 | ❌ | 拖动改 `visibleUv` 中心 |
| 打开时释放光标 | ❌ | `Cursor.lockState = None; Cursor.visible = true;`，关闭时还原 |

**唯一的真实约束是光标**：TLD 在游戏进行中锁定鼠标（用来转头）。用户反馈"上一版必须在暂停页/背包页才能拖"——**根因是光标被游戏锁着，不是代码问题**。打开全屏地图时自行释放光标即可。

**用户反馈的"必须先放缩一下才能拖拽"**：逻辑上正确但体验别扭（整图全显示时平移无意义）。配合"打开时即居中于玩家 + 默认放大"后自然消失。

**实现位置**：`UpdateUnityUi`（`ModEntry.cs:567`）里的 `fullMap` 分支——目前 `:589-592` 直接把 `visibleUv` 写成整图，改成和角落小地图共用同一套 `visibleUv` 计算（`_settings.Zoom`）并叠加平移即可。

**风险**：改动完全在我们自己的 Canvas 内，不触碰游戏面板，崩溃风险接近零。

### 19.3 明确不做：不替换游戏的内置地图模块

**用户明确要求不要碰，理由充分且我同意：**

- 游戏内置地图（`Panel_Map`）的标记实现质量很差，**一碰就崩**
- 国外工作室做过额外区域，**宁可单独发布社区地图，也不碰游戏内置地图**
- 我们**没有这个需求**——全屏地图是我们自己的实现（见 19.2）

**这条要作为长期约束记下来：永远不要尝试替换或深度修改 `Panel_Map`。**

只读它（`CaptureVanillaMap` / `CaptureVanillaIcons` / `FindActiveRegionMap`）是可以的，那已经是现有功能的依赖。

### 19.4 待做：过渡区回退开关

见 §18.6：6 个过渡区拿不到原版底图。建议加设置项：

```csharp
[Section("地图来源")]
[Name("原版缺失时使用民间图")]
[Description("原版制图没有提供该区域的地图时（如过渡区），自动改用民间高清图，避免 HUD 消失。")]
public bool FallbackToCommunity = true;
```

| 情况 | 行为 |
|---|---|
| 原版有底图 | 用原版 |
| 原版没有 | 回退民间图，HUD 角落显示小字提示（如 `民间图`） |
| 都没有 | 隐藏（现状） |

**理由**：用户的核心需求是"HUD 必须一直显示"，6 个过渡区如果是黑洞就直接违背了这条。

**附带影响**：过渡区目前没有校准数据，回退后能显示地图但**不会画玩家指针**；要让指针也出来，这 6 个区也得走一遍 F9 + 点锚点。

### 19.5 明天要跑的诊断（构建已装好）

当前安装：`Mods\CommunityMinimap.dll`，2026-09-27 23:37:32，含两项诊断。

**A. 底图贴图旋转诊断**（见 §18.4）
原版制图模式下进 **LakeRegion / TracksRegion（对照）/ CoastalRegion / RuralRegion**，**每个按一次 M**。日志出现：

```
Vanilla map panel refreshed from <场景>_RegionMap; ... rotation=(0.0,0.0,90.0), scale=(...), texture=1024x1024.
```

读 `rotation.z`。

**B. 过渡区 region 诊断**（新增）
在任意过渡区停留 3 秒以上，若日志出现：

```
No region spec for <场景>: GameManager.TryGetCurrentRegion() stayed null for 3s,
so there is no vanilla base map here.
```

则确认是 `TryGetCurrentRegion()` 返回 null。

**同时做一个独立测试**：在过渡区**按 M**（游戏自己的地图，不是 Tab），看游戏自己的地图显示不显示。

| 按 M 的结果 | 日志 | 结论 |
|---|---|---|
| 有图 | 有 warning | 我们的 API 用错了 → 换 `GameManager` 的别的字段，或按场景名找 `*_RegionMap` |
| 没图 | 有 warning | 游戏本身没有 → 硬限制，走 19.4 的回退 |
| 有图 | 无 warning | 那说明请求根本没发出，查 `_currentDefinition` / `playerReady` 条件 |

**建议一次跑完**：先跑 A 的 4 个区域，再跑 B 的 6 个过渡区，最后把整份日志给我。

### 19.6 本地提交状态

- `c378297` 过渡区诊断 —— **已提交，推送失败**（网络 `Connection was reset`）
- 连同之前的提交，等网络恢复一条 `git push` 全部上传
---

## 20. 社区地图标记 BUG 与数据源机会（2026-09-27 深夜，读 MapIconFix 后）

### 20.1 参考项目

- 仓库：https://github.com/hzb1130/MapIconFix
- 文件：`MapIconFix.cs`（4505 字节）、`MaoIconFix.csproj`、`README.md`
- **无 LICENSE 文件**（见 20.5）

### 20.2 BUG 机制：游戏既不删数据也不删图标

`MapDetail` 记录着每处资源对应的可采集物：

```
mapDetail.m_HarvestablesForMapVisibility    // 各自的 h.m_Harvested 会变成 true
mapDetail.m_HarvestablesSharingIcon         // 共用同一图标的那些
```

采集后 `h.m_Harvested` 翻成 `true`，**但没有任何东西把这个状态传给地图**。于是：

- `MapDetailManager.s_MapDetails` 里那条记录还在
- `MapElements` 里的 UI 图标也还画着

MapIconFix 的做法（挂在 `Panel_Map.Enable(bool)` 的 Postfix 上）：

```csharp
mapPanel.RemoveMapDetailFromMap(marker, 0f);   // 隐藏图标
MapDetailManager.s_MapDetails.Remove(marker);  // 从数据源删除
mapPanel.RefreshIconVisibility();
```

判定条件是 `marker.m_IsSurveyed && 所有可采集物都已采集`。

README 印证这是**跨存档累积**的：打开地图发现脏图标 → 关闭（修复）→ 再打开（正常）。之所以要开关一次，是因为清理挂在 `Enable` 上。

### 20.3 我们的暴露面：会中招，但有条件

我们的标记是从面板的 `MapElements` **UI 对象**刮下来的，游戏不删图标我们就刮到脏数据。

但用户早期反馈"**游戏地图会消失、小地图不会**"，说明**同一次游戏内的采集游戏是处理的**。推测：

| 情况 | 游戏 | 我们 |
|---|---|---|
| 本次游戏内采集 | 会移除 | 跟着正确 |
| **跨存档残留的旧标记** | 不删 | **也会画出来** |

这解释了为什么早期验证没发现问题——当时测的是当场采集。

**重要**：用户装了 MapIconFix 之后，`s_MapDetails` 会在打开地图时被真的清干净，我们之后刮到的就是干净数据。**两个 mod 互补，不冲突。**

### 20.4 最大的收获：可能一直在用最脏的数据源

现状是**刮 UI 精灵**，为此付出了一整套代价（全部是实测踩出来的）：

| 被迫做的事 | 原因 |
|---|---|
| 从图集手工重建 `drawingUVs`（含 V 翻转） | 面板关闭时 `drawingUVs` 是空的 |
| 乘 1/3 | 面板外元素边界恰好大 3 倍 |
| 跳过 `HoverWidget` / `Label` / `LabelBG` | 那是装饰不是标记 |
| 用 `sprite.enabled && alpha*color.a` 判可见 | 面板关闭时 `isVisible` 不可用 |
| 每秒比对签名 | 数据变了不会通知我们 |

**而 `MapDetailManager.s_MapDetails` 是干净的数据源。** `MapDetail` 对象自带地图位置、类型、勘测状态、是否已采集——正是我们手工猜的那些东西。

**若能确认 `MapDetail` 暴露地图坐标**（从它被 `RemoveMapDetailFromMap` 和 `MapElements` 消费来看极可能存在），则可以：

- **扔掉 UI 刮取**，改读数据
- **顺便自动修掉这个 bug**（自己读 `m_Harvested` 过滤，不依赖别人的 mod）
- 打开标记类型、图层筛选、路线点等功能的可能性

**这可能是接手以来性价比最高的一次重构**——不是加功能，而是**换掉最脆弱的一段代码**。

**下一步**：探测 `MapDetail` 的字段（用 Il2CppInterop 反射列出字段名与类型）。

### 20.5 许可证注意

仓库只有 `MapIconFix.cs`、`MaoIconFix.csproj`、`README.md`，**没有 LICENSE 文件**。

按惯例：**无许可证 = 默认保留所有权利**，不属于可自由使用的开源代码。

| 可以 | 不可以 |
|---|---|
| 读它、理解机制 | 逐行照抄其代码 |
| 使用它揭示的**游戏 API**（`s_MapDetails`、`RemoveMapDetailFromMap` 属于游戏，不属于作者） | 复制其 `AreAllHarvestablesHarvested` 实现 |
| 独立实现同样逻辑，注明受其启发 | —— |

**"开源"不等于"随便用"。** GitHub 上没写 license 的仓库默认 all rights reserved。

**建议**：社区氛围友好，直接找 hzb 要一个 MIT 许可（加个 LICENSE 文件即可）。大概率会给，之后参考什么都名正言顺。

### 20.6 重要修正：面板风险要收窄（补充 §19.3）

MapIconFix 是一份**反例证据**——它在改游戏的数据模型，而且是个已发布、能用的 mod：

```csharp
MapDetailManager.s_MapDetails.Remove(marker);   // 直接改游戏数据模型
mapPanel.RemoveMapDetailFromMap(marker, 0f);    // 调面板公开方法
mapPanel.RefreshIconVisibility();
```

所以"碰一下就崩"的判断**必须收窄**：

| 操作 | 实证结论 |
|---|---|
| 调面板公开方法（`RemoveMapDetailFromMap` / `RefreshIconVisibility`） | ✅ 安全，两边都在用 |
| **增删 `s_MapDetails` 条目** | ✅ **MapIconFix 就在做** |
| **`LoadMapElementsForScene` 重建** | ⚠️ **会追加**（我们实测 26→52） |

**"面板是天坑"这个印象，很可能主要来自某一个方法的重入行为。** 这把"整块禁区"缩小成了"一个要小心的调用"。

§19.3 的"不要碰"仍然成立，但**理由要更新为工程判断，而不是恐惧**：

> 不碰是因为**没必要**（替代品已经能达成目标），而不是因为**碰不得**。
> 唯一确认的危险点是 `LoadMapElementsForScene` 的重入语义。

### 20.7 行动清单（按性价比）

| # | 事项 | 价值 |
|---|---|---|
| 1 | 探测 `MapDetail` / `MapDetailManager.s_MapDetails` 字段，评估替换 UI 刮取 | ⭐⭐⭐ 换掉最脆的代码 |
| 2 | 自己实现标记清理（读 `m_Harvested` 过滤），不依赖 MapIconFix | ⭐⭐ 顺带修 bug，HUD 永远干净 |
| 3 | 找 hzb 要许可证 | ⭐⭐ 法律上站得住 |
| 4 | 记录用户已取得社区地图作者的**再分发 / 修改 / 打包**授权（见 20.8） | ⭐⭐ |

第 1 与第 2 是同一件事的两面：一旦读 `MapDetail`，就同时得到干净数据源与自动过滤能力。

### 20.8 分发与授权的新进展

用户已确认：**社区地图作者的授权已拿到手。**

由此：

- **地图包拆分从可选变成应该做**，但驱动力是**体积**（源图约 110 MB）和**开箱即用**，不再是版权
- **可以做开箱即用**：`CommunityMinimap.Maps\` 打包 JPG + `calibrations.json`，装上即用，无需玩家跑 `prepare-maps.ps1`
- **校准也一起发**：`calibrations.json` 是我们自己的数据，本来就不涉及版权

**仍然不按"原版 / 社区"拆代码**（理由见 §19.2 与本次讨论）：90% 复杂度共享、回退功能需要同进程双来源、双 HUD 冲突、双倍维护。**正确的切分线是代码与数据。**

**另需补做**：把授权写进仓库（`CREDITS.md` / `MAP-LICENSE.md`），记录署名、授权范围、日期、原帖链接。口头授权在公开项目里无法自证。README 与设置页必须保留作者署名。

### 20.9 顺带确认的一条 UX 问题

**玩家肌肉记忆是按 M，不是按 Tab。** 现在我们的全屏地图在 `Tab`，而 `M` 打开的是游戏自己的地图（未勘测就是空的）。很多人不会发现 Tab。

**接管 M 键**（拦住 → 打开我们的全屏地图）是投入产出比最高的一次改动：

- 约 10 行（`InputPatches.cs` 已有基础设施，现成在拦 Escape）
- **不打开游戏面板**，风险接近于零
- 一次性解决发现性问题

做完之后，"要不要碰 Panel_Map"的动机又少一大半。
---

## 21. 与第三方 mod 的兼容性（2026-09-27 深夜）

### 21.1 审计方法：六个维度

每引入一个第三方 mod 或新功能，按这六项过一遍：

| # | 维度 | 查什么 |
|---|---|---|
| 1 | **Harmony 目标** | 有没有 patch 同一个方法？**尤其是 prefix 返回 false 的** |
| 2 | **按键** | 有没有占用同一个 KeyCode？ |
| 3 | **渲染层级** | 自建 Canvas 还是挂进原版 UI 层级？sortingOrder 谁在上？ |
| 4 | **共享游戏对象** | 有没有改同一个游戏侧的数据结构 / 控件？ |
| 5 | **屏幕空间** | 默认位置会不会重叠？层级上谁遮谁？ |
| 6 | **光标** | 有没有改 `Cursor.lockState`？ |

审计手段：**下载源码到本地用 grep 扫**，不要把整个文件读进上下文（`Core.cs` 有 64 KB）。关注 `HarmonyPatch`、`KeyCode`、`AddComponent<`、`SetParent`、`anchorMin/Max`、`Panel_Map`、`MapDetail`。

### 21.2 InterloperHUDPro 审计结果：技术上零冲突

- 仓库：https://github.com/EtherSystem/InterloperHUDPro （默认分支 `Main`，注意大写）
- 功能：生存数据 HUD（日期时间、温度、负重、移速、手持物耐久、破冰计时、薄冰计时、风向风速）
- **`license: null`**（GitHub API 确认，又一个不写许可证的 TLD mod）

| 维度 | InterloperHUDPro | 我们 | 结论 |
|---|---|---|---|
| Harmony 目标 | `GameManager.Start`、`StatusBar.Update`、`Panel_HUD.Update`、`Panel_IceFishingHoleClear.*` | `InputManager.GetEscapePressed`、`GetPauseMenuTogglePressed` | ✅ 零重叠 |
| 按键 | **一个都没有**（纯 ModSettings 开关） | Tab / F8 / F9 | ✅ |
| 地图模块 | **从不触碰**（grep `Panel_Map`/`MapDetail`/`RegionMap`/`MapElements` 全空） | 核心对象 | ✅ |
| 渲染 | NGUI `UILabel`，**挂进原版 HUD 层级**（`outerBoxSprite.transform.parent`、`barSprite.transform.parent`） | 自建 `ScreenSpaceOverlay` Canvas，`sortingOrder 2000` | ✅ 两套体系 |
| 屏幕空间 | 原版寒冷条**上方**（左下）+ 风力块 + 日期 + 手持物 | 四角之一（默认左上） | ⚠️ 默认不重叠，调左下可能压住 |
| 光标 | 不动 | 全屏地图打开时会释放（待做） | ✅ |

**两个 mod 连一个共同方法都没碰**，这是最理想的情况。

层级上**永远是我们盖住它**（`ScreenSpaceOverlay` + `sortingOrder 2000` 在所有 NGUI 之上）。重叠时用户可自行调整——我们有四角位置设置，它有逐元素 X/Y 偏移设置。

**结论：这次审计反过来确认了我们架构是"友好"的。**

- **自建 Canvas**、不改原版 UI 层级 → 不和任何"挂进原版 HUD"的 mod 打架
- **不碰原版 HUD 控件** → `StatusBar` / `Panel_HUD` 那片全是别人的地盘，我们不进去
- **只读地图面板** → 与地图类 mod 也基本不冲突
- **唯一的理论风险来自"想改原版地图"那条路**——而我们已经决定不走。**又一条"替代品比补丁安全"的实证。**

### 21.3 按键冲突的注意点

`Tab` 和 `F8` 在 mod 生态里是**热门键**（Tab 尤其）。好消息：我们的三个键**都可以在 ModSettings 里改**，用户能自行解决，不需要我们发版。

**建议改进**：在设置项的 `Description` 里写明"若与其他 mod 冲突请改这里"，否则用户不一定知道去哪儿改。

### 21.4 「标记修复」的两条实现路径与 MapIconFix 的共存

背景见 §20.2（游戏不删 `s_MapDetails` 里的已采集条目，也不删 UI 图标）。

用户已安装 MapIconFix。问题：**如果我们自己也做标记修复，会不会冲突？**

#### 路径 A：改数据（照 MapIconFix 的做法）

```csharp
mapPanel.RemoveMapDetailFromMap(marker, 0f);   // 隐藏 UI
MapDetailManager.s_MapDetails.Remove(marker);  // 删数据
mapPanel.RefreshIconVisibility();
```

**基本安全，因为天然幂等。** Harmony postfix 顺序执行，两边都从**共享列表**里删条目：

| 顺序 | 结果 |
|---|---|
| 我们先 | 我们删干净 → MapIconFix 扫到空列表，什么都不做 |
| MapIconFix 先 | 它删干净 → 我们扫到空列表，什么都不做 |

`List.Remove` 删不存在的元素是 no-op，**两种顺序都安全**。

**但有一个真陷阱：触发时机不同就会踩。**

如果我们**不挂 `Panel_Map.Enable`**，而挂在自己每秒轮询上：

```
1. 我们扫描 → 收集到 marker M
2. 用户打开地图 → MapIconFix 把 M 从 s_MapDetails 删除、UI 也移除
3. 我们拿着已失效的 M 调 RemoveMapDetailFromMap(M, 0f)   ← 可能空引用
```

MapIconFix 在删数据前检查了 `Contains(marker)`，但**对 UI 那一步没做同样检查**（因为它和删数据同帧同段，不会被打断）。**照抄时必须补这层防护。**

**另有实现级坑（两边都会踩）**：**必须先收集、后删除两趟走**，边遍历边删会抛 `InvalidOperationException`。两者都不会因此互相冲突，只是各自可能崩。

#### 路径 B：只读过滤（推荐）

```csharp
// 读的时候判断，不写任何东西
bool stale = marker.m_IsSurveyed && AllHarvestablesHarvested(marker);
if (stale && !showStaleMarkers) continue;   // 跳过，不画
```

| | 路径 A（改数据） | **路径 B（只读过滤）** |
|---|---|---|
| 装了 MapIconFix | 幂等，基本安全（有 1 个陷阱） | ✅ **完全无交互** |
| 没装 MapIconFix | 我们顺便修了游戏 bug | 我们的 HUD 干净；**游戏自己的 M 地图仍脏** |
| 是否碰游戏数据 | 是 | ❌ **不碰** |
| 崩溃面 | 有 | **接近零** |

**路径 B 的唯一"损失"**：游戏自己那张 M 地图的脏图标不会被我们修掉。但那不是我们的职责（我们打开的是自己的地图），要修让 MapIconFix 去修。

**语义注意**：如果我们的判定**比 MapIconFix 宽**（例如也清理"已搜刮的容器"），用户会看到"装了 MapIconFix 但某些图标还在"——那不在它的范围内，也不该它管。**建议对齐语义**：只处理 `m_IsSurveyed && 所有可采集物已采集`；想扩展就做成设置项。

#### 结论

**路径 B 与我们前面认定的"最高性价比重构"（§20.4，改用 `MapDetailManager.s_MapDetails` 作为标记数据源）是同一件事。**

一旦换成读数据，**三个问题一次解决**：

1. 标记修复
2. 对游戏 bug 免疫
3. 与 MapIconFix 等第三方 mod 的兼容性

**文档里写一句"与 MapIconFix 可共存"即可。**
---

## 22. 2026-09-28 工作日记录

### 22.1 接管游戏地图键（已实现并安装）

**动机**：玩家肌肉记忆是 **M**，不是 Tab。旧行为下按 M 打开的是游戏自己的地图（未勘测就是空的），很多人根本不会发现 Tab 能开地图。

**实现**：拦**动作**而不是改面板。游戏所有"打开地图"的路径都汇聚到一个方法：

```csharp
[HarmonyPrefix]
[HarmonyPatch(typeof(InputManager), nameof(InputManager.ExecuteOpenMapAction))]
private static bool RedirectOpenMap() => !ModEntry.TryRedirectGameMap();
```

返回 `false` 跳过原方法 → **游戏的面板根本不会打开**，显示我们的全屏地图。`Panel_Map` 一行都没碰。覆盖按键、目标提示等所有路径。

设置项：`RedirectGameMap`（默认 `true`），关掉即恢复游戏原行为。

**踩到的坑**：游戏对**一次按键触发两次** `ExecuteOpenMapAction`，日志里同毫秒出现 `FullMap` 然后 `MiniMap`，净效果等于没反应。修法：120ms 内的重复调用直接吞掉。

> **教训**：功能"看起来没生效"时，先看日志里到底执行了几次，不要先怀疑 patch 没打上。

### 22.2 地图裁剪：源图附带整张"图纸家具"

**问题**：源图（`漫漫长夜v2.39地图高清重制`）每张是一整张**图纸**，除画面外还带：标尺、符号图例表、作者/版本信息、指北针、以及**插图**（如公路废墟的「三号煤矿厂」、沿海公路的「废弃矿井」）。`prepare-maps.ps1` 的 crop 参数是手工调的，有几张没切干净。

**可靠判据（重要）**：**画面印在米色纸上，图例印在白纸上。**

```python
cream = ((r - b) > 16) & (r > 195)   # 米色纸
white = (abs(r - b) < 10) & (r > 235) # 白纸
```

从底部向上扫，米色持续出现处即为画面边界。**比墨迹剖面可靠得多**——图例又密又和标尺交错，墨迹法会误判。

**本次已修**（只裁底部，所有 (x,y) 不变，锚点仍然有效，只需同步 `imageHeight`）：

| 地图 | 原高 | 裁后 | 同步 |
|---|---|---|---|
| 山间小镇 | 4401 | **4198** | `calibrations.json` imageHeight 已改 |
| 守山人山隘 | 2495 | **2090** | 无校准 |

裁剪参数已写回 `tools/map-sources.json`（图片本身不入库，crop 参数是唯一持久记录）。

**判据的失效场景（务必注意）**：米色判据对**下部是大片水域**的图会失效，脚本会对沿海公路、荒芜据点给出"裁掉 87%"的**荒谬建议**。脚本 `tools/check-sheet.py` 只作**提示**，**每条建议都必须目视确认后才能执行**。

**待处理（未动，避免越改越糟）**：

| 地图 | 问题 |
|---|---|
| 公路废墟 `crumbling_highway` | 画面框底边约 2343，下面依次是标尺、图例表、「三号煤矿厂」插图；**左侧还有一条装标题和作者信息的页边**（在画框之外），需要同时裁 x |
| 蜿蜒河流 `winding_river_dam` | **不是一张图，是多面板拼版**（大坝上层 / 蜿蜒河流 / 卡特大坝 / 两个小图 + 一大片图例），需要从源图重新确定要提取哪一块 |

两张都是**过渡区**，优先级低。**修法建议**：直接调 `map-sources.json` 的 crop 再从源图重新生成，不要在已裁过的图上二次裁。

### 22.3 部署流程的坑（重要）

`prepare-maps.ps1` 的默认输出是**工作区的 `prepared-maps`**，**不会自动拷到游戏目录**：

```powershell
[string]$DestinationDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) "prepared-maps")
```

**⇒ "裁好图"和"游戏里生效"之间差一次手工复制**，本次就漏了一次。改了任何地图图片后，必须同时更新：

```
工作区  prepared-maps\<map>.jpg
游戏    TheLongDark\Mods\CommunityMinimap\maps\<map>.jpg   ← 模组实际读取的位置
```

**建议**：给 `prepare-maps.ps1` 加一个 `-DeployToGame` 开关，直接输出到游戏目录并自动备份被覆盖的旧文件。

### 22.4 MelonLoader 联网校验：可以关掉

`MelonLoader/Dependencies/Il2CppAssemblyGenerator/` 每次启动会 **Contacting RemoteAPI**，网络差时会一直等到超时。

**开关在 `UserData\Loader.cfg` 第 57 行**：

```ini
[unityengine]
force_offline_generation = true     # 默认 false，已改为 true
```

等价于启动参数 `--melonloader.agfoffline`（Steam 启动选项里填，两种方法选一种）。

**何时需要改回 `false`**：游戏大版本更新后，Assembly-CSharp 哈希变化，生成器可能需要从远程 API 取对应版本的 Cpp2IL / Il2CppInterop。若更新后所有 mod 失效且日志报类型找不到，改回 `false` 跑一次再改回来。

Cpp2IL 已完整下载在本地（`Dependencies\Il2CppAssemblyGenerator\Cpp2IL\Cpp2IL.exe` 约 15 MB + 全套 `UnityDependencies`），所以离线多数情况下仍能重新生成，只是跳过版本校验。

备份：`UserData\Loader.cfg.bak`。

### 22.5 校准进度

**6 / 22**：神秘湖、孤寂沼地、断开的铁路、沿海公路、怡人山谷、**山间小镇**。

山间小镇 5 个锚点（米尔顿信用合作社 / 邮局 / 教堂 / **棚屋** / 地堡）。第四次点原先标为"天堂草地农场"，实为**棚屋**，已更正，修正后每点误差都在 22px 以内。

```
mapX =  1.8099·wx + 0.0131·wz −  419.5
mapY =  0.0127·wx − 1.8673·wz + 4637.4
```

### 22.6 取点经验（累积）

1. **锚点必须铺开**，覆盖不同象限；**边角各放一个**。最小二乘在控制点包围盒之外是**外推**，误差放大——山间小镇的地堡离前四点最远，4 点拟合在它那里误差 48px，补第 5 点后降到 13px。
2. **避开重复结构**（湖上多间相同的冰钓小屋无法区分）。
3. **相邻建筑不要当两个点**（信用合作社和邮局只隔 55 世界单位，会造成病态拟合）。
4. **工具报的锚点误差基于"相似变换"，比实际严格**——模组用更通用的**仿射**（6 参数）拟合，实际误差通常更小。误差 < 45px 即可用。

### 22.7 一条方法论教训（重复犯过两次）

**跨画风的弱相关不是证据。**

我两次从"底图配准"的相关系数里读出 90° 旋转，两次都被用户一眼否定：

| 地图 | 最佳相关系数 | 可信度 |
|---|---|---|
| 沿海公路 | **+0.368** | 强，可作证据 |
| 神秘湖 | +0.126 | 弱，**误导了我一次** |
| 山间小镇 | +0.056 | 无区分度 |

**结论**：只有强信号（>0.3）才能当依据；弱信号一律忽略，改用**直接目视对比**或**用户实测**。

同时，那次的调查**意外确认了一件事**：`basemap_*.png` 与 NGUI 帧**方向一致、无旋转**（控件 `rotation=(0,0,0)`、`scale=(1,1,1)`），并且这给了一个可行的验证手段——**用底图当基准交叉验证社区图校准**（沿海公路 0° 拿到 +0.368 就说明那份借用 S 的校准其实没问题）。

### 22.8 过渡区结论（已确认）

```
No region spec for RavineTransitionZone: GameManager.TryGetCurrentRegion() stayed null for 3s
```

**游戏对这些场景没有提供 `RegionSpecification`**，所以原版模式拿不到底图 → 没有 HUD。**这是游戏侧限制，不是映射 bug**（22 个场景映射全部正确）。

**待决**：是否加"原版缺失时回退民间图"开关（默认开）。见 §19.4。

### 22.9 推送积压

GitHub 从 2026-09-28 中午起持续不可达（`Failed to connect to github.com:443`）。本地提交完好，**积压 2 个提交**待网络恢复后推送：

- `380a686` M 键接管
- `35720f2` 重复触发修复 + 两张图裁剪 + 山间小镇校准更正
---

## 23. 全屏地图交互：实现与已确认的问题（2026-09-28 下午）

### 23.1 本轮修好的三件事

**① 标记尺寸固定下来（根因是归一化基准选错）**

```csharp
_vanillaIconMaxUv = 所有图标里的最大值        // ✗ 错
markerScale = MarkerIconSize / _vanillaIconMaxUv
```

**只要有一个异常大的元素**（没被 `IsMapIconChrome` 过滤掉的标签或区域块），基准就被撑大，**普通标记被压成 1~2 像素**，滑块调到 48 也不够。

改成**中位数 + 限幅**：

```csharp
sizes.Sort();
_vanillaIconMaxUv = sizes[sizes.Count / 2];        // 中位数
ratioX = Mathf.Clamp(MapUvSize.x * markerScale / MarkerIconSize, 0.6f, 1.8f);
```

滑块放宽到 `[Slider(10, 120, 111)]`，默认 `MarkerIconSize = 44`。

**② 光标释放（`ShowCursor` 不够）**

`InputManager.ShowCursor(true)` **没有真正解锁光标**。证据：滚轮能缩放（`Input.mouseScrollDelta` 不受锁定影响），但 `Input.mousePosition` 完全不变，所以拖不动。

所以**直接驱动 Unity 状态并且每帧都设**（游戏会重新锁）：

```csharp
if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
if (!Cursor.visible) Cursor.visible = true;
```

**③ 输入上下文（静态方法）**

`InputManager.PushContext(MonoBehaviour)` / `PopContext` / `ShowCursor(bool)` **都是静态方法**——编译器报 `CS0176` 才点破的。原来的实例捕获管道已删除。

```csharp
InputManager.PushContext(_backgroundImage);   // 屏蔽玩家移动/转头
InputManager.ShowCursor(true);
```

设置项 `ReleaseMouseOnFullMap`（默认开）。配对由 `_mapContextPushed` 保证只 pop 一次。

**④ 全屏地图缩放与拖拽**

先前全屏模式写死 `uvRect = (0,0,1,1)`。现在与角落小地图**共用同一套可见窗口逻辑**：

- 打开时**以玩家为中心**（`_fullMapCenterValid = false` → 首帧取玩家 uv）
- **滚轮**缩放 1x~24x（指数手感）
- **按住左键拖动**平移
- `Esc` / `M` 关闭并交还输入

用户确认：**标记尺寸、鼠标释放/拖拽均可用。**

### 23.2 待优化（用户提出，明确说优先级不高）

| # | 项 | 说明 |
|---|---|---|
| 1 | **Tab 已冗余** | M 被接管后 `MapModeKey = Tab` 意义不大。**待决**：移除，还是保留作为别名（有些玩家已习惯） |
| 2 | **小地图与大地图加边框** | 让玩家知道地图边界在哪。目前底图有透明边距时完全看不出范围 |
| 3 | **打开大地图以玩家为中心** | 已实现（见 23.1④），但用户仍提到，**需实测确认是否真的生效** |
| 4 | 整体手感 | 用户评价"够用，但体感上不算多好"，还有很多体验要打磨 |

### 23.3 待确认：原版底图的透明边距

**用户反馈**：打开全屏地图，"底图显示范围只有中央的方块，不太够用"。

**我的分析**：全屏布局本身没问题——2560×1600 屏幕上，1:1 的图会渲染成 1536×1536（占满高度 96%），左右留白是方形图的必然结果。

**真正的原因很可能是原版底图 PNG 有大片透明边距**：

```
basemap_LakeRegion.png 不透明区域只占整帧 51.3%
（x 56..981, y 79..968，而图是 1024×1024）
```

⇒ **一半的画面是空的**，用户看到的"中央方块/圆形"其实是**区域本身的轮廓**，四周透明，3D 世界透了进来。

**修法**：读底图时算一次 **alpha 包围盒**，作为地图的内容范围，让区域真正铺满视图。**注意**：uv 空间随之改变，标记位置用的是同一套 uv，所以会同步正确；但 widget 比例应按**内容**而非整图计算，否则会有几个百分点的拉伸。

**待用户确认**是"透明边距太多"（按上述修）还是"希望拉伸铺满整屏"（会变形，不推荐）。

### 23.4 主线仍然待做：`MapDetail` 重构

**用户反馈**："玩家用木炭更新了地图，打开原版内置地图也没用，小地图上的标记也没更新，需要切换一次场景才会刷新。"

**根因**：**游戏自己的面板就不重建**——`Panel_Map` 的标记树要等换场景才刷新。我们的标记就是从那个面板读的，所以完全继承了该行为。

**修法**（即 §20.4 的重构）：直接读 `MapDetailManager.s_MapDetails`。

| 收益 | 说明 |
|---|---|
| 实时刷新 | `m_IsSurveyed` 勘测即翻 true，不依赖面板重建 |
| 免疫采集 bug | 自己读 `Harvestable.IsHarvested()` 过滤 |
| 地名 / 分类 | `m_LocID`（地名）、`m_IconType`、`m_SpriteName` |
| 图层筛选 | `Panel_Map.ToggleIconDisplayFlag(IconDisplayFilters)` 游戏自带分类 |
| 位置 | `MapDetail.GetWorldPosition()` → 走我们**已有的校准**，与玩家指针同一链路 |

**这仍然是接手以来性价比最高的一次重构。**

### 23.5 推送状态

GitHub 自 2026-09-28 中午起持续不可达。本地提交完好，积压待推。
---

## 24. `MapDetail` 实测结果（2026-09-28，重大进展）

在 Mountain Town 按 F9，把 `MapDetailManager.s_MapDetails` 全量打出：

```
MapDetail dump: 816 entries.
[0]  sprite='icoMap_churchMilton' loc='GAMEPLAY_mtChurch'      type=TopIcon     surveyed=False discovered=False unlocked=False world=(688.6,288.4,2102.0) target=(0,0,0)
[2]  sprite=''                    loc='GAMEPLAY_mtSchool'      type=Text        surveyed=True  ...        world=(981.7,266.8,1727.1)
[7]  sprite='icoMap_crossroads'   loc='SCENENAME_MiltonHouse'  type=DetailIcon  surveyed=True  ...        world=(1131.6,263.4,1755.1)
[10] sprite=''                    loc='GAMEPLAY_mtTownCentre'  type=Text        surveyed=True  unlocked=True
[18] sprite='icoMap_crossroads'   loc='GAMEPLAY_mtCreditUnion' type=DetailIcon  surveyed=True
[21] sprite='icoMap_cattails'     loc='GAMEPLAY_CattailPlant'  type=DetailEntry surveyed=False ... (大量重复)
```

### 24.1 确认可用的字段

| 字段 | 结论 |
|---|---|
| **`GetWorldPosition()`** | ✅ **有值**，直接给世界坐标 → **可以走我们已有的校准**，与玩家指针同一条链路 |
| `m_TargetPosition` | ❌ 恒为 `(0,0,0)`，**不可用**，必须用 `GetWorldPosition()` |
| **`m_SpriteName`** | ✅ **语义化图标名**（`icoMap_churchMilton`、`icoMap_cattails`、`icoMap_crossroads`…） |
| **`m_LocID`** | ✅ 本地化 key（`GAMEPLAY_mtChurch`、`SCENENAME_MiltonHouse`）→ **可做地名提示/搜索** |
| `m_IconType` | 渲染分类，不是语义：`TopIcon` / `DetailIcon` / `DetailEntry` / **`Text`** / `Area` |
| `m_IsSurveyed` | ✅ **"已揭示"标志**——勘测即翻 true，正可用于实时刷新 |
| `m_IsUnlocked` | 部分条目为 true（如 `mtTownCentre`） |

### 24.2 关键发现

1. **`type=Text` 是地名标签**（`m_SpriteName` 为空）——**做标记时要滤掉**，但它们**免费提供了地名数据**。
2. **`world` 全部有值**——所以标记位置**不需要再刮 UI**，直接 `GetWorldPosition()` → 我们的校准 → uv ✓。
3. **条目数 816**，绝大多数是重复的资源条目（香蒲之类）——需要按类型/精灵名聚合或过滤。
4. **`surveyed` 标志实时变化**——这就是"木炭更新后要切场景才刷新"的正解：**不再依赖面板重建**。

### 24.3 唯一剩下的问题：图标从哪来

`m_SpriteName` 只是**字符串**，不是精灵本体。要画出图标需要拿到贴图。**突破口**：刮 UI 时 `UISprite` 本身有 `spriteName` 属性，可以在现有刮取逻辑里**顺便建立 `名字 → (贴图, UV)` 字典**，之后由 `s_MapDetails` 驱动位置和可见性。

> 若某个新勘测的地名还没有对应的 UI 对象（字典里查不到），退化为通用标记即可。

### 24.4 重构后的收益（一次解决五件事）

| 收益 | 说明 |
|---|---|
| 实时刷新 | 读 `m_IsSurveyed`，不需要换场景 |
| 免疫采集 bug | 自己读 `Harvestable.IsHarvested()` 过滤 |
| 地名 | `m_LocID` 可本地化显示 |
| 过滤/图层 | 按 `m_IconType`（滤掉 Text）与精灵名分类 |
| 位置链路统一 | `GetWorldPosition()` → 与玩家指针**共用同一份校准** |

### 24.5 已装的诊断

`DumpMapDetails()` 挂在 **F9** 上（与校准点记录同时触发），日志打印前 40 条。保留着，重构过程中还要反复对照。

**编译通过本身已确认**：`MapDetailManager.s_MapDetails`、`GetWorldPosition()`、`m_SpriteName`、`m_LocID`、`m_IconType`、`m_IsSurveyed`、`m_IsDiscovered`、`m_IsUnlocked`、`m_TargetPosition` **全部可从我们的模组访问**。
---

## 25. 图标链路打通：100%（2026-09-28，重构已解锁）

### 25.1 第一步的失败教训（先量再改）

先试"刮 UI 时记录 `sprite.spriteName` 建字典"，量出来：

```
icon table holds 8 sprite names
802 markers with a sprite name, 205 resolvable      ← 只有 26%
Captured 162 vanilla map marker layers (panel-free)
```

**字典里只有 8 个名字**——面板关闭时它只实例化了很少几种图标。**若当时直接重写，四分之三的标记会变成空白。** 幸好先用诊断量了一遍。

### 25.2 正解：走图集

`UISprite` 上除 `spriteName` 还有 **`atlas`**；NGUI 的 `UIAtlas` 提供 **`GetSprite(name)`**，拿到图集后**任何名字都能查出贴图**，不再依赖"面板恰好实例化了哪些"。

刮取时存一个图集引用即可：

```csharp
if (ReferenceEquals(_mapIconAtlas, null) && !ReferenceEquals(sprite.atlas, null))
    _mapIconAtlas = sprite.atlas;
```

### 25.3 实测结果：100%

```
MapDetail dump: 816 entries; icon table holds 8 sprite names.
MapDetail summary: 802 markers with a sprite name;
                   205 found among the scraped sprites,
                   597 more resolvable through the atlas (100% total);
                   atlas present: True;
                   14 carry no sprite name (labels and areas).
```

**802 / 802 全部可解析。** `atlas present: True`（面板关闭时精灵的 `atlas` 也有值）。

### 25.4 重构的全部要素已确认

| 要素 | 来源 | 状态 |
|---|---|---|
| **位置** | `MapDetail.GetWorldPosition()` → 我们已有的校准 → uv | ✅ |
| **图标** | `_mapIconAtlas.GetSprite(m_SpriteName)` → 贴图 + UV | ✅ 100% |
| **可见性** | `m_IsSurveyed` / `m_IsUnlocked`（实时） | ✅ |
| **过滤** | `m_SpriteName` 为空 = 地名标签 / 区域，不画 | ✅ |
| **地名** | `m_LocID`（可本地化） | ✅ |
| **分类** | `m_SpriteName` 前缀（`icoMap_*`） | ✅ |

**注意**：`m_TargetPosition` 恒为 `(0,0,0)`，**必须用 `GetWorldPosition()`**。

### 25.5 下一步（重写标记来源）

目标：把 `_vanillaIcons` 的来源从"刮 UI 精灵"换成"读 `s_MapDetails`"。

```
每个 MapDetail:
  if (m_SpriteName 为空) 跳过                       // 标签/区域
  if (!m_IsSurveyed && !m_IsUnlocked) 跳过          // 未揭示
  if (所有可采集物已采集) 跳过                      // 免疫采集 bug
  world = GetWorldPosition()
  if (!TryPlayerToMapUv(world, out uv)) 跳过        // 与玩家指针共用校准
  sprite = _mapIconAtlas.GetSprite(m_SpriteName)    // 100% 可得
  → 画在 uv 上，尺寸按精灵的像素尺寸换算
```

**同时**：签名改为从 `s_MapDetails` 计算（数量 + 勘测位 + 采集位），**实现实时刷新，不需要换场景**。

**保留 UI 刮取**，但只作为**图集与精灵尺寸的来源**，不再决定画什么。

**保留 F9 诊断**，重写过程中反复对照。

### 25.6 待观察

- **816 条 vs 实际显示数量**：绝大多数是重复资源条目（香蒲等），需要确认原版面板是否会把同类聚合显示（`m_MultiMarkerIconSize`、`DoMapIconSpacing` 暗示有聚合逻辑）。**若不聚合，全画出来会很乱**，可能要看齐原版的聚合行为。
- **`m_IsSurveyed` 与 `m_IsUnlocked` 的组合**：需要确认哪些组合该画（目前 dump 里见到 `surveyed=True`、`unlocked=True`、两者皆 False 的情况）。
---

## 26. 产品方向与优先级纪律（2026-09-28）

### 26.1 勘测弹图：改为三选项

玩家用木炭点亮地图时，游戏会强制打开一次地图（走 `ExecuteOpenMapActionFromObjective`，已拦截）。

**用户要求做成三选项**：

```
勘测后弹图：[原版地图 / 我们的地图 / 不弹]
```

**理由（用户原话的意思）**：我们的地图**天然点亮**，弹不弹信息量差不多，所以"不弹"也合理——少一次打扰。

### 26.2 「手动点亮」比「天然点亮」更贴近玩家需求

**这是本节最重要的判断，且纠正了之前的思路。**

"免M + 天然点亮"只解决了"看不见图"，但**顺手把探索乐趣也拿掉了**——勘测 / 木炭是 TLD 的核心体验之一。

**游戏本来就有雾，而且数据我们都已经拿到：**

| 游戏机制 | 我们探到的 |
|---|---|
| 每个地点的揭示状态 | `MapDetail.m_IsSurveyed`（**实时**） |
| 每个地点的勘测半径 | `MapDetail.m_SurveyRadius` |
| 游戏自己的迷雾 | `Panel_Map.FOGOFWAR_RADIUS_MULTIPLIER` |

**⇒ 我们的 HUD 可以照游戏自己的勘测进度画雾**：玩家点亮一块，我们的图亮一块。

**建议的默认**：

```
[跟随游戏勘测进度（推荐） / 天然全亮]
```

- **跟随进度**：保留探索感，给想正常玩的玩家
- **天然全亮**：给"我就想看全图"的玩家

### 26.3 更长远的可能：做「自定义地图的运行层」

> ⚠️ **这是方向，不是承诺。用户明确要求：不要好高骛远，优先兑现当前承诺。**

观察：原版 `Panel_Map` 那套没人愿意碰（**国外工作室宁可单独发布社区地图**），自制地图作者各自为战。

而我们**已经打通了最难的部分**：

| 自制地图作者现在要面对 | 我们已经有 |
|---|---|
| 原版地图模块的天坑 | **一个 JPG + 一份校准 JSON** |
| 没有小地图 HUD | ✅ 已有的 HUD |
| 没有标记系统 | ✅ 从 `MapDetail` 读 |
| 校准靠自己猜 | ✅ **`tools/calibrate.html` 能自助出校准** |
| 地名 / 悬停 | ✅ `m_LocID` |
| 雾 / 进度 | ⏳ 待做（见 26.2） |

**⇒ 潜在定位：自定义地图的运行层。** 作者只提供**图 + 校准**，其余由我们负责。

**若真要做，两条纪律**：

1. **API 是长期承诺**：要版本化、写文档、兼容旧格式。**现在是定格式的最佳时机**（mod 未发布），发布后再改会得罪所有作者。
2. **`calibrate.html` 是这个愿景的关键一环**——让作者**自助**配准，不必来找我们。

### 26.4 优先级纪律（用户明确要求）

> **不要把目光放得太长远，优先实现我们的承诺：小地图 HUD。**

**"承诺"的具体内容**（已对用户承诺并部分验收）：

| 承诺 | 状态 |
|---|---|
| **不按 M 就能看到地图**（免M） | ✅ 已验收 |
| **标记跟着地图走** | ✅ 基本可用 |
| **社区高清图可用** | ⏳ **6 / 22 已校准** |
| 标记可靠（实时刷新、位置正确） | ⏳ 依赖 26.5 的重写 |

**⇒ 当前优先级只有一个：把承诺做完。**

### 26.5 当前待办（按优先级，已按 26.4 收敛）

| 优先 | 事项 | 说明 |
|---|---|---|
| **1** | **继续校准剩余 16 张图** | 承诺的核心；流程已跑通三次 |
| **2** | **标记来源换成 `MapDetail`** | 根治刷新；前置验证 100% 完成（§25） |
| **3** | **开箱即用**（地图包 + 署名文件） | 授权已到手；否则 22 张图的价值出不来 |
| 4 | ~~构建失败仍安装的脚本隐患~~ | **已完成，见 §28.4** |
| 5 | ~~勘测弹图三选项（26.1）~~ | **已完成，见 §28.2** |
| 6 | 公路废墟 / 蜿蜒河流的裁剪 | 前者要裁左边，后者是多面板拼版 |
| 7 | 过渡区回退开关、`prepare-maps.ps1 -DeployToGame` | 流程完善 |
| 8 | ~~小地图/大地图解耦、Tab 循环档位~~ | **已完成，见 §28.1** |
| 9 | ~~全屏地图按键提示条~~ | **已完成，见 §28.3** |
| 10 | 手柄支持 | **真实缺陷（手柄关不掉地图），设计见 §28.5，需先测量** |
| 11 | UX：地图边框、整体手感 | 用户说优先级不高 |
| 12 | 已采集标记清理 | **待定**——两个字段全空，数据源不明，投入产出比存疑。当前为"只报告"模式，**不会改数据** |
| — | 雾 / 手动点亮（26.2） | 定位问题，但排在承诺之后 |
| — | 运行层愿景（26.3） | **不做承诺** |

---

## 27. 用户新点子与可行性分析（2026-09-28 傍晚，只记录不实现）

用户提出两件事，并问"还有没有别的改良游戏体验的想法"。本节只做**可行性判断和记录**，不动代码。
优先级仍以 §26.4 为准：**先兑现小地图 HUD 的承诺**。

### 27.1 想法 A：小地图用官方图源，大地图用社区图源

> ⚠️ **2026-09-28 晚修正：本节结论被 AI 扩大化了，见 §32。**
> 原文说"它被标记重写卡住，必须先做 §25.5 的重构"——那**只在"两个图源都要有标记"时才成立**。
> 用户从头到尾没打算在社区地图上画标记，所以图源分离和标记重写**没有依赖关系**。

**结论：能做到，但它被"标记重写"卡住，必须先做 §25.5 的重构。**

现状（`ModEntry.cs`）：

- 整张地图只有**一套**资源：`_currentTexture`（:79）、`_currentDefinition`（:77）、`_mapImage`（:89）。
- 小地图和全屏地图**是同一个 UI 对象**，只是换了锚点和尺寸（:884-912 的两个分支改的是同一个 `_mapRect`）。
- 图源选择是**全局**的：`ShouldUseCommunityMap()`（:320-327）读 `_settings.MapSource`，跟显示模式无关。

真正的障碍不是"两个纹理槽"，而是**两套互不兼容的坐标基准**：

| | 世界 → 贴图 UV 的换算 | 代码 |
|---|---|---|
| 社区图 | 仿射拟合 `CalibrationStore`（每张图一套） | `TryPlayerToMapUv` 第一分支 :828-833 |
| 原版底图 | NGUI 局部坐标 → `_vanillaTextureUv` 的线性映射 | `VanillaLocalToTextureUv` :1523-1532 |

而**标记**目前存的 `MapUv` 是**原版底图 UV 空间**的值——`CaptureVanillaIconsRecursive` 在 :1488 调 `VanillaLocalToTextureUv` 算出 UV，绘制时在 :806-816 直接拿来用。
也就是说：**抓来的图标没有世界坐标，只在一个基准里成立。** 两个图层基准不同，就不能共用一份图标列表。

⇒ **`MapDetail.GetWorldPosition()` 的重写不只是"刷新更可靠"，它同时是图源分离的前置条件。**
拿到世界坐标后，同一个标记才可以在两个基准里各自投影一次。

代价与注意（重写完成后）：

- 需要两个纹理槽 + **按图层惰性加载**。山间小镇 4625×4198 解压后约 55–75 MB，两张常驻要翻倍；不能每次按 M 现读盘（4400² 的 JPG 解码会卡 200–400 ms）。
- 原版小地图免费：免开面板的底图捕获（§16.1）本来就支持，所以"小地=原版"不需要玩家做任何事。
- 未校准的区域（现在有 16 张）在"大地图=社区图"下会**没有可用基准**——需要回退规则（未校准时该图层强制用原版）。
- `MapSource` 这个设置要从"全局"改成"每图层一份"（或保留全局作为默认值，两个图层可各自覆盖）。

### 27.2 想法 B：可见性解耦 + Tab 循环档位

**结论：能做到，而且便宜。这一条不依赖重写，可以单独做。**

现状问题：

- `_settings.Enabled`（:19 "启用小地图"）在 :228-229 直接决定 `shouldShow`，而全屏地图走的是同一个 UI 根 ⇒ **关了小地图就等于关了大地图**。
- `DisplayMode` 只有两个值（:33-37），Tab（`MapModeKey`）和 M（`TryRedirectGameMap` :399-417）走的都是同一个 `ToggleDisplayMode()`（:376-394）⇒ 两者语义完全相同，无法分开表达。

**建议的状态模型：把"全屏地图"当成模态覆盖层，把"小地图"当成常驻底层。**

```
private enum MapViewMode { None, Mini, Full }
```

- **底层状态** `_viewMode`：由 Tab 循环（或 F8）改变。
- **全屏是覆盖层**：M 永远打开全屏，**与底层状态无关**；再按 M / Esc 关闭，回到打开前的底层状态。
- 这样"小地图关了"和"能不能看大地图"彻底无关——**这正是用户要的**。
- F8（临时隐藏）保持正交，不动。

Tab 循环档位做成一个设置 `TabCycleMode`，正好覆盖用户说的模式 1/2/3：

| 选项 | 循环 |
|---|---|
| `小地图 ↔ 无` | Mini → None |
| `大地图 ↔ 无` | Full → None |
| `小地图 ↔ 大地图 ↔ 无`（用户说的模式 3） | Mini → Full → None |
| `小地图 ↔ 大地图`（当前行为） | Mini → Full |

配套设置改动：

- `Enabled` 保留字段名（ModSettings 按字段名存 `Loader.cfg`，改名会丢用户值），语义收窄为"显示小地图 HUD"。
- 新增 `TabCycleMode`（Choice）、`MenuKeyOpensFullMap`（bool，默认 true，关掉即恢复 Tab 单一控制）。
- `MapModeKey` / `GameMapKey` 不动。

估时：1~1.5 小时，低风险，可单独验证。

### 27.3 一条排序结论

| 想法 | 依赖 | 结论 |
|---|---|---|
| B（可见性解耦 / Tab 循环） | 无 | **可以插进"顺手做"档**，和 §26.5 的 4~7 一起 |
| A（图源分离） | **必须先完成 §25.5 标记重写** | 重写之后做，属于"锦上添花" |

### 27.4 其他改良游戏体验的点子（供挑选，均未承诺）

按"价值 / 成本"排序。标 ⚠ 的依赖标记重写。

| # | 点子 | 说明 | 成本 |
|---|---|---|---|
| 1 | **全屏地图上点一下放路径钉** | 显示到玩家的直线距离与方位角。TLD 本身没有任何路线规划工具，这是最贴合生存玩法的一个。只做直线距离，不做寻路——诚实且便宜 | 中 |
| 2 | **玩家轨迹** | 在全屏地图上画最近 N 分钟走过的路。暴风雪、洞穴、回头路时极其有用。默认**关**，避免破坏原版体验；要不要跨场景/跨存档保存另说（不存最省事） | 低~中 |
| 3 | **距离比例尺** | 用已有的标定数据（每个图的 px/世界单位）在角落画一根"100 m"标尺。副作用是**它同时也是一张图校准好坏的肉眼自检**——拟合错了比例尺会明显荒谬 | 低 |
| 4 | **按键提示条** | 全屏地图底部一行："滚轮缩放 / 左键拖动 / M 或 Esc 关闭"。我们发明了交互却没告诉玩家 | 很低 |
| 5 | **"回到玩家位置"键** | 有了拖拽和缩放之后很容易迷路（中键或空格） | 很低 |
| 6 | ⚠ **标记分类筛选 / 图例** | 后期地图上是一片图标墙。`MapDetail` 有 `m_IconType` / `m_SpriteName` / `m_LocID`，天然支持按类别开关。**顺带解决性能问题**（见下） | 中 |
| 7 | ⚠ **点击标记看名称** | 需要 `m_LocID` 走本地化 | 中 |
| 8 | **过渡区提示牌** | 没底图的区域不再"静默没 HUD"，显示一个"此区域无地图"的小条（和待办的过渡区回退开关是同一件事） | 很低 |
| 9 | **未校准区域的回退** | 现在 16/22 未校准。未校准时应显示整图而不是一个错位的裁切，并明确标"未校准" | 低 |
| 10 | **手柄支持** | TLD 大量玩家用手柄。现在的拖拽/缩放全假设鼠标。**这是一个已知缺口**，至少要让手柄玩家能开关地图 | 中 |
| 11 | **导出当前地图视图为 PNG** | 对玩家价值一般，但**对我们是校准利器**（带标记和玩家的实拍图，可与原图叠） | 低 |
| 12 | 世界地图（22 个区域缩略图 + 连接关系） | 就是 §26.3 那个愿景 | **大，不承诺** |

**一条技术债，顺手记下：** 现在每个标记是一个独立 `GameObject` + `RawImage`（`_vanillaIcons`，`ModEntry.cs:94`）。现在就有 800+ 条，标记重写后只会更多。§27.4 的第 6 项（分类筛选）能缓解，但**根本解法是合并成一次绘制**。重写时值得一并评估。

### 27.5 与 §26.5 的关系

本节**不改变** §26.5 的优先级，只做两处增补：

- 在 §26.5 第 7 项（流程完善）附近插入 **27.2 的可见性解耦**——它便宜、独立、且修的是一个用户已经踩到的真实问题。
- 在 §26.5 第 2 项（标记重写）下面加一条注记：**该重写同时解锁 27.1 的图源分离**，所以它的性价比比原先估计的更高。

---

## 28. 视图解耦、勘测弹图、按键提示（2026-09-28 傍晚，已实现并安装）

已安装并**尚未经用户实测**。旧版自动备份在 `Mods\CommunityMinimap\backups\`。

> 这里**故意不钉构建哈希**：本工程是确定性编译，源文件字节（连行尾符都算进去）会进 MVID，
> 重编译一次哈希就变，钉在文档里只会变成过期噪音。要复现某一版，请用 git 提交号。

### 28.1 小地图与全屏地图彻底解耦（§27.2 想法 B，已实现）

`DisplayMode`（只有 MiniMap / FullMap 两态）**已删除**，换成两个独立的图层开关：

```csharp
private bool _miniMapOn = true;   // 常驻底层
private bool _fullMapOn;          // 模态覆盖层
private bool MiniMapVisible => _miniMapOn && _settings.Enabled;
private bool FullMapVisible  => _fullMapOn && _settings.FullMapEnabled;
```

- **全屏地图是覆盖层，不是"另一个模式"**：`_miniMapOn` 在打开全屏地图时**不变**，只是渲染时全屏优先。
  关闭全屏地图后自然回到原来的小地图状态，不需要额外的"记住之前状态"变量。
  （这也是 §27.1 图源分离将来需要的形状：两层各自独立。）
- **`M` 永远打开全屏地图**，与 Tab 循环走到哪一档无关 —— 这就是用户要的"小地图关了也能看大地图"。
- **Tab** 走 `GetTabCycle()` 生成的序列，由新设置 `TabCycleMode` 决定，并**自动剔除被关掉的图层**：
  两层都关时序列只剩 `(false,false)`，Tab 变成空操作而不是出错。
- **输入上下文（鼠标释放）**改由每帧对账：
  `if (s_fullMapActive != FullMapVisible) { … Push/Pop … }`
  ⇒ 在设置里关掉全屏地图时也会把鼠标还给游戏，不会留下一个锁住的指针。
- 新增设置：`FullMapEnabled`（启用全屏地图）、`TabCycleMode`（4 档循环）。
  `Enabled` 的字段名**保持不变**（ModSettings 按字段名存 `Loader.cfg`，改名会丢用户的值），
  只把语义收窄为"角落小地图"。

### 28.2 勘测弹图三选项（§26.1，已实现）

`ExecuteOpenMapActionFromObjective` 不再复用按键那条路径，改走
`TryRedirectGameMapFromObjective()`，按新设置 `SurveyPopup` 分三路：

| 选项 | 行为 |
|---|---|
| 原版地图 | 不动游戏原行为，让原版地图面板打开 |
| 我们的地图 | 与按 M 相同，打开本模组全屏地图 |
| 不弹 | 直接吃掉 |

两条路径共用同一个 120 ms 去重窗口并各自写日志（`Survey popup: …` / `Duplicate map action …`），
所以"勘测时到底哪条路径被触发了几次"可以从日志直接读出来 —— 这正是要实测确认的东西。

### 28.3 全屏地图按键提示条（§27.4 第 4 项，已实现）

全屏地图底部一行：`滚轮 缩放    左键拖动 平移    M / Esc 关闭`（`ReleaseMouseOnFullMap` 关掉时不显示前半段）。

- 文字用 `UnityEngine.UI.Text`，字体按顺序探测并**写日志**：
  内置 `LegacyRuntime.ttf` → 内置 `Arial.ttf` → OS 字体 `Microsoft YaHei` / `SimHei` / `Noto Sans CJK SC` / `Arial`。
- 全部失败则**不显示提示条**并 `Warning`，不影响地图本身。
- 为此 `CommunityMinimap.csproj` 新增引用 `UnityEngine.TextRenderingModule.dll`
  （`UnityEngine.Font` 在这个模块里，之前没引用，编译直接报 `CS0246`）。
- 设置：`ShowKeyHints`。

### 28.4 `build.ps1` 加固（§26.5 第 4 项，已完成）

原来的隐患是"构建失败仍可能安装"。现在：

1. **先删掉 bin 里的产物**，再构建 ⇒ 构建失败时根本没有东西可拷，不再依赖 `$LASTEXITCODE` 一个判据。
2. 构建后校验产物**存在**，且**不早于最新源文件**。
   （不能用墙上时间比较：增量构建会把上一次的产物拷过来并保留时间戳，好构建会被误判为旧。）
3. **检测游戏是否在运行**（`Get-Process -Name 'TheLongDark'`，精确名，不用 `tld*` 通配以免命中 `TLDConsole`），在跑就拒绝。
4. 安装前**自动备份**旧 DLL 到 `Mods\CommunityMinimap\backups\`。
5. 安装后**比对 SHA256**，不一致就报错。
6. 默认游戏目录改为**探测**常见 Steam 根目录（本机在 D:，原来硬编码 C: 直接失败）。
7. 新增 `-SkipInstall`。

### 28.5 手柄支持：**设计，尚未实现**（§27.4 第 10 项）

**已经能用的部分**：手柄的"地图"键走的是游戏自己的 `ExecuteOpenMapAction`，我们已经拦截
⇒ **手柄玩家现在就能打开我们的全屏地图。**

**真实缺陷（优先级不低）**：

- `ShouldSuppressGameEscape()` 要求 `Input.GetKeyDown(KeyCode.Escape)`，而手柄不产生这个键；
  我们监听的关闭键又是 `GameMapKey`（默认 `KeyCode.M`）。
  ⇒ **手柄打开地图后关不掉**，同时手柄的 Start 还会把游戏暂停菜单一起打开。
  ⇒ 手柄玩家有可能被卡在全屏地图里，只能强退。

**按"先量再改"，先测这四件事**（不要凭猜写映射）：

1. 手柄地图键在 MelonLoader 侧对应哪个 `KeyCode`（`JoystickButton0..19`）？
   做法：`OnUpdate` 里临时打印任何被按下的 `JoystickButton`。
2. 手柄 Start 对应哪个 `KeyCode`。
3. 左/右摇杆在 Unity 传统输入下有没有可用轴名（TLD 用自己的 `InputManager`，轴可能压根没配）。
4. **游戏的暂停菜单如何判定为"已打开"** —— 参照 `TryGetOpenVanillaMap` 找 `Panel_Map` 的写法找暂停面板。

**设计草案（拿到 1~4 之后）**：

| 功能 | 建议输入 |
|---|---|
| 开/关全屏地图 | 游戏原本的地图键（开启已可用，关闭待键码） |
| 退出全屏地图 | Start 或 B —— **至少要有一个可靠出口** |
| 平移 | 左摇杆 |
| 缩放 | 右摇杆 或 LB/RB |
| 回到玩家位置 | 按下左摇杆 |

**不依赖任何测量的兜底（建议先做这个）**：
全屏地图打开时，一旦检测到游戏暂停菜单被打开，就自动关闭我们的地图。
这样手柄玩家至少有一条出路（Start → 暂停 → 地图已关），而且只需要第 4 项那一个判据。

---

## 29. 设置界面重做 + 二级菜单（2026-09-28 傍晚，已实现并安装）

**已安装，尚未实测。** 版本字符串改为 `社区HUD地图 0.7.0`，日志里可以据此分辨构建。

### 29.1 一句话结论：原来 24 项，现在 23 项，默认可见 17 项

删掉的冗余：`启用小地图`、`启用全屏地图`、`Tab 循环内容`、`切换小地图/完整地图`（按键）。

**关键认识：`小地图常驻` 是个伪需求。** 它当初存在的唯一理由是"关掉小地图会连带关掉大地图"，
而那个 bug 在 §28.1 的图层解耦里已经修掉了。所以直接回到一个普通开关 `显示小地图`：

| | 小地图 | 全屏地图 |
|---|---|---|
| `显示小地图` = 开 | 一直显示 | **M 打开/关闭** |
| `显示小地图` = 关 | 不显示 | **M 照样打开/关闭** |

**循环键整个删掉了** —— 一个开关 + M 已经覆盖所有情况，没有东西可循环。
**Tab 冲突不是靠换键解决的，是靠不需要那个键解决的。**

### 29.2 临时隐藏键（F8）的语义改窄了

- 改名 `临时隐藏 HUD` → **`临时隐藏小地图`**
- **只隐藏小地图，全屏地图不受影响**（按 F8 之后 M 照常打开全屏地图）
- 修掉一个连带 bug：原来的 `shouldShow` 里 `!_temporarilyHidden` 会把全屏地图一起藏掉

### 29.3 二级菜单：已确认可用并已使用

`ModSettingsBase` 的 API（**dump 确认，不是猜的**）：

```
SetFieldVisible(string, bool)                 [public]
IsFieldVisible(string)                        [public]
RefreshGUI()                                  [public]
OnChange(FieldInfo, object, object)           [protected virtual]   ← 覆写
OnConfirm()                                   [protected virtual]   ← 覆写
```

实现要点（`MinimapSettings.ApplyVisibility`）：

- 用 `OnChange` 传进来的 `newValue`，**不要回读字段** —— ModSettings 有 `confirmedValues`
  暂存机制支持取消修改，改动期间字段里未必是新值。
- `OnChange` 由设置界面驱动，而模组加载时界面还不存在，
  所以 `OnInitializeMelon` 里必须**手调一次** `ApplyVisibility(null, null)`。
- 整段包在 try/catch 里，失败只写日志，不影响模组运行。

**两组互斥显示：**

| 主开关 | 被隐藏的项 |
|---|---|
| `开发者模式` = 关 | 接管游戏地图键、显示坐标诊断、记录校准点、关闭全屏地图的按键、清理已采集标记（仅报告）、真正删除已采集标记 |
| `HUD 位置` ≠ 自定义 | 自定义位置 X、自定义位置 Y |
| `HUD 位置` = 自定义 | 边距 |

### 29.4 自定义位置（取代"拖动"，阶段 A）

`HUD 位置` 增加第 5 个选项「自定义」，配 `自定义位置 X` / `自定义位置 Y` 两个滑条（0–100%）。
百分比指的是**小地图中心的位置**（50% = 屏幕正中），比"角落 + 边距"更符合"躲开那个 HUD 元素"的意图。

用滑条而不是拖动的原因：游玩时鼠标是锁定的，拖动必须释放鼠标，而 TLD 站着不动时间照常流逝
（在雪地里摆 HUD 是会被狼咬的）。拖动记为**阶段 B**，等滑条用下来确认真的需要再说。

### 29.5 探针：`InputManager.GetOpenMapPressed`（只读，不改行为）

`InputManager` 有两个 public static（dump 确认）：

```
public static bool GetOpenMapPressed(MonoBehaviour)
public static bool GetKeyDown(MonoBehaviour, KeyCode)
```

**要回答的问题**：我们拦的是游戏的动作（`ExecuteOpenMapAction`），所以**打开**天然跟随玩家改键；
但**关闭**不行，因为为了不让玩家一边走一边开枪，我们推了自己的输入上下文，
而它同时挡住了游戏派发地图动作 —— 这才是 `关闭全屏地图的按键` 这个设置存在的**唯一**原因。

如果 `GetOpenMapPressed(_backgroundImage)` 在我们的上下文压着的时候仍然能正确回答，
**这个设置就可以彻底删掉**，关闭也会跟随玩家绑定。

**它现在只写日志**（`ProbeOpenMapQuery`），因为猜错的后果是：要么静默无效，要么地图一开就立刻关掉。

**实测步骤：**

1. 进游戏，按 **M** 打开全屏地图 → 日志出现 `Open-map probe armed (manual close key is M)`
2. 再按 **M** → 日志应出现 `Open-map probe: GetOpenMapPressed -> True/False (manual key M down this frame: True)`
3. **再去游戏设置里把地图键改成 N，重复一次**
4. 把这几行日志发回来

判读：

| 日志 | 结论 |
|---|---|
| `-> True` 且 `manual key ... True` | **可用**，删掉设置，打开/关闭都跟随绑定 |
| `-> False` 但 `manual key ... True` | 不可用（和动作派发一样被挡住），需要换思路：从游戏的键位表读实际绑定 |
| 每次都是 `-> True`（一开地图就 True） | 危险，说明它退化成"键还按着"，不能直接用 |

### 29.6 新布局（23 项）

**小地图**：显示小地图 / 临时隐藏小地图 / HUD 位置 / └自定义位置 X / └自定义位置 Y /
边距 / 小地图 UI 大小 / 小地图透明度 / 局部缩放 / 玩家指针尺寸 / 玩家指针配色 / 地图标记尺寸

**全屏地图**：全屏背景不透明度 / 释放鼠标（缩放与拖动）/ 按键提示 /
木炭勘测后弹图（**游戏内置 / 当前图源 / 不弹**）

**地图来源**：地图来源

**高级**：开发者模式 / └接管游戏地图键 / └显示坐标诊断 / └记录校准点 /
└关闭全屏地图的按键 / └清理已采集的标记（仅报告）/ └真正删除已采集标记

改名说明：`地图透明度` → `小地图透明度`（它只作用于小地图，代码里全屏地图的 alpha 是写死的 1）。
字段名保持不变（`Opacity`），**只有 `[Name]` 变了**，所以玩家已有的配置不会丢。

### 29.7 两个要注意的坑

1. **`记录校准点` 默认是 F9，而 TLD 自己很可能有快速存档/读档动作**
   （dump 里有 `ExecuteQuickSaveAction` / `ExecuteQuickLoadAction`）。
   如果 F9 是快速读档，我们的校准键就是在触发读档。**实测时留意**，必要时换键。
2. 删掉 `FullMapEnabled` / `TabCycleMode` / `MapModeKey` 之后，玩家 `Loader.cfg` 里会留下
   这几个旧键。TinyJSON 按字段名填充，多余的键一般会被忽略，但**第一次进游戏要确认没报错**。

### 29.8 怎么让玩家知道按 M 打开大地图（方案 A，已实现）

删掉 Tab 循环键之后模组少了一个"入口"，所以要把打开方式写进设置界面。

**结论：打开方式本身就是最自然的信息源。** 我们接管的是**游戏自己的地图键**，
任何玩过 TLD 的人 M 都在肌肉记忆里——他第一次按下去看到的就是我们的地图。
**这正是当初接管 M 键的意义：不是多开一个入口，而是玩家不用改习惯。**

顺带纠正一个想法：**Tab 从来不是玩家的信息源，是开发者的习惯。**
玩家发现功能只靠三处——设置菜单、肌肉记忆、屏幕上看得见的东西。Tab 一个都不占。

真正需要教的只有"打开之后能干什么"（缩放 / 拖动），那个已经由 §28.3 的按键提示条覆盖。

**已实现的两处文字：**

| 位置 | 内容 | 为什么放这里 |
|---|---|---|
| `显示小地图` 的描述 | "全屏地图请按游戏自己的地图键打开（默认 M —— 本模组接管了这个键，你不用改习惯）；打开后用滚轮缩放、按住左键拖动，再按一次 M 或 Esc 关闭。" | 它是模组设置的第一项，打开菜单默认就选中，描述面板会直接显示 |
| `全屏地图` 的**分节标题** | 改为 `全屏地图（按游戏地图键打开，默认 M）` | **分节标题是这个菜单里唯一常驻在屏幕上的文字**——单项描述要点中才显示 |

**记录备查、本次没做：**

- **首次提示**：小地图下方一行小字，玩家**第一次打开全屏地图后永久消失**。
  设计要点是"自终止"，不会变成贴一辈子、人人无视的提示。需要隐藏的
  `是否打开过全屏地图` 开关来记录。
- **游戏原生通知**：如果 `Panel_HUD` 有消息接口，可以推一条游戏风格的通知。
  最"原生"但最容易被误认为游戏自己的话。**没有 dump 确认过，不要当成可行方案。**
- **反向提示**（"原版地图还能用"）：**不做。** 走放射菜单和背包的玩家是自己找到那条路的，
  那是他们的选择，我们只要不把它弄坏就行。

### 29.9 视图轮换按键加回来了（用户要求，已实现）

删掉 Tab 循环之后用户提出**还是要加回来**，理由是"用起来真的很顺手，就在移动键旁边"。
采纳，但**降级成可选**，放在 `高级`：

```
高级
  开发者模式                    [关]     ← 不控制下面两项
  启用视图轮换按键              [开]
  视图轮换按键                  [Tab]    ← 关闭上一项时隐藏
```

**和 §28.1 那版循环的区别：那版有 4 档预设 + 一个 `Tab 循环内容` 选择器，太绕；
这版只有一个开关 + 一个键。**

**轮换顺序：小地图 → 全屏地图 → 不显示 → 小地图。**

```csharp
if (_fullMapOn)          ApplyViewState(false, false);   // 全屏 -> 无
else if (_miniMapOn)     ApplyViewState(true,  true);    // 小地图 -> 全屏
else                     ApplyViewState(_settings.Enabled, !_settings.Enabled);  // 无 -> 小地图
```

两个关键设计：

1. **下一个状态由当前状态推导，不用存索引。** 因为游戏地图键（M）会在同样的几个状态之间
   切换，存索引会失效并跳过一个状态。
2. **从小地图去全屏地图时，故意让小地图在底下保持开启。** 这样用 M 关闭全屏地图时
   会回到小地图，而不是回到"不显示"。

**已知冲突（用户知情并接受）**：游戏自己也用 Tab 打开生存面板（显示时间），两者会同时响应。
描述里写明了，冲突可以换键。**如果用户嫌烦，可以 patch `InputManager.ExecuteSurvivalPanelAction`
在轮换键按下的那一帧吃掉它** —— 但那是替用户做决定，先不做。

按键提示条也会带上轮换键：`滚轮 缩放   左键拖动 平移    Tab 切换视图    M / Esc 关闭`。

---

## 30. 待办总表（2026-09-28 整理，**以此节为准**）

前面各节的待办散落在 §11 / §12 / §18.5 / §19 / §20.7 / §22 / §23.2 / §26.5 / §27.4 / §28.5 / §29。
本节是唯一的总表；与旧节冲突时以本节为准。

### A. 等你实测（**已装进游戏，但都还没验证**）

这一批是当前离"可用"最近的，测完就能定下一轮改什么。

| # | 事项 | 怎么测 | 记在哪 |
|---|---|---|---|
| A1 | 设置界面重构 + 二级菜单 | 4 个分节；`开发者模式` 开关 6 项；`HUD 位置`=自定义 时 X/Y 出现、`边距` 消失 | §29.6 |
| A2 | 图层解耦 + F8 语义 | F8 **只藏小地图**；此时按 M **全屏地图照常打开** | §29.2 |
| A3 | `M` 关闭全屏地图 | 打开后再按 M 应关闭 | §28 前身 |
| A4 | 勘测弹图三选项 | 三档各试一次（要木炭） | §28.2 |
| A5 | 按键提示条 + 字体探测 | 全屏地图底部一行字；日志应有 `Key hint font: OS font ...` | §28.3 |
| A6 | `GetOpenMapPressed` 探针 | M 开→M 关；**再去游戏里把地图键改成 N 重复一次**；把 probe 日志发回来 | §29.5 |
| A7 | 旧配置残留 | `Loader.cfg` 里有三个已删字段，确认不报错 | §29.7 |
| A8 | **视图轮换按键（Tab）** | 高级里开关默认开、键默认 Tab；连按应在 **小地图 → 全屏地图 → 不显示** 之间轮换 | §29.9 |

### B. 主线（**承诺，最高优先级**）

| # | 事项 | 状态 |
|---|---|---|
| **B1** | **校准剩余 16 张图**（22 张中已完成 6） | 流程已跑通三次；下一张建议 `scene CrashMountainRegion`（林狼雪岭） |
| **B2** | **标记来源重写为 `MapDetail`** | 前置验证 100% 完成（§25）；约 150 行，**需要充足上下文时一次做完** |
| **B3** | **开箱即用**（地图包 + `CREDITS.md`） | 授权已到手；不做则 22 张图的价值出不来 |

### C. 和 B2 同一批做（**同一份数据，分两次就是翻两遍地**）

> ⚠️ **本节 2026-09-28 晚已修正，见 §32。** 原文把 C2/C4 说成"被 B2 解锁"，
> 那是 AI 自己推出来的需求，**用户从未打算把标记画到社区地图上**。
> `UpdateVanillaIcons` 里 `bool show = _usingVanillaMap` 是**原设计，不是缺陷**。

| # | 事项 | 和 B2 的真实关系 |
|---|---|---|
| C1 | **迷雾模式**（`天然全亮` / `跟随游戏勘测进度`） | 读 `MapDetail.m_IsSurveyed` / `Panel_Map.m_DetailSurveyPositions`，和 B2 确实是同一份数据。**只对原版图源有意义**（社区图是手绘的，本来就全亮） |
| C2 | ~~标记分类筛选 / 图例~~ | **降级为"想做再说"**。它当初的理由是"社区图会变成图标墙"——**那个前提不存在**，因为社区图上不画标记 |
| C3 | **性能：合并标记绘制** | 仍然成立但**不紧急**：标记只画在原版图源上，数量由游戏自己的地图决定，不会因为 B2 变多 |
| C4 | **图源分离**（小地用官方、大图用社区） | **和 B2 无关。** 只要"标记只画在原版图源上"，最小实现就是：小地图=原版（有标记）+ 大地图=社区（没有标记）。**不需要世界坐标，不需要 B2** |

### D. 独立小项（随时可做）

| # | 事项 | 备注 |
|---|---|---|
| D1 | 公路废墟裁剪（要裁左边）、蜿蜒河流裁剪（多面板拼版） | §22.2 |
| D2 | 过渡区回退开关 | 6 个过渡区现在没有 HUD（§19.4、§22.8） |
| D3 | `prepare-maps.ps1 -DeployToGame` | 防"改了没生效"（§22.3） |
| D4 | 拖拽编辑小地图位置（**阶段 B**） | 阶段 A（X/Y 滑条）已做；拖动要先解决"编辑时游戏不暂停"的问题（§29.4） |
| D5 | 首次提示（方案 B） | 本次只做了方案 A（写进设置）。B 的要点是"自终止"（§29.8） |
| D6 | 手柄支持 | **真实缺陷：手柄开得了地图但关不掉**，Start 还会把暂停菜单一起打开（§28.5）。需先测量 |
| D7 | 放射菜单 / 背包的地图入口 | 用户明确说**不急**。`Panel_ActionsRadial.DoOpenMap()` 很可能绕过我们（§30.2） |

### E. 待定 / 存疑（**不要当成任务开始做**）

| # | 事项 | 卡在哪 |
|---|---|---|
| E1 | 已采集标记清理 | `m_HarvestablesForMapVisibility` / `m_HarvestablesSharingIcon` **两字段对 816 条全空**，数据源不明。当前是"只报告"模式，**不会改数据**（§20） |
| E2 | `记录校准点` 默认 F9 | TLD 有 `ExecuteQuickLoadAction`，F9 可能是快速读档。**实测时留意**，必要时换键（§29.7） |
| E3 | 地图来源面板的透明边距 | 原版底图 PNG 有大量透明边距（神秘湖不透明区仅 51.3%），可能是"地图只有中间一块"的原因。用户澄清（A 透明边距 / B 拉伸）**仍未给出**（§23.3） |

### F. 已明确搁置

| 事项 | 结论 |
|---|---|
| 小地图渲染层级下沉（钻到游戏 HUD 下面） | 用户说**暂时不考虑**。前提未知：Unity Overlay 画布在所有相机之后合成，先把游戏 HUD 怎么渲染的量出来（§29 讨论） |
| 渲染层级诊断 | 跟着上一条一起放 |
| 运行层愿景（做自定义地图的运行层） | **不做承诺**（§26.3） |
| UX：地图边框、整体手感 | 用户说优先级不高 |

### G. 工程债

| # | 事项 |
|---|---|
| G1 | **推送积压：17 个本地提交未推**（GitHub 连不上，整天 `Failed to connect to github.com:443`） |
| G2 | `TAKEOVER-REPORT.md` 未跟踪，去留未定 |
| G3 | `README.md` 在粗校准完成前不更新 |
| G4 | 地图包授权范围 / 署名格式 / 日期，落地到 `CREDITS.md` |
| G5 | `tools/align.html` 是死路，`tools/check-sheet.py` 仅供参考——考虑归档或删掉 |

### 30.1 迷雾模式：可行性已经量过（2026-09-28）

**能做，而且数据比预想的好。** 先解释现象：游戏里有**两张不同的图**。

| | 是什么 | 我们用的 |
|---|---|---|
| 底图资源 | 干净的原始地形图 | ✅ 免开面板抓的就是这张 |
| `FogOfWar.m_RevealedMapTex` | 游戏渲染好的"底图 + 迷雾"结果（RenderTexture） | ❌ 没用 |

**所以原版图源是"天然全亮"的，而它本来不该是。** 社区图是手绘的、本来就带全部标注，不存在这个问题。

游戏自己的迷雾系统：

```
Panel_Map:
  Dictionary<string, Il2Cpp.FogOfWar>              m_FogOfWar          ← 每场景一份
  Dictionary<string, List<DetailSurveyPosition>>   m_DetailSurveyPositions
  float MAP_RADIUS / FOGOFWAR_RADIUS_MULTIPLIER
  float m_DetailSurveyRadiusMeters 等四个半径
  RevealFogForScene(...) / RevealCurrentScene()

FogOfWar:
  m_RevealedMapTex / m_RevealedMapZoomDetailsTex   ← 已盖好迷雾的贴图
  m_Shader / m_Material / m_CommandBuffer / m_Camera
  m_RevealColor / m_RevealColorFadeTime / m_RedrawEveryFrame
  m_VistaRevealMasks

DetailSurveyPosition { x, y, h, r, m_Time }        ← 每个揭示点是一个圆
```

**两条路：**

- **路线 A**：直接拿 `m_RevealedMapTex` —— 和游戏**像素级一致**，连淡入动画都是现成的。
- **路线 B**：用 `m_DetailSurveyPositions` 的圆自己画遮罩 —— 工作量大，但完全可控，
  而且两个图源可以统一处理。

**必须先测的坎（决定 A 能否用）：** 这些都挂在 `Panel_Map` 这个**面板**上，而我们的核心承诺是
"不用打开面板就有 HUD"。要测三件事，**面板开/关两种状态各记一次**：

1. 面板**关着**时 `Panel_Map.m_FogOfWar` 里有没有实例？
2. 面板**关着**时 `m_DetailSurveyPositions` 有没有数据？（存档数据，大概率一直在）
3. `m_RevealedMapTex` 是一直有效，还是随面板关闭释放？

**1 或 3 若为否，路线 A 就退化成"只有开过面板的人才有迷雾"，那不如直接做路线 B。**

### 30.2 放射菜单 / 背包的地图入口（用户说不急，记录备查）
`InputManager` 里开地图的方法**只有两个**，我们两个都拦了。但放射菜单有自己的入口：

```
Panel_ActionsRadial.DoOpenMap()   [public]
```

它是**面板上的方法**，很可能绕过我们直接开 `Panel_Map`。背包那条是同一类问题的可能性很大。

**现状不破坏核心承诺**（小地图 HUD 与怎么开全屏地图无关），但会造成不一致：
习惯用放射菜单的玩家**永远看不到我们的全屏地图**。

**正确修法是拦面板本身，不是追调用者：**

```csharp
[HarmonyPatch(typeof(Panel_Map), nameof(Panel_Map.Enable), new[] { typeof(bool) })]
```

一个补丁覆盖所有入口，包括以后新增的。**但有两个坑：**

1. `地图来源 = 原版制图` 时我们**要靠这个面板**抓底图和标记；`木炭勘测后弹图 = 游戏内置` 时也要放它过去
   ⇒ 需要一个"这次是我们自己要的"标志位。
2. `Enable(true)` **可能不只是"玩家打开了地图"** —— 面板内部刷新、`EnableWorldMapOfCurrentScene`
   之类都可能调它。无脑拦可能把面板内部逻辑搞坏。

**所以先加只读日志探针**，用 M 键 / 放射菜单 / 背包各开一次地图，看谁在调 `Enable(true)`。

---

## 31. 首次实测结果与三个被证伪的旧结论（2026-09-28 晚）

用户 20:22 跑了一次（山间小镇），日志 `MelonLoader\Latest.log`。**零 warning、零 error。**

### 31.1 实测通过的部分

| 项 | 证据 |
|---|---|
| 版本字符串 | `社区HUD地图 0.7.0 initialized.` |
| 二级菜单没崩 | 无异常；`DeveloperMode: true` 已存进配置 |
| 删除旧字段无害 | 配置里 `GameMapKey` 等仍在，**没有任何报错** |
| 按键提示条 | `Key hint font: OS font "Microsoft YaHei"` → 提示条创建成功 |
| **Tab 轮换** | 日志连续 `View cycle: FullMap → None → MiniMap → FullMap …` **完全正确** |
| **F8 临时隐藏** | `Mini map shown/hidden temporarily` 正常 |
| **M 开→M 关** | `Map key: view is now None` 正常关闭 |

### 31.2 被证伪的结论一：`GetOpenMapPressed` 可用，关闭键可以删掉

**用户把游戏地图键改成 N 之后**，日志：

```
Open-map probe: GetOpenMapPressed -> True   (manual key M down this frame: False)
Open-map probe: GetOpenMapPressed -> True   (manual key M down this frame: False)
Open-map probe: GetOpenMapPressed -> True   (manual key M down this frame: False)
```

**按了 4 次 N，`GetOpenMapPressed` 4 次全 `True`，而硬编码的 M 全程 `False`。**

⇒ 游戏自己的动作查询**在我们的输入上下文里完全可用，而且跟随玩家改键**。

**已实现**：`GameMapKey` 设置**已删除**，改为 `PollOpenMapKey()` 读 `GetOpenMapPressed`
的上升沿来关闭。因为**打开地图的那一次按键在这里也会读到 True**，所以它复用打开路径的
120 ms 去重窗口——不然地图会在打开的那一帧立刻关掉（这正是当初不敢直接用的原因）。

Esc 仍然永远能关，所以就算这条路径在某种情况下失效，玩家也不会被困住。

### 31.3 被证伪的结论二：游戏进程名是 `tld`，不是 `TheLongDark`

**这是一条一直在起反作用的"防御"。** §10 和 §28.4 都写着"用精确的
`Get-Process -Name 'TheLongDark'` 检查游戏是否在运行"——**这个名字永远匹配不到**，
`tld.exe` 的进程名是 `tld`。

后果：`build.ps1` 的游戏运行防线**从来没有生效过**。这次它在游戏运行时照样去覆盖被锁住的
DLL，只在 `Copy-Item` 抛 IOException 时才暴露出来。

**已修**：改用 `@('tld', 'TheLongDark')`，**不带通配符**（`tld*` 会连 `TLDConsole` 一起命中，
那是另一个工具，不锁 DLL）。进程名和 PID 会打进错误信息里。

### 31.4 被证伪的结论三：设置文件不是 `Loader.cfg`

实际是 **`TheLongDark\Mods\CommunityMinimap.json`**，每个 mod 一个文件。§4 和 §29.7 写错了。

**而且这有一个重要后果：改代码里的默认值，对已经有配置的玩家无效——JSON 里的旧值优先。**

例如这次把 `ToggleKey` 默认从 `F8` 改成 `X`，但用户的 JSON 里已经写着 `"ToggleKey": "F8"`，
所以**新默认不会生效**，必须从 JSON 里删掉这几项（或让玩家在界面里改）。

### 31.5 游戏默认键位表（**重要参考，别再猜**）

从游戏的「选项 → 按键设置」抄下来的完整列表：

| 键 | 功能 | 键 | 功能 |
|---|---|---|---|
| W/A/S/D | 前进/后退/向左/向右 | F | 状态 |
| LSHIFT | 冲刺 | I | 背包 |
| LCTRL | 蹲下 | C | 衣着 |
| LMB | 互动 / 射击 | K | 制作 |
| RMB | 放置 / 瞄准 / 投掷 | G | 烹调 |
| R | 装填 | J | 日志 |
| H | 放回 | **M** | **地图** |
| **SPACE** | **放射形菜单** | ESCAPE 🔒 | 暂停菜单 |
| **TAB** | **生存面板** | 1/2/3/4 | 光源/武器/诱饵/生火 |
| Z | 自动行走 | F5 🔒 | 快速保存 |
| Q / E | 向左/向右旋转 | F6 🔒 | 快速读取 |
| **F8** 🔒 | **调试截屏** | F9 🔒 | 截屏 |
| **F9** 🔒 | **截屏** | F10 🔒 | 高清截图（屏蔽 HUD） |

🔒 = 游戏内不可改绑。

**空着的键**：`B L N O P T U V X Y`、`F1 F2 F3 F4 F7 F11 F12`（以及大多数标点）。

**由此发现两个真实冲突**（已修）：

| 我们的项 | 原来 | 撞上 | 改为 |
|---|---|---|---|
| 临时隐藏小地图 | `F8` | **调试截屏** —— 每次隐藏 HUD 都顺手截一张图 | **`X`** |
| 记录校准点 | `F9` | **截屏** | **`F11`** |

**选择理由**：`X` 空着、左手够得到、旁边没有破坏性按键（F 排不行——`F7` 紧挨着
`F6` 快速读取，误按就是读档）。`F11` 给开发者用的校准键，孤立且空着。

**Tab 冲突是已知且用户接受的**：Tab 是生存面板，我们的轮换键也是 Tab，两者同时响应。
用户明确要 Tab（"就在移动键旁边，用起来真的很顺手"）。**如果以后嫌烦**，可以 patch
`InputManager.ExecuteSurvivalPanelAction` 在轮换键那一帧吃掉它——但那是替用户做决定，先不做。

### 31.6 又被证伪一次：勘测弹图拦错了方法

**现象**：`木炭勘测后弹图` 三个选项**全都弹出游戏内置地图**。

**证据**：整场游戏日志里**一条 `Survey popup:` 都没有**，而 `Vanilla map panel refreshed from
MountainTownSandbox_RegionMap` 出现了 —— 说明内置面板**确实被打开了，但我们的补丁没被执行**。

**结论：`InputManager.ExecuteOpenMapActionFromObjective` 根本不在这条路径上。**

从名字就能看出来：它是 **FromObjective** —— **任务目标提示**那条路，不是木炭勘测。
之前记的"加了补丁就修好了"是**误判**，当时没有验证。

**这是同一个错误的第二次**：不测量就假定某条路径存在。第一次是 Tab 冲突，第二次是这个。

**已加只读探针** `PanelProbe.cs`（临时文件，用完删）。它把一次勘测可能走的入口全盯上，
**只写日志、不改行为**：

```
Panel_Map.Enable(bool / bool,bool)     ← 面板自己的激活入口，最可能
Panel_Map.DoDetailSurvey(SurveyType)   ← 勘测动作本身
Panel_Map.RevealFogForScene(string)
Panel_Map.RevealCurrentScene()
Panel_ActionsRadial.DoOpenMap()        ← 放射菜单（顺带量 §30.2 那条）
```

两个设计要点：

1. **不用 `PatchAll`，逐个手动注册并 try/catch。** 某个目标的签名不对时 Harmony 会抛异常，
   而 `PatchAll` 里抛一次会把整个模组带下去。
2. **记录调用者**（`System.Diagnostics.StackTrace`，去重后取前 4 帧）。既然已经猜错过一次，
   这次要看到底是谁调的；IL2CPP 下拿不到托管栈就明说，不假装。

#### 探针结果（一次就定位）

| 时间 | 日志 | 用户操作 |
|---|---|---|
| 20:46:58 | `View cycle: FullMap` | 按 Tab |
| **20:47:22** | **`Panel_Map.Enable(True, True)`** | **木炭勘测** |
| 20:47:23 | `Vanilla map panel refreshed` | 内置面板确实开了 |
| 20:47:32 | `Panel_ActionsRadial.DoOpenMap()` + `Enable(True)` | **Space 放射菜单** |

**结论：**

- **勘测 → `Panel_Map.Enable(bool, bool)`，参数 `(true, true)`**
- **放射菜单 → `Panel_ActionsRadial.DoOpenMap()` + `Panel_Map.Enable(bool)`**
- **两条路用的重载不同，可以精确分开** —— 所以压制勘测弹窗不会连带压制放射菜单
- `DoDetailSurvey` / `RevealFogForScene` / `RevealCurrentScene` **一次都没触发**，
  它们也不在这条路上

**已修**：`InputPatches` 新增 `Panel_Map.Enable(bool, bool)` 前缀，接进 `HandleSurveyMapPopup`。
`ExecuteOpenMapActionFromObjective` 那个补丁**保留但降级**——它不在勘测路径上（名字里的
"FromObjective" 是任务目标提示），但剧情模式里确实有目标提示，所以仍接同一套三选项。

**"当前图源"这一档刻意不走 `OpenFullMap()`**，而是直接 `ApplyViewState(_miniMapOn, true)` ——
`OpenFullMap` 会看"接管游戏地图键"那个设置，而勘测弹图是**独立决定**；
否则关掉接管会让这一档既吃掉面板又什么都不显示。

**放射菜单那条路（§30.2）现在也有确切答案了**：`Panel_ActionsRadial.DoOpenMap()` →
`Panel_Map.Enable(bool)`。用户说"不急"，但**要修的话就是同一个补丁换个重载，一行的事**。


### 31.7 修了一个真崩溃：`HandleFullMapInput` 空引用

```
[20:42:00.317] System.NullReferenceException
   at CommunityMinimap.ModEntry.HandleFullMapInput()
```

场景是 `GreyMothersHouseA` —— **一个没有地图定义的场景**。Tab 把视图切到了 FullMap，
但那个场景从来没建过 UI（`EnsureUnityUi` 只在有地图的区域才被调到），于是 `_mapRect` 是 null，
左键拖动时崩在 `_mapRect.rect.size`。

**修法**：`OnUpdate` 里三处都加上"UI 不存在就别碰"的判断——全屏地图输入、按键轮询、
输入上下文对账。全屏地图的**状态**仍然可以存在（切场景回来还在），只是没有 UI 时不做任何操作。

### 31.8 用户实测：勘测弹图成功，但牵出两个新 bug

#### Bug A：`Panel_Map.Enable(bool, bool)` 不是勘测专用

**现象**：设成「不弹」或「当前图源」后，**从背包点地图也看不了内置地图**，只有设成「游戏内置」才行。

**原因**：**背包的地图按钮走的是同一个重载。** 我又一次把"勘测走这个"当成了"只有勘测走这个"。

**修法：给勘测加一个时间信号。** patch `MapDetail.Surveyed()`（勘测会把每个揭示的地点标记为已勘测），
记下时间戳；`Enable(bool,bool)` 只在**距上次勘测 3 秒内**才交给三选项回答，否则**一律放行**。

```csharp
double sinceSurvey = (DateTime.UtcNow - s_lastSurveyUtc).TotalSeconds;
if (sinceSurvey > SurveyPopupWindowSeconds)   // 3.0
    return false;      // 玩家自己开的，别碰
```

**这个设计是"向安全侧失败"**：如果 `Surveyed()` 哪天不触发了，行为退化成"内置地图照常打开"，
**不会再把玩家的地图锁死**——而这次的 bug 正是锁死了。

`MapDetail.Surveyed()` 有没有真的触发，探针里加了 `MapDetail.Surveyed()` 这一行日志确认。

#### Bug B：切换图源后 HUD 永久消失，必须换场景

**现象**：社区地图 → 原版地图，小地图和大地图**都不显示了**，切场景才恢复。

**原因**（`ApplyMapSourceSelection` + `TryRequestVanillaBaseMap`）：

```csharp
if (_baseMapPending || string.Equals(_baseMapRequestedScene, sceneName, ...))
    return;      // 「这个场景已经请求过了」→ 直接跳过
```

切到社区图时 `LoadCurrentMapIntoUnityUi` 会 **`Destroy` 掉那张原版底图**，
但 `_baseMapRequestedScene` 还写着这个场景名。切回原版时请求被跳过，
`_textureReady` 永远是 false → HUD 消失。只有换场景会重置这个标记，所以才"必须切场景才恢复"。

**修法**：`ApplyMapSourceSelection` 里**换图源就清空 `_baseMapRequestedScene` 和 `_baseMapPending`**——
不管往哪个方向切，离开的那个图源产生的贴图都会被销毁，所以"已请求过"这个标记本身就是谎话。

#### 教训（第三次同类）

**"某条路径走 X"不等于"只有那条路径走 X"。** 这已经是第三次：
Tab 冲突 → 勘测走了错误的方法 → `Enable(bool,bool)` 被当成勘测专用。

**共同的根因是同一个：把"观察到的一次"当成"规律"。** 防御手段也一样：
**凡是拦截类的改动，都要想清楚"如果还有别的调用者会怎样"，并且默认向不拦截的方向失败。**

---

## 32. 修正：AI 自己造了一个需求（2026-09-28 晚，用户指出）

### 32.1 我造了什么

用户问："我最开始就没打算往社区地图上按标记啊？你是怎么想到这一层的？"

**确实是我推出来的，用户从没提过。** 而且证据一直在代码里，我读过却没当回事：

```csharp
// UpdateVanillaIcons
bool show = _usingVanillaMap;    // 标记只在原版图源下绘制
```

**这是原设计。** 标记是按**原版贴图的 UV 空间**抓取和存放的（`VanillaLocalToTextureUv`
读到的是 `_vanillaTextureUv`），贴到社区图上会整体错位 —— 所以原作者（也是我）
**有意只在原版图源下画标记**。我却在 §31.8 和对话里说"社区图不画标记是缺陷"、
"B2 要解决的核心问题"、**"社区高清图 + 游戏标记才是这个模组真正的卖点"**。
**这几句都是我自己编的，不是用户的需求。**

### 32.2 推导链（哪一步开始错的）

```
用户说："小地用官方图源，大地图用社区图源"          ← 用户真的说过（§27.1 想法 A）
  ↓
我推断：两个图层基准不同，不能共用一份图标列表        ← 这一步是对的
  ↓
我推断：所以要让它成立，标记必须能在两个基准里各投影一次 ← 这一步是"我假设用户要两个图层都有标记"
  ↓
我推断：这需要 MapDetail.GetWorldPosition() 重写      ← 至此 B2 被我说成"图源分离的前置"
  ↓
我在 §30 把 C2/C4 都写成"和 B2 绑定"，B2 变成"枢纽"    ← 需求被我越滚越大
```

**错误在第三步：我把"图层"和"标记"绑死了。** 但用户的想法完全可以更简单：
**小地图用原版（有标记）+ 大地图用社区（没有标记）** —— 一致、可实现、**不需要世界坐标，也不需要 B2**。

### 32.3 修正后的结论

| 事项 | 修正 |
|---|---|
| 社区地图上画游戏标记 | **不是需求。删掉。** `show = _usingVanillaMap` 保持原样 |
| 图源分离（C4） | **和 B2 解耦**，最小实现不需要标记重写 |
| 标记分类筛选（C2） | 降级为"想做再说"——它当初的理由是"社区图会变成图标墙"，**前提不存在** |
| 标记合并绘制（C3） | 仍成立但不紧急 |
| **B2 标记重写** | ⚠️ **理由待重新确认，见 §34。** 本文档一直引用"我们采集完，地图上的标记就会消失，但是小地图不会"作为 B2 的唯一理由，**用户 2026-09-28 晚指出这句不成立**："实际上没有标记会消失"。**在重新确认之前，不要拿它去论证 B2 的优先级。** |

### 32.4 教训（**比 §31.8 那条更严重**）

§31.8 记的是"把观察到的一次当成规律"——那是**技术判断**出错。
这一次是**产品判断**出错：**我把自己的推论当成了用户的需求，而且反复加固它，最后写进了待办总表。**

**防御手段：**
1. **用户没说过的需求，写进手册时必须标清楚"来源：AI 推断"**，不能混在用户需求里。
2. **推论链要留痕。** 这次能查出来，是因为 §27.1 把推导写下来了；如果只写了结论，就永远查不出。
3. **当一个"前置依赖"把某件事的重要性抬高时，回头确认那个依赖是不是真的。**
   B2 从"让标记不过期"被我抬成"模组的卖点"，靠的就是一串没被验证的"所以"。

---

## 33. 评估记录：游戏标记叠到社区图上（**结论未验证，暂不做**）

用户问："社区图上画游戏标记有价值吗？只要把大地图和小地图分开，这个价值也没多大吧？"

### 33.1 已经确定的事实

用 `tools/marker-overlay-check.py` 离线渲染（§24 记的 3 个真实世界坐标 + `calibrations.json`
的仿射 + 实际在用的 `mountain_town.jpg`）。**不需要游戏运行。**

**唯一有效的交叉验证**——教堂：

| | 世界坐标 |
|---|---|
| 用户记录的校准点 | `(699.761, 2102.211)` |
| `MapDetail` 给的 `icoMap_churchMilton` | `(688.6, 2102.0)` |
| 差 | **11.2 世界单位 ≈ 图形上 20px（图宽的 0.46%）** |

⇒ **`MapDetail.GetWorldPosition()` 和我们的校准点在同一坐标系里**，投影机制是通的。
**这一条是成立的。**

### 33.2 站不住的推论（AI 犯的第二个产品判断错误）

上一轮我说 `mtSchool` 和 `MiltonHouse` 是"样本外验证"，**说重了**。实测距离：

| 标记 | 最近的校准点 | 距离 |
|---|---|---|
| `mtSchool` 老旧校舍 | 米尔顿信用合作社 | 173px（4.0%） |
| `SCENENAME_MiltonHouse` 小镇地名 | 米尔顿邮局 | 83px（1.9%） |

**它们和最近的校准点不是同一个物体**，所以这个距离**既不是误差，也证明不了任何东西**。
放大到 4 倍看，学校那个红十字落在「老旧校舍」标签上方**一堆图标中间**（房屋、人影、矿车、蘑菇）——
**看着像对的，但不是证明。**

### 33.3 结构性障碍（**这条最重要**）

**校准点永远无法验证手绘图在锚点之间的精度。** 仿射是被这些点钉住的，锚点上的残差必然是 0
（实测最大 0.1px）。它**只反映锚点，不反映锚点之间有没有扭曲**。

要验证锚点之间，唯一的路是**看大量标记的整体分布**：800 个标记若沿着手绘道路/建筑铺开，
图就是准的；若成片偏离，叠上去只会暴露它。

**用户否决了这个实验**（816 条太多），所以：

> **⚠️ §33.1 之外的一切都是未验证的。**
> "社区图已经很准，不值得叠加"这个结论**建立在 3 个样本、其中 2 个无法验证之上**，
> **不是结论，是待验证的猜想。** 以后不要引用它当作已决事项。

### 33.4 当前决定

**不做叠加。** 理由是用户的判断（社区图自带完整图标层，价值有限），**不是我们的测量结论**。

若将来要做，两个前提缺一不可：

1. 22 张图全部校准完，且每张的残差都验证过
2. **只叠"有状态"的标记**——`m_IsSurveyed`、采集状态、玩家自己的营地、任务点。
   静态地理一律不画：**作者画得比我们好，而且已经画完了。** 手绘图告诉你"有什么"，
   只有游戏知道"还剩什么"，这是唯一不可替代的那部分。

**附带更正一个前提**：导出标记**不需要玩家站在任何地方**。
`MapDetail.GetWorldPosition()` 是物体自身的坐标，与玩家位置无关。

### 33.5 留下的工具

`tools/marker-overlay-check.py`——改开头的 `MARKERS` 列表就能重跑，
产出全景图 + 每个标记的宽松/紧密裁切。**要重新评估时直接用它，别再从零写。**

---

## 34. 又一句被撤销的前提：标记到底会不会消失（2026-09-28 晚）

**用户原话**："采集完，地图上的标记就会消失，但是小地图不会 —— 这个不对，实际上没有标记会消失。"

**这句话从本对话很早期就被记进手册，并且一直是 B2（标记重写）的**唯一**理由。**
现在被撤销了，所以 **B2 目前没有经过确认的理由。**

### 34.1 已知的、互相矛盾的线索（**都还没验证**）

| 来源 | 说法 |
|---|---|
| 手册 §20.2（读 MapIconFix 得出的） | **游戏本身有 bug：既不删数据也不删图标**——该消失的标记不消失 |
| 本次用户观察 | **没有标记会消失**——和上面一致 |
| 手册早期引用的用户原话 | 地图上的标记会消失、小地图不会——**现在被撤销** |

**§20.2 和用户现在的观察是一致的**：标记不会消失。如果是这样，那问题就不是"刷新不及时"，
而是"**该消失的标记永远留着**"——那是**游戏自己的 bug**，也正好是 MapIconFix 存在的理由。

### 34.2 我们自己的相关代码（现状）

- `TryRefreshVanillaIcons`：每秒轮询面板的标记容器，签名变了就重新抓——**为"标记会消失"设计的**。
- `CleanHarvestedMapMarkers`：扫描已采集的标记，**目前只报告不删除**；实测
  「none of 816 entries look collected」，因为 `m_HarvestablesForMapVisibility` /
  `m_HarvestablesSharingIcon` **两个字段全是空的**（§20）。

⇒ **我们既不知道标记该不该消失，也没有能力判断哪个已采集。** 这两件事都要先解决，
才有资格谈"重写标记来源"。

### 34.3 待确认（**问清楚再动，不要再猜**）

1. **你期望标记消失吗？** 采完的资源、搜完的容器，你**希望**它从小地图上消失，还是**希望**它留着？
2. **游戏内置地图上**，同一个东西采完之后标记还在吗？
3. 如果你希望它消失而现在不消失，**那是游戏的 bug**——我们要做的是"**在我们的 HUD 上替它修**"，
   而不是"刷新不及时"。**这两件事的实现完全不同。**

---

## 35. 标记丢失的真正成因（2026-09-28 深夜，已修）

**现象**：切换图源后标记丢失，只剩内置地图**点亮区域**的标记。用户怀疑是"偶发现象"。

### 35.1 成因是确定的，不是偶发

`CaptureVanillaIconsRecursive`：

```csharp
alpha = sprite.enabled ? sprite.alpha * sprite.color.a : 0f;   // enabled=false → 直接丢弃
```

**而 `Panel_Map.RefreshIconVisibility()` 正是靠 `enabled` 画迷雾的。**

| 状态 | 抓到的标记数 |
|---|---|
| 面板**从没打开过** | **162** ← 迷雾还没应用 |
| 面板**打开过一次之后** | **28** ← 只剩点亮区域 |

**证据就在 20:42 那次的日志里，一直在那儿**：

```
[20:42:13.477] Captured 162 vanilla map marker layers (panel-free).
[20:42:53.310] Captured 28  vanilla map marker layers (panel-free).
[20:43:16.866] Captured 28  vanilla map marker layers (panel-free).
```

`keepExistingWhenEmpty` **挡不住**，因为 28 不是空。

**为什么感觉是偶发**：`TryRefreshVanillaIcons` 只在**签名变化时**才重抓
（`if (signature == _vanillaIconSignature) return;`）。所以成因确定，
但**触发时机取决于玩家行为**，于是看起来像运气。

### 35.2 两处修复

1. **切换图源不再销毁标记、不再强制重抓。**（这一条是我上一轮引入的回归：我清了
   `_elementsLoadedForScene` 逼它重抓，正好踩进上面那个坑。）保留它还顺带避开另一个坑——
   `LoadMapElementsForScene` 是**追加**的，重抓会让每个标记**翻倍**。
2. **抓取结果比现有集合小就拒绝**，并写日志
   `Kept N markers: the re-capture returned only M`。理由：**游戏根本不会删除标记**（§20.2），
   所以"变少"只可能是迷雾。

### 35.3 记下的架构问题

**现在"抓取"和"可见性"是混在一起的**——抓取时用 `enabled` 判断，而那是渲染期的概念。

**正确的分层：抓取只管收集，可见性在绘制时决定。**
这正好是**迷雾模式（§30.1）需要的地基**：那时迷雾会变成"画不画"的问题，而不是"抓不抓"的问题。

**未验证**：修复 ② 靠"保留较大的集合"，代价是**可能留住过期标记**。
当前与"游戏从不删标记"等价，所以没有实际损失；但**一旦开始做迷雾或采集清理，这条必须重新审。**
---

## 36. 林狼雪岭校准完成，以及一次测量出来的方法论错误（2026-09-29）

**7 / 22 张完成。**

### 36.1 结果

用户用 `tools/calibrate.html` 自己在图上点地物，一次做对：

| | |
|---|---|
| 最大残差 | **0.1px** |
| 各向异性 | **0.04%** |
| 两轴夹角 | **90.00°** |
| 1000 世界 x / z | **2074.8px / 2075.7px** |

⇒ **纯相似变换：尺度 2.075 px/世界单位，无旋转、无剪切。**

对比其余 6 张：残差全部 0.0–0.1px。**所以校准这个环节从来不是瓶颈，瓶颈是"点在哪"。**

### 36.2 我犯的错：拿标签文字的中心当地物位置

我之前用"找蓝色地名标签 → 取文字中心"来定位地标，拟合出 **266.9px 最差残差**、
**180% 各向异性**、两轴夹角 97.7°——完全是垃圾。

即使用两个自洽的点反推、再自动匹配最近的标签，也只压到 **75.8px**。

**用户点地物后，同一批点给出 0.1px。** 两者的差就是标签的偏移：

| 地标 | 我（标签文字中心） | 用户（地物） | 偏移 |
|---|---|---|---|
| 机尾 | (1728, 1656) | (1938.7, 1712.1) | +210, +56 |
| 登山者棚屋 | (1656, 3408) | (1855.1, 3447.6) | +199, +40 |
| 起落架 | (768, 3600) | (906.0, 3568.8) | +138, −31 |
| 机翼-右下角 | (3216, 3120) | (3271.2, 3173.4) | +55, +53 |
| 瀑布山洞 | (792, 360) | (908.3, 385.5) | +116, +26 |

**全部朝同一方向偏（以向右为主，约 +55~210px）。** 这是这张图的标签排版规律——
**白框标签画在地物左上方以避让图面**——不是我偶然点错。**可复现，所以是可预防的。**

### 36.3 追加到 §9 标准校准流程

**校准点必须点在"地物本身"上，绝不能用地图上的文字标签定位。**

- 飞机残骸就点黑色残骸图形，不要点「机尾」白框
- 洞口就点洞口，不要点「洞穴」白框
- 标签框是**排版产物**，和地物有 50–210px 的系统偏移

**自检手段**：`calibrate.html` 的「4 · 结果」里
**各向异性应该接近 0%、两轴夹角接近 90°**。我那次 180% / 97.7° 就是明确的失败信号——
**这两个数字是免费的体检报告，以后每次都看。**

### 36.4 顺带作废的一条旧说法

手册 §17 说"社区地图与官方地图成固定比例，原版投影的各向异性被社区图继承"。
**林狼雪岭实测各向异性 0.04%**，即社区图在这张图上没有任何继承来的压扁。
那条规律的适用范围需要重新审——**至少不能当成普遍规律用。**

### 36.5 待办更新

| | 事项 | 状态 |
|---|---|---|
| B1 | 校准剩余 **15** 张（22 张完成 7 张） | 流程已验证，**用 calibrate.html 点地物** |
| — | 未校准区域全屏地图无法缩放/拖动 | **已定位 bug**：`UpdateUnityUi` 在设 `uvRect` 之前 `return`。等游戏关闭时修 |
| — | 全屏地图上那块软斑 | **已排除贴图**（原图源与部署副本都干净）。小地图无、轮换后仍在 → 稳定绘制在全屏地图路径上。待修 |
---

## 37. 图源分离 + 标记重写：执行准备（2026-09-29）

用户暂停校准，转入这两项。本节是**开工前的准备**，不是已完成的工作。**顺序：先图源分离，后标记重写**——
分离先定下"图层"这个抽象，重写才有明确的落点。

### 37.1 图源分离（小地图用官方、大地图用社区）

**现状**：只有一套资源，图源是全局的。

| 位置 | 现状 |
|---|---|
| `_currentDefinition` / `_currentTexture` / `_usingVanillaMap` | **各一套**，两层共用 |
| `_mapImage` / `_mapRect` | **同一个 UI 对象**，只换锚点和尺寸 |
| `ShouldUseCommunityMap()` | 读全局 `_settings.MapSource`，和显示模式无关 |
| `_baseMapRequestedScene` / `_elementsLoadedForScene` | 单份"已请求过"标记 |

**目标**：`MapSource` 拆成 `MiniMapSource` + `FullMapSource`（各自 自动/民间高清/原版制图），
两层各有自己的贴图、定义、基准。

**一个重要的简化**：两层的坐标变换**本来就是分开的代码路径**，所以改动主要是"当前生效的是哪一层"：

```csharp
if (!_usingVanillaMap)
    return _currentDefinition.TryWorldToMap(worldPosition, out uv);      // 社区图：仿射校准
// 原版图：panel.WorldPositionToMapPosition(scene, world) → NGUI → _vanillaTextureUv
```

⇒ **原版那一层不需要校准，全区可用；社区那一层需要校准，目前 7/22。**
这正是分离的价值：小地图用原版（全区 + 有标记），大地图用社区（漂亮，用于规划）。

**执行步骤**

1. 引入"当前图层"概念，由 `FullMapVisible` 决定；把 `_currentDefinition` / `_currentTexture` /
   `_usingVanillaMap` 改成**按图层取**（两个槽位，或一个小结构体数组）。
2. `ShouldUseCommunityMap()` 改成接受图层参数，读对应的设置项。
3. `LoadCurrentMapIntoUnityUi` 载入到**当前图层**的槽位；两层都保留已载入的贴图（切换时不重新解码）。
4. `UpdateUnityUi` 用当前图层的贴图 + 变换。
5. `_baseMapRequestedScene` / `_elementsLoadedForScene` **必须按图层分开**，否则又会踩
   §35.1 那类"离开的图源把标记作废、但'已完成'标记还在"的坑。

**已知代价 / 风险**

- **显存**：两张贴图同时驻留。林狼雪岭 4380×4302 解压后约 72MB，两张约 144MB。**按需惰性加载**。
- **切换时的卡顿**：不能在切视图时现读盘解码（4400² 的 JPG 要 200–400ms）。所以要预载。
- **标记基准**：`UpdateVanillaIcons` 的 `show = _usingVanillaMap` 要改成看**当前图层**。
  标记只属于原版那一层（§32.1 已确认这是原设计）。
- `_vanillaProjectionScene` / `_vanillaMapLocalBounds` / `_vanillaTextureUv` 是原版层专用的，不要被社区层覆盖。

### 37.2 标记重写（从 `MapDetail` 读，不再刮 UI）

**为什么做**——**这是实测数字，不是推断**：

| | |
|---|---|
| `MapDetailManager.s_MapDetails` 条目 | **723** |
| 从 UI 容器刮到的标记 | **148** |
| 丢失 | **80%** |

用户原话："原版制图的图源标记不全啊，怎么只有自然资源的标记"——**不是要玩家点亮，是我们抓的地方本来就不全。**

**已经确认可用的字段**（§24 + §36 的实测）

| 字段 | 结论 |
|---|---|
| `GetWorldPosition()` | ✅ 有值，**和我们的校准点同一坐标系**（教堂点相差 11.2 世界单位） |
| `m_TargetPosition` | ❌ 恒为 `(0,0,0)`，**不能用** |
| `m_SpriteName` | ✅ 语义化图标名，**通过 `_mapIconAtlas.GetSprite(name)` 100% 解析得到**（§25.3） |
| `m_LocID` | ✅ 本地化 key，可做地名 |
| `m_IconType` | **`Text` 是地名标签**（`m_SpriteName` 为空），要滤掉 |
| `m_IsSurveyed` | ✅ 实时勘测状态 |
| **`m_HarvestablesForMapVisibility`** | ✅ **有值**：163 条非空 / 387 个对象，**场景加载时即填充，不需要开面板** |
| `m_HarvestablesSharingIcon` | ❌ **全空**（0 条），**不要依赖它** |

> ⚠️ **§20 记的"两个字段全空"是错的**，已被 §36 的普查推翻。以本节为准。

**设计**

1. **位置**：`GetWorldPosition()` → **当前图层**的变换 → uv。与原版层的
   `WorldPositionToMapPosition` 或社区层的仿射共用同一条链路。
2. **图标**：`m_SpriteName` → `_iconBySpriteName`（刮取时顺便建的字典）→ 退化到
   `_mapIconAtlas.GetSprite(name)` → 再退化到通用标记。
3. **过滤**：跳过 `m_SpriteName` 为空的（`Text` 标签）。
4. **可见性放到绘制期决定**，不要像现在这样在抓取时用 `sprite.enabled` 判（§35.3）。
   这一步是**迷雾模式的地基**：那时"画不画"由 `m_IsSurveyed` 决定，而不是"抓不抓"。
5. **去重 / 聚类**：166 条香蒲、103 条树枝……同位置会叠成一坨。按距离聚类，或在同一图标上计数。

**风险**

- **723 个 `GameObject` + `RawImage`**：现在 148 个就已经不轻。**必须一并做 §30 的 C3（合并绘制）**，
  否则重写完性能会明显变差。这是重写里**最容易被低估**的一块。
- 图标解析依赖 `_mapIconAtlas`，而图集是**刮取时**拿到的。**第一次抓取前没有图集** →
  需要保留一条刮取路径专门用于建图标字典，或者从 `MapElements` 单独取一次图集。

### 37.3 开工前必须先做的一件事

**§35.2 的修复②（保留较大的集合）要重新审。** 它是为了止血写的启发式：
"抓回来变少 = 迷雾，不是标记消失"。

**标记重写之后，标记集合不再来自抓取，这条启发式就失去意义**，应该换成
**基于 `m_HarvestablesForMapVisibility` + `IsHarvested()` 的真实判据**。

⇒ 重写时要**把这条启发式一起删掉**，否则会带着一个过期的补丁进新架构。

### 37.4 本次已修（顺带）

| 事项 | 说明 |
|---|---|
| **未校准区域全屏地图不能缩放/拖动** | `UpdateUnityUi` 在设 `uvRect` 之前 `return`。现在只在**小地图**需要投影时才提前返回；全屏地图的缩放/平移是纯 uv 运算，不需要校准。顺带让**标记**在未校准时也照画（标记本来就在贴图 uv 空间里） |
| **`PanelProbe` 已删除** | 临时诊断，使命完成。保留的 `MapDetail.Surveyed()` 挂钩仍在，用于勘测弹图的时间信号 |

---

## 38. 图源分离的前置测量（2026-09-29，进行中）

§37.1 的分离要动十个现在全局共享的字段，其中「原版那一层用哪个基准」是**承重**的：
`UseVanillaBaseMap` 把 `_vanillaMapLocalBounds` **硬编码**成 `Rect(-325,-325,650,650)`，
而 `CaptureVanillaMap` 从面板控件的 `drawingDimensions` **推导**同一个字段。
今天两层共用一份，所以不冲突；分离之后小地图走 base map、大地图走面板，两者都要用它。

### 38.1 已挖出来的数据：原版两条路径同框

不需要新跑一次游戏。**旧日志里已经有面板路径的原始数字**，因为 `CaptureVanillaMap`
每次开面板都会打印它。全部日志里的去重结果只有一种取值：

```
MountainTownSandbox_RegionMap   bounds=(x:-325.00, y:-325.00, width:650.00, height:650.00)
                                uv=(x:0.00, y:0.00, width:1.00, height:1.00)
                                widget=650x650  rotation=(-0.0, 0.0, 0.0)  texture=2048x2048
```

对上 `UseVanillaBaseMap` 的硬编码：**两者逐位相同**，`uvRect` 是单位矩形，
旋转为零。⇒ 对 MountainTownRegion 而言，**硬编码的基准是对的**，
而且 §18.4 那条「原版底图贴图疑似旋转存储」在这张图上可以排除（旋转为 0）。
`GAME BASEMAP FRAMING: VERIFIED FOR MOUNTAINTOWN ONLY.`

### 38.2 这条证据的边界（**不要当成全区结论**）

手里只有 **1 个区域**的取值。样本量 1，不能外推，理由有两条：

1. `WindowRegionMap` 这类区域，面板控件的度量范围**可能**覆盖整张世界地图，
   而 `RegionMap` 只覆盖单个区域 ⇒ `drawingDimensions` 可能随场景变化。
2. 面板控件的尺寸也可能就是同一个 650×650 预制件、由 `WorldPositionToMapPosition`
   把世界坐标归一化进去 —— 那样它就是常量，硬编码永远对。**两种解释都符合现有数据。**

⇒ 结论必须按区域验证，不能按「已确认」写。

### 38.3 怎么补上其余区域

`ProbeVanillaFraming()`（`ModEntry.cs`，dev 模式开关控制，1.5 秒一次、同一场景最多 20 次）
在**开图时无论当前图源是什么**都会跑，会打印：

- 面板路径的 `drawing` / `uvRect` / `panelBounds` / `baseBounds`；
- 两者之比与偏移；
- `identical=` 布尔；
- 面板控件的旋转（顺带回答 §18.4）；
- **真正决定分离可行性的那个量**：把**同一个世界坐标**分别走两条链路算到**最终 uv**，
  报告差值，在玩家处和偏离 600 世界单位处各算一次
  （中心点会让纯比例误差互相抵消，所以必须有第二个点）。

判读方式：`delta` 折算到 2048 像素。**判据是「两条链路最终 uv 一致」**，
不是「bounds 相同」—— bounds 只是中间量，标记和指针消费的都是最终 uv。

> 这仍然是**单点采样**：它只能证明「这两条链路在这个区域一致」，
> 不能证明「两条链路数学上等价」。要真正证明等价，需要对多个世界坐标重复采样。

### 38.4 状态

| 事项 | 状态 |
|---|---|
| MountainTownRegion 的同框确认 | ✅ 已从旧日志挖出（§38.1） |
| 其余区域 | ⏳ 等一次实测：在**一个**非 MountainTown 区域开一次地图 |
| 最终 uv 一致性 | ⏳ 同上，探针已在游戏里 |
| 图源分离的实现 | ✅ **已实现并安装**，见 §38.5 |

> **来源：AI 推断** —— `WindowRegionMap` 可能覆盖整张世界地图这一条是推断，**未验证**。
> 它只用来解释「为什么要多测几个区域」，不作为任何实现依据。

### 38.5 图源分离：已实现（2026-09-29，**尚未实测**）

两阶段做的，因为「先验证再动手」把承重假设卡住了：

**阶段一（行为保持不变）** —— 把 §37.1 列的十个共享字段搬进 `MapLayer` 槽位：

| 从 | 到 |
|---|---|
| `_currentDefinition` / `_currentTexture` / `_loadedMapId` | `MapLayer.Definition` / `.Texture` / `.LoadedMapId` |
| `_usingVanillaMap` / `_textureReady` | `MapLayer.UsingVanilla` / `.TextureReady` |
| `_baseMapRequestedScene` / `_baseMapPending` / `_baseMapHandle` / `_baseMapRequestUtc` | `MapLayer` 同名成员 |
| `_elementsLoadedForScene` | `MapLayer.ElementsLoadedForScene`（**外加**一个全局的同名标记） |
| `_vanillaProjectionScene` / `_vanillaMapLocalBounds` / `_vanillaTextureUv` | `MapLayer.Vanilla*` |

活动图层由 `FullMapVisible` 决定：`ActiveLayerId => _fullMapOn ? LayerFull : LayerMini`。
所有「为了画而读地图状态」的地方都走 `ActiveLayer`。

**阶段二** —— `ReadLayerSettings()` 改成一层读一项
（`MiniMapSource` / `FullMapSource`），两项默认都是 `自动`，
⇒ **不碰设置的安装看起来和分离前一模一样**。

**两条刻意的设计决定**（都不是推断，是代码结构上的必然）：

1. **`_elementsLoadedForScene` 保留为全局。** 它是「这些标记已经请求过了」的标记，
   而 `LoadMapElementsForScene` 是**追加**语义 —— 按图层各调一次会让每个标记**翻倍**
   （§35.2 已经踩过）。`MapLayer.ElementsLoadedForScene` 是另一个事实
   （「这一层已经有标记了」），**不授权第二次调用**。
2. **原版投影状态放进 `MapLayer`，虽然它的值是全区域共享的。**
   曾经想放全局（「两层问的是同一个 `Panel_Map`，是区域的事实」），但这样有个真 bug：
   阶段二下如果小地图=原版、大地图=社区，大地图那次 `ApplyMapSourceSelection`
   会把**共享的** `VanillaProjectionScene` 清空，而小地图正在用它投影。
   ⇒ 值冗余、所有权分开。
3. **`_loadAfterUtc` 仍是全局。** 它现在只服务社区层的按需加载，
   而活动图层决定是谁在拖后腿；若将来两层都需要各自的加载延迟，这里要再拆。

**还没做**：§37.1 步骤 3 的「两层都保留已载入的贴图、切换时不重新解码」
只做了**一半** —— 每层各持自己的 `Texture2D`，不会被对方销毁，
但**惰性加载**意味着没显示过的图层仍然是空的，切过去时要现读盘解码。
§37.1 已经指出这是 200–400ms 的卡顿，**这一块没解决**。

**`MapSource` 这个旧键作废了。** 设置文件里原来那一项不再被读取，
`MiniMapSource` / `FullMapSource` 会以默认值 `0` 出现。
`ModSettings.dll` 没有暴露存档路径，所以没写迁移；
对当前用户无影响（他的 `MapSource` 本来就是 `0`）。

### 38.6 为验证补的两个诊断（都在游戏里了，**都还没跑过**）

分离是否正确，看单层状态行是看不出来的 —— **被对方冲掉的那一层，
在对方动之前看起来完全正常**。所以补了两条：

| 诊断 | 位置 | 输出 |
|---|---|---|
| **双层并排状态行** | `LogStateHeartbeat`（dev 模式，10 秒一次） | `[layers] mini: src=… using=… tex=… ready=… bounds=(…) proj=… baseReq='…' elems='…' \| full: …` |
| **标记分类普查** | `ClassifyMapDetails`，挂在 **F11** 上 | `Marker census: N entries; M 无 sprite 名（标签/区域）; K 个不同 sprite 名。可解析 a（表内 b + 图集 c），不可解析 d。能投影到图上 e，不能 f。最大分组：… ` |

**为什么先做分类普查而不是直接写标记**：§37.2 说对象数量是重写里「最容易被低估的一块」，
但真正要的数字不是 723，而是**能解析出精灵、能过标签过滤、能落在图内**的那部分。
`ClassifyMapDetails` **一个 GameObject 都不建**，只数数和按 sprite 名分组，
所以「166 条香蒲」这类堆叠会直接变成一个数字，而不是一个关于密度的主观判断。
它用 `TryPlayerToMapUv` 做投影判定 —— 和指针、和将来的标记**共用同一条链路**。

⇒ 拿到这个数字之前**不要开始建对象**：可能不需要聚类，也可能必须先把 723 个
`RawImage` 合并绘制（§30 C3）才有意义，这两条路的写法完全不同。

### 38.7 下一轮实测要看的四行

一次游戏会话（开一次地图 + 按一次 F11）能同时回答四件事：

1. `Framing probe … agree=` —— §38.3，两条链路的最终 uv 是否一致（**本区域**）。
2. `Framing probe offset …` —— 同上，偏离 600 世界单位处（防纯比例误差在中心抵消）。
3. `[layers] mini: … | full: …` —— 分离后两层是否各持各的贴图/基准，互不覆盖。
4. `Marker census …` —— §38.6，标记重写的真实工作量。

这四条**都不需要改设置**（dev 模式已开、图源是 `自动`）。
若要看分离本身，额外把小地图图源设成原版、全屏设成民间高清即可 ——
那也是唯一会暴露跨层覆盖的配置。

---

## 39. 第一次实测（2026-09-29 21:58 会话）：三个发现

日志 `MelonLoader\Logs\26-9-29_21-58-37.log`。区域 MountainTownRegion（山间小镇）。
用户实测反馈：**按 M 打开全屏地图，白屏约 2 秒。**

### 39.1 白屏：两个叠加的缺陷（**已修，待复验**）

日志里的证据是 `Loaded 山间小镇: 4360x4198` 出现了**两次**，相隔 4.5 秒：

```
[21:59:25.511] Loaded 山间小镇: 4360x4198.     ← 场景载入后正常加载
[21:59:29.011] Map key: view is now FullMap.   ← 用户按 M
[21:59:30.088] Loaded 山间小镇: 4360x4198.     ← 贴图已在内存里，却又解码了一次
```

**缺陷一：共享的加载时钟被反复推后。** `_loadAfterUtc` 是**全局**的（只有活动图层会被
加载循环服务），但两个图层都会写它。`ApplyMapSourceSelection` 进入时**无条件**把它设成
`UtcNow`，紧接着 `OnUpdate` 里那段「场景切换后延后一秒」的补丁又把它推后 ——
于是「已经装好了」这个状态被丢掉，内存里的贴图被重新读盘解码。
⇒ 修法：`RequestMapLoad()` 只允许把时钟**提前**，不允许推后。

**缺陷二：旧贴图在 UI 还指着它的时候就被销毁。** `LoadCurrentMapIntoUnityUi` 先
`layer.Texture = texture`，紧接着 `Destroy(previousTexture)`。但共享的 `_mapImage`
要到这一帧的 `UpdateUnityUi` 才改绑，中间这段窗口里 widget 指向已销毁贴图 ——
**画出来就是一块白**。这正是「白屏」而不是「卡顿」的原因，也说明了为什么只在
**可见图层重载**时出现。
⇒ 修法：`RetireTexture()` 把旧贴图挂到 `_retiredTextures`，下一帧开头
`SweepRetiredTextures()` 才真正销毁。

> 缺陷二是分离**放大**出来的：以前只有一份贴图，重载路径一样悬空，但重载本身很少发生。

**顺带**：`LoadCurrentMapIntoUnityUi` 现在也会**预热不在屏幕上的那一层**
（`_warmUpDone`，每场景一次、活动层就绪之后、场景载入 4 秒后）。
按需惰性加载正是白屏的成因，所以第一次切视图不该再付解码的钱。

### 39.2 图集和图标表在最常见的配置下是空的（**未修，阻塞标记重写**）

```
[21:59:43.612] Marker census (F11): ... Resolvable: 0
               (0 from the scraped table, 0 via the atlas), unresolvable 802.
[21:59:43.613] MapDetail summary: ... atlas present: False
```

**根因不是意外，是路径根本没跑**：`_mapIconAtlas` 和 `_iconBySpriteName` 只在
`CaptureVanillaIcons` 里填充，而那条路径挂在 `TryLoadVanillaElementsWithoutPanel` 上，
后者在 `OnUpdate` 里被 `if (!preferCommunity && ...)` 挡着。
⇒ **图源选「自动」（有民间高清图）时，原版图集永远不会被取到。**

这直接顶到 §37.2 的设计第 2 条（「图标解析依赖 `_mapIconAtlas`，而图集是刮取时拿到的，
第一次抓取前没有图集」）。**§25.3 那个「100% 可解析」是在原版图源下测的，
不能外推到默认配置。**

⇒ 标记重写需要一个**与图层无关**的图集获取路径：场景载入时无条件调一次
`ForceUpdateRegion()` + `LoadMapElementsForScene()` 把图集和图标表填上
（两者都幂等：后者是追加语义，所以必须保持 §38.5 那个全局「只请求一次」的闸门）。
**这一步必须先在实测里确认图集真的到手，才能开始建对象。**

### 39.3 密度问题比 §37.2 预想的小得多

```
Marker census: 816 entries; 14 without a sprite name; 33 distinct sprite names.
Projected onto the map: 802, not projected: 0.
Largest groups: icoMap_cattails=251, icoMap_rosehips=127, icoMap_limb=72,
                icoMap_sapling=52, icoMap_oldmansbeard=45, icoMap_reishi=39,
                icoMap_burdock=32, icoMap_crossroads=24.
```

关键在 `Census (F11)` 同一时刻那一行：**802 个带精灵名的标记里只有 39 个 surveyed。**

而最大的那几组（香蒲 251、玫瑰果 127、树枝 72、树苗 52、老人须 45、灵芝 39、牛蒡 32）
**全都是可采集资源** —— 也就是迷雾模式下**本来就该隐藏**的那些。

⇒ 「723 个对象会把地图铺成图标墙」这个担心，**在跟随游戏勘测进度的可见性规则下基本不成立**。
**但这不是现在就能下的结论**：§39.2 的图集还没拿到，`m_IsSurveyed` 与
`m_IsUnlocked` 该按什么组合判定也还没测（§25.6 待观察）。
⇒ 真要下这个结论，需要一个**只统计、不绘制**的可见性过滤版本的普查
（按 surveyed/unlocked 分组给出条数）。

### 39.4 还剩一条没验到

用户只跑到切图源就退出了，所以 **`Framing probe` 一行都没有**（那需要开着地图面板）。
§38.3 的最终 uv 一致性**仍然未知**，非 MountainTown 区域的基准**也仍然未知**。
`[layers]` 行确认了 mini 和 full 在 `自动` 下确实各持一份 **4360x4198 的独立贴图**，
但**原版/社区混合**那个真正会暴露跨层覆盖的配置**还没试过**。

---

## 40. 第二次实测（2026-09-29 22:04 会话）

日志 `MelonLoader\Logs\26-9-29_22-4-43.log`。用户反馈：**小地图切场景时会闪一下全图**。

### 40.1 ✅ 图源分离第一次被真正验证（§37.1 的核心断言成立）

用户在会话中把小地图图源改成了**原版**。22:05:42 那行是决定性的：

```
mini: src=vanilla using=vanilla tex=1024x1024
      bounds=(-325,-325,650x650) proj=MountainTownRegion baseReq='MountainTownRegion' elems='MountainTownRegion'
full: src=automatic using=community tex=4360x4198
      bounds=(-1024,-1024,2048x2048) proj=- baseReq='' elems=''
```

两个图层**同时持有不同的贴图、不同的基准、不同的投影状态**，互不覆盖。
⇒ §38.5 里担心的那个跨层覆盖（大地图那次 `ApplyMapSourceSelection` 清空共享的
`VanillaProjectionScene`）**在实测里没有发生**。
⇒ 小地图走原版 base map（`1024x1024`，全区可用、不需要校准）、
大地图走民间高清（`4360x4198`）—— **这正是 §37.1 描述的最终形态，且已跑通。**

**尚未验证**：反过来（小地图=社区、大地图=原版）那一半。

### 40.2 ⚠️ framing 探针在「接管地图键」开着时永远不会触发（**结构性**）

两次会话 `vanillaPanelOpen` 全为 `False`，`Framing probe` 一行都没有。原因不是探针坏了：

**探针读的是游戏自己的 `Panel_Map` 里的 `_RegionMap` 控件，而「接管游戏地图键」
（`RedirectGameMap`，默认开）会拦掉那个动作 —— 游戏面板从头到尾就没打开过。**

⇒ 要拿到 §38.3 的数据，**必须先把「接管游戏地图键」关掉**，然后正常按游戏地图键。
这和探针的 dev 开关是两个独立的开关，之前没说清楚，记在这里。

### 40.3 小地图切场景闪全图（**已修，待复验**）

`UpdateUnityUi` 在 `!hasPosition && !fullMap` 时把 `uvRect` 设成整张图再返回 ——
那是 §37.4 为「未校准区域的全屏地图也能缩放」写的，但副作用是**小地图在拿到投影之前
会先画一遍全图**，然后才跳到玩家为中心。切场景、校准重载这类瞬间都会闪一下。

⇒ 修法：那个窗口里把 `_mapImage.enabled` 关掉，一有投影就恢复。
全屏地图**不动**（它的缩放平移是纯 uv 运算，本来就不需要校准）。

### 40.4 仍需注意

- 社区图源的加载日志现在会带上图层名，因为连续两行 `Loaded 山间小镇` 分不清是
  **预热**（§39.1）还是**重复加载**（§39.1 缺陷一）。修完缺陷一之后那两行
  （22:05:07.261 与 22:05:09.071，相隔 1.8 秒）**是预热，不是重复**。
- §39.2 的图集问题**依旧存在**：本次 `Marker census` 仍然是 `Resolvable: 0`、
  `atlas present: False`。默认配置下标记重写依然被卡住。

### 40.5 图集获取已与图层解耦（**已修，待复验**）

新增 `PopulateIconTableOnce(sceneName)`，在 `OnUpdate` 里**无条件**调用
（只要求有定义、玩家就绪、游戏面板没开着），不再受 `!preferCommunity` 限制。

它做的就是原版路径那次一次性面板调用**减去一切与贴图有关的东西** ——
它要的是**图集**，不是 base map，所以**不再等贴图**：

```csharp
panel.ForceUpdateRegion();
panel.LoadMapElementsForScene(sceneName);
_elementsLoadedForScene = sceneName;          // 全局一次性闸门，防追加语义重复
CaptureVanillaIcons(mapElements, true, true); // 填 _iconBySpriteName 和 _mapIconAtlas
```

原版路径不受影响：它先跑到，全局闸门已置位，这里直接 no-op。

**复验时要看的新日志行**：

```
Icon table [MountainTownRegion]: N sprite names, atlas present: True
```

以及随后的 `Marker census` 里 `Resolvable` 应该不再是 0。
**这两行没出现或 atlas 仍为 False，标记重写就不能开工。**

> **来源：AI 推断** —— 「`ForceUpdateRegion` + `LoadMapElementsForScene` 在社区图源下
> 也能安全地把图集填上」是推断。依据是它俩本来就是与图源无关的游戏 API，
> 且 §25.3 实测过面板关闭时精灵的 `atlas` 有值。**但这一步本身没有实测过。**

---

## 41. 第三次实测（2026-09-29 22:08 会话）：前置验证全部通过

日志 `MelonLoader\Logs\26-9-29_22-8-28.log`。**关掉「接管游戏地图键」之后，
游戏面板终于打开，探针跑起来了。**

### 41.1 ✅ 原版两条路径的最终 uv 完全一致（§38.3 结案）

```
Framing probe 1 [MountainTownRegion]: regionMap=MountainTownSandbox_RegionMap
  widget=650x650 texture=2048x2048
  drawing=(-325.0,-325.0)-(325.0,325.0)  uvRect=(0.0000,0.0000,1.0000,1.0000)
  panelBounds=(-325.0,-325.0,650.0x650.0) baseBounds=(-325.0,-325.0,650.0x650.0)
  panelOverBase=1.0000x1.0000  offset=(0.0,0.0)  identical=True

Framing probe player: world=(1104.9,269.7,1783.5) mapPos=(17.3,158.0)
  panelUv=(0.52660,0.74301) baseUv=(0.52660,0.74301)
  delta=(0.00000,0.00000)  px@2048=(0.0,0.0)  agree=True

Framing probe offset: +600 world units -> delta=(0.00000,0.00000)
  px@2048=(0.0,0.0)  agree=True
```

**两条链路算出的最终 uv 逐位相同**，在玩家处和偏离 600 世界单位处都是 **0.0 像素差**。
⇒ §37.1「两层可以共用同一个原版基准」这个承重假设**有实测支撑了**。
⇒ 偏移 600 单位那一测是关键：它排除了「纯比例误差在中心互相抵消」这种假阳性。

**注意这次只有 `probe 1`**（成功即止，符合设计）。
**仍然只覆盖 MountainTownRegion 一个区域** —— `window/dlc` 类区域的基准未测。

### 41.2 ✅ 图集到手，802/802 全部可解析（§39.2 结案）

```
[22:09:26.700] Icon table [MountainTownRegion]: 8 sprite names, atlas present: True.
[22:09:49.043] Marker census (F11): 816 entries; 14 without a sprite name;
               33 distinct sprite names. Resolvable: 802
               (205 from the scraped table, 597 via the atlas), unresolvable 0.
               Projected onto the map: 802, not projected: 0.
[22:09:49.044] MapDetail summary: 802 markers with a sprite name;
               205 found among the scraped sprites, 597 more resolvable through the atlas
               (100% total); atlas present: True; 14 carry no sprite name.
```

**与 §25.3 的原版图源数字完全吻合**（205 表内 + 597 图集 = 802，100%）。
⇒ `PopulateIconTableOnce()` 在社区图源下**确实能填上图集**，
⇒ §40.5 那条「AI 推断」现在**升级为实测通过**。
⇒ **标记重写的最后一道前置障碍清除。**

### 41.3 ⚠️ 逐层存投影导致过期值（**已修**）

```
[22:09:45.443] [layers] mini: src=automatic using=community tex=4360x4198 ready=True
                        bounds=(-325,-325,650x650) ...
               full: src=automatic using=community tex=4360x4198 ready=True
                        bounds=(-1024,-1024,2048x2048) ...
```

`mini` 这一层用着社区图，`bounds` 却还是**原版**的 `(-325,-325,650x650)`。

**成因**：base map 只写进**当时活动的**那一层。`UseVanillaBaseMap` 是按图层存的，
而一次 base map 加载只会发生在活动层上，另一层就留住了它上次见到的旧值。

**这是一次方向相反的跨层污染** —— 我原本担心的是「一层清空共享值影响另一层」，
实际发生的是「一层写入不了另一层的副本」。**逐层存投影这个设计从头到尾没有带来任何好处**：
两层永远算出同一个值，却被我按图层拆开，于是多了两种不同步的方式。

⇒ 修法：投影值**不再按图层的作用域管理**。
`ClearVanillaProjection()` 按**区域**清（在 `ObserveScene` 里，而不是切图源时），
`UseVanillaBaseMap` 一次写进所有图层。切回原版的那一层会重新请求自己的 base map，
请求标记本来就把这个顺序管住了。

> **来源：AI 推断** —— 「切图源时不必清投影」是推断，未测。
> 最坏后果是 `TryPlayerToMapUv` 在某一帧读到旧区域的值；按现有请求序列，
> `VanillaProjectionScene` 会挡住它（它按场景名比对）。

### 41.4 标记重写现在缺的只剩「可见性判据」

§39.3 已知：**802 个带精灵名的标记里只有 39 个 surveyed**，
而最大的几组（香蒲 251、玫瑰果 127、树枝 72…）**全是可采集资源**。

⇒ 开工前还差一次**只统计不绘制**的普查：按 `m_IsSurveyed` / `m_IsUnlocked` /
`AllHarvestablesCollected` 三个条件的组合给出条数。
**这个数字决定标记重写是「直接画」还是「必须先聚类/合并绘制」（§30 C3）。**
§25.6 早就把 `m_IsSurveyed × m_IsUnlocked` 的组合列为待观察，现在它成了主线的下一步。

---

## 42. 第四次实测（2026-09-29 22:11 会话）：可见性**不是**由标志位决定的

日志 `MelonLoader\Logs\26-9-29_22-11-46.log`。

### 42.1 ❌ 被证伪：用 `m_IsSurveyed` / `m_IsUnlocked` 判可见性

```
Marker visibility (F11): surveyed 37, unlocked 7, surveyed&&unlocked 7,
                         surveyed||unlocked 37, fully harvested 0
                         Would draw under (surveyed||unlocked) && !fullyHarvested: 37
                         Currently scraped from the panel's sprites: 162
```

CSV（`mapdetails_MountainTownRegion_221215.csv`，816 行）的三标志交叉表更直白：

| 条数 | surveyed | discovered | unlocked |
|---|---|---|---|
| **765** | False | False | False |
| 30 | True | False | False |
| 7 | True | False | True |

**765 / 802 个带精灵名的标记，三个标志全是 false** —— 而游戏**照样建了 162 个元素**。

⇒ 按 `surveyed||unlocked` 画只有 **37** 个，比游戏少 **125** 个。
**这就是「标记不全」的真正来源，而它恰好和用户最初的抱怨是同一件事**
（「原版制图的图源标记不全啊，怎么只有自然资源的标记」）。
⇒ **§37.2 设计第 4 条「可见性只由 `m_IsSurveyed` 决定」是错的，不能照做。**
⇒ §25.6 那条待观察现在有答案了：`m_IsSurveyed` **不是**可见性判据。

**`m_IsUnlocked` 是 `m_IsSurveyed` 的子集**（7 个全在 37 里），两个都解释不了 162。

### 42.2 那么游戏到底按什么画？（**未解决**）

已知的候选，**都没有证据**：

- 迷雾（fog-of-war）可能作用在**贴图**上，而标记是**另建**的 ——
  §35.1 观察到的 162→28 是**开过面板之后**精灵 `enabled` 被关掉的结果，
  那是**渲染期**的旗标，和 `MapDetail` 上的字段是两回事。
- 可能有一个我们还没读到的手工「已发现」集合。
- 「只在点亮过区域内建」也解释不通 162 这个数字（surveyed 只有 37）。

⇒ **唯一可信的「游戏画了什么」的陈述，是刮取到的集合本身（162 个）。**
这就是为什么刮取路径**不能**像 §37.2 计划的那样被完全删掉 ——
它可以不再决定**位置**，但它是目前唯一能告诉我们**哪些该画**的东西。

### 42.3 密度问题的答案，以及为什么它现在不是瓶颈

33 个分组里，前 8 组占了 802 中的 633：

| sprite | 条数 |
|---|---|
| `icoMap_cattails` | 251 |
| `icoMap_rosehips` | 127 |
| `icoMap_limb` | 72 |
| `icoMap_sapling` | 52 |
| `icoMap_oldmansbeard` | 45 |
| `icoMap_reishi` | 39 |
| `icoMap_burdock` | 32 |
| `icoMap_crossroads` | 24 |

**全是可采集资源。** 所以「全画出来会是一堵图标墙」是**真的** —— 但只有在
「全都画」的前提下才成立。**一旦确定了正确的可见性规则，密度大概率自动解决**，
不需要先做聚类。⇒ §37.2 风险第一条（必须先做 §30 C3 合并绘制）
**降级为「等可见性规则定了再看」**，不再是开工前提。

### 42.4 下一步：按组对照（已加诊断，待测）

`Marker census` 现在多打一行，把**游戏实际建的**和**数据里有的**按组并排：

```
Marker groups (scraped/total) [F11]: icoMap_cattails A/251, icoMap_rosehips B/127, ...
```

**判读方式**：

- 某组 `刮取数 = 0` ⇒ 游戏**完全不画**这一类 ⇒ 重写时直接跳过。
- 某组 `刮取数 = 总数` ⇒ 游戏**全画** ⇒ 直接照画。
- 某组 `0 < 刮取数 < 总数` ⇒ 这一组内还有第二个判据（很可能就是我们要找的规则）。

⇒ **这一行是决定过滤规则的关键**，而且**不需要改设置、不需要开面板**，按一次 F11 就有。

---

## 43. 第五次实测（2026-09-29 22:15 会话）：规则是**按 sprite 组**，且可复现

### 43.1 结果：全有或全无，没有中间值

```
Marker groups (scraped/total) [F11]:
  icoMap_sapling      52/52      icoMap_oldmansbeard 45/45
  icoMap_reishi       39/39      icoMap_crossroads   24/24
  icoMap_car          23/23      icoMap_corpse       21/21
  icoMap_greyMother    1/1
  ---
  icoMap_cattails      0/251     icoMap_rosehips      0/127
  icoMap_limb          0/72      icoMap_burdock       0/32
  icoMap_burntHusk     0/22      icoMap_rabbit        0/21
  icoMap_container     0/17      icoMap_deerCarcass   0/14
  ... 其余 19 组全部 0/N
```

**33 个组里没有一组是中间值。**

⇒ 如果游戏在**逐条**过滤，0/251 几乎不可能 —— 251 个里一个不剩。
⇒ **判据是组级的**，不是一个我们还没找到的逐条字段。
⇒ 这同时解释了 §42 的困惑：162 **不是** 802 的任意子集，
而是**组级选择的并集**。52+45+39+24+23+21+1+1 = **162**，完全对得上。

### 43.2 判据可复现，不是随进度变化

两次连续会话（22:14 与 22:15）的分组表**逐位相同**，`m_HarvestablesForMapVisibility`
两次都是 **136 条 / 304 个对象**。

⇒ 这是**确定性**的规则，不是「玩家进度到了才有」。
⇒ 用户提示：**当前点亮范围正好以 `icoMap_greyMother`（灰色母亲的家）为中心** ——
而且 `icoMap_greyMother` 恰好是**唯一那组 1/1 的单点建筑标记**。

### 43.3 最可能的解释：刮取到的是**上雾之后 `enabled` 为真**的那些

把 §35.1 的旧观察重新读一遍，它其实是**同一个数字**：

| 时机 | 抓到的标记数 |
|---|---|
| 面板**从未打开** | **162** ← 迷雾还没应用 |
| 面板**打开过一次之后** | **28** ← 只剩点亮区域（§35.1 原文） |

⇒ **162 就是「未上雾」的组级集合**，而**28 是「上雾之后」的数目**。
⇒ 用户说的「点亮范围以灰色母亲的家为中心」，对应的是**上雾后**剩下的那一小块。

**组级判据和迷雾是两件事**：组级决定哪些标记**会被建出来**，
迷雾在**渲染期**用 `sprite.enabled` 决定其中哪些**真的画**（§35.3 早就指出了这个分层）。

### 43.4 那组级差异是什么？（**未验证，两条候选都还站得住**）

两组之间最明显的差别是**可采集物的类型**：

- **画**：树苗、玫瑰果、灵芝、老人须、鹿尸、车、断十字路口、灰色母亲的家
- **不画**：香蒲、树枝、牛蒡、烧焦外壳、兔子、容器、鹿尸…

**候选一（AI 推断）**：不画的那些是**耗尽型**资源，只有在玩家采集过、
游戏建立链接之后才会出现 —— 而这类标记的 `m_HarvestablesForMapVisibility` 为 0，
正好和「136 条有链接」对得上。**这条自洽，但未验证。**

**候选二**：`s_MapDetails` 里包含**全部** 802 个注册项，
但面板只为**组级白名单**建对象。**也没验证。**

⇒ **不要去猜是哪一个。** 见 §43.5 —— 有一个不需要验证就能用的做法。

### 43.5 结论：刮取集合可以直接当**组级白名单**用

不管 §43.4 是哪个候选，**「游戏建了哪些组」这个事实本身就在 `_iconBySpriteName` 里** ——
`Icon table: 8 sprite names` 那 8 个名字，就是**全部该画的组**。

⇒ **标记重写不再被阻塞。** 过滤规则 = 「`m_SpriteName` ∈ 刮取到的 sprite 名集合」。

**这比原计划更好的三点**：

1. **位置精度**：`GetWorldPosition()` 是**校准用的同一坐标系**（§37.2），
   而现在的 148/162 个标记位置是从**精灵角点反算**的。
   ⇒ 即使数量不变，**位置更准**。
2. **实时刷新**：§37.2 要的「可见性在绘制期决定」，数据源天然支持。
3. **自维护**：玩家采集后精灵消失，白名单跟着变 —— **不需要我们实现采集追踪**。
   这正好把 §37.3 那条「保留较大集合」启发式的替代品一并解决了：
   **它是抓取期的事实，不是猜测。**

> **需要保留的**：刮取路径**不能删**。它不再是位置的来源，
> 但它是**唯一**告诉我们哪些组该画的东西（§42.2 的结论依然成立）。

**留给以后**：真正的迷雾（让已采集/未点亮逐个消失）需要 §43.4 的答案，
那是 §30.1 的范围，不在本次两项任务里。

**留给用户定的**：要不要**多画**那些组（比如全部 802 个）。
本文档不替用户决定 —— 那是产品取舍，不是实现细节。

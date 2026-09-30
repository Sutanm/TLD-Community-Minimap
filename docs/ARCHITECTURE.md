# 架构

> 更新：2026-09-30 · 对应源码：9 个 `ModEntry.*.cs` 分部类 + 5 个独立文件

## 1. 文件划分

`ModEntry` 是**一个类型**，拆在多个文件里（`partial class`）。
拆分是**纯搬运**，成员没动过（有验证脚本，见 `tools/verify-split-equivalence.py`）。

| 文件 | 行数 | 装什么 |
|---|---|---|
| `ModEntry.Core.cs` | 930 | **入口 + 全部共享状态 + 全部嵌套类型** |
| `ModEntry.Diagnostics.cs` | 1312 | 普查、探针、导出、采集清理、校准记录 |
| `ModEntry.Markers.cs` | 687 | 标记：构建、分类、颜色、绘制 |
| `ModEntry.Labels.cs` | 600 | 地名、本地化、悬停提示 |
| `ModEntry.VanillaMap.cs` | 594 | 原版底图请求、图标表、面板捕获 |
| `ModEntry.Rendering.cs` | 519 | Canvas/RawImage、指针、投影换算、通用工具 |
| `ModEntry.Input.cs` | 375 | 地图键接管、全屏地图输入、按键提示 |
| `ModEntry.Layers.cs` | 348 | 图层与图源、视图循环 |
| `ModEntry.CapturedMap.cs` | 287 | 抓取底图的保存与加载 |
| `MinimapSettings.cs` | 300 | 设置定义（ModSettings） |
| `CalibrationStore.cs` | 214 | 校准数据读写与仿射变换 |
| `MapDefinition.cs` | 136 | 22 个区域的静态目录 |
| `InputPatches.cs` | 86 | Harmony patch |
| `SceneCatalogExporter.cs` | 64 | 场景目录导出 |

### ⚠️ Core 为什么这么大

**`ModEntry.Core.cs` 装着全部字段和全部嵌套类型**，这是**刻意的**，不是偷懒：

字段审计显示 `_mapRect` 被 **11 个**功能区访问，`_hintFont` / `_settings` / `_uiRoot`
被 **10 个**访问。**这些字段就是共享状态** —— 任何画东西的代码都要用。

把它们集中在一处、并写明这一点，比分散到各文件更诚实。
**不要为了"看起来更整洁"把它们拆开** —— 那只会让耦合变得隐蔽。

### 嵌套类型的位置

| 类型 | 在哪 | 是什么 |
|---|---|---|
| `MapLayer` | Core | 单个图层的全部状态（见 §2） |
| `VanillaIcon` | Core | 一个标记的运行时数据（含 `Backing` 描边对象） |
| `MapLabel` | Core | 一个地名的运行时数据 |
| `IconRef` | Core | 图集贴图 + uv 矩形 |
| `MarkerCategory` | Core | 5 类 + `Unclassified` |
| `AssetProbe` / `PrefabProbe` | Core | 探针状态 |

---

## 2. 图层系统

两个图层，各自**完全独立**的状态：

```csharp
private readonly MapLayer[] _layers = { new MapLayer(), new MapLayer() };
private const int LayerMini = 0;
private const int LayerFull = 1;
private MapLayer ActiveLayer => _layers[ActiveLayerId];
```

`MapLayer` 持有：`Source`（实际用的）、`RequestedSource`（设置要的）、`Definition`、
`Texture`、`UsingVanilla`、`TextureReady`、`BaseMap*`（请求状态）、
`ElementsLoadedForScene`、`Vanilla*`（投影基准）。

### 图源由设置决定，每层一份

```
MiniMapSource  →  _layers[LayerMini].RequestedSource
FullMapSource  →  _layers[LayerFull].RequestedSource
```

取值：`0` 自动 / `1` 民间高清 / `2` 原版制图。

> **旧的全局键 `MapSource` 已作废。** `ModSettings` 未暴露存档路径，**无法写迁移**，
> 所以旧键只是被忽略。对当前用户无影响。

### 两条硬性纪律

1. **`ClearVanillaProjection()` 按区域清**，`UseVanillaBaseMap` 一次写**所有**图层。
   历史上出现过"`mini` 层还留着旧 bounds，而它在用社区图"的错位。
2. **`_elementsLoadedForScene` 是全局唯一闸门。**
   `MapLayer.ElementsLoadedForScene` 只是状态记录，**不授权第二次调用**
   `LoadMapElementsForScene`（它会 **APPEND** 到面板，调两次就是双份标记）。

---

## 3. 标记管线

```
MapDetailManager.s_MapDetails          (816 条)
        ↓  过滤：跳过无 sprite 名的（那是地名，走 Labels 管线）
RebuildMarkersFromMapDetails           (802 个)
        ↓  位置：MapDetail.GetWorldPosition()
        ↓     → TryWorldToMarkerUv（走当前图层的投影）
        ↓  图标：TryResolveIcon（先查表，再查图集）
        ↓  分类：CategorizeSprite（按 **sprite 名**，不是 m_IconType）
        ↓  颜色：MarkerColourForActiveLayer
        ↓  描边：BuildMarkerBacking（深色垫底，可选）
BuildMarkerIcon → _pendingVanillaIcons
        ↓  ClearVanillaIcons(); _vanillaIcons.AddRange(_pending)
_vanillaIcons                          (绘制列表)
        ↓  UpdateVanillaIcons（每帧）
        可见性 = 图层允许 && 类别开关 && 在可视 uv 范围内
```

### 关键点

- **`m_IconType` 只表示绘制尺寸**，分类完全按 **sprite 名**。不要把两者混用。
- **可见性不在构建期决定**。全部 802 个都建对象，画不画在**每帧**由 5 个分类开关决定。
  这是迷雾模式要用的地基。
- **构建 key** 决定何时重建：

  ```
  "场景名|标记来源|投影|条目数"
  ```

  投影变化（`LastProjection`）**立即重建**，不受限流 —— 它让每个位置都失效。
  同投影下条目数变化才限流（区域流式加载时条目会 769→816 抖动）。

  > 这里出过一个 bug：限流只比对**条目数**，导致切图源时标记带着原版的 uv 画在社区图上。
  > **改这个 key 的格式时，组装处和解析处必须一起改。**

---

## 4. 坐标系

三种坐标，别混：

| 坐标 | 含义 |
|---|---|
| **世界坐标** | 游戏里的 `Vector3`，玩家位置、`MapDetail.GetWorldPosition()` |
| **地图坐标** | 面板的局部坐标 `(x, y)`，范围由 `localBounds` 界定 |
| **uv** | 贴图的 `0..1` 归一化坐标 |

### 原版路径（精确）

```
世界坐标
  → panel.WorldPositionToMapPosition(scene, world)      // 面板自己的换算
  → VanillaMapPositionToUv(mapPos, VanillaMapLocalBounds, VanillaTextureUv)
```

`VanillaMapLocalBounds` 和 `VanillaTextureUv` 是**探针实测**的，只在
`MountainTownRegion` 验证过（两条链路最终 uv 差 **0.0 像素**）。

> **不要试图"拟合"原版地图。** 社区图是**按原版图对齐的**，反过来不成立。
> 原版必须保持绝对精确。

### 社区路径（有天花板）

用 `calibrations.json` 里的三点以上仿射校准。

> **实测：社区图不是 1:1 画的** —— 两轴比例相差 16%（X 2.04 px/单位，Z 1.76 px/单位）。
> 所以这条路径**精度有上限**，靠加校准点无法根治。

### 玩家指针

`TryPlayerToMapUv` 是所有"世界 → uv"的统一入口，标记和指针共用，
所以**两者永远一致**（不会出现标记准而指针偏）。

---

## 5. 渲染

- **持久 `Canvas` + `RawImage`**。**绝不引入 `OnGUI`**（历史上试过，已废弃）。
- 一个 `_mapImage` 承载**当前图层**的贴图，每帧绑定 —— 加载图层**不能抢走**另一个的对象。
- **`RetireTexture` / `SweepRetiredTextures`**：旧贴图不能立刻销毁，
  因为共享的 `_mapImage` 可能还引用它（曾导致全屏白屏 2 秒）。
- **每帧写 UI 值要加"变了才写"保护**。`anchoredPosition` / `sizeDelta` / `text` / `fontSize`
  赋值都会标记 layout 或 text mesh 为脏，即使值相同。802 个标记每帧写两次 = 拖拽卡顿。
  详见 [PERFORMANCE.md](PERFORMANCE.md)。

---

## 6. 抓取的底图

```
Mods\CommunityMinimap\captured\<场景名>.png          2048×2048
Mods\CommunityMinimap\captured\<场景名>.png.framing  "boundsX,boundsY,boundsW,boundsH,uvX,uvY,uvW,uvH"
```

`TryLoadCapturedMap` 在区域加载时**优先**使用它（没有才退回 1024 贴图）。
副档记录**抓取时**的投影基准，所以读回来不需要重新探测。

**每次抓取都覆盖**同名文件 —— 早期版本"文件存在就跳过"，留下了一张点亮前的迷雾图，
误导了一整轮排查。**测量/导出工具不要做这种跳过。**

---

## 7. 构建与部署

```powershell
dotnet build .\CommunityMinimap.csproj -c Release -p:GameDirectory='<游戏目录>'
.\build.ps1 -GameDirectory '<游戏目录>'
```

- 构建**必须 0 警告 0 错误**
- **游戏进程名是 `tld`**（不是 `TheLongDark`）
- `build.ps1` **在检查进程之前**不理会 `-SkipInstall`，所以游戏运行时无法用 `-SkipInstall` 绕过
- 部署产物：`<游戏目录>\Mods\CommunityMinimap.dll`，旧版备份到 `Mods\CommunityMinimap\backups\`
- **设置文件是 `Mods\CommunityMinimap.json`**（不是 `Loader.cfg`），字段名就是 JSON 键

---

## 8. 区域目录

`MapDefinition.cs` 是 **22 个区域**的静态表（mapId、中文名、图片文件名）。
`CalibrationStore.cs` 读 `calibrations.json`（含 `maps[]` 与 `sceneAliases[]`）。

两者是**独立的**：`MapDefinition` 说"有这些区域"，`calibrations.json` 说"这几个校准过"。
**未校准的区域照样能用**（全屏地图的缩放/平移是纯 uv 数学，不依赖校准）。

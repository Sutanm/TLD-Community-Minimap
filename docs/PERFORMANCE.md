# 性能记录

> 更新：2026-09-30 · 依据：游戏日志、内存测量、代码位置

## 结论先说

**已经修掉的都是"每帧白做的功"** —— 每帧抛异常、每帧写 UI 值、每个标记一次反射查找、
每个字形画五遍。

**没有做的是"减少对象数"** —— 802 个标记仍是 802 个独立对象，不参与批处理。
这是**有意的取舍**，理由见 §3。

---

## 1. 已修复的问题

### 1.1 每帧 377 次异常 ⭐ 最严重

| | |
|---|---|
| **症状** | 打开全屏地图后指针跟随变慢，拖动像慢一拍。**看起来像渲染慢** |
| **真因** | 悬停提示创建失败（组件取不到），**每次调用都重新抛一遍异常** |
| **证据** | 日志里 **377 次 `NullReferenceException`**，栈指向 `ShowHoverLabel` → `UpdateMapLabels` → `UpdateUnityUi` |
| **修法** | 修正创建过程（组件列表不能随意加，见 [PITFALLS.md](PITFALLS.md) B2）；失败时**打一次完整异常并禁用该功能**，不再每帧重试 |

> **这是最值得记的一条**：异常在 Unity 里代价极高，**它本身就是性能问题**，
> 而不只是"一条错误信息"。
>
> 方向是被用户纠正的 —— 我按"渲染/对象数"查了很久，
> 用户一句 **"不悬停就不该有性能问题"** 才把我推回正确方向。

**修后实测**：最近 10 次会话的异常计数为 **0**（仅两次各 1 条，是提示创建失败的那一次记录）。

### 1.2 每帧无条件写 UI 值

| | |
|---|---|
| **症状** | 拖拽地图时"慢一拍" |
| **真因** | `anchoredPosition` / `sizeDelta` / `text` / `fontSize` **即使赋相同的值也会标脏** layout 或 text mesh |
| **规模** | 802 个标记 × 每帧 2 次写，再加地名与提示 |
| **修法** | 一律改成"变了才写"：`if (rect.anchoredPosition != p) rect.anchoredPosition = p;` |

### 1.3 每个标记一次反射本地化查找

| | |
|---|---|
| **症状** | 加了悬停名字后帧率崩塌 |
| **真因** | 给**每个标记**调一次本地化查找，而那次查找走**反射 `Invoke`**（IL2CPP 下很贵） |
| **规模** | **802 次反射调用**，且在区域流式加载期间**每 2 秒重建一次** |
| **修法** | 按 key **记忆化**。标记共享少量 key（香蒲 251 个共用一个），所以实际只查几次 |

> **为什么地名没暴露这个问题**：地名只有 **14 个**，标记有 **802 个**。

### 1.4 `Outline` 导致顶点数翻五倍

| | |
|---|---|
| **症状** | 加了"文字描边"后明显变卡 |
| **真因** | `Outline` 为**每个字形**再画 **4 份**副本 —— 给几百个 CJK 标签加它是灾难 |
| **修法** | 改用 `Shadow`（**1 份**额外副本），代价降到五分之一，可读性接近 |

### 1.5 白屏 2 秒（纹理销毁竞态）

| | |
|---|---|
| **症状** | 打开全屏地图时出现约 2 秒的白屏 |
| **真因** | 两个叠加：① 图源切换时**无条件**把加载时间提前，导致同一张贴图被解码两次；② 旧贴图被立刻 `Destroy`，而共享的 `_mapImage` **还引用着它** |
| **修法** | ① `RequestMapLoad` **只把时间往早推**；② `RetireTexture` / `SweepRetiredTextures` —— 替换下来的贴图**下一帧**再释放 |

---

## 2. 标记规模的真实数字

来自 `mapdetails_MountainTownRegion_*.csv`（山间小镇实测）：

| 指标 | 数值 | 依据 |
|---|---|---|
| `s_MapDetails` 条目 | **816** | census CSV 816 行 |
| 其中有 sprite 名（= 标记） | **802** | 日志 `802 drawn`；816 − 14 = 802 |
| 无 sprite 名（= **地名**，走 Labels 管线） | **14** | 日志 `Map labels: 14 placed from 816 entries` |
| **唯一 sprite 名** | **33** | 日志 `33 distinct sprite names` |
| 唯一 `sprite × m_IconType` 组合（= 唯一贴图变体） | **34** | 对 census CSV 按 `sprite\|type` 去重 |
| `surveyed=true`（全图点亮**后**） | **599** | census CSV 与日志一致 |
| `surveyed=true`（点亮**前**） | **37** | 日志 `surveyed 37, unlocked 7` |
| 旧刮取路径的上限 | **162** | 日志 `Currently scraped from the panel's sprites: 162` |

> ⚠️ **两个容易搞错的地方（我都错了一次，实测纠正）**：
>
> 1. **`m_IconType == Area` 的 3 条不是地名。** 它们**带 sprite 名**
>    （`icoMap_Generic`，locid 是 `GAMEPLAY_VisorNoteMapIcon` 系列），
>    按当前代码**会被建成标记**。真正走地名管线的**只有那 14 条无 sprite 名的 `Text`**。
>    **不要以为"Area 就是地名"。**
> 2. **点亮后的 `surveyed` 是 599，不是 601。**

**关键**: 香蒲 251 个、玫瑰果 127 个、树枝 72 个 —— 但**图只有一张**。
前 8 组占 **633 / 802**，所以全画确实是一面图标墙。

### 新旧路径的覆盖度差距

| 路径 | 能看到多少 | 为什么 |
|---|---|---|
| 旧：刮取 UI | **162** | 游戏**只实例化** 162 个，刮取永远超不过它 |
| 新：读 `s_MapDetails` | **802** | 完整登记表 |

> **162 是结构性上限**，不是刮取写得不好。
> 实测日志：`Markers rebuilt from MapDetail: 802 drawn, 14 skipped (labels, off-map), 0 unresolved.`

---

## 3. 仍然存在的性能结构

### 3.1 标记不参与批处理

802 个标记 = **802 个独立 `RawImage` GameObject**，各有 `RectTransform` + `CanvasRenderer`，
**uGUI 无法把它们合并成一个绘制调用**。

**开「标记描边」后翻倍到约 1604 个**（每个标记多一个深色垫底对象）。

### 3.2 为什么没做合并绘制

合并绘制（把 802 个 quad 合成一个 mesh）**技术上可行，但没有做**，理由：

1. **对象数从未被证明是瓶颈。** 我一度用"对象数"解释卡顿，
   但那个判断的依据后来被判定**不可靠**，已撤回。
   **真正被证明的是"每帧白做的功"** —— 而那个已经修完了。
2. 修完之后**用户实测不卡**（"拖拽顺利，不卡"）。
3. 合并不是小改动：它要求自己管理顶点缓冲和 UV，会**替换掉现在这条已经稳定的渲染路径**。
   在"已经能跑"和"可能更快但有风险"之间，**现在的取舍是保留能跑的**。

> ⚠️ **触发条件**：如果将来标记数大幅增长（更多区域、更多类别）或「标记描边」
> 在低端机器上造成卡顿，**那才是做合并绘制的时机** —— 那时有明确理由承担它带来的风险。

### 3.3 已修的是"浪费"，不是"数量"

| | 状态 |
|---|---|
| 每帧抛异常 | ✅ 已修 |
| 每帧写 UI 值 | ✅ 已修 |
| 每标记一次反射 | ✅ 已修（记忆化） |
| 字形画五遍 | ✅ 已修 |
| **802 个对象 / 绘制调用** | ❌ **未动，且暂无必要** |

---

## 4. 测量方法上的教训

| 教训 | 具体经过 |
|---|---|
| **异常先去日志里数** | 377 次 `NullReferenceException` 被当成"渲染慢"查了很久 |
| **先量再改，用数字对账** | 分割器调试六轮，每次都是"看着对"，直到用数字对账才发现 bug |
| **未被证实的理论不能当依据** | "对象数导致卡顿"这个理论从未被证实，却被我用作方向 |
| **测量工具不能"文件存在就跳过"** | 抓到的是点亮地图**之前**的迷雾图，据此推错一整轮 |
| **一次只改一样** | 同时加描边+底片+字号，定位花了五轮 |

---

## 5. 怎么测量

### 日志位置

```
<游戏目录>\MelonLoader\Logs\*.log        ← 按时间排序取最新的
```

> **没有 `Latest.log`** —— 实测确认不存在，不要去找它。
> 一次会话一个文件，文件名是 `<年>-<月>-<日>_<时>-<分>-<秒>.log`。

### 关键日志行（对账用）

| 行 | 含义 |
|---|---|
| `Markers rebuilt from MapDetail: N drawn, M skipped (labels, off-map), K unresolved.` | 标记对账，正常是 **802 / 14 / 0** |
| `Map labels: N placed from M entries (localization: found\|missing).` | 地名对账，正常是 **14 / 816** |
| `Marker hover names: N of M.` | 悬停名字覆盖面，正常 **802 / 802** |
| `Marker census (F11): … 33 distinct sprite names.` | 规模与可解析率 |
| `Marker visibility (F11): surveyed … unlocked …` | 可见性组合与"游戏实际建了几个" |
| `Marker groups (scraped/total) [F11]: …` | 组级对照。判读：`0/N` = 游戏完全不画；`N/N` = 全画；**中间值 = 还有第二个判据** |
| `Census (panel opened/closed): 816 entries, 802 with a sprite name, N surveyed.` | 面板开 / 关各一条 |
| `[state] scene=… markers=… view=…` + `[layers] mini: … \| full: …` | 10 秒一次心跳，证明两个图层互不覆盖 |
| `Framing probe …` | 原版面板投影基准（需**关掉**「接管游戏地图键」才触发） |

### 常用的日志计数

```powershell
# 异常数 —— 判断卡顿的第一步
Select-String -Path <日志> -Pattern 'Exception' | Measure-Object | Select-Object Count

# 标记重建与对账
Select-String -Path <日志> -Pattern 'Markers rebuilt from MapDetail|Marker hover names|Map labels'

# 图源与投影
Select-String -Path <日志> -Pattern 'Using the captured map'

# 不该出现的输出（旧启发式仍在生效的标志）
Select-String -Path <日志> -Pattern 'Kept .* markers'
```

### 游戏内探针

开启设置里的 **「开发者模式」**（默认关）后启用。所有探针都**只在开发者模式下运行** ——
注释里写明理由：普查会写出 **70 KB** 的 CSV，「绝不能为一个没要求的玩家跑」。

| 探针 | 触发 / 产物 |
|---|---|
| `CensusMapDetails` | 每 2 秒限流；写 `mapdetails_<场景>_<HHmmss>.csv`（**70 KB**） |
| `ClassifyMapDetails` | **只统计不建对象**，打印上面那四行 census / visibility / groups / categories。测量成本为零 |
| `ProbeRegionTextures` / 资源探针 | 枚举 `RegionSpecification` 的贴图成员 —— **"1024 是上限"这个否定性结论就是这么来的** |
| `LogStateHeartbeat` | 每 10 秒一条 `[state]` + `[layers]`。注释：「Deliberately cheap: it never walks the 816 map entries」 |
| `DumpMapDetails` | 随 `记录校准点`（默认 `F11`）一起触发 |
| `ShowDiagnostics` | 小地图下方的坐标文字 |

导出到 `<游戏目录>\Mods\CommunityMinimap\`：

| 文件 | 内容 |
|---|---|
| `mapdetails_<场景>_<时间>.csv` | `s_MapDetails` 全量导出（**带时间戳，会堆积 —— 定期清理**） |
| `vanilla_map_hierarchy.txt` | 面板对象层次（每次覆盖） |
| `panelmap_<场景>.png` | 面板贴图导出（每次覆盖） |
| `regionmap_<场景>.png` | 区域自带贴图导出（每次覆盖） |
| `calibration_points*.csv` | 校准点记录 |

> **只有 `mapdetails_*` 会堆积**（带时间戳）。曾经积累到 **89 个文件 / 6.9 MB**，已清理。
> 其余都是固定名、每次覆盖。
>
> 分析用 `tools/analyse-census.py <csv>`：按 `m_IconType` 分组、三标志计数、
> 带 sprite 名的条目与唯一名数量、`m_HarvestablesForMapVisibility` 覆盖。

### 构建与安装

```powershell
# 只构建（安全）
dotnet build .\CommunityMinimap.csproj -c Release -p:GameDirectory='<游戏目录>'

# 构建 + 安装（覆盖游戏里的 DLL）
.\build.ps1 -GameDirectory '<游戏目录>'
```

- 要求 **0 警告 0 错误**
- **游戏进程名是 `tld`** —— **不是 `TheLongDark`**。`build.ps1` 靠它判断能否安装；
  早期按 `TheLongDark` 查会**静默地永不匹配**，于是安装跑进被锁定的文件，
  表现为看不懂的 `Copy-Item: IOException`
- 进程检查在 `-SkipInstall` 判断**之前**，所以游戏运行时**无法**绕过
- 安装前自动备份到 `Mods\CommunityMinimap\backups\CommunityMinimap_<yyyyMMdd_HHmmss>.dll`，
  安装后**用 SHA256 比对**"装上去的就是刚构建的"；产物缺失或时间戳早于任一源文件都会中止
- 改 `calibrations.json` **不需要重建**（运行中热重载，每秒查一次 `LastWriteTimeUtc`）

### 仍然存在的 O(802) 遍历

`UpdateVanillaIcons` 每帧遍历全部标记做可见性判断，
`RefreshMarkerCategoryFlags` 每帧比对一次分类开关状态（只在变化时才写字段）。

**这是 O(n) 遍历，但不写 UI** —— 所以不会触发 layout 重建。
「来源：AI 推断」：这一点没有单独测量过，是从"写 UI 才标脏"推出来的。

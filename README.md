# STDF Viewer

A small Windows desktop tool for looking inside **STDF V4** (Standard Test Data Format) files: scan the record stream, browse the records, and decode the fields of any record.

**English** | [中文](#中文)

---

## English

### Features

- **Summary** tab — a one-page overview of the loaded file: size / records / types / DUT count / yield / scan time / byte order, then the decoded `File`, `MIR`, `MRR`, `SDR`, `WIR`/`WRR`/`WCR`, `PCR` sections, `HBR` / `SBR` tables and a record-count breakdown. Record names and field names are always shown in English.
- **Wafer** tab — a die map built from `PRR` X/Y and hard bin, one map per `WIR`/`WRR` wafer (pick the wafer at the top). The die grid is scaled independently in X and Y to fill a round wafer, so rectangular die arrays still render as a circle. Die cells are coloured by hard bin with a count/share legend, X to the right and Y up; hover shows X/Y/bin/site. The notch direction comes from the `WCR` `WF_FLAT` orientation (`U`/`D`/`L`/`R`); without a usable `WCR` it defaults to down and can be overridden with the notch selector. Duplicate coordinates (retests) keep the last `PRR` and are counted; yield uses the `WRR` good/part counts.
- **Scan** the whole STDF file into a record index (offset / type / length). The scan runs in the background, so the window stays responsive on large files.
- **Record types** panel — per type/sub pair: count, total bytes and first record number. Click a row to filter the record list.
- **Records** panel — records in file order with `#`, type name, `typ,sub`, offset and `REC_LEN`. Type a record number into the box and press Enter to jump to it.
- **Fields** panel — the selected record decoded field by field, showing the value, the raw bytes the field consumed, and a status (`header`, `truncated`, `omitted`, `layout gap`, ...).
- Detects little/big endian from the record headers, and reports `CPU_TYPE` / `STDF_VER` from the FAR record.
- Status bar summarises file size, record count, type count and scan time. Very large files are capped at 20,000 listed rows — pick a type to narrow the list.
- **History** tab — every successfully parsed file is remembered (path, size, record count and per-type statistics). Click an entry to look at that parse result again; deleting an entry removes only the history row, never the `.std` on disk.
- **Compare** tab — pick two history entries and diff them: a per-type count table, where rows that exist on only one side (`only left` / `only right`) are highlighted, plus a record-structure check that collapses the record stream into RLE segments and reports where the two files diverge.
- **English / 中文** — the button on the right of the toolbar switches the UI language immediately, no restart. The choice is remembered in `settings.json` next to the executable; the first run follows the system language.

### Requirements

- Windows 10 / 11
- Build: .NET 9 SDK. The published `StdfViewer.exe` needs nothing installed.

### Build and run

```powershell
dotnet build .\StdfViewer.csproj
.\bin\Debug\net9.0-windows\StdfViewer.exe <your-file>.std
```

Or, without building first:

```powershell
dotnet run -- <your-file>.std
```

Launching with no argument starts with an empty window — use **Open STDF...** to pick a file.

### Publish

There is exactly one release shape: a **single self-contained exe**. It needs nothing installed — no .NET runtime, no VC++ redistributable. `Publish` is pinned to the `win-x64-single-file` profile:

```powershell
dotnet publish .\StdfViewer.csproj
# -> bin\publish\StdfViewer.exe   (~63 MB, one file)
```

On first start the app unpacks its native libraries to `%TEMP%\.net` (one-time, ~0.6 s; later starts are unaffected). `history.json` / `settings.json` are created next to the exe; if that folder is read-only the app still works, the preferences just are not persisted.

The output of a plain `dotnet build` is a development artifact and still needs the .NET 9 Desktop Runtime — ship `bin\publish\StdfViewer.exe`. WPF cannot be trimmed or AOT-compiled, so ~63 MB is the floor for a self-contained WPF app.

### Usage

1. **Open STDF...** (or pass a path on the command line) to load a file.
2. **Summary** tab: the file header, `MIR`/`MRR`/`SDR`, wafer records, bin tables and record counts.
3. **Browse** tab — left panel: click a record type to narrow the middle list; **All types** clears the filter.
4. Middle panel: click a record. The `#` column is the record number, which is what the jump box expects.
5. Right panel: the decoded fields of that record. `Raw bytes` shows exactly the bytes consumed by the field, so a field list that does not add up is visible at a glance.
6. **Wafer** tab: pick a wafer, check the notch direction, and hover a die for its coordinate and hard bin.

### Supported record types

`FAR` `ATR` `MIR` `MRR` `PCR` `HBR` `SBR` `PMR` `PGR` `PLR` `RDR` `SDR` `WIR` `WRR` `WCR` `PIR` `PRR` `TSR` `PTR` `MPR` `FTR` `BPS` `EPS` `GDR` `DTR`

Variable-length types (`Cn`, `Bn`, `Dn`, `Vn`) and repeated `k`-prefixed fields are handled, including the count field they depend on.

### Known limitations

- Record IDs follow the STDF V4 table this project was built against (`FAR` = 0,10 / `PIR` = 5,10 / `RDR` = 1,70), which differs from the numbering some other tools use.
- `MPR` and `FTR` layouts come from the specification table and have not been regression-tested against real bytes.
- Unknown record types are listed but shown as `(no layout)` in the Fields panel.
- If a record's declared length exceeds its layout, the leftover bytes are reported as a `layout gap`.
- History snapshots keep per-type totals only, not the record-by-record sequence, so the structure comparison needs both files to still be on disk.
- The wafer map takes coordinates from `PRR`; files without `PRR` X/Y show no map. Die colours follow the hard bin number; the `HBR` pass/fail flag is not used for colouring yet.
- `history.json` (parse history) and `settings.json` (UI language) are written next to the executable. If that folder is read-only, both still work for the current session but are not persisted.

### Project structure

| File | Role |
| --- | --- |
| `App.xaml` / `App.xaml.cs` | Application entry point, command-line file argument |
| `ViewerWindow.xaml` / `ViewerWindow.xaml.cs` | Window layout (XAML) and interaction logic |
| `Themes/Palette.xaml` | Colours, font and size tokens |
| `Themes/Controls.xaml` | Control styles (buttons, lists, headers, scrollbars, splitters) |
| `Themes/Strings.en-US.xaml` / `Themes/Strings.zh-CN.xaml` | UI strings; record and field names stay English |
| `Loc.cs` | Language switching and `T(key)` lookups |
| `UiSettings.cs` | UI preferences, persisted to `settings.json` |
| `StdfFile.cs` | File scan: record index, byte order detection, type summary |
| `StdfRecordTable.cs` | Record and field definitions |
| `StdfDecoder.cs` | Field decoder |
| `StdfSummary.cs` | Summary-tab model built from a scanned file |
| `WaferMap.cs` | Per-wafer die map model built from `WIR`/`WRR`/`WCR`/`PRR` |
| `WaferMapView.cs` | Wafer map drawing, notch/axes and hover hit-testing |
| `RecordRow.cs` | Record list row model and builder |
| `HistoryStore.cs` | Parse-result snapshots, persisted to `history.json` next to the executable |
| `StdfComparer.cs` | Type-level diff, type-order check and RLE structure comparison |

---

## 中文

一个用于查看 **STDF V4**（Standard Test Data Format）文件的 Windows 小工具：扫描记录流、浏览记录、并解码任意一条记录的字段。

### 功能

- **汇总页**：一页看完当前文件——大小 / 记录数 / 类型数 / DUT 数 / 良率 / 扫描耗时 / 字节序，下面依次是 `File`、`MIR`、`MRR`、`SDR`、`WIR`/`WRR`/`WCR`、`PCR` 各记录段的字段、`HBR` / `SBR` 分箱表和记录条数统计。记录名与字段名始终显示英文。
- **晶圆页**：用 `PRR` 的 X/Y 和硬 bin 画 die map，按 `WIR`/`WRR` 分片，可在顶部切换晶圆；die 网格按 X/Y 分别缩放后铺满圆形晶圆，矩形 die 阵列也会显示为圆形。die 按硬 bin 上色，右侧图例带数量和占比，X 向右、Y 向上，悬停显示 X/Y/bin/site。缺口方向取自 `WCR` 的 `WF_FLAT`（`U`/`D`/`L`/`R`）；文件里没有可用的 `WCR` 时默认缺口朝下，可用缺口下拉手动改。同坐标重测以后一条 `PRR` 为准并计入重测数；良率用 `WRR` 的 GOOD_CNT/PART_CNT。
- **扫描**整个 STDF 文件，建立记录索引（偏移 / 类型 / 长度）；扫描在后台线程执行，大文件时界面不会卡住。
- **Record types** 面板：按 类型/子类型 统计条数、总字节数、首个记录号；点击某行即可过滤中间的记录列表。
- **Records** 面板：按文件顺序列出记录，显示 `#`、记录名、`typ,sub`、偏移和 `REC_LEN`；在跳转框里输入记录号回车即可跳转。
- **Fields** 面板：把选中记录逐字段解码，显示字段值、该字段实际占用的原始字节，以及解码状态（`header`、`truncated`、`omitted`、`layout gap` 等）。
- 自动从记录头识别小端 / 大端，并从 FAR 记录读出 `CPU_TYPE` / `STDF_VER`。
- 状态栏汇总文件大小、记录数、类型数和扫描耗时。超大文件最多列出 20000 行，选中具体类型可缩小范围。
- **History** 页：每次成功解析都会留下快照（路径、大小、记录数、各类型统计）。点条目回看上次解析结果；删除只删历史条目，不动磁盘上的 `.std`。
- **Compare** 页：选两条历史记录对比——逐类型条数差异表（只有一侧存在的 `only left` / `only right` 行有底色高亮），外加记录结构对比（把记录流折叠成 RLE 段，指出两份文件从哪一段开始不一样）。
- **中英双语**：工具栏右侧按钮即时切换界面语言，不用重启。选择记在 exe 同目录的 `settings.json`；首次启动跟随系统语言。

### 运行环境

- Windows 10 / 11
- 构建需要 .NET 9 SDK；发布出来的单个 exe 不需要安装任何运行时

### 构建与运行

```powershell
dotnet build .\StdfViewer.csproj
.\bin\Debug\net9.0-windows\StdfViewer.exe 你的文件.std
```

或者不先构建：

```powershell
dotnet run -- 你的文件.std
```

不带参数启动时是一个空窗口，点 **Open STDF...** 选择文件即可。

### 发布

只有一种发布形态：**自包含单文件 exe**。目标机器不需要装任何东西——不用 .NET 运行时，也不用 VC++ 运行库。`Publish` 已固定使用 `win-x64-single-file` 配置：

```powershell
dotnet publish .\StdfViewer.csproj
# 产物：bin\publish\StdfViewer.exe（约 63 MB，只有一个文件）
```

首次启动会把原生库解到 `%TEMP%\.net`（一次性，约 +0.6 秒；之后启动不受影响）。`history.json` / `settings.json` 写在 exe 同目录；该目录只读时程序照常运行，只是偏好不会保存。

普通 `dotnet build` 的产物是开发用中间件，仍需 .NET 9 Desktop Runtime——分发时只给 `bin\publish\StdfViewer.exe`。WPF 不支持裁剪和 AOT，自包含 WPF 程序大约 63 MB 起步。

### 使用步骤

1. 点 **Open STDF...**（或在命令行带上文件路径）加载文件。
2. **汇总页**：文件头、`MIR`/`MRR`/`SDR`、wafer 记录、分箱表和记录条数。
3. **浏览页**左侧面板：点击某个记录类型可过滤中间列表；**All types** 清除过滤。
4. 中间面板：点击一条记录，`#` 列就是可用于跳转框的记录号。
5. 右侧面板：显示该记录的字段解码结果。`Raw bytes` 是该字段实际占用的字节，字段表对不上时一眼可见。
6. **晶圆页**：选晶圆、确认缺口方向，悬停 die 查看坐标和硬 bin。

### 支持的记录类型

`FAR` `ATR` `MIR` `MRR` `PCR` `HBR` `SBR` `PMR` `PGR` `PLR` `RDR` `SDR` `WIR` `WRR` `WCR` `PIR` `PRR` `TSR` `PTR` `MPR` `FTR` `BPS` `EPS` `GDR` `DTR`

支持变长类型（`Cn`、`Bn`、`Dn`、`Vn`）以及重复的 `k` 前缀字段（依赖各自的计数字段）。

### 已知限制

- 记录号按本项目所依据的 STDF V4 表定义（`FAR` = 0,10 / `PIR` = 5,10 / `RDR` = 1,70），与部分其它工具的编号不同。
- `MPR` 与 `FTR` 的字段表按规范表抄录，尚未与真实字节做过回归验证。
- 未知记录类型会列出，但 Fields 面板显示 `(no layout)`。
- 记录的声明长度大于字段表时，多余字节会以 `layout gap` 提示。
- 历史快照只保存类型层统计、不保存逐条记录序列，所以结构对比需要两个文件都还在磁盘上。
- 晶圆图从 `PRR` 取坐标；没有 `PRR` X/Y 的文件不显示地图。die 颜色按硬 bin 编号分配，暂未使用 `HBR` 的通过/失败标志着色。
- `history.json`（解析历史）与 `settings.json`（界面语言）写在 exe 同目录；该目录只读时两者仍可在本次会话内使用，但不会落盘。

### 目录结构

| 文件 | 作用 |
| --- | --- |
| `App.xaml` / `App.xaml.cs` | 程序入口、命令行文件参数 |
| `ViewerWindow.xaml` / `ViewerWindow.xaml.cs` | 窗口界面（XAML）与交互逻辑 |
| `Themes/Palette.xaml` | 颜色、字体与尺寸资源 |
| `Themes/Controls.xaml` | 控件样式（按钮、列表、列头、滚动条、分割条） |
| `Themes/Strings.en-US.xaml` / `Themes/Strings.zh-CN.xaml` | 界面文案；记录名与字段名不翻译 |
| `Loc.cs` | 语言切换与 `T(key)` 取词 |
| `UiSettings.cs` | 界面偏好，落到 `settings.json` |
| `StdfFile.cs` | 文件扫描：记录索引、字节序识别、类型统计 |
| `StdfRecordTable.cs` | 记录与字段定义表 |
| `StdfDecoder.cs` | 字段解码器 |
| `StdfSummary.cs` | 汇总页模型（从已扫描的文件构建） |
| `WaferMap.cs` | 晶圆图模型（按 `WIR`/`WRR`/`WCR`/`PRR` 分片） |
| `WaferMapView.cs` | 晶圆图绘制、缺口/坐标轴与悬停命中 |
| `RecordRow.cs` | 记录列表行模型与构建器 |
| `HistoryStore.cs` | 解析结果快照（存到 exe 同目录的 `history.json`） |
| `StdfComparer.cs` | 类型层差异、类型出现顺序与 RLE 结构对比 |

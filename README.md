# STDF Viewer

A small Windows desktop tool for looking inside **STDF V4** (Standard Test Data Format) files: scan the record stream, browse the records, and decode the fields of any record.

**English** | [中文](#中文)

---

## English

### Features

- **Scan** the whole STDF file into a record index (offset / type / length). The scan runs in the background, so the window stays responsive on large files.
- **Record types** panel — per type/sub pair: count, total bytes and first record number. Click a row to filter the record list.
- **Records** panel — records in file order with `#`, type name, `typ,sub`, offset and `REC_LEN`. Type a record number into the box and press Enter to jump to it.
- **Fields** panel — the selected record decoded field by field, showing the value, the raw bytes the field consumed, and a status (`header`, `truncated`, `omitted`, `layout gap`, ...).
- Detects little/big endian from the record headers, and reports `CPU_TYPE` / `STDF_VER` from the FAR record.
- Status bar summarises file size, record count, type count and scan time. Very large files are capped at 20,000 listed rows — pick a type to narrow the list.
- **History** tab — every successfully parsed file is remembered (path, size, record count and per-type statistics). Click an entry to look at that parse result again; deleting an entry removes only the history row, never the `.std` on disk.
- **Compare** tab — pick two history entries and diff them: a per-type count table, where rows that exist on only one side (`only left` / `only right`) are highlighted, plus a record-structure check that collapses the record stream into RLE segments and reports where the two files diverge.

### Screenshot

![STDF Viewer](docs/screenshot.png)

### Requirements

- Windows 10 / 11
- .NET 9 SDK to build; the .NET 9 Desktop Runtime is enough to run a published build

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

### Usage

1. **Open STDF...** (or pass a path on the command line) to load a file.
2. Left panel: click a record type to narrow the middle list; **All types** clears the filter.
3. Middle panel: click a record. The `#` column is the record number, which is what the jump box expects.
4. Right panel: the decoded fields of that record. `Raw bytes` shows exactly the bytes consumed by the field, so a field list that does not add up is visible at a glance.

### Supported record types

`FAR` `ATR` `MIR` `MRR` `PCR` `HBR` `SBR` `PMR` `PGR` `PLR` `RDR` `SDR` `WIR` `WRR` `WCR` `PIR` `PRR` `TSR` `PTR` `MPR` `FTR` `BPS` `EPS` `GDR` `DTR`

Variable-length types (`Cn`, `Bn`, `Dn`, `Vn`) and repeated `k`-prefixed fields are handled, including the count field they depend on.

### Known limitations

- Record IDs follow the STDF V4 table this project was built against (`FAR` = 0,10 / `PIR` = 5,10 / `RDR` = 1,70), which differs from the numbering some other tools use.
- `MPR` and `FTR` layouts come from the specification table and have not been regression-tested against real bytes.
- Unknown record types are listed but shown as `(no layout)` in the Fields panel.
- If a record's declared length exceeds its layout, the leftover bytes are reported as a `layout gap`.
- A few decode status messages are still in Chinese.
- History snapshots keep per-type totals only, not the record-by-record sequence, so the structure comparison needs both files to still be on disk.
- `history.json` is written next to the executable. If that folder is read-only, history still works for the current session but is not persisted.

### Project structure

| File | Role |
| --- | --- |
| `App.xaml` / `App.xaml.cs` | Application entry point, command-line file argument |
| `ViewerWindow.xaml` / `ViewerWindow.xaml.cs` | Window layout (XAML) and interaction logic |
| `Themes/Palette.xaml` | Colours, font and size tokens |
| `Themes/Controls.xaml` | Control styles (buttons, lists, headers, scrollbars, splitters) |
| `StdfFile.cs` | File scan: record index, byte order detection, type summary |
| `StdfRecordTable.cs` | Record and field definitions |
| `StdfDecoder.cs` | Field decoder |
| `RecordRow.cs` | Record list row model and builder |
| `HistoryStore.cs` | Parse-result snapshots, persisted to `history.json` next to the executable |
| `StdfComparer.cs` | Type-level diff, type-order check and RLE structure comparison |

---

## 中文

一个用于查看 **STDF V4**（Standard Test Data Format）文件的 Windows 小工具：扫描记录流、浏览记录、并解码任意一条记录的字段。

### 功能

- **扫描**整个 STDF 文件，建立记录索引（偏移 / 类型 / 长度）；扫描在后台线程执行，大文件时界面不会卡住。
- **Record types** 面板：按 类型/子类型 统计条数、总字节数、首个记录号；点击某行即可过滤中间的记录列表。
- **Records** 面板：按文件顺序列出记录，显示 `#`、记录名、`typ,sub`、偏移和 `REC_LEN`；在跳转框里输入记录号回车即可跳转。
- **Fields** 面板：把选中记录逐字段解码，显示字段值、该字段实际占用的原始字节，以及解码状态（`header`、`truncated`、`omitted`、`layout gap` 等）。
- 自动从记录头识别小端 / 大端，并从 FAR 记录读出 `CPU_TYPE` / `STDF_VER`。
- 状态栏汇总文件大小、记录数、类型数和扫描耗时。超大文件最多列出 20000 行，选中具体类型可缩小范围。
- **History** 页：每次成功解析都会留下快照（路径、大小、记录数、各类型统计）。点条目回看上次解析结果；删除只删历史条目，不动磁盘上的 `.std`。
- **Compare** 页：选两条历史记录对比——逐类型条数差异表（只有一侧存在的 `only left` / `only right` 行有底色高亮），外加记录结构对比（把记录流折叠成 RLE 段，指出两份文件从哪一段开始不一样）。

### 运行环境

- Windows 10 / 11
- 构建需要 .NET 9 SDK；只运行发布产物的话，装 .NET 9 Desktop Runtime 即可

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

### 使用步骤

1. 点 **Open STDF...**（或在命令行带上文件路径）加载文件。
2. 左侧面板：点击某个记录类型可过滤中间列表；**All types** 清除过滤。
3. 中间面板：点击一条记录，`#` 列就是可用于跳转框的记录号。
4. 右侧面板：显示该记录的字段解码结果。`Raw bytes` 是该字段实际占用的字节，字段表对不上时一眼可见。

### 支持的记录类型

`FAR` `ATR` `MIR` `MRR` `PCR` `HBR` `SBR` `PMR` `PGR` `PLR` `RDR` `SDR` `WIR` `WRR` `WCR` `PIR` `PRR` `TSR` `PTR` `MPR` `FTR` `BPS` `EPS` `GDR` `DTR`

支持变长类型（`Cn`、`Bn`、`Dn`、`Vn`）以及重复的 `k` 前缀字段（依赖各自的计数字段）。

### 已知限制

- 记录号按本项目所依据的 STDF V4 表定义（`FAR` = 0,10 / `PIR` = 5,10 / `RDR` = 1,70），与部分其它工具的编号不同。
- `MPR` 与 `FTR` 的字段表按规范表抄录，尚未与真实字节做过回归验证。
- 未知记录类型会列出，但 Fields 面板显示 `(no layout)`。
- 记录的声明长度大于字段表时，多余字节会以 `layout gap` 提示。
- 少量解码状态文案仍是中文。
- 历史快照只保存类型层统计、不保存逐条记录序列，所以结构对比需要两个文件都还在磁盘上。
- `history.json` 写在 exe 同目录；该目录只读时历史仍可在本次会话内使用，但不会落盘。

### 目录结构

| 文件 | 作用 |
| --- | --- |
| `App.xaml` / `App.xaml.cs` | 程序入口、命令行文件参数 |
| `ViewerWindow.xaml` / `ViewerWindow.xaml.cs` | 窗口界面（XAML）与交互逻辑 |
| `Themes/Palette.xaml` | 颜色、字体与尺寸资源 |
| `Themes/Controls.xaml` | 控件样式（按钮、列表、列头、滚动条、分割条） |
| `StdfFile.cs` | 文件扫描：记录索引、字节序识别、类型统计 |
| `StdfRecordTable.cs` | 记录与字段定义表 |
| `StdfDecoder.cs` | 字段解码器 |
| `RecordRow.cs` | 记录列表行模型与构建器 |
| `HistoryStore.cs` | 解析结果快照（存到 exe 同目录的 `history.json`） |
| `StdfComparer.cs` | 类型层差异、类型出现顺序与 RLE 结构对比 |

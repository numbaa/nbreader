# NbReader 持久化设计（SQLite）

> 草稿 — 待评审修改。
> **注意**：Schema 已被 [`product-design.md`](product-design.md) 第 6 节覆盖，本文档保留技术实现细节。
> 关联：[short-term-plan.md](short-term-plan.md) | [architecture.md](architecture.md) | [product-design.md](product-design.md)

---

## 1. 概述

统一使用 SQLite 单文件作为应用唯一的持久化后端，承载用户设置、阅读进度、书架索引三类数据。

---

## 2. 设计原则

- **单文件**：`nbreader.db`，零配置，跨平台一致
- **无阻塞 UI**：翻页不写库，只在关闭漫画/退出应用时落盘
- **接口隔离**：`IStorageService` 抽象，上层不感知 SQL
- **可测试**：支持 `Data Source=:memory:` 内存模式

---

## 3. 依赖

| 包 | 用途 | 备注 |
|----|------|------|
| `Microsoft.Data.Sqlite` | ADO.NET SQLite 提供程序 | 微软官方，跨平台，无原生依赖 |

添加到 `NbReader.Core.csproj`，核心库不引入 UI 依赖。

---

## 4. 数据库位置

跨平台统一：

| OS | 路径 |
|----|------|
| Windows | `%APPDATA%/NbReader/nbreader.db` |
| Linux | `~/.local/share/NbReader/nbreader.db` |
| macOS | `~/Library/Application Support/NbReader/nbreader.db` |

通过 `Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)` 获取。

数据库文件在首次访问时自动创建（含建表）。

---

## 5. Schema

> ⚠️ **此 Schema 已被 [`product-design.md`](product-design.md) 第 6 节取代。**
> 下面保留初版以供参考，实际实施以 product-design.md 中 `comic_works` + `comic_resources` 双层模型为准。

```sql
-- 用户设置（key-value）
CREATE TABLE IF NOT EXISTS settings (
    key   TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

-- 阅读进度（按漫画文件路径索引）
CREATE TABLE IF NOT EXISTS progress (
    comic_path     TEXT PRIMARY KEY,
    current_page   INTEGER NOT NULL DEFAULT 0,
    total_pages    INTEGER NOT NULL DEFAULT 0,
    last_read_time TEXT    NOT NULL
);

-- 书架/图书馆
CREATE TABLE IF NOT EXISTS library (
    id             INTEGER PRIMARY KEY AUTOINCREMENT,
    title          TEXT    NOT NULL,
    file_path      TEXT    NOT NULL UNIQUE,
    cover_path     TEXT,
    page_count     INTEGER NOT NULL DEFAULT 0,
    file_size      INTEGER NOT NULL DEFAULT 0,
    added_date     TEXT    NOT NULL,
    last_read_date TEXT
);

CREATE INDEX IF NOT EXISTS idx_progress_path ON progress(comic_path);
CREATE INDEX IF NOT EXISTS idx_library_title  ON library(title);
CREATE INDEX IF NOT EXISTS idx_library_path   ON library(file_path);
```

---

## 6. 接口设计

文件：`NbReader.Core/Abstractions/IStorageService.cs`

```csharp
namespace NbReader.Core.Abstractions;

/// <summary>
/// 统一持久化服务。
/// </summary>
public interface IStorageService
{
    // ── 设置 ──────────────────────────────────────────

    /// <summary>读取字符串设置</summary>
    string? GetSetting(string key);

    /// <summary>写入字符串设置</summary>
    void SetSetting(string key, string value);

    /// <summary>读取对象设置（JSON 序列化）</summary>
    T? GetSetting<T>(string key) where T : class;

    /// <summary>写入对象设置（JSON 序列化）</summary>
    void SetSetting<T>(string key, T value) where T : class;

    // ── 阅读进度 ──────────────────────────────────────

    /// <summary>获取漫画阅读进度</summary>
    Models.ReadingProgress? GetProgress(string comicPath);

    /// <summary>保存漫画阅读进度</summary>
    void SaveProgress(string comicPath, Models.ReadingProgress progress);

    /// <summary>删除漫画阅读进度</summary>
    void DeleteProgress(string comicPath);

    // ── 书架 ──────────────────────────────────────────

    /// <summary>获取全部书架条目</summary>
    List<Models.ComicInfo> GetLibrary();

    /// <summary>添加或更新书架条目（按 file_path 去重）</summary>
    void AddOrUpdateComic(Models.ComicInfo comic);

    /// <summary>从书架移除</summary>
    void RemoveComic(string filePath);

    /// <summary>搜索书架（按标题模糊匹配）</summary>
    List<Models.ComicInfo> SearchLibrary(string keyword);
}
```

---

## 7. 实现概要

文件：`NbReader.Core/Services/SqliteStorageService.cs`

```csharp
public class SqliteStorageService : IStorageService, IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteStorageService(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        InitializeSchema();
    }

    private void InitializeSchema()
    {
        // 执行第 5 节中的 CREATE TABLE IF NOT EXISTS ...
    }

    public void Dispose() => _connection?.Dispose();
}
```

关键的实现细节：
- 所有写操作使用参数化 SQL（防注入、正确处理特殊字符）
- 设置读写：`INSERT OR REPLACE INTO settings ...`
- 进度保存：`INSERT OR REPLACE INTO progress ...`
- 书架增改：`INSERT OR REPLACE INTO library ...`
- 时间字段统一用 ISO 8601 字符串存储

---

## 8. DI 注册

文件：`NbReader/App.axaml.cs` — `ConfigureServices` 方法

```csharp
// 单例，应用生命周期内共享一个连接
var dbPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "NbReader", "nbreader.db");
services.AddSingleton<IStorageService>(new SqliteStorageService(dbPath));
```

---

## 9. 数据流

```
┌─────────────────────────────────────────────────────┐
│                      应用启动                        │
│  new SqliteStorageService(path) → 自动建表           │
│  GetSetting("defaultDirection") → 注入 ReaderVM     │
│  GetSetting("defaultFitMode")  → 注入 ReaderVM     │
└─────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────┐
│                    打开漫画                          │
│  LoadFileSourceAsync(fileSource)                    │
│    ├→ GetProgress(path)                             │
│    │   ├ 有记录 → CurrentPageIndex = record.Page    │
│    │   └ 无记录 → CurrentPageIndex = 0              │
│    └→ ReloadPagesForCurrentModeAsync()              │
└─────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────┐
│                    翻页（不写库）                      │
│  GoToNextPage / GoToPrevPage                        │
│    └→ 仅更新内存 CurrentPageIndex                    │
└─────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────┐
│                    关闭漫画                          │
│  Close()                                            │
│    ├→ SaveProgress(path, currentState)              │
│    └→ _fileSource.Dispose() + ClearBitmaps()        │
└─────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────┐
│                    切换设置                          │
│  CycleReadingDirection()                            │
│    ├→ ReadingDirection = newValue                   │
│    └→ SetSetting("defaultDirection", newValue)      │
│                                                     │
│  CycleFitMode()                                     │
│    ├→ FitMode = newValue                            │
│    └→ SetSetting("defaultFitMode", newValue)        │
└─────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────┐
│                    应用退出                          │
│  SqliteStorageService.Dispose() → 连接关闭           │
└─────────────────────────────────────────────────────┘
```

---

## 10. 与 ViewModel 的集成

`ReaderViewModel` 当前已有 `Close()` 方法。改动点：

| 方法 | 改动 |
|------|------|
| `LoadFileSourceAsync` | 开头调用 `_storage.GetProgress(path)`，有记录则设置 `CurrentPageIndex` |
| `Close` | 末尾调用 `_storage.SaveProgress(path, state)` |
| `CycleReadingDirection` | 末尾调用 `_storage.SetSetting("defaultDirection", ...)` |
| `CycleFitMode` | 末尾调用 `_storage.SetSetting("defaultFitMode", ...)` |
| 构造函数 | 新增 `IStorageService` 参数 |

---

## 11. 设置键约定

| Key | 类型 | 默认值 | 说明 |
|-----|------|--------|------|
| `defaultDirection` | string | `LeftToRight` | 默认阅读方向 |
| `defaultFitMode` | string | `Uniform` | 默认适应模式 |
| `theme` | string | `dark` | 主题（后续） |
| `windowWidth` | int | — | 窗口宽度（后续） |
| `windowHeight` | int | — | 窗口高度（后续） |

---

## 12. 测试策略

### 12.1 单元测试（`SqliteStorageServiceTests.cs`）

使用 `Data Source=:memory:` 内存数据库：

```csharp
[Fact]
public void SaveProgress_ThenGetProgress_ShouldReturnSame()
{
    using var svc = new SqliteStorageService(":memory:");
    svc.SaveProgress("/test.cbz", new ReadingProgress
    {
        ComicPath = "/test.cbz",
        CurrentPage = 5,
        TotalPages = 20
    });
    var p = svc.GetProgress("/test.cbz");
    p.Should().NotBeNull();
    p!.CurrentPage.Should().Be(5);
}
```

覆盖：
- 建表幂等（多次创建不报错）
- 设置读/写/覆盖
- 进度增/改/删/不存在返回 null
- 书架增/改/删/搜索/去重

### 12.2 集成测试（`ReaderViewModelTests.cs`）

Mock `IStorageService`，验证：
- 打开漫画时调用了 `GetProgress`
- 关闭漫画时调用了 `SaveProgress`
- 切换方向时调用了 `SetSetting`

---

## 13. 计划实施顺序

| 步骤 | 内容 | 产出 |
|------|------|------|
| **S1** | 添加 `Microsoft.Data.Sqlite` 包 | 修改 `.csproj` |
| **S2** | 创建 `IStorageService` 接口 | `Abstractions/IStorageService.cs` |
| **S3** | 实现 `SqliteStorageService` | `Services/SqliteStorageService.cs` |
| **S4** | DI 注册 + 启动读取设置 | `App.axaml.cs` |
| **S5** | `ReaderViewModel` 集成进度/设置 | `ReaderViewModel.cs` |
| **S6** | 单元测试 | `SqliteStorageServiceTests.cs` |
| **S7** | 更新文档（short-term-plan 等） | `docs/` |

# NbReader 架构设计

## 1. 解决方案结构

```
nbreader/
├── NbReader.sln
├── Directory.Build.props          # 统一版本号、分析器配置
├── .gitignore
├── AGENTS.md
├── README.md
├── docs/                          # 设计文档
│   ├── long-term-plan.md
│   ├── short-term-plan.md
│   └── architecture.md
├── src/
│   ├── NbReader/                  # Avalonia 主项目（UI 层）
│   │   ├── NbReader.csproj
│   │   ├── App.axaml / App.axaml.cs
│   │   ├── Program.cs
│   │   ├── Views/                  # 视图（AXAML + code-behind）
│   │   │   ├── MainWindow.axaml
│   │   │   ├── MainView.axaml
│   │   │   ├── ReaderView.axaml
│   │   │   └── Controls/           # 可复用控件
│   │   ├── ViewModels/             # 视图模型
│   │   │   ├── MainViewModel.cs
│   │   │   ├── ReaderViewModel.cs
│   │   │   └── ViewModelBase.cs
│   │   ├── Converters/             # 值转换器
│   │   ├── Services/               # UI 层服务
│   │   │   └── DialogService.cs
│   │   └── Assets/                 # 静态资源
│   │       └── appsettings.json
│   │
│   ├── NbReader.Core/              # 核心逻辑库（无 UI 依赖）
│   │   ├── NbReader.Core.csproj
│   │   ├── Models/                 # 领域模型
│   │   │   ├── ComicInfo.cs        # 漫画元数据
│   │   │   ├── PageInfo.cs         # 单页信息
│   │   │   └── ReadingProgress.cs  # 阅读进度
│   │   ├── Abstractions/           # 接口定义
│   │   │   ├── IFileSource.cs      # 文件源接口
│   │   │   ├── IImageLoader.cs     # 图片加载接口
│   │   │   └── IArchiveService.cs  # 压缩包服务接口
│   │   ├── Services/               # 核心服务实现
│   │   │   ├── FileSourceFactory.cs
│   │   │   ├── DirectoryFileSource.cs
│   │   │   ├── CbzFileSource.cs
│   │   │   └── ImageLoader.cs
│   │   └── Extensions/             # 扩展方法
│   │       └── EnumerableExtensions.cs
│   │
│   └── NbReader.Tests/             # 测试项目
│       ├── NbReader.Tests.csproj
│       ├── Core/                   # 核心逻辑测试
│       │   ├── Services/
│       │   │   ├── DirectoryFileSourceTests.cs
│       │   │   ├── CbzFileSourceTests.cs
│       │   │   └── FileSourceFactoryTests.cs
│       │   └── Models/
│       └── UI/                     # UI 逻辑测试
│           └── ViewModels/
│               ├── MainViewModelTests.cs
│               └── ReaderViewModelTests.cs
```

---

## 2. 分层架构

```
┌──────────────────────────────────────────┐
│              UI Layer (NbReader)         │
│  Avalonia Views + ViewModels + Converters │
├──────────────────────────────────────────┤
│          Core Layer (NbReader.Core)      │
│     Models + Services + Abstractions     │
├──────────────────────────────────────────┤
│          Infrastructure (NuGet)          │
│  Avalonia, SharpCompress, SkiaSharp, ... │
└──────────────────────────────────────────┘
```

**依赖规则：**
- `NbReader` → `NbReader.Core` ✓
- `NbReader.Core` → `NbReader` ✗（核心库不依赖 UI）
- `NbReader.Tests` → `NbReader.Core` + `NbReader` ✓

---

## 3. 核心接口设计

### 3.1 `IFileSource` — 文件源

```csharp
namespace NbReader.Core.Abstractions;

/// <summary>
/// 漫画文件源：抽象本地文件、压缩包、在线源等。
/// </summary>
public interface IFileSource
{
    /// <summary>源名称（用于显示）</summary>
    string Name { get; }

    /// <summary>总页数</summary>
    int PageCount { get; }

    /// <summary>获取指定页的图片流</summary>
    Task<Stream> GetPageStreamAsync(int pageIndex);

    /// <summary>获取指定页的缩略图流（可选，用于预览）</summary>
    Task<Stream?> GetThumbnailStreamAsync(int pageIndex);

    /// <summary>释放资源</summary>
    void Dispose();
}
```

### 3.2 `IImageLoader` — 图片加载器

```csharp
namespace NbReader.Core.Abstractions;

/// <summary>
/// 图片加载器：将流解码为可渲染的位图对象。
/// </summary>
public interface IImageLoader
{
    /// <summary>从流加载图片</summary>
    Task<IImage> LoadAsync(Stream stream);

    /// <summary>从文件加载图片</summary>
    Task<IImage> LoadAsync(string filePath);
}

/// <summary>
/// 跨平台位图抽象（解耦具体图片库）。
/// </summary>
public interface IImage : IDisposable
{
    int Width { get; }
    int Height { get; }
    object NativeImage { get; }  // 平台相关位图对象
}
```

### 3.3 `IArchiveService` — 压缩包服务

```csharp
namespace NbReader.Core.Abstractions;

/// <summary>
/// 压缩包服务：处理 ZIP/RAR 等格式的解压。
/// </summary>
public interface IArchiveService
{
    /// <summary>支持的扩展名</summary>
    IReadOnlySet<string> SupportedExtensions { get; }

    /// <summary>判断是否支持该文件</summary>
    bool CanOpen(string filePath);

    /// <summary>列出所有条目</summary>
    Task<IReadOnlyList<ArchiveEntry>> ListEntriesAsync(string filePath);

    /// <summary>提取指定条目</summary>
    Task<Stream> ExtractEntryAsync(string filePath, string entryName);
}

public record ArchiveEntry(string Name, long Size, bool IsDirectory);
```

---

## 4. MVVM 导航设计

```mermaid
graph TD
    MW[MainWindow] --> MV[MainView]
    MV --> LV[LibraryView<br/>书架视图]
    MV --> RV[ReaderView<br/>阅读器视图]
    MV --> SV[SettingsView<br/>设置视图]
    
    MVM[MainViewModel] --> RVM[ReaderViewModel]
    MVM --> LVM[LibraryViewModel]
    MVM --> SVM[SettingsViewModel]
    
    NS[NavigationService] --> MVM
```

**导航方式：** 使用 `ContentControl` + 数据模板切换，由 `MainViewModel` 管理当前活跃视图。

```csharp
public class MainViewModel : ViewModelBase
{
    public ViewModelBase CurrentView { get; set; }

    public ICommand NavigateToReader { get; }
    public ICommand NavigateToLibrary { get; }
    public ICommand NavigateToSettings { get; }
}
```

---

## 5. 数据流

```mermaid
sequenceDiagram
    participant U as User
    participant MW as MainWindow
    participant MVM as MainViewModel
    participant RVM as ReaderViewModel
    participant FS as FileSourceFactory
    participant IL as ImageLoader
    participant RV as ReaderView

    U->>MW: 打开 CBZ 文件
    MW->>MVM: OpenFileCommand
    MVM->>FS: Create(filePath)
    FS-->>MVM: IFileSource
    MVM->>RVM: SetFileSource(source)
    RVM->>FS: GetPageStream(0)
    FS-->>RVM: Stream
    RVM->>IL: LoadAsync(stream)
    IL-->>RVM: IImage
    RVM-->>RV: PropertyChanged → CurrentImage
    RV->>RV: Render image
```

---

## 6. 关键技术选型

| 组件 | 选型 | 理由 |
|------|------|------|
| UI 框架 | Avalonia UI 11.x | 跨平台，XAML 风格，成熟 |
| MVVM | CommunityToolkit.Mvvm | 官方推荐，源生成器减少样板代码 |
| DI 容器 | Microsoft.Extensions.DependencyInjection | .NET 标准，轻量 |
| 图片渲染 | SkiaSharp | 高性能 2D 渲染，WebP 支持好 |
| 压缩处理 | SharpCompress | 支持 ZIP / RAR / 7Z |
| 测试 | xUnit + FluentAssertions | 社区标准 |
| 日志 | Microsoft.Extensions.Logging | .NET 标准 |
| 配置 | JSON (appsettings.json) | 简单通用 |

---

## 7. 设计原则（SOLID）

| 原则 | 实践 |
|------|------|
| **S** 单一职责 | 每个类只做一件事（读文件、渲染图片、管理状态分离） |
| **O** 开闭原则 | 通过 `IFileSource` 接口扩展新格式，无需修改现有代码 |
| **L** 里氏替换 | 所有 `IFileSource` 实现可互相替换 |
| **I** 接口隔离 | 小而专注的接口（`IFileSource` ≠ `IImageLoader`） |
| **D** 依赖反转 | 核心库定义接口，UI 层通过 DI 注入实现 |

---

## 8. 协程与线程模型

- **UI 线程：** 所有 Avalonia 属性绑定和 UI 更新
- **后台线程：** 文件 I/O、图片解码、压缩包解压
- **线程安全：** `ViewModelBase` 内置 `Dispatcher.UIThread` 调度

```csharp
// ViewModel 中启动后台任务更新属性
public async Task LoadPageAsync(int index)
{
    IsLoading = true;
    var stream = await Task.Run(() => _fileSource.GetPageStreamAsync(index));
    var image = await Task.Run(() => _imageLoader.LoadAsync(stream));
    CurrentImage = image; // 在 UI 线程设置（由源生成器处理）
    IsLoading = false;
}
```

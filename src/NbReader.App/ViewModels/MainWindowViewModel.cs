using System.Collections.Generic;
using NbReader.Catalog;
using NbReader.Import;
using NbReader.Infrastructure;
using NbReader.Metadata;
using NbReader.Reader;
using NbReader.Search;

namespace NbReader.App.ViewModels;

public sealed class MainWindowViewModel
{
    private readonly AppRuntime _runtime;

    public MainWindowViewModel(AppRuntime runtime)
    {
        _runtime = runtime;
    }

    public string Title => "NbReader";

    public string Subtitle =>
        "面向 Phase 0 的应用骨架已经就绪，当前目标是验证 Avalonia、.NET 9、SQLite、配置加载和日志基础设施的可行性。";

    public IReadOnlyList<string> Modules { get; } =
    [
        CatalogModule.Name,
        ImportModule.Name,
        ReaderModule.Name,
        SearchModule.Name,
        MetadataModule.Name,
        InfrastructureModule.Name,
    ];

    public IReadOnlyList<string> CurrentGoals { get; } =
    [
        "启动 Avalonia 应用并建立最小壳层界面",
        "验证 .NET 9 环境下的项目恢复与构建",
        "完成 SQLite 初始化、配置文件加载和文件日志",
        "建立 Catalog / Import / Reader / Search / Metadata / Infrastructure 模块边界",
        "为后续 zip 枚举、页面读取抽象和导入任务持久化预留入口",
    ];

    public string NextMilestone =>
        "Milestone A：完成 zip 与目录枚举，建立 Source / Volume / Page 的最小入库闭环。";

    public IReadOnlyList<string> RuntimeChecks =>
    [
        $"SQLite: {_runtime.Database.DatabaseFilePath}",
        $"Settings: {AppEnvironment.SettingsFilePath}",
        $"Log: {_runtime.Logger.LogFilePath}",
        $"Schema Version: {_runtime.Database.ReadMetaValue("schema_version") ?? "unknown"}",
        $"Library Roots: {_runtime.Settings.LibraryRoots.Count}",
    ];

    public string EnvironmentSummary =>
        $"应用目录：{AppEnvironment.AppBaseDirectory} | 数据目录：{AppEnvironment.DataRoot} | 缓存根目录：{AppEnvironment.CacheRoot}";

    public string Footer =>
        "正式设计文档已落盘，当前应用骨架对应 development-plan.md 中的 Phase 0，并已具备 SQLite、配置和日志基础设施。";
}
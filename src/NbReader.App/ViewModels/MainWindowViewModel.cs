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
    public string Title => "NbReader";

    public string Subtitle =>
        "面向 Phase 0 的应用骨架已经就绪，当前目标是验证 Avalonia、.NET 9、zip 读取和数据库接入的基础可行性。";

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
        "建立 Catalog / Import / Reader / Search / Metadata / Infrastructure 模块边界",
        "为后续 SQLite、zip 枚举和页面读取抽象预留入口",
    ];

    public string NextMilestone =>
        "Milestone A：完成 zip 与目录枚举，建立 Source / Volume / Page 的最小入库闭环。";

    public string EnvironmentSummary =>
        $"应用目录：{AppEnvironment.AppBaseDirectory} | 缓存根目录：{AppEnvironment.CacheRoot}";

    public string Footer =>
        "正式设计文档已落盘，当前应用骨架对应 development-plan.md 中的 Phase 0。";
}
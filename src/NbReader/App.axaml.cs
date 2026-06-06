using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using NbReader.ViewModels;
using NbReader.Views;

namespace NbReader;

public partial class App : Application
{
    /// <summary>
    /// DI 服务容器。
    /// </summary>
    public IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 配置 DI 容器
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// 注册服务。
    /// </summary>
    private static void ConfigureServices(IServiceCollection services)
    {
        // ViewModels — 单例（整个应用生命周期内保持状态）
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<ReaderViewModel>();
    }
}
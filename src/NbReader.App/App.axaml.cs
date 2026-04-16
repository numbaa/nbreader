using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using NbReader.Infrastructure;

namespace NbReader.App;

public partial class App : Application
{
    public AppRuntime? Runtime { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                Runtime = AppBootstrapper.Initialize();
                Runtime.Logger.Info("Application bootstrap completed.");
                desktop.MainWindow = new MainWindow(Runtime);
            }
            catch (Exception exception)
            {
                desktop.MainWindow = BuildStartupErrorWindow(exception);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static Window BuildStartupErrorWindow(Exception exception)
    {
        return new Window
        {
            Width = 760,
            Height = 420,
            Title = "NbReader - Startup Error",
            Content = new TextBox
            {
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                Text = $"应用启动失败。{Environment.NewLine}{Environment.NewLine}{exception}",
            },
        };
    }
}
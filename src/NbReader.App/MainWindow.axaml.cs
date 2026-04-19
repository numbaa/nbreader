using Avalonia.Controls;
using NbReader.App.ViewModels;
using NbReader.Infrastructure;

namespace NbReader.App;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    public MainWindow(AppRuntime runtime)
        : this()
    {
        _viewModel = new MainWindowViewModel(runtime);
        DataContext = _viewModel;
    }

    private async void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        try
        {
            await _viewModel.LoadSeriesAsync();
        }
        catch (Exception ex)
        {
            _viewModel.NavigateTo("Catalog");
            _viewModel.ReportStatus("系列列表加载失败，请查看日志。");
            _viewModel.Runtime.Logger.Error("Failed to load series list.", ex);
        }
    }

    private async void OnOpenSelectedVolumeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        try
        {
            await _viewModel.OpenSelectedVolumeAsync();
        }
        catch (Exception ex)
        {
            _viewModel.ReportStatus($"打开卷失败：{ex.Message}");
            _viewModel.Runtime.Logger.Error("Failed to open selected volume.", ex);
        }
    }

    private async void OnOpenContinueReadingClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        try
        {
            await _viewModel.OpenContinueReadingAsync();
        }
        catch (Exception ex)
        {
            _viewModel.ReportStatus($"继续阅读失败：{ex.Message}");
            _viewModel.Runtime.Logger.Error("Failed to open continue reading entry.", ex);
        }
    }

    private async void OnOpenSelectedRecentReadingClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        try
        {
            await _viewModel.OpenSelectedRecentReadingAsync();
        }
        catch (Exception ex)
        {
            _viewModel.ReportStatus($"打开最近阅读失败：{ex.Message}");
            _viewModel.Runtime.Logger.Error("Failed to open selected recent reading entry.", ex);
        }
    }

    private void OnReaderPreviousPageClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewModel?.ShowPreviousPage();
    }

    private void OnReaderNextPageClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewModel?.ShowNextPage();
    }

    private void OnReaderToggleModeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewModel?.ToggleReaderDisplayMode();
    }

    private void OnReaderToggleDirectionClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewModel?.ToggleReadingDirection();
    }

    private async void OnSearchRefreshClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        try
        {
            await _viewModel.RefreshSearchWorkspaceAsync();
        }
        catch (Exception ex)
        {
            _viewModel.ReportStatus($"刷新搜索整理视图失败：{ex.Message}");
            _viewModel.Runtime.Logger.Error("Failed to refresh search workspace.", ex);
        }
    }

    private async void OnSearchRetryFailedTaskClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        try
        {
            await _viewModel.RetrySelectedFailedImportTaskAsync();
        }
        catch (Exception ex)
        {
            _viewModel.ReportStatus($"重新处理失败任务时出错：{ex.Message}");
            _viewModel.Runtime.Logger.Error("Failed to retry failed import task.", ex);
        }
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        _viewModel?.FlushReadingProgress();
    }
}
using Avalonia.Controls;
using Avalonia.Platform.Storage;
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

    private async void OnImportAnalyzeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        try
        {
            await _viewModel.AnalyzeImportInputAsync();
        }
        catch (Exception ex)
        {
            _viewModel.ReportStatus($"分析导入输入失败：{ex.Message}");
            _viewModel.Runtime.Logger.Error("Failed to analyze import input.", ex);
        }
    }

    private void OnCatalogGoToImportClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewModel?.GoToImportSection();
    }

    private async void OnImportPickFolderClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
        {
            _viewModel.ReportStatus("当前环境不支持路径选择器。请手动输入路径。");
            return;
        }

        try
        {
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "选择漫画目录",
                AllowMultiple = false,
            });

            var selected = folders.FirstOrDefault();
            if (selected is null)
            {
                return;
            }

            var localPath = selected.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(localPath))
            {
                _viewModel.ImportPathInput = localPath;
            }
        }
        catch (Exception ex)
        {
            _viewModel.ReportStatus($"选择目录失败：{ex.Message}");
            _viewModel.Runtime.Logger.Error("Failed to pick import folder.", ex);
        }
    }

    private async void OnImportPickZipClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
        {
            _viewModel.ReportStatus("当前环境不支持文件选择器。请手动输入路径。");
            return;
        }

        try
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择 zip 文件",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("ZIP")
                    {
                        Patterns = ["*.zip"],
                    },
                ],
            });

            var selected = files.FirstOrDefault();
            if (selected is null)
            {
                return;
            }

            var localPath = selected.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(localPath))
            {
                _viewModel.ImportPathInput = localPath;
            }
        }
        catch (Exception ex)
        {
            _viewModel.ReportStatus($"选择 zip 失败：{ex.Message}");
            _viewModel.Runtime.Logger.Error("Failed to pick import zip.", ex);
        }
    }

    private async void OnImportExecuteClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        try
        {
            await _viewModel.ExecuteImportAsync();
        }
        catch (Exception ex)
        {
            _viewModel.ReportStatus($"执行导入失败：{ex.Message}");
            _viewModel.Runtime.Logger.Error("Failed to execute import.", ex);
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

    private async void OnSearchApplySeriesRenameClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        try
        {
            await _viewModel.ApplySeriesRenameAsync();
        }
        catch (Exception ex)
        {
            _viewModel.ReportStatus($"系列改名失败：{ex.Message}");
            _viewModel.Runtime.Logger.Error("Failed to apply series rename.", ex);
        }
    }

    private async void OnSearchMergeSeriesClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        try
        {
            await _viewModel.MergeSelectedSeriesAsync();
        }
        catch (Exception ex)
        {
            _viewModel.ReportStatus($"系列合并失败：{ex.Message}");
            _viewModel.Runtime.Logger.Error("Failed to merge series.", ex);
        }
    }

    private async void OnSearchApplyVolumeNumberClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        try
        {
            await _viewModel.ApplySelectedVolumeNumberCorrectionAsync();
        }
        catch (Exception ex)
        {
            _viewModel.ReportStatus($"卷号修正失败：{ex.Message}");
            _viewModel.Runtime.Logger.Error("Failed to apply volume number correction.", ex);
        }
    }

    private async void OnSearchApplySeriesMetadataClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        try
        {
            await _viewModel.ApplySeriesMetadataAsync();
        }
        catch (Exception ex)
        {
            _viewModel.ReportStatus($"作者/标签更新失败：{ex.Message}");
            _viewModel.Runtime.Logger.Error("Failed to apply series metadata edits.", ex);
        }
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        _viewModel?.FlushReadingProgress();
    }
}
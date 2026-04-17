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
}
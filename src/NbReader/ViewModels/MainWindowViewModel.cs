using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace NbReader.ViewModels;

/// <summary>
/// 主窗口视图模型：管理全局导航和应用状态。
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    /// <summary>
    /// 当前激活的视图模型。
    /// </summary>
    [ObservableProperty]
    private ViewModelBase _currentView;

    /// <summary>
    /// 阅读器视图模型（常驻）。
    /// </summary>
    public ReaderViewModel Reader { get; }

    public MainWindowViewModel()
    {
        Reader = ((App)Application.Current!).Services.GetRequiredService<ReaderViewModel>();
        CurrentView = Reader;
    }

    /// <summary>
    /// 打开漫画文件。
    /// </summary>
    [RelayCommand]
    private async Task OpenFileAsync()
    {
        // TODO: 实现文件打开对话框 + FileSourceFactory
        await Task.CompletedTask;
    }
}

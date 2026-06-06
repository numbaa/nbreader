using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NbReader.Core.Services;

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

    public MainWindowViewModel(ReaderViewModel reader)
    {
        Reader = reader;
        CurrentView = reader;
    }

    /// <summary>
    /// 打开漫画文件（由 View 层调用，传入文件/文件夹路径）。
    /// </summary>
    public async Task OpenFileAsync(string path)
    {
        try
        {
            var fileSource = FileSourceFactory.Create(path);
            await Reader.LoadFileSourceAsync(fileSource);
            CurrentView = Reader;
        }
        catch (Exception ex)
        {
            Reader.StatusText = $"打开失败: {ex.Message}";
        }
    }

    /// <summary>
    /// [验证用] 加载演示图片，验证渲染引擎。
    /// </summary>
    [RelayCommand]
    private async Task LoadDemoAsync()
    {
        await Reader.LoadDemoImageAsync();
        CurrentView = Reader;
    }
}

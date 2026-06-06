using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NbReader.Core.Services;
using SharpCompress.Common;

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

            // 检测空文件源（压缩包中无图片）
            if (fileSource.PageCount == 0)
            {
                fileSource.Dispose();
                Reader.StatusText = "❌ 文件中没有找到图片";
                return;
            }

            await Reader.LoadFileSourceAsync(fileSource);
            CurrentView = Reader;
        }
        catch (DirectoryNotFoundException)
        {
            Reader.StatusText = "❌ 目录不存在，请检查路径";
        }
        catch (FileNotFoundException)
        {
            Reader.StatusText = "❌ 文件不存在，请检查路径";
        }
        catch (NotSupportedException ex)
        {
            Reader.StatusText = $"❌ 不支持的格式: {ex.Message}";
        }
        catch (SharpCompress.Common.ArchiveException)
        {
            Reader.StatusText = "❌ 压缩包损坏或格式无效";
        }
        catch (Exception ex)
        {
            Reader.StatusText = $"❌ 打开失败: {ex.Message}";
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

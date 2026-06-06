using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NbReader.Core.Abstractions;

namespace NbReader.ViewModels;

/// <summary>
/// 阅读器视图模型：管理当前显示的图片、缩放、翻页等状态。
/// </summary>
public partial class ReaderViewModel : ViewModelBase
{
    private IFileSource? _fileSource;

    [ObservableProperty]
    private object? _currentImage;

    [ObservableProperty]
    private int _currentPageIndex;

    [ObservableProperty]
    private int _totalPages;

    [ObservableProperty]
    private double _zoomLevel = 1.0;

    [ObservableProperty]
    private string _statusText = "就绪";

    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// 当前漫画名称。
    /// </summary>
    [ObservableProperty]
    private string _comicName = string.Empty;

    /// <summary>
    /// 加载文件源。
    /// </summary>
    public async Task LoadFileSourceAsync(IFileSource fileSource)
    {
        _fileSource = fileSource;
        ComicName = fileSource.Name;
        TotalPages = fileSource.PageCount;
        CurrentPageIndex = 0;
        await LoadCurrentPageAsync();
    }

    /// <summary>
    /// 加载当前页。
    /// </summary>
    private async Task LoadCurrentPageAsync()
    {
        if (_fileSource is null || CurrentPageIndex < 0 || CurrentPageIndex >= TotalPages)
            return;

        IsLoading = true;
        StatusText = $"加载中... {CurrentPageIndex + 1}/{TotalPages}";

        try
        {
            var stream = await Task.Run(() => _fileSource.GetPageStreamAsync(CurrentPageIndex));
            // TODO: use IImageLoader to decode
            // For now, set stream as placeholder
            CurrentImage = stream;
            StatusText = $"{CurrentPageIndex + 1} / {TotalPages}";
        }
        catch (Exception ex)
        {
            StatusText = $"加载失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 上一页。
    /// </summary>
    [RelayCommand]
    private async Task GoToPrevPageAsync()
    {
        if (CurrentPageIndex > 0)
        {
            CurrentPageIndex--;
            await LoadCurrentPageAsync();
        }
    }

    /// <summary>
    /// 下一页。
    /// </summary>
    [RelayCommand]
    private async Task GoToNextPageAsync()
    {
        if (CurrentPageIndex < TotalPages - 1)
        {
            CurrentPageIndex++;
            await LoadCurrentPageAsync();
        }
    }

    /// <summary>
    /// 跳转到指定页。
    /// </summary>
    [RelayCommand]
    private async Task GoToPageAsync(int pageIndex)
    {
        if (pageIndex >= 0 && pageIndex < TotalPages)
        {
            CurrentPageIndex = pageIndex;
            await LoadCurrentPageAsync();
        }
    }

    /// <summary>
    /// 缩放。
    /// </summary>
    [RelayCommand]
    private void ZoomIn()
    {
        ZoomLevel = Math.Min(ZoomLevel * 1.25, 10.0);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        ZoomLevel = Math.Max(ZoomLevel / 1.25, 0.1);
    }

    [RelayCommand]
    private void ZoomReset()
    {
        ZoomLevel = 1.0;
    }

    /// <summary>
    /// 关闭当前漫画。
    /// </summary>
    [RelayCommand]
    private void Close()
    {
        _fileSource?.Dispose();
        _fileSource = null;
        CurrentImage = null;
        TotalPages = 0;
        CurrentPageIndex = 0;
        ComicName = string.Empty;
        StatusText = "就绪";
    }
}

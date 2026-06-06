using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NbReader.Core.Abstractions;
using SkiaSharp;

namespace NbReader.ViewModels;

/// <summary>
/// 阅读器视图模型：管理当前显示的图片、缩放、翻页等状态。
/// </summary>
public partial class ReaderViewModel : ViewModelBase
{
    private readonly IImageLoader _imageLoader;
    private IFileSource? _fileSource;

    [ObservableProperty]
    private IImage? _currentImage;

    [ObservableProperty]
    private Avalonia.Media.Imaging.Bitmap? _displayBitmap;

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

    public ReaderViewModel(IImageLoader imageLoader)
    {
        _imageLoader = imageLoader ?? throw new ArgumentNullException(nameof(imageLoader));
    }

    /// <summary>
    /// [验证用] 加载演示测试图片（无文件依赖）。
    /// </summary>
    public async Task LoadDemoImageAsync()
    {
        using var stream = CreateDemoImageStream();
        var image = await _imageLoader.LoadAsync(stream);

        CurrentImage?.Dispose();
        CurrentImage = image;
        DisplayBitmap?.Dispose();
        DisplayBitmap = Converters.ImageConverter.ToAvaloniaBitmap(image);

        ComicName = "🎨 演示图片";
        TotalPages = 1;
        CurrentPageIndex = 0;
        StatusText = $"1 / 1   {image.Width}×{image.Height}";
        IsLoading = false;
    }

    /// <summary>
    /// 生成演示图片（渐变 + 文字 + 网格线）。
    /// </summary>
    private static Stream CreateDemoImageStream()
    {
        const int w = 800, h = 600;
        var bitmap = new SkiaSharp.SKBitmap(w, h, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);

        // 背景
        canvas.Clear(SkiaSharp.SKColors.DarkSlateGray);

        // 方格棋盘图案
        const int cellSize = 40;
        using var paint = new SkiaSharp.SKPaint();
        for (int y = 0; y < h; y += cellSize)
        {
            for (int x = 0; x < w; x += cellSize)
            {
                paint.Color = ((x / cellSize + y / cellSize) % 2 == 0)
                    ? new SkiaSharp.SKColor(60, 60, 80)
                    : new SkiaSharp.SKColor(40, 40, 60);
                canvas.DrawRect(x, y, cellSize, cellSize, paint);
            }
        }

        // 彩色圆
        paint.Color = new SkiaSharp.SKColor(220, 80, 80, 180);
        canvas.DrawCircle(w / 2f, h / 2f, 120, paint);
        paint.Color = new SkiaSharp.SKColor(80, 180, 220, 180);
        canvas.DrawCircle(w / 3f, h / 2f, 80, paint);
        paint.Color = new SkiaSharp.SKColor(80, 220, 120, 180);
        canvas.DrawCircle(w * 2f / 3f, h / 2f, 80, paint);

        // 文字 — 使用中文字体
        var typeface = SkiaSharp.SKTypeface.FromFamilyName("Microsoft YaHei")
            ?? SkiaSharp.SKTypeface.Default;

        using var font = new SkiaSharp.SKFont(typeface, 32);
        paint.Color = SkiaSharp.SKColors.White;
        paint.IsAntialias = true;
        paint.Style = SkiaSharp.SKPaintStyle.Fill;
        canvas.DrawText("NbReader 演示图片", w / 2f - 160, 50, font, paint);

        using var smallFont = new SkiaSharp.SKFont(typeface, 18);
        canvas.DrawText("Ctrl+滚轮 缩放 | 中键拖拽 平移 | ← → 翻页", w / 2f - 210, h - 30, smallFont, paint);

        canvas.Flush();

        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 90);
        var stream = new MemoryStream();
        data.SaveTo(stream);
        stream.Position = 0;
        return stream;
    }

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
            var stream = await _fileSource.GetPageStreamAsync(CurrentPageIndex);
            var image = await _imageLoader.LoadAsync(stream);
            await stream.DisposeAsync();

            CurrentImage?.Dispose();
            CurrentImage = image;

            // 转换为 Avalonia 可渲染的 Bitmap
            DisplayBitmap = Converters.ImageConverter.ToAvaloniaBitmap(image);

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
        CurrentImage?.Dispose();
        CurrentImage = null;
        DisplayBitmap?.Dispose();
        DisplayBitmap = null;
        TotalPages = 0;
        CurrentPageIndex = 0;
        ComicName = string.Empty;
        StatusText = "就绪";
    }
}

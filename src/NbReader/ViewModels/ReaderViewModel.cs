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
    /// [验证用] 加载 3 页演示图片（无文件依赖，可测试翻页）。
    /// </summary>
    public async Task LoadDemoImageAsync()
    {
        var demoSource = new DemoFileSource();
        await LoadFileSourceAsync(demoSource);
    }

    /// <summary>
    /// 生成演示图片流。
    /// </summary>
    internal static Stream CreateDemoPageStream(int pageIndex, int totalPages)
    {
        const int w = 800, h = 600;
        var bitmap = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);

        // 不同页面不同背景色
        var colors = new[] {
            new SKColor(50, 60, 80),
            new SKColor(60, 50, 70),
            new SKColor(40, 60, 60)
        };
        var bgColor = colors[pageIndex % colors.Length];

        // 棋盘格背景
        const int cell = 40;
        using var paint = new SKPaint();
        for (int y = 0; y < h; y += cell)
        {
            for (int x = 0; x < w; x += cell)
            {
                paint.Color = ((x / cell + y / cell) % 2 == 0)
                    ? bgColor
                    : new SKColor((byte)(bgColor.Red + 20), (byte)(bgColor.Green + 20), (byte)(bgColor.Blue + 20));
                canvas.DrawRect(x, y, cell, cell, paint);
            }
        }

        // 彩色圆形
        var circleColors = new[] {
            new SKColor(220, 80, 80, 180),
            new SKColor(80, 220, 120, 180),
            new SKColor(80, 180, 220, 180)
        };
        paint.Color = circleColors[pageIndex % circleColors.Length];
        canvas.DrawCircle(w / 2f, h / 2f, 120, paint);

        // 页码文字
        var typeface = SKTypeface.FromFamilyName("Microsoft YaHei") ?? SKTypeface.Default;
        using var font = new SKFont(typeface, 36);
        paint.Color = SKColors.White;
        paint.IsAntialias = true;
        paint.Style = SKPaintStyle.Fill;
        var pageText = $"第 {pageIndex + 1} 页 / 共 {totalPages} 页";
        canvas.DrawText(pageText, w / 2f - 160, 60, font, paint);

        using var smallFont = new SKFont(typeface, 16);
        canvas.DrawText("Ctrl+滚轮 缩放 | 中键拖拽 平移 | ← → 翻页", w / 2f - 195, h - 30, smallFont, paint);

        canvas.Flush();

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        var stream = new MemoryStream();
        data.SaveTo(stream);
        stream.Position = 0;
        return stream;
    }

    /// <summary>
    /// 演示用 IFileSource：内存中生成 3 页图片。
    /// </summary>
    private sealed class DemoFileSource : IFileSource
    {
        private readonly List<byte[]> _pages = new();
        public string Name => "🎨 演示（3 页）";
        public int PageCount => _pages.Count;

        public DemoFileSource()
        {
            for (int i = 0; i < 3; i++)
            {
                using var s = CreateDemoPageStream(i, 3);
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                _pages.Add(ms.ToArray());
            }
        }

        public Task<Stream> GetPageStreamAsync(int pageIndex)
        {
            if (pageIndex < 0 || pageIndex >= _pages.Count)
                throw new ArgumentOutOfRangeException(nameof(pageIndex));
            return Task.FromResult<Stream>(new MemoryStream(_pages[pageIndex]));
        }

        public Task<Stream?> GetThumbnailStreamAsync(int pageIndex)
            => Task.FromResult<Stream?>(null);

        public void Dispose() => _pages.Clear();
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

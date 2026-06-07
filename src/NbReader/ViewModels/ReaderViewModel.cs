using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NbReader.Core.Abstractions;
using SkiaSharp;

namespace NbReader.ViewModels;

/// <summary>
/// 图片适应模式。
/// </summary>
public enum FitMode
{
    /// <summary>等比缩放以适应视口（默认）。</summary>
    Uniform,
    /// <summary>宽度撑满视口。</summary>
    FillWidth,
    /// <summary>高度撑满视口。</summary>
    FillHeight,
    /// <summary>1:1 原始像素。</summary>
    Original
}

/// <summary>
/// 阅读模式。
/// </summary>
public enum ReadingMode
{
    /// <summary>单页模式（默认）。</summary>
    SinglePage,
    /// <summary>双页模式（左右并排）。</summary>
    DualPage,
    /// <summary>滚动模式（连续垂直排列）。</summary>
    Scroll
}

/// <summary>
/// 阅读方向。
/// </summary>
public enum ReadingDirection
{
    /// <summary>左到右（← 上一页，→ 下一页）。</summary>
    LeftToRight,
    /// <summary>右到左（← 下一页，→ 上一页）。</summary>
    RightToLeft
}

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

    /// <summary>
    /// 当前图片适应模式。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FitModeText))]
    private FitMode _fitMode = FitMode.Uniform;

    /// <summary>
    /// 适应模式的中文描述（状态栏显示）。
    /// </summary>
    public string FitModeText => FitMode switch
    {
        FitMode.Uniform => "适应页面",
        FitMode.FillWidth => "适应宽度",
        FitMode.FillHeight => "适应高度",
        FitMode.Original => "原始大小",
        _ => ""
    };

    /// <summary>
    /// 当前阅读模式。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReadingModeText))]
    [NotifyPropertyChangedFor(nameof(IsSinglePage))]
    [NotifyPropertyChangedFor(nameof(IsDualPage))]
    [NotifyPropertyChangedFor(nameof(IsScrollMode))]
    private ReadingMode _readingMode = ReadingMode.SinglePage;

    /// <summary>
    /// 阅读方向。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReadingDirectionText))]
    private ReadingDirection _readingDirection = ReadingDirection.LeftToRight;

    /// <summary>
    /// 双页模式下的右侧位图。
    /// </summary>
    [ObservableProperty]
    private Avalonia.Media.Imaging.Bitmap? _displayBitmapRight;

    /// <summary>
    /// 阅读模式的中文描述。
    /// </summary>
    public string ReadingModeText => ReadingMode switch
    {
        ReadingMode.SinglePage => "单页",
        ReadingMode.DualPage => "双页",
        ReadingMode.Scroll => "滚动",
        _ => ""
    };

    /// <summary>
    /// 阅读方向的中文描述。
    /// </summary>
    public string ReadingDirectionText => ReadingDirection switch
    {
        ReadingDirection.LeftToRight => "L→R",
        ReadingDirection.RightToLeft => "R→L",
        _ => ""
    };

    /// <summary>
    /// 视图绑定的便捷属性。
    /// </summary>
    public bool IsSinglePage => ReadingMode == ReadingMode.SinglePage;
    public bool IsDualPage => ReadingMode == ReadingMode.DualPage;
    public bool IsScrollMode => ReadingMode == ReadingMode.Scroll;

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
        await ReloadPagesForCurrentModeAsync();
    }

    /// <summary>
    /// 根据当前阅读模式加载页面。
    /// </summary>
    private async Task ReloadPagesForCurrentModeAsync()
    {
        if (_fileSource is null) return;

        ClearBitmaps();

        switch (ReadingMode)
        {
            case ReadingMode.SinglePage:
                await LoadSinglePageAsync(CurrentPageIndex);
                break;
            case ReadingMode.DualPage:
                await LoadDualPagesAsync(CurrentPageIndex);
                break;
            case ReadingMode.Scroll:
                await LoadScrollPagesAsync();
                break;
        }
    }

    /// <summary>
    /// 单页模式：加载指定页。
    /// </summary>
    private async Task LoadSinglePageAsync(int index)
    {
        if (_fileSource is null || index < 0 || index >= TotalPages) return;

        IsLoading = true;
        StatusText = $"加载中... {index + 1}/{TotalPages}";

        try
        {
            DisplayBitmap = await DecodePageAsync(index);
            StatusText = $"{index + 1} / {TotalPages}";
        }
        catch (Exception ex)
        {
            StatusText = $"加载失败: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    /// <summary>
    /// 双页模式：加载 left (index) 和 right (index+1)。
    /// 约定：对开页 (0,1) (2,3) (4,5)...，index 为对开页左页。
    /// </summary>
    private async Task LoadDualPagesAsync(int leftIndex)
    {
        if (_fileSource is null || leftIndex < 0 || leftIndex >= TotalPages) return;

        IsLoading = true;
        var rightIndex = leftIndex + 1;
        StatusText = $"加载中... {leftIndex + 1}-{Math.Min(rightIndex + 1, TotalPages)}/{TotalPages}";

        try
        {
            var leftTask = DecodePageAsync(leftIndex);
            var rightTask = rightIndex < TotalPages
                ? DecodePageAsync(rightIndex)
                : Task.FromResult<Avalonia.Media.Imaging.Bitmap?>(null);

            await Task.WhenAll(leftTask, rightTask);

            DisplayBitmap = await leftTask;
            DisplayBitmapRight = await rightTask;

            StatusText = rightIndex < TotalPages
                ? $"{leftIndex + 1}-{rightIndex + 1} / {TotalPages}"
                : $"{leftIndex + 1} / {TotalPages}";
        }
        catch (Exception ex)
        {
            StatusText = $"加载失败: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    /// <summary>
    /// 滚动模式：加载所有页面。
    /// </summary>
    private async Task LoadScrollPagesAsync()
    {
        if (_fileSource is null) return;

        IsLoading = true;
        StatusText = $"加载中... 共 {TotalPages} 页";

        try
        {
            // 先加载第一页作为 DisplayBitmap（兼容状态栏），其余在 View 中按需构建
            DisplayBitmap = await DecodePageAsync(0);
            StatusText = $"1 / {TotalPages}  滚动模式";
        }
        catch (Exception ex)
        {
            StatusText = $"加载失败: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    /// <summary>
    /// 解码指定页为 Avalonia Bitmap。
    /// </summary>
    private async Task<Avalonia.Media.Imaging.Bitmap?> DecodePageAsync(int pageIndex)
    {
        if (_fileSource is null) return null;

        var stream = await _fileSource.GetPageStreamAsync(pageIndex);
        var image = await _imageLoader.LoadAsync(stream);
        await stream.DisposeAsync();
        return Converters.ImageConverter.ToAvaloniaBitmap(image);
    }

    /// <summary>
    /// 清理所有位图资源。
    /// </summary>
    private void ClearBitmaps()
    {
        CurrentImage?.Dispose();
        CurrentImage = null;
        DisplayBitmap?.Dispose();
        DisplayBitmap = null;
        DisplayBitmapRight?.Dispose();
        DisplayBitmapRight = null;
    }

    /// <summary>
    /// 上一页。
    /// </summary>
    [RelayCommand]
    private async Task GoToPrevPageAsync()
    {
        int step = ReadingMode == ReadingMode.DualPage ? 2 : 1;
        if (CurrentPageIndex >= step)
        {
            CurrentPageIndex -= step;
            await ReloadPagesForCurrentModeAsync();
        }
    }

    /// <summary>
    /// 下一页。
    /// </summary>
    [RelayCommand]
    private async Task GoToNextPageAsync()
    {
        int step = ReadingMode == ReadingMode.DualPage ? 2 : 1;
        if (CurrentPageIndex + step < TotalPages)
        {
            CurrentPageIndex += step;
            await ReloadPagesForCurrentModeAsync();
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
            // 双页模式下，对齐到对开页左页
            if (ReadingMode == ReadingMode.DualPage)
                pageIndex = pageIndex / 2 * 2;

            CurrentPageIndex = pageIndex;
            await ReloadPagesForCurrentModeAsync();
        }
    }

    /// <summary>
    /// 循环切换阅读模式: SinglePage → DualPage → Scroll → SinglePage。
    /// </summary>
    [RelayCommand]
    private async Task CycleReadingModeAsync()
    {
        ReadingMode = ReadingMode switch
        {
            ReadingMode.SinglePage => ReadingMode.DualPage,
            ReadingMode.DualPage => ReadingMode.Scroll,
            ReadingMode.Scroll => ReadingMode.SinglePage,
            _ => ReadingMode.SinglePage
        };

        // 切换到双页模式时，对齐页码到对开页左页
        if (ReadingMode == ReadingMode.DualPage)
            CurrentPageIndex = CurrentPageIndex / 2 * 2;

        await ReloadPagesForCurrentModeAsync();
    }

    /// <summary>
    /// 切换阅读方向。
    /// </summary>
    [RelayCommand]
    private void CycleReadingDirection()
    {
        ReadingDirection = ReadingDirection switch
        {
            ReadingDirection.LeftToRight => ReadingDirection.RightToLeft,
            ReadingDirection.RightToLeft => ReadingDirection.LeftToRight,
            _ => ReadingDirection.LeftToRight
        };
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
    /// 循环切换适应模式: Uniform → FillWidth → FillHeight → Original → Uniform。
    /// </summary>
    [RelayCommand]
    private void CycleFitMode()
    {
        FitMode = FitMode switch
        {
            FitMode.Uniform => FitMode.FillWidth,
            FitMode.FillWidth => FitMode.FillHeight,
            FitMode.FillHeight => FitMode.Original,
            FitMode.Original => FitMode.Uniform,
            _ => FitMode.Uniform
        };
    }

    /// <summary>
    /// 关闭当前漫画。</summary>
    [RelayCommand]
    private void Close()
    {
        _fileSource?.Dispose();
        _fileSource = null;
        ClearBitmaps();
        TotalPages = 0;
        CurrentPageIndex = 0;
        ComicName = string.Empty;
        ReadingMode = ReadingMode.SinglePage;
        StatusText = "就绪";
    }
}

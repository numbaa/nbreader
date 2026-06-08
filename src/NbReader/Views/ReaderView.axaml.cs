using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using NbReader.ViewModels;

namespace NbReader.Views;

public partial class ReaderView : UserControl
{
    private ScrollViewer? _scrollViewer;

    // 单页模式控件（AXAML 中定义）
    private Border? _singlePageContainer;
    private Image? _imageControl;

    // 双页模式控件（动态创建）
    private Border? _dualPageContainer;
    private Image? _imageControlLeft;
    private Image? _imageControlRight;

    // 滚动模式控件（动态创建）
    private StackPanel? _scrollPageStack;
    private int _scrollPageImageCount; // 已创建的 Image 数量

    // 缩放/平移状态
    private double _originalWidth;
    private double _originalHeight;
    private Point _lastMousePosition;
    private bool _isPanning;

    // 当前布局模式
    private ReadingMode _currentLayoutMode;

    public ReaderView()
    {
        InitializeComponent();
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _scrollViewer = this.FindControl<ScrollViewer>("ScrollViewer");
        _singlePageContainer = this.FindControl<Border>("SinglePageContainer");
        _imageControl = this.FindControl<Image>("ImageControl");

        if (_scrollViewer is not null)
        {
            _scrollViewer.AddHandler(PointerPressedEvent, OnScrollPointerPressed,
                RoutingStrategies.Tunnel, handledEventsToo: true);
            _scrollViewer.AddHandler(PointerMovedEvent, OnScrollPointerMoved,
                RoutingStrategies.Tunnel, handledEventsToo: true);
            _scrollViewer.AddHandler(PointerReleasedEvent, OnScrollPointerReleased,
                RoutingStrategies.Tunnel, handledEventsToo: true);
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is ReaderViewModel vm)
        {
            vm.PropertyChanged += (s, args) =>
            {
                switch (args.PropertyName)
                {
                    case nameof(ReaderViewModel.DisplayBitmap):
                        SetImageSource(vm.DisplayBitmap);
                        break;
                    case nameof(ReaderViewModel.DisplayBitmapRight):
                        SetRightImageSource(vm.DisplayBitmapRight);
                        break;
                    case nameof(ReaderViewModel.ZoomLevel):
                        ApplyZoom(vm.ZoomLevel);
                        break;
                    case nameof(ReaderViewModel.FitMode):
                        ApplyFitMode(vm.FitMode);
                        break;
                    case nameof(ReaderViewModel.ReadingMode):
                        SwitchLayout(vm.ReadingMode);
                        break;
                    case nameof(ReaderViewModel.ReadingDirection):
                        if (_currentLayoutMode == ReadingMode.DualPage)
                            RefreshDualPageImages(vm);
                        break;
                }
            };

            SetImageSource(vm.DisplayBitmap);
            SwitchLayout(vm.ReadingMode);
        }
    }

    // ─── 布局切换 ────────────────────────────────────────────────────

    private void SwitchLayout(ReadingMode mode)
    {
        if (_scrollViewer is null) return;

        // 离开滚动模式时，清理旧的滚动布局
        if (_currentLayoutMode == ReadingMode.Scroll && mode != ReadingMode.Scroll)
        {
            _scrollPageStack?.Children.Clear();
            _scrollPageImageCount = 0;
        }

        // 隐藏所有布局
        if (_singlePageContainer is not null)
            _singlePageContainer.IsVisible = false;
        if (_dualPageContainer is not null)
            _dualPageContainer.IsVisible = false;
        if (_scrollPageStack is not null)
            _scrollPageStack.IsVisible = false;

        switch (mode)
        {
            case ReadingMode.SinglePage:
                EnsureSinglePageLayout();
                break;
            case ReadingMode.DualPage:
                EnsureDualPageLayout();
                break;
            case ReadingMode.Scroll:
                EnsureScrollLayout();
                break;
        }

        _currentLayoutMode = mode;

        // 应用当前适应模式
        if (DataContext is ReaderViewModel vm)
            ApplyFitMode(vm.FitMode);
    }

    private void EnsureSinglePageLayout()
    {
        if (_scrollViewer is null) return;
        _singlePageContainer ??= this.FindControl<Border>("SinglePageContainer");
        _imageControl ??= this.FindControl<Image>("ImageControl");
        if (_singlePageContainer is not null)
        {
            _singlePageContainer.IsVisible = true;
            _scrollViewer.Content = _singlePageContainer;
        }
    }

    private void EnsureDualPageLayout()
    {
        if (_scrollViewer is null) return;

        if (_dualPageContainer is null)
        {
            _imageControlLeft = new Image
            {
                Stretch = Stretch.Uniform,
                Margin = new Thickness(0, 0, 2, 0)
            };
            RenderOptions.SetBitmapInterpolationMode(_imageControlLeft, BitmapInterpolationMode.HighQuality);

            _imageControlRight = new Image
            {
                Stretch = Stretch.Uniform,
                Margin = new Thickness(2, 0, 0, 0)
            };
            RenderOptions.SetBitmapInterpolationMode(_imageControlRight, BitmapInterpolationMode.HighQuality);

            var stack = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
            stack.Children.Add(_imageControlLeft);
            stack.Children.Add(_imageControlRight);

            _dualPageContainer = new Border
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Background = Brushes.Transparent,
                Child = stack
            };
        }

        _dualPageContainer.IsVisible = true;
        _scrollViewer.Content = _dualPageContainer;

        // 恢复双页图片
        if (DataContext is ReaderViewModel vm)
            RefreshDualPageImages(vm);
    }

    /// <summary>
    /// 刷新双页模式图片，处理 R→L 交换和落单页对齐。
    /// </summary>
    private void RefreshDualPageImages(ReaderViewModel vm)
    {
        if (_imageControlLeft is null || _imageControlRight is null) return;
        if (_dualPageContainer is null) return;

        bool isRTL = vm.ReadingDirection == ReadingDirection.RightToLeft;
        bool isCover = vm.CurrentPageIndex == 0;
        bool isSinglePage = vm.DisplayBitmapRight is null;

        // R→L 时交换左右图片
        var leftBitmap = isRTL ? vm.DisplayBitmapRight : vm.DisplayBitmap;
        var rightBitmap = isRTL ? vm.DisplayBitmap : vm.DisplayBitmapRight;

        _imageControlLeft.Source = leftBitmap;
        _imageControlRight.Source = rightBitmap;
        _imageControlRight.IsVisible = rightBitmap is not null;

        // 落单页（非封面）按阅读方向对齐：L→R 左对齐，R→L 右对齐
        // 封面及对开页始终居中
        if (isSinglePage && !isCover)
        {
            _dualPageContainer.HorizontalAlignment = isRTL
                ? Avalonia.Layout.HorizontalAlignment.Right
                : Avalonia.Layout.HorizontalAlignment.Left;
        }
        else
        {
            _dualPageContainer.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        }
    }

    private void EnsureScrollLayout()
    {
        if (_scrollViewer is null || DataContext is not ReaderViewModel vm) return;

        if (_scrollPageStack is null)
        {
            _scrollPageStack = new StackPanel { Orientation = Orientation.Vertical };
            _scrollViewer.ScrollChanged += OnScrollViewerScrolled;
        }

        // 订阅位图追加事件
        vm.ScrollBitmapsAppended -= OnScrollBitmapsAppended;
        vm.ScrollBitmapsAppended += OnScrollBitmapsAppended;

        _scrollPageStack.IsVisible = true;
        _scrollPageImageCount = 0;
        _scrollViewer.Content = _scrollPageStack;

        // 从已有数据构建 Image
        AppendScrollImagesFrom(vm);
    }

    private void OnScrollBitmapsAppended()
    {
        if (DataContext is ReaderViewModel vm)
            AppendScrollImagesFrom(vm);
    }
    private void AppendScrollImagesFrom(ReaderViewModel vm)
    {
        if (_scrollPageStack is null) return;
        if (vm.ScrollBitmaps is null) return;

        for (int i = _scrollPageImageCount; i < vm.ScrollBitmaps.Count; i++)
        {
            var bitmap = vm.ScrollBitmaps[i];
            if (bitmap is null) continue;
            var img = new Image
            {
                Source = bitmap,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            RenderOptions.SetBitmapInterpolationMode(img, BitmapInterpolationMode.HighQuality);
            _scrollPageStack.Children.Add(img);
        }
        _scrollPageImageCount = vm.ScrollBitmaps.Count;
    }

    /// <summary>
    /// 外层 ScrollViewer 滚动：更新页码 + 触发按需加载。
    /// </summary>
    private async void OnScrollViewerScrolled(object? sender, ScrollChangedEventArgs e)
    {
        if (_scrollViewer is null || DataContext is not ReaderViewModel vm) return;
        if (_scrollPageStack is null || _scrollPageStack.Children.Count == 0) return;

        var offsetY = _scrollViewer.Offset.Y;
        var vh = _scrollViewer.Viewport.Height;
        var extent = _scrollViewer.Extent.Height;

        // 根据子元素实际位置计算当前可视页码
        int visiblePage = 0;
        double accumulatedY = 0;
        foreach (var child in _scrollPageStack.Children)
        {
            if (child is Control ctrl)
            {
                double childHeight = ctrl.Bounds.Height > 0 ? ctrl.Bounds.Height : ctrl.DesiredSize.Height;
                if (accumulatedY + childHeight > offsetY)
                {
                    visiblePage = _scrollPageStack.Children.IndexOf(child);
                    break;
                }
                accumulatedY += childHeight;
            }
        }
        vm.VisiblePageIndex = Math.Clamp(visiblePage, 0, _scrollPageStack.Children.Count - 1);

        // 距末尾 1 屏时加载后续 2 页
        if (offsetY > 0 && offsetY + vh * 2 >= extent)
            await vm.LoadMoreScrollPagesAsync(2);
    }

    // ─── 图片来源设置 ────────────────────────────────────────────────

    private void SetImageSource(Bitmap? bitmap)
    {
        if (bitmap is not null)
        {
            _originalWidth = bitmap.Size.Width;
            _originalHeight = bitmap.Size.Height;
        }

        if (_currentLayoutMode == ReadingMode.SinglePage)
        {
            if (_imageControl is not null)
                _imageControl.Source = bitmap;
        }
        else if (_currentLayoutMode == ReadingMode.DualPage)
        {
            if (DataContext is ReaderViewModel vm)
                RefreshDualPageImages(vm);
        }
        // 滚动模式：StackPanel 自动通过追加构建，无需手动设置

        if (DataContext is ReaderViewModel vm2)
            ApplyFitMode(vm2.FitMode);
    }

    private void SetRightImageSource(Bitmap? bitmap)
    {
        if (_currentLayoutMode == ReadingMode.DualPage && DataContext is ReaderViewModel vm)
            RefreshDualPageImages(vm);
    }

    // ─── 缩放 ────────────────────────────────────────────────────────

    public void ApplyZoom(double zoom)
    {
        zoom = Math.Clamp(zoom, 0.1, 10.0);

        switch (_currentLayoutMode)
        {
            case ReadingMode.SinglePage:
                ApplyZoomToImage(_imageControl, zoom);
                break;
            case ReadingMode.DualPage:
                ApplyZoomToImage(_imageControlLeft, zoom);
                ApplyZoomToImage(_imageControlRight, zoom);
                break;
            case ReadingMode.Scroll:
                // 滚动模式暂不支持缩放
                break;
        }

        if (DataContext is ReaderViewModel vm)
            vm.ZoomLevel = zoom;
    }

    private void ApplyZoomToImage(Image? image, double zoom)
    {
        if (image is null || _scrollViewer is null) return;
        if (_originalWidth <= 0 || _originalHeight <= 0) return;

        var oldW = image.Width;
        var oldH = image.Height;

        image.Width = _originalWidth * zoom;
        image.Height = _originalHeight * zoom;

        if (oldW > 0 && oldH > 0)
        {
            var vw = _scrollViewer.Viewport.Width;
            var vh = _scrollViewer.Viewport.Height;
            var ratio = zoom / (oldW / _originalWidth);
            _scrollViewer.Offset = new Vector(
                Math.Max(0, (_scrollViewer.Offset.X + vw / 2.0) * ratio - vw / 2.0),
                Math.Max(0, (_scrollViewer.Offset.Y + vh / 2.0) * ratio - vh / 2.0));
        }
    }

    public void ApplyFitMode(FitMode mode)
    {
        if (_scrollViewer is null) return;
        if (_originalWidth <= 0 || _originalHeight <= 0) return;

        var vw = _scrollViewer.Viewport.Width;
        var vh = _scrollViewer.Viewport.Height;

        // 双页模式：总宽度是两页宽度
        double effectiveWidth = _currentLayoutMode == ReadingMode.DualPage
            ? _originalWidth * 2
            : _originalWidth;

        double targetZoom = mode switch
        {
            FitMode.Uniform => Math.Min(vw / effectiveWidth, vh / _originalHeight),
            FitMode.FillWidth => vw / effectiveWidth,
            FitMode.FillHeight => vh / _originalHeight,
            FitMode.Original => 1.0,
            _ => 1.0
        };

        ApplyZoom(targetZoom);
    }

    // ─── 中键拖拽平移 ────────────────────────────────────────────────

    private void OnScrollPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(_scrollViewer);
        if (point.Properties.IsMiddleButtonPressed)
        {
            _isPanning = true;
            _lastMousePosition = e.GetPosition(_scrollViewer);
            Cursor = new Cursor(StandardCursorType.SizeAll);
            e.Pointer.Capture(_scrollViewer);
            e.Handled = true;
        }
    }

    private void OnScrollPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPanning || _scrollViewer is null) return;

        var pos = e.GetPosition(_scrollViewer);
        var delta = _lastMousePosition - pos;
        _scrollViewer.Offset = new Vector(
            _scrollViewer.Offset.X + delta.X,
            _scrollViewer.Offset.Y + delta.Y);
        _lastMousePosition = pos;
        e.Handled = true;
    }

    private void OnScrollPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isPanning)
        {
            e.Pointer.Capture(null);
            _isPanning = false;
            Cursor = Cursor.Default;
            e.Handled = true;
        }
    }
}

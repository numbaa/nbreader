using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
        {
            if (vm.DisplayBitmap is not null && _imageControlLeft is not null)
                _imageControlLeft.Source = vm.DisplayBitmap;
            if (vm.DisplayBitmapRight is not null && _imageControlRight is not null)
                _imageControlRight.Source = vm.DisplayBitmapRight;
        }
    }

    private void EnsureScrollLayout()
    {
        if (_scrollViewer is null) return;

        if (_scrollPageStack is null)
        {
            _scrollPageStack = new StackPanel { Orientation = Avalonia.Layout.Orientation.Vertical };
        }

        _scrollPageStack.IsVisible = true;
        _scrollViewer.Content = _scrollPageStack;

        // 重建滚动模式图片（从 ViewModel 获取数据）
        BuildScrollImages();
    }

    private void BuildScrollImages()
    {
        if (_scrollPageStack is null || DataContext is not ReaderViewModel vm) return;

        _scrollPageStack.Children.Clear();

        if (vm.TotalPages <= 0) return;

        // 先检查 DisplayBitmap 是否对应第一页，是则作为已加载的首页
        // 后续页通过 IFileSource 按需加载（简化实现：只显示第一页，后续页点击加载）
        // 完整实现可预加载后续页

        var firstImage = new Image
        {
            Source = vm.DisplayBitmap,
            Stretch = Stretch.Uniform
        };
        RenderOptions.SetBitmapInterpolationMode(firstImage, BitmapInterpolationMode.HighQuality);
        _scrollPageStack.Children.Add(firstImage);
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
            if (_imageControlLeft is not null)
                _imageControlLeft.Source = bitmap;
        }
        else if (_currentLayoutMode == ReadingMode.Scroll)
        {
            BuildScrollImages();
        }

        if (DataContext is ReaderViewModel vm)
            ApplyFitMode(vm.FitMode);
    }

    private void SetRightImageSource(Bitmap? bitmap)
    {
        if (_currentLayoutMode == ReadingMode.DualPage && _imageControlRight is not null)
            _imageControlRight.Source = bitmap;
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

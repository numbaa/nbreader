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
    private Image? _imageControl;
    private ScrollViewer? _scrollViewer;
    private double _originalWidth;
    private double _originalHeight;
    private Point _lastMousePosition;
    private bool _isPanning;

    public ReaderView()
    {
        InitializeComponent();
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _imageControl = this.FindControl<Image>("ImageControl");
        _scrollViewer = this.FindControl<ScrollViewer>("ScrollViewer");

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
                if (args.PropertyName == nameof(ReaderViewModel.DisplayBitmap))
                    SetImageSource(vm.DisplayBitmap);
                else if (args.PropertyName == nameof(ReaderViewModel.ZoomLevel))
                    ApplyZoom(vm.ZoomLevel);
                else if (args.PropertyName == nameof(ReaderViewModel.FitMode))
                    ApplyFitMode(vm.FitMode);
            };

            SetImageSource(vm.DisplayBitmap);
        }
    }

    private void SetImageSource(Bitmap? bitmap)
    {
        if (_imageControl is null) return;
        _imageControl.Source = bitmap;

        if (bitmap is not null)
        {
            _originalWidth = bitmap.Size.Width;
            _originalHeight = bitmap.Size.Height;

            // 新图片加载时，按当前适应模式计算初始缩放
            if (DataContext is ReaderViewModel vm)
                ApplyFitMode(vm.FitMode);
            else
                ApplyZoom(1.0);
        }
    }

    /// <summary>
    /// 根据适应模式和视口大小计算目标缩放比例，并应用。
    /// </summary>
    public void ApplyFitMode(FitMode mode)
    {
        if (_imageControl is null || _scrollViewer is null) return;
        if (_originalWidth <= 0 || _originalHeight <= 0) return;

        var vw = _scrollViewer.Viewport.Width;
        var vh = _scrollViewer.Viewport.Height;

        double targetZoom = mode switch
        {
            FitMode.Uniform => Math.Min(vw / _originalWidth, vh / _originalHeight),
            FitMode.FillWidth => vw / _originalWidth,
            FitMode.FillHeight => vh / _originalHeight,
            FitMode.Original => 1.0,
            _ => 1.0
        };

        ApplyZoom(targetZoom);
    }

    public void ApplyZoom(double zoom)
    {
        if (_imageControl is null || _scrollViewer is null) return;

        zoom = Math.Clamp(zoom, 0.1, 10.0);
        var oldW = _imageControl.Width;
        var oldH = _imageControl.Height;

        _imageControl.Width = _originalWidth * zoom;
        _imageControl.Height = _originalHeight * zoom;

        if (oldW > 0 && oldH > 0)
        {
            var vw = _scrollViewer.Viewport.Width;
            var vh = _scrollViewer.Viewport.Height;
            var ratio = zoom / (oldW / _originalWidth);
            _scrollViewer.Offset = new Vector(
                Math.Max(0, (_scrollViewer.Offset.X + vw / 2.0) * ratio - vw / 2.0),
                Math.Max(0, (_scrollViewer.Offset.Y + vh / 2.0) * ratio - vh / 2.0));
        }

        if (DataContext is ReaderViewModel vm)
            vm.ZoomLevel = zoom;
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

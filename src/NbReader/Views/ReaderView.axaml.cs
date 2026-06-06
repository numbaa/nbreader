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

    public ReaderViewModel? ViewModel => DataContext as ReaderViewModel;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is ReaderViewModel vm)
        {
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(ReaderViewModel.DisplayBitmap))
                    SetImageSource(vm.DisplayBitmap);
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
            ApplyZoom(1.0);
        }
    }

    /// <summary>
    /// 通过修改 Image 的 Width/Height 实现缩放。
    /// 这会让 ScrollViewer 感知到真实内容大小，从而原生滚动。
    /// </summary>
    public void ApplyZoom(double zoom)
    {
        if (_imageControl is null || _scrollViewer is null) return;

        zoom = Math.Clamp(zoom, 0.1, 10.0);
        var oldW = _imageControl.Width;
        var oldH = _imageControl.Height;

        _imageControl.Width = _originalWidth * zoom;
        _imageControl.Height = _originalHeight * zoom;

        // 以视口中心为锚调整滚动偏移
        if (oldW > 0 && oldH > 0)
        {
            var vw = _scrollViewer.Viewport.Width;
            var vh = _scrollViewer.Viewport.Height;
            var ratio = zoom / (oldW / _originalWidth);
            _scrollViewer.Offset = new Vector(
                Math.Max(0, (_scrollViewer.Offset.X + vw / 2.0) * ratio - vw / 2.0),
                Math.Max(0, (_scrollViewer.Offset.Y + vh / 2.0) * ratio - vh / 2.0));
        }

        if (ViewModel is { } vm)
            vm.ZoomLevel = zoom;
    }

    public double GetZoom()
    {
        if (_imageControl is null || _originalWidth <= 0) return 1.0;
        return _imageControl.Width / _originalWidth;
    }

    /// <summary>
    /// Ctrl+滚轮缩放。普通滚轮由 ScrollViewer 原生处理。
    /// </summary>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.Control)
        {
            var factor = e.Delta.Y > 0 ? 1.15 : 1.0 / 1.15;
            ApplyZoom(GetZoom() * factor);
            e.Handled = true;
        }
        // 不设 Handled → ScrollViewer 原生滚动
        base.OnPointerWheelChanged(e);
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

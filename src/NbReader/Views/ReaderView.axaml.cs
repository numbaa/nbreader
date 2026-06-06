using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using NbReader.ViewModels;

namespace NbReader.Views;

/// <summary>
/// 阅读器视图：图片缩放、平移、翻页。
/// </summary>
public partial class ReaderView : UserControl
{
    private Image? _imageControl;
    private ScrollViewer? _scrollViewer;
    private Avalonia.Point _lastMousePosition;
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
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is ReaderViewModel vm)
        {
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(ReaderViewModel.DisplayBitmap))
                {
                    SetImageSource(vm.DisplayBitmap);
                }
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
            ResetView();
        }
    }

    /// <summary>
    /// 重置视图（缩放 1:1，居中）。
    /// </summary>
    public void ResetView()
    {
        if (_imageControl is null || _scrollViewer is null) return;

        _imageControl.RenderTransform = new ScaleTransform(1.0, 1.0);
        _scrollViewer.Offset = new Vector(0, 0);
    }

    /// <summary>
    /// 以视口中心为锚点设置缩放。
    /// </summary>
    private void SetZoom(double zoom)
    {
        if (_imageControl is null || _scrollViewer is null) return;

        zoom = Math.Clamp(zoom, 0.1, 10.0);

        var viewportW = _scrollViewer.Viewport.Width;
        var viewportH = _scrollViewer.Viewport.Height;
        var centerX = viewportW / 2.0 + _scrollViewer.Offset.X;
        var centerY = viewportH / 2.0 + _scrollViewer.Offset.Y;

        var oldZoom = (_imageControl.RenderTransform as ScaleTransform)?.ScaleX ?? 1.0;
        if (Math.Abs(oldZoom - zoom) < 0.0001) return;

        var ratio = zoom / oldZoom;

        _imageControl.RenderTransform = new ScaleTransform(zoom, zoom);

        var newOffsetX = centerX * ratio - viewportW / 2.0;
        var newOffsetY = centerY * ratio - viewportH / 2.0;
        _scrollViewer.Offset = new Vector(Math.Max(0, newOffsetX), Math.Max(0, newOffsetY));

        if (DataContext is ReaderViewModel vm)
        {
            vm.ZoomLevel = zoom;
        }
    }

    /// <summary>
    /// Ctrl+滚轮缩放，普通滚轮垂直滚动。
    /// </summary>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (e.KeyModifiers == KeyModifiers.Control)
        {
            var oldZoom = (_imageControl?.RenderTransform as ScaleTransform)?.ScaleX ?? 1.0;
            var factor = e.Delta.Y > 0 ? 1.15 : 1.0 / 1.15;
            SetZoom(oldZoom * factor);
            e.Handled = true;
        }
        else if (_scrollViewer is not null)
        {
            var delta = e.Delta.Y * 40;
            _scrollViewer.Offset = new Vector(
                _scrollViewer.Offset.X,
                Math.Max(0, _scrollViewer.Offset.Y - delta));
            e.Handled = true;
        }
    }

    /// <summary>
    /// 鼠标中键拖拽平移。
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsMiddleButtonPressed)
        {
            _isPanning = true;
            _lastMousePosition = e.GetPosition(this);
            Cursor = new Cursor(StandardCursorType.SizeAll);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!_isPanning || _scrollViewer is null) return;

        var currentPos = e.GetPosition(this);
        var delta = _lastMousePosition - currentPos;

        _scrollViewer.Offset = new Vector(
            _scrollViewer.Offset.X + delta.X,
            _scrollViewer.Offset.Y + delta.Y);

        _lastMousePosition = currentPos;
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _isPanning = false;
        Cursor = Cursor.Default;
    }

    /// <summary>
    /// 键盘快捷键：翻页、缩放。
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (DataContext is not ReaderViewModel vm) return;

        switch (e.Key)
        {
            case Key.Left:
            case Key.PageUp:
                vm.GoToPrevPageCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Right:
            case Key.PageDown:
            case Key.Space:
                vm.GoToNextPageCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.OemPlus or Key.Add when e.KeyModifiers == KeyModifiers.Control:
                SetZoom((_imageControl?.RenderTransform as ScaleTransform)?.ScaleX * 1.25 ?? 1.25);
                e.Handled = true;
                break;

            case Key.OemMinus or Key.Subtract when e.KeyModifiers == KeyModifiers.Control:
                SetZoom((_imageControl?.RenderTransform as ScaleTransform)?.ScaleX / 1.25 ?? 0.8);
                e.Handled = true;
                break;

            case Key.D0 or Key.NumPad0 when e.KeyModifiers == KeyModifiers.Control:
                ResetView();
                if (DataContext is ReaderViewModel vm2) vm2.ZoomLevel = 1.0;
                e.Handled = true;
                break;
        }
    }
}

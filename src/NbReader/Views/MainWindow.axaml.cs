using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NbReader.ViewModels;

namespace NbReader.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // 全局键盘 + 滚轮处理 — 隧道策略在子控件前优先捕获
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerWheelChangedEvent, OnWindowPointerWheel, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void OnWindowPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.Control) return;
        if (DataContext is not MainWindowViewModel mwvm) return;

        var reader = mwvm.Reader;
        if (reader is null) return;

        var factor = e.Delta.Y > 0 ? 1.15 : 1.0 / 1.15;
        reader.ZoomLevel = Math.Clamp(reader.ZoomLevel * factor, 0.1, 10.0);
        e.Handled = true;
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel mwvm) return;
        var reader = mwvm.Reader;
        if (reader is null) return;

        switch (e.Key)
        {
            case Key.Left:
            case Key.PageUp:
                if (reader.GoToPrevPageCommand.CanExecute(null))
                    reader.GoToPrevPageCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Right:
            case Key.PageDown:
            case Key.Space:
                if (reader.GoToNextPageCommand.CanExecute(null))
                    reader.GoToNextPageCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.OemPlus or Key.Add when e.KeyModifiers == KeyModifiers.Control:
                reader.ZoomInCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.OemMinus or Key.Subtract when e.KeyModifiers == KeyModifiers.Control:
                reader.ZoomOutCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.D0 or Key.NumPad0 when e.KeyModifiers == KeyModifiers.Control:
                reader.ZoomResetCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}
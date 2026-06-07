using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NbReader.ViewModels;

namespace NbReader.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

#if !DEBUG
        // 演示按钮仅在 Debug 模式下可见
        DemoButton.IsVisible = false;
#endif

        // 全局键盘 + 滚轮处理 — 隧道策略在子控件前优先捕获
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerWheelChangedEvent, OnWindowPointerWheel, RoutingStrategies.Tunnel, handledEventsToo: true);

        // 拖放文件支持
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    /// <summary>
    /// 📂 打开文件 — 选择 CBZ/ZIP 漫画文件。
    /// </summary>
    private async void OnOpenFileClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "打开漫画文件",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("漫画文件 (*.cbz, *.zip, *.cbr)") { Patterns = ["*.cbz", "*.zip", "*.cbr"] },
                new FilePickerFileType("所有文件") { Patterns = ["*.*"] }
            ]
        });

        if (files.Count > 0 && DataContext is MainWindowViewModel vm)
        {
            await vm.OpenFileAsync(files[0].Path.LocalPath);
        }
    }

    /// <summary>
    /// 📁 打开文件夹 — 选择包含图片的文件夹。
    /// </summary>
    private async void OnOpenFolderClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "打开图片文件夹",
            AllowMultiple = false
        });

        if (folders.Count > 0 && DataContext is MainWindowViewModel vm)
        {
            await vm.OpenFileAsync(folders[0].Path.LocalPath);
        }
    }

    /// <summary>
    /// 拖放悬停 — 仅接受文件/文件夹。
    /// </summary>
    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            e.DragEffects = DragDropEffects.Copy;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    /// <summary>
    /// 拖放释放 — 打开拖入的文件或文件夹。
    /// </summary>
    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            var files = e.Data.GetFiles();
            if (files is not null)
            {
                var fileList = files.ToList();
                if (fileList.Count > 0 && DataContext is MainWindowViewModel vm)
                {
                    var path = fileList[0].Path.LocalPath;
                    await vm.OpenFileAsync(path);
                }
            }
        }
        e.Handled = true;
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

            case Key.F11:
                WindowState = WindowState == WindowState.FullScreen
                    ? WindowState.Normal
                    : WindowState.FullScreen;
                e.Handled = true;
                break;
        }
    }
}
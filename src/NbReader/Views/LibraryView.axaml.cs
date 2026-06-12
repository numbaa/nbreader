using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NbReader.Core.Models;
using NbReader.ViewModels;

namespace NbReader.Views;

public partial class LibraryView : UserControl
{
    /// <summary>右键菜单触发时暂存的漫画资源。</summary>
    private ComicResource? _currentContextResource;

    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "NbReader", "contextmenu.log");

    public LibraryView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 拦截右键，在 ContextMenu 弹出前记录被点击的漫画。
    /// </summary>
    private void OnComicCardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border) return;
        // 只处理右键
        if (e.GetCurrentPoint(border).Properties.PointerUpdateKind != PointerUpdateKind.RightButtonPressed) return;

        _currentContextResource = border.DataContext as ComicResource;
        Log($"[PTR] resource={_currentContextResource?.Title ?? "NULL"}, id={_currentContextResource?.Id}");
    }

    /// <summary>
    /// 右键菜单打开后，给 MenuItem 挂 Click 并填充「移至分类」子菜单。
    /// </summary>
    private void OnComicCardContextMenuOpened(object? sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu)
        {
            Log("[MENU] sender is not ContextMenu");
            return;
        }
        if (DataContext is not LibraryViewModel vm)
        {
            Log("[MENU] DataContext is not LibraryViewModel");
            return;
        }

        var placement = menu.PlacementTarget;
        Log($"[MENU] Opened, PlacementTarget={placement?.GetType().Name ?? "NULL"}, _currentResource={_currentContextResource?.Title ?? "NULL"}");

        // 给阅读/移除/从本分类移除 挂上 handler
        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            if (item.Name == "MenuRead")
            {
                item.Click -= OnReadClick;
                item.Click += OnReadClick;
            }
            else if (item.Name == "MenuRemove")
            {
                item.Click -= OnRemoveClick;
                item.Click += OnRemoveClick;
            }
            else if (item.Name == "MenuRemoveFromCat")
            {
                // 只在查看特定分类时显示
                item.IsVisible = vm.SelectedCategory is not null;
                item.Click -= OnRemoveFromCategoryClick;
                item.Click += OnRemoveFromCategoryClick;
            }
        }

        // 填充移至分类子菜单
        var moveItem = menu.Items.OfType<MenuItem>()
            .FirstOrDefault(m => m.Name == "MoveToCategoryMenu");
        if (moveItem is null) return;

        moveItem.Items.Clear();
        foreach (var cat in vm.Categories)
        {
            var catName = cat.Name;
            var catItem = new MenuItem { Header = $"📁 {catName}" };
            catItem.Click += (s, _) =>
            {
                Log($"[MOVE] category={catName}, resource={_currentContextResource?.Title ?? "NULL"}");
                if (_currentContextResource is not null)
                    vm.MoveToCategory(_currentContextResource, cat);
            };
            moveItem.Items.Add(catItem);
        }
    }

    private void OnReadClick(object? sender, RoutedEventArgs e)
    {
        Log($"[READ] resource={_currentContextResource?.Title ?? "NULL"}");
        if (_currentContextResource is null) return;
        if (DataContext is LibraryViewModel vm)
            vm.OpenComicCommand.Execute(_currentContextResource);
    }

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        Log($"[REMOVE] resource={_currentContextResource?.Title ?? "NULL"}");
        if (_currentContextResource is null) return;
        if (DataContext is LibraryViewModel vm)
            vm.RemoveFromLibraryCommand.Execute(_currentContextResource);
    }

    private void OnRemoveFromCategoryClick(object? sender, RoutedEventArgs e)
    {
        Log($"[REMOVE_CAT] resource={_currentContextResource?.Title ?? "NULL"}");
        if (_currentContextResource is null) return;
        if (DataContext is LibraryViewModel vm)
            vm.RemoveFromCurrentCategory(_currentContextResource);
    }

    /// <summary>
    /// 右键菜单「重命名」— 弹出输入框，确认后调用 ViewModel。
    /// </summary>
    private async void OnRenameCategoryClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;
        if (menuItem.DataContext is not Category category) return;
        if (DataContext is not LibraryViewModel vm) return;

        var dialog = new PromptDialog("重命名分类", $"请输入「{category.Name}」的新名称：", category.Name);

        var owner = VisualRoot as Window;
        if (owner is not null)
            await dialog.ShowDialog(owner);
        else
            await dialog.ShowDialog(owner!);

        if (dialog.Result is not null && dialog.Result != category.Name)
            vm.RenameCategory(category.Id, dialog.Result);
    }

    /// <summary>
    /// 双击漫画卡片 → 打开阅读器。
    /// </summary>
    private void OnComicCardDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border border) return;
        if (border.DataContext is not ComicResource resource) return;
        if (DataContext is not LibraryViewModel vm) return;

        vm.OpenComicCommand.Execute(resource);
    }

    private static void Log(string msg)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath)!;
            Directory.CreateDirectory(dir);
            File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");
        }
        catch { /* 日志失败不影响主流程 */ }
    }
}

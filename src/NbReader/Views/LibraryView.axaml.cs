using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using NbReader.Core.Models;
using NbReader.ViewModels;

namespace NbReader.Views;

public partial class LibraryView : UserControl
{
    /// <summary>右键菜单触发时暂存的漫画资源。</summary>
    private ComicResource? _currentContextResource;

    /// <summary>拖拽中的漫画资源。</summary>
    private ComicResource? _draggingResource;

    /// <summary>拖拽起始点（用于判断是否移动足够距离以启动拖拽）。</summary>
    private Point _dragStartPoint;

    /// <summary>是否正在拖拽。</summary>
    private bool _isDragging;

    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "NbReader", "contextmenu.log");

    public LibraryView()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, OnCategoryDrop);
        AddHandler(DragDrop.DragOverEvent, OnCategoryDragOver);
    }

    /// <summary>
    /// 拦截指针按下：右键记录上下文菜单项，左键启动拖拽检测。
    /// </summary>
    private void OnComicCardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border) return;

        var point = e.GetCurrentPoint(border);
        var kind = point.Properties.PointerUpdateKind;

        // 右键：记录上下文菜单数据
        if (kind == PointerUpdateKind.RightButtonPressed)
        {
            _currentContextResource = border.DataContext as ComicResource;
            Log($"[PTR] resource={_currentContextResource?.Title ?? "NULL"}, id={_currentContextResource?.Id}");
            return;
        }

        // 左键：启动拖拽检测
        if (kind == PointerUpdateKind.LeftButtonPressed)
        {
            _draggingResource = border.DataContext as ComicResource;
            _dragStartPoint = e.GetPosition(this);
            _isDragging = false;

            // 捕获指针以接收后续 PointerMoved 事件
            e.Pointer?.Capture(border);

            // 订阅 PointerMoved 以检测拖拽
            border.PointerMoved += OnComicCardPointerMoved;
            border.PointerReleased += OnComicCardPointerReleased;
            border.PointerCaptureLost += OnComicCardPointerCaptureLost;
        }
    }

    /// <summary>
    /// 检测拖拽：移动足够距离后启动 DragDrop。
    /// </summary>
    private async void OnComicCardPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDragging || _draggingResource is null) return;

        var currentPoint = e.GetPosition(this);
        var delta = currentPoint - _dragStartPoint;

        // 移动超过 5px 阈值才启动拖拽
        if (Math.Abs(delta.X) < 5 && Math.Abs(delta.Y) < 5) return;

        _isDragging = true;

        // 取消事件订阅
        if (sender is Border border)
        {
            border.PointerMoved -= OnComicCardPointerMoved;
            border.PointerReleased -= OnComicCardPointerReleased;
            border.PointerCaptureLost -= OnComicCardPointerCaptureLost;
            e.Pointer?.Capture(null);
        }

        // 启动 Avalonia 拖拽
        var data = new DataObject();
        data.Set("ComicResource", _draggingResource);
        await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);

        _draggingResource = null;
        _isDragging = false;
    }

    /// <summary>
    /// 指针释放：取消耗未完成的拖拽。
    /// </summary>
    private void OnComicCardPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        CleanupDrag(sender);
    }

    /// <summary>
    /// 指针捕获丢失：取消耗未完成的拖拽。
    /// </summary>
    private void OnComicCardPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        CleanupDrag(sender);
    }

    /// <summary>
    /// 清理拖拽状态。
    /// </summary>
    private void CleanupDrag(object? sender)
    {
        if (sender is Border border)
        {
            border.PointerMoved -= OnComicCardPointerMoved;
            border.PointerReleased -= OnComicCardPointerReleased;
            border.PointerCaptureLost -= OnComicCardPointerCaptureLost;
        }
        _draggingResource = null;
        _isDragging = false;
    }

    /// <summary>
    /// 拖拽悬停到分类名上时，显示允许放入的图标。
    /// </summary>
    private void OnCategoryDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains("ComicResource"))
        {
            e.DragEffects = DragDropEffects.Move;
            e.Handled = true;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    /// <summary>
    /// 拖拽放入分类名 → 将漫画归入该分类。
    /// </summary>
    private void OnCategoryDrop(object? sender, DragEventArgs e)
    {
        var resource = e.Data.Get("ComicResource") as ComicResource;
        if (resource is null)
        {
            Log("[DRAG_DROP] No ComicResource in data");
            return;
        }
        if (DataContext is not LibraryViewModel vm)
        {
            Log("[DRAG_DROP] DataContext is not LibraryViewModel");
            return;
        }

        // 尝试从事件源或其父级找到分类按钮
        Category? category = null;

        if (e.Source is Button btn && btn.DataContext is Category cat)
        {
            category = cat;
        }
        else if (e.Source is StyledElement se)
        {
            // 向上遍历可视化树查找分类按钮
            var parent = se.Parent;
            while (parent is not null)
            {
                if (parent is Button parentBtn && parentBtn.DataContext is Category parentCat)
                {
                    category = parentCat;
                    break;
                }
                parent = parent.Parent;
            }
        }

        if (category is not null)
        {
            vm.MoveToCategory(resource, category);
            Log($"[DRAG_DROP] resource={resource.Title}, category={category.Name}");
        }
        else
        {
            Log($"[DRAG_DROP] No category found, Source={e.Source?.GetType().Name}");
        }
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

    /// <summary>
    /// 「扫描目录」按钮 — 打开文件夹选择器，扫描选中的目录。
    /// </summary>
    private async void OnScanDirectoryClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not LibraryViewModel vm) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
            new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "选择要扫描的漫画目录",
                AllowMultiple = false
            });

        if (folders.Count == 0) return;

        var path = folders[0].Path.LocalPath;
        await vm.ScanDirectoryAndRefreshAsync(path);
    }

    /// <summary>
    /// 移除筛选 Chip。
    /// </summary>
    private void OnRemoveFilterClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        if (button.DataContext is not ActiveFilter filter) return;
        if (DataContext is not LibraryViewModel vm) return;

        vm.RemoveFilter(filter);
    }

    /// <summary>
    /// 「+ 筛选」中添加语言筛选。
    /// </summary>
    private void OnAddLanguageFilterClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        if (button.DataContext is not string langCode) return;
        if (DataContext is not LibraryViewModel vm) return;

        vm.AddLanguageFilter(langCode);
        vm.IsFilterDropdownOpen = false;
    }

    /// <summary>
    /// 「+ 筛选」中添加内容类型筛选。
    /// </summary>
    private void OnAddContentTypeFilterClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        if (button.DataContext is not string contentType) return;
        if (DataContext is not LibraryViewModel vm) return;

        vm.AddContentTypeFilter(contentType);
        vm.IsFilterDropdownOpen = false;
    }
}

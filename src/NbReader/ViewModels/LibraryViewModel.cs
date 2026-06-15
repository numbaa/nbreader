using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NbReader.Core.Abstractions;
using NbReader.Core.Models;
using NbReader.Core.Services;

namespace NbReader.ViewModels;

/// <summary>
/// 书架视图模型：管理漫画列表、分类导航、筛选和排序。
/// </summary>
public partial class LibraryViewModel : ViewModelBase
{
    private readonly IStorageService _storage;
    private readonly LibraryScanner _scanner;
    private readonly Action<string> _openComicCallback;

    /// <summary>漫画列表</summary>
    public ObservableCollection<ComicResource> Books { get; } = new();

    /// <summary>分类列表（含计数）</summary>
    public ObservableCollection<Category> Categories { get; } = new();

    /// <summary>当前选中的分类（null = 全部）</summary>
    [ObservableProperty]
    private Category? _selectedCategory;

    /// <summary>搜索文本</summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>排序字段</summary>
    [ObservableProperty]
    private string _sortBy = "added";

    /// <summary>是否降序</summary>
    [ObservableProperty]
    private bool _sortDescending = true;

    /// <summary>是否为网格视图（false = 列表）</summary>
    [ObservableProperty]
    private bool _isGridView = true;

    /// <summary>筛选语言</summary>
    [ObservableProperty]
    private string? _filterLanguage;

    /// <summary>筛选内容类型</summary>
    [ObservableProperty]
    private string? _filterContentType;

    /// <summary>书架上漫画总数</summary>
    [ObservableProperty]
    private int _totalCount;

    /// <summary>全部漫画总数（不受筛选影响）</summary>
    [ObservableProperty]
    private int _allBooksCount;

    /// <summary>空状态提示</summary>
    [ObservableProperty]
    private string _emptyMessage = "拖放漫画文件或添加监控目录开始阅读";

    /// <summary>书架是否为空</summary>
    [ObservableProperty]
    private bool _isEmpty = true;

    /// <summary>排序选项文本</summary>
    public string SortByText => SortBy switch
    {
        "read" => "最近阅读",
        "title" => "标题 A-Z",
        "pages" => "页数",
        _ => "最近添加"
    };

    // ═══════════════════════════════════════════════════════════
    // 筛选 Chip
    // ═══════════════════════════════════════════════════════════

    /// <summary>活跃的筛选 Chip 列表</summary>
    public ObservableCollection<ActiveFilter> ActiveFilters { get; } = new();

    /// <summary>可用的语言选项</summary>
    public ObservableCollection<string> AvailableLanguages { get; } = new();

    /// <summary>可用的内容类型选项</summary>
    public ObservableCollection<string> AvailableContentTypes { get; } = new();

    /// <summary>可用的标签选项</summary>
    public ObservableCollection<Tag> AvailableTags { get; } = new();

    /// <summary>筛选下拉面板是否打开</summary>
    [ObservableProperty]
    private bool _isFilterDropdownOpen;

    /// <summary>是否有活跃的筛选</summary>
    public bool HasActiveFilters => ActiveFilters.Count > 0;

    public LibraryViewModel(IStorageService storage, Action<string> openComicCallback,
        LibraryScanner? scanner = null)
    {
        _storage = storage;
        _scanner = scanner ?? new LibraryScanner(storage, new ComicInfoXmlParser());
        _openComicCallback = openComicCallback;
    }

    /// <summary>
    /// 刷新书架数据。
    /// </summary>
    public void Refresh()
    {
        LoadCategories();
        RefreshAllCount();
        LoadBooks();
        LoadAvailableFilters();
    }

    /// <summary>
    /// 加载分类列表。
    /// </summary>
    private void LoadCategories()
    {
        var cats = _storage.GetCategories();
        Categories.Clear();
        foreach (var c in cats)
            Categories.Add(c);
    }

    /// <summary>
    /// 加载漫画列表。
    /// </summary>
    private void LoadBooks()
    {
        List<ComicResource> books;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            books = _storage.SearchLibrary(SearchText);
        }
        else
        {
            var query = new LibraryQuery
            {
                CategoryId = SelectedCategory?.Id,
                Language = FilterLanguage,
                ContentType = FilterContentType,
                SortBy = SortBy,
                Descending = SortDescending
            };
            books = _storage.GetLibrary(query);
        }

        Books.Clear();
        foreach (var b in books)
            Books.Add(b);

        TotalCount = Books.Count;
        IsEmpty = TotalCount == 0;
        EmptyMessage = IsEmpty
            ? "书架是空的，拖放漫画文件或添加监控目录开始阅读"
            : "";
    }

    /// <summary>
    /// 刷新全部计数（不受筛选影响）。
    /// </summary>
    private void RefreshAllCount()
    {
        var all = _storage.GetLibrary(new LibraryQuery());
        AllBooksCount = all.Count;
    }

    /// <summary>
    /// 选中分类筛选。
    /// </summary>
    [RelayCommand]
    private void SelectCategory(Category? category)
    {
        SelectedCategory = category;
        LoadBooks();
    }

    /// <summary>
    /// 创建新分类。
    /// </summary>
    [RelayCommand]
    private void CreateCategory()
    {
        // 简化实现：直接用内置名称。后续可改为弹出输入框。
        var name = $"新分类 {Categories.Count + 1}";
        _storage.CreateCategory(name);
        LoadCategories();
    }

    /// <summary>
    /// 删除分类。
    /// </summary>
    [RelayCommand]
    private void DeleteCategory(Category category)
    {
        _storage.DeleteCategory(category.Id);
        if (SelectedCategory?.Id == category.Id)
            SelectedCategory = null;
        LoadCategories();
        LoadBooks();
    }

    /// <summary>
    /// 重命名分类（由 View 层弹窗获取新名称后调用）。
    /// </summary>
    public void RenameCategory(int categoryId, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return;
        _storage.RenameCategory(categoryId, newName.Trim());
        LoadCategories();
    }

    /// <summary>
    /// 上移分类。
    /// </summary>
    [RelayCommand]
    private void MoveCategoryUp(Category category)
    {
        var cats = _storage.GetCategories();
        var index = cats.FindIndex(c => c.Id == category.Id);
        if (index <= 0) return;

        (cats[index - 1], cats[index]) = (cats[index], cats[index - 1]);
        _storage.ReorderCategories(cats.Select(c => c.Id).ToList());
        LoadCategories();
    }

    /// <summary>
    /// 下移分类。
    /// </summary>
    [RelayCommand]
    private void MoveCategoryDown(Category category)
    {
        var cats = _storage.GetCategories();
        var index = cats.FindIndex(c => c.Id == category.Id);
        if (index < 0 || index >= cats.Count - 1) return;

        (cats[index + 1], cats[index]) = (cats[index], cats[index + 1]);
        _storage.ReorderCategories(cats.Select(c => c.Id).ToList());
        LoadCategories();
    }

    /// <summary>
    /// 打开漫画。
    /// </summary>
    [RelayCommand]
    private void OpenComic(ComicResource resource)
    {
        var path = resource.IsDownloaded ? resource.LocalPath : resource.SourceId;
        if (path is not null)
            _openComicCallback(path);
    }

    /// <summary>
    /// 从书架移除漫画（不删除文件）。
    /// </summary>
    [RelayCommand]
    private void RemoveFromLibrary(ComicResource resource)
    {
        _storage.DeleteResource(resource.Id);
        Books.Remove(resource);
        TotalCount = Books.Count;
        LoadCategories();
        RefreshAllCount();
    }

    /// <summary>
    /// 将漫画移至指定分类（由右键菜单调用）。
    /// </summary>
    public void MoveToCategory(ComicResource resource, Category category)
    {
        _storage.AddToCategory(resource.Id, category.Id);
        LoadCategories();
        RefreshAllCount();
    }

    /// <summary>
    /// 从当前选中的分类中移除漫画（不删除漫画本身）。
    /// </summary>
    public void RemoveFromCurrentCategory(ComicResource resource)
    {
        if (SelectedCategory is null) return;
        _storage.RemoveFromCategory(resource.Id, SelectedCategory.Id);
        LoadCategories();
        LoadBooks();
    }

    /// <summary>
    /// 切换网格/列表视图。
    /// </summary>
    [RelayCommand]
    private void ToggleView()
    {
        IsGridView = !IsGridView;
    }

    /// <summary>
    /// 循环排序方式。
    /// </summary>
    [RelayCommand]
    private void CycleSort()
    {
        SortBy = SortBy switch
        {
            "added" => "read",
            "read" => "title",
            "title" => "pages",
            _ => "added"
        };
        OnPropertyChanged(nameof(SortByText));
        LoadBooks();
    }

    /// <summary>
    /// 执行搜索。
    /// </summary>
    [RelayCommand]
    private void Search()
    {
        LoadBooks();
    }

    /// <summary>
    /// 清除搜索。
    /// </summary>
    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
        LoadBooks();
    }

    // ═══════════════════════════════════════════════════════════
    // 筛选 Chip 管理
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 加载可用筛选选项。
    /// </summary>
    private void LoadAvailableFilters()
    {
        // 可用语言
        var languages = _storage.GetDistinctLanguages();
        AvailableLanguages.Clear();
        foreach (var lang in languages)
            AvailableLanguages.Add(lang);

        // 可用内容类型
        var contentTypes = _storage.GetDistinctContentTypes();
        AvailableContentTypes.Clear();
        foreach (var ct in contentTypes)
            AvailableContentTypes.Add(ct);

        // 可用标签
        var tags = _storage.GetAllTags();
        AvailableTags.Clear();
        foreach (var tag in tags)
            AvailableTags.Add(tag);
    }

    /// <summary>
    /// 获取语言的显示名称。
    /// </summary>
    public static string GetLanguageDisplayName(string langCode) => langCode switch
    {
        "zh" => "中文",
        "ja" => "日文",
        "en" => "英文",
        "ko" => "韩文",
        _ => langCode
    };

    /// <summary>
    /// 获取内容类型的显示名称。
    /// </summary>
    public static string GetContentTypeDisplayName(string ct) => ct switch
    {
        "manga" => "漫画",
        "doujinshi" => "同人志",
        "artist cg" => "画集",
        "comic" => "美漫",
        _ => ct
    };

    /// <summary>
    /// 添加语言筛选 Chip。
    /// </summary>
    public void AddLanguageFilter(string langCode)
    {
        // 移除已有的语言筛选
        RemoveFiltersByType("language");
        FilterLanguage = langCode;
        ActiveFilters.Add(new ActiveFilter
        {
            FilterType = "language",
            Label = GetLanguageDisplayName(langCode),
            Value = langCode
        });
        OnPropertyChanged(nameof(HasActiveFilters));
        LoadBooks();
    }

    /// <summary>
    /// 添加内容类型筛选 Chip。
    /// </summary>
    public void AddContentTypeFilter(string contentType)
    {
        RemoveFiltersByType("content_type");
        FilterContentType = contentType;
        ActiveFilters.Add(new ActiveFilter
        {
            FilterType = "content_type",
            Label = GetContentTypeDisplayName(contentType),
            Value = contentType
        });
        OnPropertyChanged(nameof(HasActiveFilters));
        LoadBooks();
    }

    /// <summary>
    /// 移除指定筛选 Chip。
    /// </summary>
    public void RemoveFilter(ActiveFilter filter)
    {
        ActiveFilters.Remove(filter);

        if (filter.FilterType == "language")
            FilterLanguage = null;
        else if (filter.FilterType == "content_type")
            FilterContentType = null;

        OnPropertyChanged(nameof(HasActiveFilters));
        LoadBooks();
    }

    /// <summary>
    /// 移除所有指定类型的筛选。
    /// </summary>
    private void RemoveFiltersByType(string filterType)
    {
        var toRemove = ActiveFilters.Where(f => f.FilterType == filterType).ToList();
        foreach (var f in toRemove)
            ActiveFilters.Remove(f);
    }

    /// <summary>
    /// 切换筛选下拉面板。
    /// </summary>
    [RelayCommand]
    private void ToggleFilterDropdown()
    {
        IsFilterDropdownOpen = !IsFilterDropdownOpen;
    }

    // ═══════════════════════════════════════════════════════════
    // 本地扫描
    // ═══════════════════════════════════════════════════════════

    /// <summary>是否正在扫描</summary>
    [ObservableProperty]
    private bool _isScanning;

    /// <summary>扫描状态文本</summary>
    [ObservableProperty]
    private string _scanStatusText = string.Empty;

    /// <summary>
    /// 扫描指定目录，导入发现的漫画。
    /// 由 View 层调用（通过文件夹选择器获取路径后）。
    /// </summary>
    public async Task ScanDirectoryAndRefreshAsync(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath)) return;

        IsScanning = true;
        ScanStatusText = "正在扫描...";

        try
        {
            var imported = await _scanner.ScanDirectoryAsync(directoryPath);
            ScanStatusText = imported.Count > 0
                ? $"扫描完成，导入了 {imported.Count} 本漫画"
                : "未发现新的漫画";
            Refresh();
        }
        catch (Exception ex)
        {
            ScanStatusText = $"扫描失败: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    /// <summary>
    /// 扫描所有已配置的监控目录。
    /// </summary>
    [RelayCommand]
    private async Task ScanAllAsync()
    {
        IsScanning = true;
        ScanStatusText = "正在扫描所有监控目录...";

        try
        {
            var imported = await _scanner.ScanAllAsync();
            ScanStatusText = imported.Count > 0
                ? $"扫描完成，导入了 {imported.Count} 本漫画"
                : "未发现新的漫画";
            Refresh();
        }
        catch (Exception ex)
        {
            ScanStatusText = $"扫描失败: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    /// <summary>
    /// 添加监控目录。
    /// </summary>
    public void AddMonitoredDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        _storage.AddMonitoredDirectory(path);
    }
}

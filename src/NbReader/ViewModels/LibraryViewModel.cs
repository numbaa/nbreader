using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NbReader.Core.Abstractions;
using NbReader.Core.Models;

namespace NbReader.ViewModels;

/// <summary>
/// 书架视图模型：管理漫画列表、分类导航、筛选和排序。
/// </summary>
public partial class LibraryViewModel : ViewModelBase
{
    private readonly IStorageService _storage;
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

    public LibraryViewModel(IStorageService storage, Action<string> openComicCallback)
    {
        _storage = storage;
        _openComicCallback = openComicCallback;
    }

    /// <summary>
    /// 刷新书架数据。
    /// </summary>
    public void Refresh()
    {
        LoadCategories();
        LoadBooks();
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
    /// 重命名分类。
    /// </summary>
    [RelayCommand]
    private void RenameCategory(Category category)
    {
        // 简化实现：追加时间戳。后续可改为弹出输入框。
        var newName = $"{category.Name}_重命名";
        _storage.RenameCategory(category.Id, newName);
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
}

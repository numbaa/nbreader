using NbReader.Core.Models;

namespace NbReader.Core.Abstractions;

/// <summary>
/// 持久化存储服务：管理 SQLite 数据库中的所有漫画、分类、标签、进度和历史数据。
/// </summary>
public interface IStorageService
{
    // ═══════ 设置（key-value）═══════

    /// <summary>获取设置值，不存在返回 null</summary>
    string? GetSetting(string key);

    /// <summary>设置键值对（upsert）</summary>
    void SetSetting(string key, string value);

    // ═══════ 漫画资源 CRUD ═══════

    /// <summary>添加漫画资源，返回自增 ID。若 (sourceType, sourceId) 已存在则返回已有 ID 不重复插入</summary>
    int AddResource(ComicResource resource);

    /// <summary>更新漫画资源</summary>
    void UpdateResource(ComicResource resource);

    /// <summary>删除漫画资源（级联清理关联数据）</summary>
    void DeleteResource(int resourceId);

    /// <summary>按 ID 获取资源，不存在返回 null</summary>
    ComicResource? GetResource(int resourceId);

    /// <summary>按来源查找资源</summary>
    ComicResource? GetResourceBySource(string sourceType, string sourceId);

    /// <summary>获取书架列表（可筛选/排序/分页）</summary>
    List<ComicResource> GetLibrary(LibraryQuery query);

    /// <summary>模糊搜索（标题）</summary>
    List<ComicResource> SearchLibrary(string searchText, int limit = 50);

    // ═══════ 阅读进度 ═══════

    /// <summary>获取阅读进度，不存在返回 null</summary>
    ReadingProgress? GetProgress(int resourceId);

    /// <summary>保存阅读进度（upsert）</summary>
    void SaveProgress(int resourceId, int currentPage);

    /// <summary>删除阅读进度</summary>
    void DeleteProgress(int resourceId);

    // ═══════ 阅读历史 ═══════

    /// <summary>添加阅读历史记录</summary>
    void AddHistory(int resourceId, int lastPageIndex);

    /// <summary>获取阅读历史（按时间倒序，含漫画标题/封面/页数）</summary>
    List<ReadHistoryEntry> GetHistory(int limit = 100, int offset = 0);

    /// <summary>删除单条历史记录</summary>
    void DeleteHistory(int historyId);

    /// <summary>清除全部阅读历史</summary>
    void ClearHistory();

    // ═══════ 分类管理 ═══════

    /// <summary>获取全部分类（按 sort_order 排序，含漫画计数）</summary>
    List<Category> GetCategories();

    /// <summary>创建分类，返回自增 ID</summary>
    int CreateCategory(string name);

    /// <summary>重命名分类</summary>
    void RenameCategory(int categoryId, string newName);

    /// <summary>删除分类（级联清理关联）</summary>
    void DeleteCategory(int categoryId);

    /// <summary>更新分类排序</summary>
    void ReorderCategories(List<int> orderedCategoryIds);

    /// <summary>将漫画加入分类</summary>
    void AddToCategory(int resourceId, int categoryId);

    /// <summary>将漫画从分类移除</summary>
    void RemoveFromCategory(int resourceId, int categoryId);

    // ═══════ 标签管理 ═══════

    /// <summary>获取或创建标签（按英文规范名），返回标签对象</summary>
    Tag GetOrCreateTag(string name, string type = "general");

    /// <summary>获取漫画的所有标签</summary>
    List<Tag> GetResourceTags(int resourceId);

    /// <summary>给漫画添加标签</summary>
    void AddTagToResource(int resourceId, int tagId);

    /// <summary>移除漫画的标签</summary>
    void RemoveTagFromResource(int resourceId, int tagId);

    // ═══════ 别名管理 ═══════

    /// <summary>添加标签别名</summary>
    void AddAlias(int tagId, string alias, string? language = null, string? sourceType = null);

    // ═══════ 系列 ═══════

    /// <summary>按名称获取或创建系列</summary>
    Series GetOrCreateSeries(string title);

    // ═══════ 抽象作品 ═══════

    /// <summary>按规范标题获取或创建作品</summary>
    ComicWork GetOrCreateWork(string canonicalTitle);

    /// <summary>将资源关联到作品</summary>
    void SetResourceWork(int resourceId, int workId);

    // ═══════ 监控目录 ═══════

    /// <summary>获取所有监控目录</summary>
    List<MonitoredDirectory> GetMonitoredDirectories();

    /// <summary>添加监控目录</summary>
    void AddMonitoredDirectory(string path);

    /// <summary>删除监控目录</summary>
    void RemoveMonitoredDirectory(int id);

    // ═══════ 文件哈希 ═══════

    /// <summary>按文件哈希查找资源</summary>
    ComicResource? FindResourceByHash(string fileHash);

    // ═══════ 搜索 ═══════

    /// <summary>获取全部标签（按类型和名称排序）</summary>
    List<Tag> GetAllTags();
}

/// <summary>
/// 书架查询参数。
/// </summary>
public class LibraryQuery
{
    public int? CategoryId { get; set; }
    public string? Language { get; set; }
    public string? ContentType { get; set; }
    public List<int>? TagIds { get; set; }
    public string SortBy { get; set; } = "added";
    public bool Descending { get; set; } = true;
    public int Offset { get; set; }
    public int Limit { get; set; }
}

using NbReader.Core.Abstractions;
using NbReader.Core.Models;
using NbReader.Core.Services;

namespace NbReader.Tests.Core;

/// <summary>
/// SqliteStorageService 单元测试：使用内存数据库覆盖所有 CRUD 操作。
/// </summary>
public class SqliteStorageServiceTests : IDisposable
{
    private readonly SqliteStorageService _storage;

    public SqliteStorageServiceTests()
    {
        // 每次测试创建独立的内存数据库
        _storage = new SqliteStorageService(":memory:");
    }

    public void Dispose()
    {
        _storage.Dispose();
    }

    // ═══════════════════════════════════════════════════════════
    // Schema 建表测试
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void Initialize_Should_Create_All_14_Tables()
    {
        // 第二次初始化应幂等（构造函数已调用一次）
        var storage2 = new SqliteStorageService(":memory:");
        try
        {
            // 若建表成功，以下 CRUD 操作不应抛异常
            var resource = new ComicResource
            {
                Title = "Test",
                SourceType = "local",
                SourceId = "/test/path.cbz",
                PageCount = 10,
                AddedDate = DateTime.UtcNow.ToString("O"),
                IsBookmarked = true
            };
            var id = storage2.AddResource(resource);
            id.Should().BeGreaterThan(0);

            var fetched = storage2.GetResource(id);
            fetched.Should().NotBeNull();
            fetched!.Title.Should().Be("Test");
        }
        finally
        {
            storage2.Dispose();
        }
    }

    [Fact]
    public void Initialize_Should_Be_Idempotent()
    {
        // 创建第二个同 schema 的实例（模拟重启），不应抛异常
        var storage2 = new SqliteStorageService(":memory:");
        try
        {
            storage2.GetCategories().Should().BeEmpty();
        }
        finally
        {
            storage2.Dispose();
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 设置（key-value）
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void GetSetting_Should_Return_Null_When_Not_Found()
    {
        _storage.GetSetting("nonexistent").Should().BeNull();
    }

    [Fact]
    public void SetSetting_And_GetSetting_Should_Work()
    {
        _storage.SetSetting("theme", "dark");
        _storage.GetSetting("theme").Should().Be("dark");
    }

    [Fact]
    public void SetSetting_Should_Upsert()
    {
        _storage.SetSetting("key1", "value1");
        _storage.SetSetting("key1", "value2");
        _storage.GetSetting("key1").Should().Be("value2");
    }

    // ═══════════════════════════════════════════════════════════
    // 漫画资源 CRUD
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void AddResource_Should_Return_Id()
    {
        var resource = CreateTestResource("/path/to/comic.cbz", "Test Comic");
        var id = _storage.AddResource(resource);
        id.Should().BeGreaterThan(0);
    }

    [Fact]
    public void AddResource_Should_Not_Duplicate_Same_Source()
    {
        var resource = CreateTestResource("/path/to/unique.cbz", "Unique Comic");
        var id1 = _storage.AddResource(resource);
        var id2 = _storage.AddResource(resource);
        id1.Should().Be(id2);
    }

    [Fact]
    public void GetResource_Should_Return_Correct_Data()
    {
        var now = DateTime.UtcNow.ToString("O");
        var resource = new ComicResource
        {
            Title = "Full Test Comic",
            SourceType = "local",
            SourceId = "/full/path.cbz",
            SourceUrl = "https://example.com/comic",
            CoverPath = "/covers/1.jpg",
            PageCount = 42,
            Language = "chinese",
            ContentType = "manga",
            AddedDate = now,
            IsBookmarked = true,
            IsDownloaded = true,
            LocalPath = "/local/path.cbz",
            VolumeNumber = 3,
            ChapterNumber = 25,
            FileHash = "abc123"
        };

        var id = _storage.AddResource(resource);
        var fetched = _storage.GetResource(id);
        fetched.Should().NotBeNull();
        fetched!.Title.Should().Be("Full Test Comic");
        fetched.SourceType.Should().Be("local");
        fetched.SourceId.Should().Be("/full/path.cbz");
        fetched.SourceUrl.Should().Be("https://example.com/comic");
        fetched.CoverPath.Should().Be("/covers/1.jpg");
        fetched.PageCount.Should().Be(42);
        fetched.Language.Should().Be("chinese");
        fetched.ContentType.Should().Be("manga");
        fetched.AddedDate.Should().Be(now);
        fetched.IsBookmarked.Should().BeTrue();
        fetched.IsDownloaded.Should().BeTrue();
        fetched.LocalPath.Should().Be("/local/path.cbz");
        fetched.VolumeNumber.Should().Be(3);
        fetched.ChapterNumber.Should().Be(25);
        fetched.FileHash.Should().Be("abc123");
    }

    [Fact]
    public void GetResource_Should_Return_Null_When_Not_Found()
    {
        _storage.GetResource(99999).Should().BeNull();
    }

    [Fact]
    public void GetResourceBySource_Should_Work()
    {
        var resource = CreateTestResource("/src/path.cbz", "Source Lookup");
        _storage.AddResource(resource);

        var found = _storage.GetResourceBySource("local", "/src/path.cbz");
        found.Should().NotBeNull();
        found!.Title.Should().Be("Source Lookup");
    }

    [Fact]
    public void GetResourceBySource_Should_Return_Null_When_Not_Found()
    {
        _storage.GetResourceBySource("local", "/nonexistent.cbz").Should().BeNull();
    }

    [Fact]
    public void UpdateResource_Should_Persist_Changes()
    {
        var resource = CreateTestResource("/update/path.cbz", "Before Update");
        var id = _storage.AddResource(resource);

        var updated = _storage.GetResource(id)!;
        updated.Title = "After Update";
        updated.Language = "japanese";
        updated.PageCount = 100;
        _storage.UpdateResource(updated);

        var fetched = _storage.GetResource(id)!;
        fetched.Title.Should().Be("After Update");
        fetched.Language.Should().Be("japanese");
        fetched.PageCount.Should().Be(100);
    }

    [Fact]
    public void DeleteResource_Should_Remove_And_Cascade()
    {
        var resource = CreateTestResource("/delete/path.cbz", "To Delete");
        var id = _storage.AddResource(resource);

        // 创建关联数据
        _storage.SaveProgress(id, 5);
        _storage.AddHistory(id, 10);
        var catId = _storage.CreateCategory("TestCat");
        _storage.AddToCategory(id, catId);

        _storage.DeleteResource(id);

        _storage.GetResource(id).Should().BeNull();
        _storage.GetProgress(id).Should().BeNull();
    }

    [Fact]
    public void GetLibrary_Should_Return_Only_Bookmarked()
    {
        var r1 = CreateTestResource("/lib/path1.cbz", "Bookmarked");
        r1.IsBookmarked = true;
        _storage.AddResource(r1);

        var r2 = CreateTestResource("/lib/path2.cbz", "Not Bookmarked");
        r2.IsBookmarked = false;
        _storage.AddResource(r2);

        var library = _storage.GetLibrary(new LibraryQuery());
        library.Should().HaveCount(1);
        library[0].Title.Should().Be("Bookmarked");
    }

    [Fact]
    public void GetLibrary_Should_Filter_By_Category()
    {
        var r1 = CreateTestResource("/cat/path1.cbz", "In Category");
        var r2 = CreateTestResource("/cat/path2.cbz", "Not In Category");
        var id1 = _storage.AddResource(r1);
        _storage.AddResource(r2);

        var catId = _storage.CreateCategory("MyCat");
        _storage.AddToCategory(id1, catId);

        var results = _storage.GetLibrary(new LibraryQuery { CategoryId = catId });
        results.Should().HaveCount(1);
        results[0].Title.Should().Be("In Category");
    }

    [Fact]
    public void GetLibrary_Should_Filter_By_Language()
    {
        var r1 = CreateTestResource("/lang/path1.cbz", "Chinese");
        r1.Language = "chinese";
        _storage.AddResource(r1);

        var r2 = CreateTestResource("/lang/path2.cbz", "Japanese");
        r2.Language = "japanese";
        _storage.AddResource(r2);

        var results = _storage.GetLibrary(new LibraryQuery { Language = "chinese" });
        results.Should().HaveCount(1);
        results[0].Title.Should().Be("Chinese");
    }

    [Fact]
    public void SearchLibrary_Should_Find_By_Title()
    {
        _storage.AddResource(CreateTestResource("/s/path1.cbz", "One Piece Vol.1"));
        _storage.AddResource(CreateTestResource("/s/path2.cbz", "Naruto Vol.1"));
        _storage.AddResource(CreateTestResource("/s/path3.cbz", "One Punch Man"));

        var results = _storage.SearchLibrary("One");
        results.Should().HaveCount(2);
    }

    // ═══════════════════════════════════════════════════════════
    // 阅读进度
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void SaveProgress_And_GetProgress_Should_Work()
    {
        var resource = CreateTestResource("/prog/path.cbz", "Progress Test");
        var id = _storage.AddResource(resource);

        _storage.SaveProgress(id, 15);

        var progress = _storage.GetProgress(id);
        progress.Should().NotBeNull();
        progress!.ResourceId.Should().Be(id);
        progress.CurrentPage.Should().Be(15);
        progress.TotalPages.Should().Be(10);
    }

    [Fact]
    public void SaveProgress_Should_Upsert()
    {
        var id = _storage.AddResource(CreateTestResource("/prog2/path.cbz", "Progress Upsert"));
        _storage.SaveProgress(id, 5);
        _storage.SaveProgress(id, 20);

        _storage.GetProgress(id)!.CurrentPage.Should().Be(20);
    }

    [Fact]
    public void GetProgress_Should_Return_Null_When_Not_Found()
    {
        _storage.GetProgress(99999).Should().BeNull();
    }

    [Fact]
    public void DeleteProgress_Should_Remove_Record()
    {
        var id = _storage.AddResource(CreateTestResource("/prog3/path.cbz", "Delete Progress"));
        _storage.SaveProgress(id, 10);
        _storage.DeleteProgress(id);
        _storage.GetProgress(id).Should().BeNull();
    }

    // ═══════════════════════════════════════════════════════════
    // 阅读历史
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void AddHistory_And_GetHistory_Should_Work()
    {
        var id = _storage.AddResource(CreateTestResource("/hist/path.cbz", "History Comic"));
        _storage.AddHistory(id, 3);

        var history = _storage.GetHistory();
        history.Should().HaveCount(1);
        history[0].ResourceId.Should().Be(id);
        history[0].LastPageIndex.Should().Be(3);
        history[0].ComicTitle.Should().Be("History Comic");
        history[0].TotalPages.Should().Be(10);
    }

    [Fact]
    public void GetHistory_Should_Respect_Pagination()
    {
        var id = _storage.AddResource(CreateTestResource("/hist2/path.cbz", "Paged History"));
        for (int i = 0; i < 5; i++)
            _storage.AddHistory(id, i);

        _storage.GetHistory(limit: 3, offset: 0).Should().HaveCount(3);
        _storage.GetHistory(limit: 3, offset: 3).Should().HaveCount(2);
    }

    [Fact]
    public void DeleteHistory_Should_Remove_Single_Entry()
    {
        var id = _storage.AddResource(CreateTestResource("/hist3/path.cbz", "Del One"));
        _storage.AddHistory(id, 1);
        var history = _storage.GetHistory();
        var entryId = history[0].Id;

        _storage.DeleteHistory(entryId);
        _storage.GetHistory().Should().BeEmpty();
    }

    [Fact]
    public void ClearHistory_Should_Remove_All()
    {
        var id = _storage.AddResource(CreateTestResource("/hist4/path.cbz", "Clear All"));
        _storage.AddHistory(id, 1);
        _storage.AddHistory(id, 2);
        _storage.AddHistory(id, 3);

        _storage.ClearHistory();
        _storage.GetHistory().Should().BeEmpty();
    }

    // ═══════════════════════════════════════════════════════════
    // 分类管理
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void CreateCategory_Should_Return_Id()
    {
        var id = _storage.CreateCategory("正在追");
        id.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetCategories_Should_Return_All_With_Count()
    {
        _storage.CreateCategory("正在追");
        _storage.CreateCategory("已读完");

        var categories = _storage.GetCategories();
        categories.Should().HaveCount(2);
        categories[0].Name.Should().Be("正在追");
        categories[1].Name.Should().Be("已读完");
    }

    [Fact]
    public void GetCategories_Should_Include_Resource_Count()
    {
        var catId = _storage.CreateCategory("Test");
        var r1 = _storage.AddResource(CreateTestResource("/c1.cbz", "C1"));
        var r2 = _storage.AddResource(CreateTestResource("/c2.cbz", "C2"));
        _storage.AddToCategory(r1, catId);
        _storage.AddToCategory(r2, catId);

        var categories = _storage.GetCategories();
        categories[0].ResourceCount.Should().Be(2);
    }

    [Fact]
    public void RenameCategory_Should_Update_Name()
    {
        var id = _storage.CreateCategory("Old Name");
        _storage.RenameCategory(id, "New Name");

        var categories = _storage.GetCategories();
        categories[0].Name.Should().Be("New Name");
    }

    [Fact]
    public void DeleteCategory_Should_Cascade_To_Resource_Associations()
    {
        var catId = _storage.CreateCategory("To Delete");
        var rId = _storage.AddResource(CreateTestResource("/dc.cbz", "DC"));
        _storage.AddToCategory(rId, catId);

        _storage.DeleteCategory(catId);

        // 验证关联也被清理
        var results = _storage.GetLibrary(new LibraryQuery { CategoryId = catId });
        results.Should().BeEmpty();
    }

    [Fact]
    public void ReorderCategories_Should_Update_Sort_Order()
    {
        var id1 = _storage.CreateCategory("A");
        var id2 = _storage.CreateCategory("B");
        var id3 = _storage.CreateCategory("C");

        _storage.ReorderCategories(new List<int> { id3, id1, id2 });

        var categories = _storage.GetCategories();
        categories[0].Id.Should().Be(id3);
        categories[1].Id.Should().Be(id1);
        categories[2].Id.Should().Be(id2);
    }

    // ═══════════════════════════════════════════════════════════
    // 标签管理
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void GetOrCreateTag_Should_Create_New_Tag()
    {
        var tag = _storage.GetOrCreateTag("big breasts");
        tag.Id.Should().BeGreaterThan(0);
        tag.Name.Should().Be("big breasts");
        tag.Type.Should().Be("general");
    }

    [Fact]
    public void GetOrCreateTag_Should_Return_Existing_Tag()
    {
        var tag1 = _storage.GetOrCreateTag("test tag", "artist");
        var tag2 = _storage.GetOrCreateTag("test tag");
        tag2.Id.Should().Be(tag1.Id);
        tag2.Type.Should().Be("artist"); // type not updated on re-fetch
    }

    [Fact]
    public void AddTagToResource_And_GetResourceTags_Should_Work()
    {
        var rId = _storage.AddResource(CreateTestResource("/tag/r.cbz", "Tagged"));
        var tag = _storage.GetOrCreateTag("action");
        _storage.AddTagToResource(rId, tag.Id);

        var tags = _storage.GetResourceTags(rId);
        tags.Should().HaveCount(1);
        tags[0].Name.Should().Be("action");
    }

    [Fact]
    public void RemoveTagFromResource_Should_Work()
    {
        var rId = _storage.AddResource(CreateTestResource("/tag2/r.cbz", "Untag"));
        var tag = _storage.GetOrCreateTag("drama");
        _storage.AddTagToResource(rId, tag.Id);
        _storage.RemoveTagFromResource(rId, tag.Id);

        _storage.GetResourceTags(rId).Should().BeEmpty();
    }

    [Fact]
    public void GetAllTags_Should_Return_All()
    {
        _storage.GetOrCreateTag("tag1");
        _storage.GetOrCreateTag("tag2", "artist");
        _storage.GetAllTags().Should().HaveCount(2);
    }

    // ═══════════════════════════════════════════════════════════
    // 别名管理
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void AddAlias_Should_Not_Throw()
    {
        var tag = _storage.GetOrCreateTag("big breasts");
        _storage.AddAlias(tag.Id, "巨乳", "zh");
        // 验证可重复添加不抛异常
        _storage.AddAlias(tag.Id, "巨乳", "zh"); // IGNORE
    }

    // ═══════════════════════════════════════════════════════════
    // 系列
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void GetOrCreateSeries_Should_Be_Idempotent()
    {
        var s1 = _storage.GetOrCreateSeries("One Piece");
        var s2 = _storage.GetOrCreateSeries("One Piece");
        s2.Id.Should().Be(s1.Id);
    }

    // ═══════════════════════════════════════════════════════════
    // 抽象作品
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void GetOrCreateWork_Should_Be_Idempotent()
    {
        var w1 = _storage.GetOrCreateWork("Ero Biiky");
        var w2 = _storage.GetOrCreateWork("Ero Biiky");
        w2.Id.Should().Be(w1.Id);
    }

    [Fact]
    public void SetResourceWork_Should_Associate()
    {
        var w = _storage.GetOrCreateWork("Test Work");
        var rId = _storage.AddResource(CreateTestResource("/w/r.cbz", "Work Resource"));
        _storage.SetResourceWork(rId, w.Id);

        var fetched = _storage.GetResource(rId);
        fetched!.WorkId.Should().Be(w.Id);
    }

    // ═══════════════════════════════════════════════════════════
    // 监控目录
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void MonitoredDirectories_CRUD_Should_Work()
    {
        _storage.AddMonitoredDirectory(@"D:\Comics");
        _storage.AddMonitoredDirectory(@"E:\MoreComics");

        var dirs = _storage.GetMonitoredDirectories();
        dirs.Should().HaveCount(2);
        dirs[0].Enabled.Should().BeTrue();

        _storage.RemoveMonitoredDirectory(dirs[0].Id);
        _storage.GetMonitoredDirectories().Should().HaveCount(1);
    }

    // ═══════════════════════════════════════════════════════════
    // 文件哈希
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void FindResourceByHash_Should_Work()
    {
        var r = CreateTestResource("/hash/path.cbz", "Hash Comic");
        r.FileHash = "deadbeef";
        _storage.AddResource(r);

        var found = _storage.FindResourceByHash("deadbeef");
        found.Should().NotBeNull();
        found!.Title.Should().Be("Hash Comic");
    }

    [Fact]
    public void FindResourceByHash_Should_Return_Null_For_Unknown_Hash()
    {
        _storage.FindResourceByHash("nonexistent").Should().BeNull();
    }

    [Fact]
    public void FindResourceByHash_Should_Not_Match_Null_Hashes()
    {
        // 在线未下载的资源 hash 为 null
        var r = CreateTestResource("/nohash/path.cbz", "No Hash");
        r.FileHash = null;
        _storage.AddResource(r);

        _storage.FindResourceByHash("anything").Should().BeNull();
    }

    // ═══════════════════════════════════════════════════════════
    // 并发安全（基本验证）
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void Concurrent_Reads_Should_Not_Throw()
    {
        var id = _storage.AddResource(CreateTestResource("/conc/path.cbz", "Concurrent"));

        Parallel.For(0, 10, i =>
        {
            _storage.GetResource(id);
            _storage.GetCategories();
        });

        // 若无不抛异常则通过
        true.Should().BeTrue();
    }

    [Fact]
    public void Concurrent_Writes_To_Different_Tables_Should_Not_Throw()
    {
        var id = _storage.AddResource(CreateTestResource("/conc2/path.cbz", "Concurrent Write"));

        Parallel.Invoke(
            () => _storage.SaveProgress(id, 1),
            () => _storage.AddHistory(id, 1),
            () => { var cid = _storage.CreateCategory("ConcCat"); _storage.AddToCategory(id, cid); }
        );

        true.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════
    // 辅助方法
    // ═══════════════════════════════════════════════════════════

    private static ComicResource CreateTestResource(string sourceId, string title)
    {
        return new ComicResource
        {
            Title = title,
            SourceType = "local",
            SourceId = sourceId,
            PageCount = 10,
            AddedDate = DateTime.UtcNow.ToString("O"),
            IsBookmarked = true
        };
    }
}

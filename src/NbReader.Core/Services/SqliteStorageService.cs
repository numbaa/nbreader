using Microsoft.Data.Sqlite;
using NbReader.Core.Abstractions;
using NbReader.Core.Models;

namespace NbReader.Core.Services;

/// <summary>
/// SQLite 持久化存储服务：管理数据库 Schema、CRUD 和查询。
/// </summary>
public class SqliteStorageService : IStorageService, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _connectionString;

    public SqliteStorageService(string dbPath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        _connection = new SqliteConnection(_connectionString);
        _connection.Open();

        InitializeDatabase();
    }

    // ═══════════════════════════════════════════════════════════
    // 初始化
    // ═══════════════════════════════════════════════════════════

    private void InitializeDatabase()
    {
        using var cmd = _connection.CreateCommand();

        // PRAGMA 配置
        cmd.CommandText = "PRAGMA foreign_keys = ON;";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "PRAGMA journal_mode = WAL;";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "PRAGMA busy_timeout = 5000;";
        cmd.ExecuteNonQuery();

        // ── 建表（所有表使用 IF NOT EXISTS，幂等）──

        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS comic_works (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                canonical_title TEXT    NOT NULL,
                description     TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_work_title ON comic_works(canonical_title);
        ";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS comic_resources (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                work_id         INTEGER,
                title           TEXT    NOT NULL,
                source_type     TEXT    NOT NULL,
                source_id       TEXT    NOT NULL,
                source_url      TEXT,
                cover_path      TEXT,
                page_count      INTEGER NOT NULL DEFAULT 0,
                language        TEXT,
                content_type    TEXT,
                added_date      TEXT    NOT NULL,
                last_read_date  TEXT,
                is_bookmarked   INTEGER NOT NULL DEFAULT 0,
                is_downloaded   INTEGER NOT NULL DEFAULT 0,
                local_path      TEXT,
                series_id       INTEGER,
                volume_number   INTEGER,
                chapter_number  INTEGER,
                file_hash       TEXT,
                FOREIGN KEY (work_id)   REFERENCES comic_works(id),
                FOREIGN KEY (series_id) REFERENCES series(id),
                UNIQUE(source_type, source_id)
            );
            CREATE INDEX IF NOT EXISTS idx_resource_work       ON comic_resources(work_id);
            CREATE INDEX IF NOT EXISTS idx_resource_series     ON comic_resources(series_id);
            CREATE INDEX IF NOT EXISTS idx_resource_added      ON comic_resources(added_date);
            CREATE INDEX IF NOT EXISTS idx_resource_read       ON comic_resources(last_read_date);
            CREATE INDEX IF NOT EXISTS idx_resource_bookmarked ON comic_resources(is_bookmarked);
            CREATE INDEX IF NOT EXISTS idx_resource_language   ON comic_resources(language);
            CREATE INDEX IF NOT EXISTS idx_resource_content    ON comic_resources(content_type);
        ";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS series (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                title       TEXT    NOT NULL,
                description TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_series_title ON series(title);
        ";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS tags (
                id           INTEGER PRIMARY KEY AUTOINCREMENT,
                name         TEXT    NOT NULL UNIQUE,
                type         TEXT    NOT NULL DEFAULT 'general',
                needs_review INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS idx_tag_type   ON tags(type);
            CREATE INDEX IF NOT EXISTS idx_tag_review ON tags(needs_review);
        ";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS entity_aliases (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                tag_id      INTEGER NOT NULL,
                alias       TEXT    NOT NULL,
                language    TEXT,
                source_type TEXT,
                FOREIGN KEY (tag_id) REFERENCES tags(id) ON DELETE CASCADE,
                UNIQUE(tag_id, alias)
            );
            CREATE INDEX IF NOT EXISTS idx_alias_tag  ON entity_aliases(tag_id);
            CREATE INDEX IF NOT EXISTS idx_alias_name ON entity_aliases(alias);
        ";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS resource_tags (
                resource_id INTEGER NOT NULL,
                tag_id      INTEGER NOT NULL,
                PRIMARY KEY (resource_id, tag_id),
                FOREIGN KEY (resource_id) REFERENCES comic_resources(id) ON DELETE CASCADE,
                FOREIGN KEY (tag_id)      REFERENCES tags(id)             ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS idx_rt_tag ON resource_tags(tag_id);
        ";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS categories (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                name       TEXT    NOT NULL,
                sort_order INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS resource_categories (
                resource_id INTEGER NOT NULL,
                category_id INTEGER NOT NULL,
                PRIMARY KEY (resource_id, category_id),
                FOREIGN KEY (resource_id) REFERENCES comic_resources(id) ON DELETE CASCADE,
                FOREIGN KEY (category_id) REFERENCES categories(id)      ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS idx_rc_cat ON resource_categories(category_id);
        ";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS reading_progress (
                resource_id     INTEGER PRIMARY KEY,
                current_page    INTEGER NOT NULL DEFAULT 0,
                last_read_time  TEXT    NOT NULL,
                FOREIGN KEY (resource_id) REFERENCES comic_resources(id) ON DELETE CASCADE
            );
        ";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS read_history (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                resource_id     INTEGER NOT NULL,
                last_page_index INTEGER NOT NULL DEFAULT 0,
                read_date       TEXT    NOT NULL,
                FOREIGN KEY (resource_id) REFERENCES comic_resources(id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS idx_history_resource ON read_history(resource_id);
            CREATE INDEX IF NOT EXISTS idx_history_date     ON read_history(read_date DESC);
        ";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS monitored_directories (
                id      INTEGER PRIMARY KEY AUTOINCREMENT,
                path    TEXT    NOT NULL UNIQUE,
                enabled INTEGER NOT NULL DEFAULT 1
            );
        ";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS comic_sources (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                source_type TEXT    NOT NULL UNIQUE,
                enabled     INTEGER NOT NULL DEFAULT 0,
                config_json TEXT
            );
        ";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS downloads (
                id            INTEGER PRIMARY KEY AUTOINCREMENT,
                resource_id   INTEGER,
                source_type   TEXT    NOT NULL,
                source_id     TEXT    NOT NULL,
                source_url    TEXT    NOT NULL,
                status        TEXT    NOT NULL DEFAULT 'queued',
                progress      INTEGER NOT NULL DEFAULT 0,
                priority      INTEGER NOT NULL DEFAULT 0,
                created_date  TEXT    NOT NULL,
                FOREIGN KEY (resource_id) REFERENCES comic_resources(id)
            );
            CREATE INDEX IF NOT EXISTS idx_dl_status ON downloads(status);
        ";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS settings (
                key   TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
        ";
        cmd.ExecuteNonQuery();
    }

    // ═══════════════════════════════════════════════════════════
    // 设置
    // ═══════════════════════════════════════════════════════════

    public string? GetSetting(string key)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key = @key;";
        cmd.Parameters.AddWithValue("@key", key);
        var result = cmd.ExecuteScalar();
        return result?.ToString();
    }

    public void SetSetting(string key, string value)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO settings (key, value) VALUES (@key, @value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;";
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", value);
        cmd.ExecuteNonQuery();
    }

    // ═══════════════════════════════════════════════════════════
    // 漫画资源 CRUD
    // ═══════════════════════════════════════════════════════════

    public int AddResource(ComicResource resource)
    {
        // 先检查 UNIQUE(source_type, source_id) 是否已存在
        var existing = GetResourceBySource(resource.SourceType, resource.SourceId);
        if (existing is not null)
            return existing.Id;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO comic_resources (
                work_id, title, source_type, source_id, source_url, cover_path,
                page_count, language, content_type, added_date, last_read_date,
                is_bookmarked, is_downloaded, local_path, series_id,
                volume_number, chapter_number, file_hash
            ) VALUES (
                @work_id, @title, @source_type, @source_id, @source_url, @cover_path,
                @page_count, @language, @content_type, @added_date, @last_read_date,
                @is_bookmarked, @is_downloaded, @local_path, @series_id,
                @volume_number, @chapter_number, @file_hash
            );
            SELECT last_insert_rowid();";

        BindResourceParameters(cmd, resource);

        return (int)(long)cmd.ExecuteScalar()!;
    }

    public void UpdateResource(ComicResource resource)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE comic_resources SET
                work_id = @work_id, title = @title, source_url = @source_url,
                cover_path = @cover_path, page_count = @page_count,
                language = @language, content_type = @content_type,
                last_read_date = @last_read_date,
                is_bookmarked = @is_bookmarked, is_downloaded = @is_downloaded,
                local_path = @local_path, series_id = @series_id,
                volume_number = @volume_number, chapter_number = @chapter_number,
                file_hash = @file_hash
            WHERE id = @id;";

        BindResourceParameters(cmd, resource);
        cmd.Parameters.AddWithValue("@id", resource.Id);
        cmd.ExecuteNonQuery();
    }

    public void DeleteResource(int resourceId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM comic_resources WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", resourceId);
        cmd.ExecuteNonQuery();
    }

    public ComicResource? GetResource(int resourceId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM comic_resources WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", resourceId);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadResource(reader) : null;
    }

    public ComicResource? GetResourceBySource(string sourceType, string sourceId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM comic_resources WHERE source_type = @st AND source_id = @si;";
        cmd.Parameters.AddWithValue("@st", sourceType);
        cmd.Parameters.AddWithValue("@si", sourceId);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadResource(reader) : null;
    }

    public List<ComicResource> GetLibrary(LibraryQuery query)
    {
        var sql = "SELECT r.* FROM comic_resources r WHERE r.is_bookmarked = 1";

        // 分类筛选
        if (query.CategoryId.HasValue)
        {
            sql += " AND r.id IN (SELECT resource_id FROM resource_categories WHERE category_id = @cat_id)";
        }

        // 语言筛选
        if (!string.IsNullOrEmpty(query.Language))
        {
            sql += " AND r.language = @lang";
        }

        // 内容类型筛选
        if (!string.IsNullOrEmpty(query.ContentType))
        {
            sql += " AND r.content_type = @ct";
        }

        // 标签筛选（AND 逻辑）
        if (query.TagIds is { Count: > 0 })
        {
            for (int i = 0; i < query.TagIds.Count; i++)
            {
                sql += $" AND r.id IN (SELECT resource_id FROM resource_tags WHERE tag_id = @tag_{i})";
            }
        }

        // 排序
        var orderBy = query.SortBy switch
        {
            "read" => "r.last_read_date",
            "title" => "r.title",
            "pages" => "r.page_count",
            "progress" => "(SELECT COALESCE(CAST(rp.current_page AS REAL) / NULLIF(r.page_count, 0), 0) FROM reading_progress rp WHERE rp.resource_id = r.id)",
            _ => "r.added_date"  // "added" (default)
        };
        sql += $" ORDER BY {orderBy} {(query.Descending ? "DESC" : "ASC")}";

        // 分页
        if (query.Limit > 0)
        {
            sql += " LIMIT @limit OFFSET @offset";
        }

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;

        if (query.CategoryId.HasValue)
            cmd.Parameters.AddWithValue("@cat_id", query.CategoryId.Value);
        if (!string.IsNullOrEmpty(query.Language))
            cmd.Parameters.AddWithValue("@lang", query.Language);
        if (!string.IsNullOrEmpty(query.ContentType))
            cmd.Parameters.AddWithValue("@ct", query.ContentType);
        if (query.TagIds is { Count: > 0 })
        {
            for (int i = 0; i < query.TagIds.Count; i++)
                cmd.Parameters.AddWithValue($"@tag_{i}", query.TagIds[i]);
        }
        if (query.Limit > 0)
        {
            cmd.Parameters.AddWithValue("@limit", query.Limit);
            cmd.Parameters.AddWithValue("@offset", query.Offset);
        }

        using var reader = cmd.ExecuteReader();
        var result = new List<ComicResource>();
        while (reader.Read())
            result.Add(ReadResource(reader));

        return result;
    }

    public List<ComicResource> SearchLibrary(string searchText, int limit = 50)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT * FROM comic_resources
            WHERE is_bookmarked = 1 AND title LIKE @search
            ORDER BY added_date DESC
            LIMIT @limit;";
        cmd.Parameters.AddWithValue("@search", $"%{searchText}%");
        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = cmd.ExecuteReader();
        var result = new List<ComicResource>();
        while (reader.Read())
            result.Add(ReadResource(reader));

        return result;
    }

    // ═══════════════════════════════════════════════════════════
    // 阅读进度
    // ═══════════════════════════════════════════════════════════

    public ReadingProgress? GetProgress(int resourceId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT rp.resource_id, rp.current_page, rp.last_read_time, r.page_count
            FROM reading_progress rp
            JOIN comic_resources r ON r.id = rp.resource_id
            WHERE rp.resource_id = @rid;";
        cmd.Parameters.AddWithValue("@rid", resourceId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return new ReadingProgress
        {
            ResourceId = reader.GetInt32(0),
            CurrentPage = reader.GetInt32(1),
            LastReadTime = reader.GetString(2),
            TotalPages = reader.GetInt32(3)
        };
    }

    public void SaveProgress(int resourceId, int currentPage)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO reading_progress (resource_id, current_page, last_read_time)
            VALUES (@rid, @page, @time)
            ON CONFLICT(resource_id) DO UPDATE SET
                current_page = excluded.current_page,
                last_read_time = excluded.last_read_time;";
        cmd.Parameters.AddWithValue("@rid", resourceId);
        cmd.Parameters.AddWithValue("@page", currentPage);
        cmd.Parameters.AddWithValue("@time", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public void DeleteProgress(int resourceId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM reading_progress WHERE resource_id = @rid;";
        cmd.Parameters.AddWithValue("@rid", resourceId);
        cmd.ExecuteNonQuery();
    }

    // ═══════════════════════════════════════════════════════════
    // 阅读历史
    // ═══════════════════════════════════════════════════════════

    public void AddHistory(int resourceId, int lastPageIndex)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO read_history (resource_id, last_page_index, read_date)
            VALUES (@rid, @page, @date);";
        cmd.Parameters.AddWithValue("@rid", resourceId);
        cmd.Parameters.AddWithValue("@page", lastPageIndex);
        cmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public List<ReadHistoryEntry> GetHistory(int limit = 100, int offset = 0)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT rh.id, rh.resource_id, rh.last_page_index, rh.read_date,
                   r.title, r.cover_path, r.page_count
            FROM read_history rh
            JOIN comic_resources r ON r.id = rh.resource_id
            ORDER BY rh.read_date DESC
            LIMIT @limit OFFSET @offset;";
        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.Parameters.AddWithValue("@offset", offset);

        using var reader = cmd.ExecuteReader();
        var result = new List<ReadHistoryEntry>();
        while (reader.Read())
        {
            result.Add(new ReadHistoryEntry
            {
                Id = reader.GetInt32(0),
                ResourceId = reader.GetInt32(1),
                LastPageIndex = reader.GetInt32(2),
                ReadDate = reader.GetString(3),
                ComicTitle = reader.IsDBNull(4) ? null : reader.GetString(4),
                CoverPath = reader.IsDBNull(5) ? null : reader.GetString(5),
                TotalPages = reader.GetInt32(6)
            });
        }

        return result;
    }

    public void DeleteHistory(int historyId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM read_history WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", historyId);
        cmd.ExecuteNonQuery();
    }

    public void ClearHistory()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM read_history;";
        cmd.ExecuteNonQuery();
    }

    // ═══════════════════════════════════════════════════════════
    // 分类管理
    // ═══════════════════════════════════════════════════════════

    public List<Category> GetCategories()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT c.id, c.name, c.sort_order,
                   COUNT(rc.resource_id) AS resource_count
            FROM categories c
            LEFT JOIN resource_categories rc ON rc.category_id = c.id
            GROUP BY c.id
            ORDER BY c.sort_order, c.id;";

        using var reader = cmd.ExecuteReader();
        var result = new List<Category>();
        while (reader.Read())
        {
            result.Add(new Category
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                SortOrder = reader.GetInt32(2),
                ResourceCount = reader.GetInt32(3)
            });
        }

        return result;
    }

    public int CreateCategory(string name)
    {
        using var cmd = _connection.CreateCommand();
        // 新分类放在最后
        cmd.CommandText = @"
            INSERT INTO categories (name, sort_order)
            VALUES (@name, COALESCE((SELECT MAX(sort_order) FROM categories), 0) + 1);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@name", name);
        return (int)(long)cmd.ExecuteScalar()!;
    }

    public void RenameCategory(int categoryId, string newName)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE categories SET name = @name WHERE id = @id;";
        cmd.Parameters.AddWithValue("@name", newName);
        cmd.Parameters.AddWithValue("@id", categoryId);
        cmd.ExecuteNonQuery();
    }

    public void DeleteCategory(int categoryId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM categories WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", categoryId);
        cmd.ExecuteNonQuery();
    }

    public void ReorderCategories(List<int> orderedCategoryIds)
    {
        using var tx = _connection.BeginTransaction();
        using var cmd = _connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "UPDATE categories SET sort_order = @order WHERE id = @id;";

        var orderParam = cmd.Parameters.Add("@order", SqliteType.Integer);
        var idParam = cmd.Parameters.Add("@id", SqliteType.Integer);

        for (int i = 0; i < orderedCategoryIds.Count; i++)
        {
            orderParam.Value = i;
            idParam.Value = orderedCategoryIds[i];
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public void AddToCategory(int resourceId, int categoryId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR IGNORE INTO resource_categories (resource_id, category_id)
            VALUES (@rid, @cid);";
        cmd.Parameters.AddWithValue("@rid", resourceId);
        cmd.Parameters.AddWithValue("@cid", categoryId);
        cmd.ExecuteNonQuery();
    }

    public void RemoveFromCategory(int resourceId, int categoryId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM resource_categories WHERE resource_id = @rid AND category_id = @cid;";
        cmd.Parameters.AddWithValue("@rid", resourceId);
        cmd.Parameters.AddWithValue("@cid", categoryId);
        cmd.ExecuteNonQuery();
    }

    // ═══════════════════════════════════════════════════════════
    // 标签管理
    // ═══════════════════════════════════════════════════════════

    public Tag GetOrCreateTag(string name, string type = "general")
    {
        // 先查是否已存在
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, name, type, needs_review FROM tags WHERE name = @name;";
        cmd.Parameters.AddWithValue("@name", name);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new Tag
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Type = reader.GetString(2),
                NeedsReview = reader.GetInt32(3) == 1
            };
        }

        reader.Close();

        // 不存在则创建
        cmd.CommandText = @"
            INSERT INTO tags (name, type) VALUES (@name, @type);
            SELECT last_insert_rowid();";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@type", type);

        var id = (int)(long)cmd.ExecuteScalar()!;
        return new Tag { Id = id, Name = name, Type = type };
    }

    public List<Tag> GetResourceTags(int resourceId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT t.id, t.name, t.type, t.needs_review
            FROM tags t
            JOIN resource_tags rt ON rt.tag_id = t.id
            WHERE rt.resource_id = @rid
            ORDER BY t.type, t.name;";
        cmd.Parameters.AddWithValue("@rid", resourceId);

        using var reader = cmd.ExecuteReader();
        var result = new List<Tag>();
        while (reader.Read())
        {
            result.Add(new Tag
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Type = reader.GetString(2),
                NeedsReview = reader.GetInt32(3) == 1
            });
        }

        return result;
    }

    public void AddTagToResource(int resourceId, int tagId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR IGNORE INTO resource_tags (resource_id, tag_id)
            VALUES (@rid, @tid);";
        cmd.Parameters.AddWithValue("@rid", resourceId);
        cmd.Parameters.AddWithValue("@tid", tagId);
        cmd.ExecuteNonQuery();
    }

    public void RemoveTagFromResource(int resourceId, int tagId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM resource_tags WHERE resource_id = @rid AND tag_id = @tid;";
        cmd.Parameters.AddWithValue("@rid", resourceId);
        cmd.Parameters.AddWithValue("@tid", tagId);
        cmd.ExecuteNonQuery();
    }

    // ═══════════════════════════════════════════════════════════
    // 别名管理
    // ═══════════════════════════════════════════════════════════

    public void AddAlias(int tagId, string alias, string? language = null, string? sourceType = null)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR IGNORE INTO entity_aliases (tag_id, alias, language, source_type)
            VALUES (@tid, @alias, @lang, @src);";
        cmd.Parameters.AddWithValue("@tid", tagId);
        cmd.Parameters.AddWithValue("@alias", alias);
        cmd.Parameters.AddWithValue("@lang", (object?)language ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@src", (object?)sourceType ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    // ═══════════════════════════════════════════════════════════
    // 系列
    // ═══════════════════════════════════════════════════════════

    public Series GetOrCreateSeries(string title)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, title, description FROM series WHERE title = @title;";
        cmd.Parameters.AddWithValue("@title", title);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new Series
            {
                Id = reader.GetInt32(0),
                Title = reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2)
            };
        }

        reader.Close();

        cmd.CommandText = @"
            INSERT INTO series (title) VALUES (@title);
            SELECT last_insert_rowid();";
        var id = (int)(long)cmd.ExecuteScalar()!;
        return new Series { Id = id, Title = title };
    }

    // ═══════════════════════════════════════════════════════════
    // 抽象作品
    // ═══════════════════════════════════════════════════════════

    public ComicWork GetOrCreateWork(string canonicalTitle)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, canonical_title, description FROM comic_works WHERE canonical_title = @title;";
        cmd.Parameters.AddWithValue("@title", canonicalTitle);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new ComicWork
            {
                Id = reader.GetInt32(0),
                CanonicalTitle = reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2)
            };
        }

        reader.Close();

        cmd.CommandText = @"
            INSERT INTO comic_works (canonical_title) VALUES (@title);
            SELECT last_insert_rowid();";
        var id = (int)(long)cmd.ExecuteScalar()!;
        return new ComicWork { Id = id, CanonicalTitle = canonicalTitle };
    }

    public void SetResourceWork(int resourceId, int workId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE comic_resources SET work_id = @wid WHERE id = @rid;";
        cmd.Parameters.AddWithValue("@wid", workId);
        cmd.Parameters.AddWithValue("@rid", resourceId);
        cmd.ExecuteNonQuery();
    }

    // ═══════════════════════════════════════════════════════════
    // 监控目录
    // ═══════════════════════════════════════════════════════════

    public List<MonitoredDirectory> GetMonitoredDirectories()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, path, enabled FROM monitored_directories ORDER BY id;";

        using var reader = cmd.ExecuteReader();
        var result = new List<MonitoredDirectory>();
        while (reader.Read())
        {
            result.Add(new MonitoredDirectory
            {
                Id = reader.GetInt32(0),
                Path = reader.GetString(1),
                Enabled = reader.GetInt32(2) == 1
            });
        }

        return result;
    }

    public void AddMonitoredDirectory(string path)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR IGNORE INTO monitored_directories (path) VALUES (@path);";
        cmd.Parameters.AddWithValue("@path", path);
        cmd.ExecuteNonQuery();
    }

    public void RemoveMonitoredDirectory(int id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM monitored_directories WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    // ═══════════════════════════════════════════════════════════
    // 文件哈希
    // ═══════════════════════════════════════════════════════════

    public ComicResource? FindResourceByHash(string fileHash)
    {
        // 排除在线未下载（hash 为 null 的条目）
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM comic_resources WHERE file_hash = @hash AND file_hash IS NOT NULL;";
        cmd.Parameters.AddWithValue("@hash", fileHash);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadResource(reader) : null;
    }

    // ═══════════════════════════════════════════════════════════
    // 搜索
    // ═══════════════════════════════════════════════════════════

    public List<Tag> GetAllTags()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, name, type, needs_review FROM tags ORDER BY type, name;";

        using var reader = cmd.ExecuteReader();
        var result = new List<Tag>();
        while (reader.Read())
        {
            result.Add(new Tag
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Type = reader.GetString(2),
                NeedsReview = reader.GetInt32(3) == 1
            });
        }

        return result;
    }

    // ═══════════════════════════════════════════════════════════
    // 辅助方法
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 绑定 ComicResource 到 SQL 参数（不含 id）。
    /// </summary>
    private static void BindResourceParameters(SqliteCommand cmd, ComicResource r)
    {
        cmd.Parameters.AddWithValue("@work_id", (object?)r.WorkId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@title", r.Title);
        cmd.Parameters.AddWithValue("@source_type", r.SourceType);
        cmd.Parameters.AddWithValue("@source_id", r.SourceId);
        cmd.Parameters.AddWithValue("@source_url", (object?)r.SourceUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@cover_path", (object?)r.CoverPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@page_count", r.PageCount);
        cmd.Parameters.AddWithValue("@language", (object?)r.Language ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@content_type", (object?)r.ContentType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@added_date", r.AddedDate);
        cmd.Parameters.AddWithValue("@last_read_date", (object?)r.LastReadDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@is_bookmarked", r.IsBookmarked ? 1 : 0);
        cmd.Parameters.AddWithValue("@is_downloaded", r.IsDownloaded ? 1 : 0);
        cmd.Parameters.AddWithValue("@local_path", (object?)r.LocalPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@series_id", (object?)r.SeriesId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@volume_number", (object?)r.VolumeNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@chapter_number", (object?)r.ChapterNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@file_hash", (object?)r.FileHash ?? DBNull.Value);
    }

    /// <summary>
    /// 从 reader 读取 ComicResource。
    /// </summary>
    private static ComicResource ReadResource(SqliteDataReader reader)
    {
        return new ComicResource
        {
            Id = reader.GetInt32(0),
            WorkId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
            Title = reader.GetString(2),
            SourceType = reader.GetString(3),
            SourceId = reader.GetString(4),
            SourceUrl = reader.IsDBNull(5) ? null : reader.GetString(5),
            CoverPath = reader.IsDBNull(6) ? null : reader.GetString(6),
            PageCount = reader.GetInt32(7),
            Language = reader.IsDBNull(8) ? null : reader.GetString(8),
            ContentType = reader.IsDBNull(9) ? null : reader.GetString(9),
            AddedDate = reader.GetString(10),
            LastReadDate = reader.IsDBNull(11) ? null : reader.GetString(11),
            IsBookmarked = reader.GetInt32(12) == 1,
            IsDownloaded = reader.GetInt32(13) == 1,
            LocalPath = reader.IsDBNull(14) ? null : reader.GetString(14),
            SeriesId = reader.IsDBNull(15) ? null : reader.GetInt32(15),
            VolumeNumber = reader.IsDBNull(16) ? null : reader.GetInt32(16),
            ChapterNumber = reader.IsDBNull(17) ? null : reader.GetInt32(17),
            FileHash = reader.IsDBNull(18) ? null : reader.GetString(18)
        };
    }

    // ═══════════════════════════════════════════════════════════
    // IDisposable
    // ═══════════════════════════════════════════════════════════

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
    }
}

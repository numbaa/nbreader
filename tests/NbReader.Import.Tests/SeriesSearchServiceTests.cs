using Microsoft.Data.Sqlite;
using NbReader.Infrastructure;
using NbReader.Search;

namespace NbReader.Import.Tests;

public sealed class SeriesSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_ShouldApplyStructuredFilters()
    {
        var dbPath = CreateTempDbPath();

        try
        {
            var database = new AppDatabase(dbPath);
            database.Initialize();

            var heroSeriesId = InsertSeries(database.ConnectionString, "Hero Saga", "2024-01-01T00:00:00Z");
            var romanceSeriesId = InsertSeries(database.ConnectionString, "Romance Notes", "2023-01-01T00:00:00Z");

            var heroVolumeId = InsertVolume(database.ConnectionString, heroSeriesId, "Vol.01", "2024-02-10T00:00:00Z");
            var romanceVolumeId = InsertVolume(database.ConnectionString, romanceSeriesId, "Vol.01", "2023-02-10T00:00:00Z");

            var authorId = InsertPerson(database.ConnectionString, "Akira Toru");
            var tagId = InsertTag(database.ConnectionString, "动作", "genre");

            LinkSeriesPerson(database.ConnectionString, heroSeriesId, authorId, "author");
            LinkSeriesTag(database.ConnectionString, heroSeriesId, tagId);

            InsertReadingProgress(database.ConnectionString, heroVolumeId, completed: false);
            InsertReadingProgress(database.ConnectionString, romanceVolumeId, completed: true);

            var service = new SeriesSearchService(database.ConnectionString);
            var results = await service.SearchAsync(new SeriesSearchQuery(
                TitleKeyword: "Hero",
                AuthorKeyword: "Akira",
                TagKeyword: "动作",
                Year: 2024,
                ReadingStatus: SeriesSearchReadingStatus.InProgress,
                SortBy: SeriesSearchSortBy.LatestUpdatedDesc));

            Assert.Single(results);
            Assert.Equal(heroSeriesId, results[0].SeriesId);
            Assert.Equal("Hero Saga", results[0].Title);
            Assert.Equal(2024, results[0].Year);
            Assert.Equal(SeriesSearchReadingStatus.InProgress, results[0].ReadingStatus);
        }
        finally
        {
            CleanupDatabaseFiles(dbPath);
        }
    }

    [Fact]
    public async Task SearchAsync_ShouldSortByTitle_AndRespectLimit()
    {
        var dbPath = CreateTempDbPath();

        try
        {
            var database = new AppDatabase(dbPath);
            database.Initialize();

            var seriesA = InsertSeries(database.ConnectionString, "Zeta", "2024-01-01T00:00:00Z");
            var seriesB = InsertSeries(database.ConnectionString, "Alpha", "2024-01-01T00:00:00Z");

            InsertVolume(database.ConnectionString, seriesA, "Vol.01", "2024-02-01T00:00:00Z");
            InsertVolume(database.ConnectionString, seriesB, "Vol.01", "2024-02-01T00:00:00Z");

            var service = new SeriesSearchService(database.ConnectionString);
            var results = await service.SearchAsync(new SeriesSearchQuery(
                SortBy: SeriesSearchSortBy.TitleAsc,
                Limit: 1));

            Assert.Single(results);
            Assert.Equal("Alpha", results[0].Title);
        }
        finally
        {
            CleanupDatabaseFiles(dbPath);
        }
    }

    [Fact]
    public async Task SearchAsync_ShouldMatchPinyin_ForTitleAndAuthor()
    {
        var dbPath = CreateTempDbPath();

        try
        {
            var database = new AppDatabase(dbPath);
            database.Initialize();

            var seriesId = InsertSeries(
                database.ConnectionString,
                "进击的巨人",
                "2024-01-01T00:00:00Z",
                titlePinyin: "jin ji de ju ren");
            var volumeId = InsertVolume(database.ConnectionString, seriesId, "Vol.01", "2024-02-10T00:00:00Z");

            var personId = InsertPerson(database.ConnectionString, "田野", namePinyin: "tian ye");
            LinkSeriesPerson(database.ConnectionString, seriesId, personId, "author");
            InsertReadingProgress(database.ConnectionString, volumeId, completed: false);

            database.RebuildSearchIndexes();

            var service = new SeriesSearchService(database.ConnectionString);
            var results = await service.SearchAsync(new SeriesSearchQuery(
                TitleKeyword: "jin ji",
                AuthorKeyword: "tian",
                SortBy: SeriesSearchSortBy.LatestUpdatedDesc));

            Assert.Single(results);
            Assert.Equal(seriesId, results[0].SeriesId);
        }
        finally
        {
            CleanupDatabaseFiles(dbPath);
        }
    }

    [Fact]
    public async Task SearchAsync_ShouldFollowFtsTriggers_WhenPinyinUpdated()
    {
        var dbPath = CreateTempDbPath();

        try
        {
            var database = new AppDatabase(dbPath);
            database.Initialize();

            var seriesId = InsertSeries(
                database.ConnectionString,
                "蓝海",
                "2024-01-01T00:00:00Z",
                titlePinyin: "lan hai");
            var volumeId = InsertVolume(database.ConnectionString, seriesId, "Vol.01", "2024-02-10T00:00:00Z");
            var personId = InsertPerson(database.ConnectionString, "林风", namePinyin: "lin feng");
            LinkSeriesPerson(database.ConnectionString, seriesId, personId, "author");
            InsertReadingProgress(database.ConnectionString, volumeId, completed: false);

            var service = new SeriesSearchService(database.ConnectionString);
            var initial = await service.SearchAsync(new SeriesSearchQuery(TitleKeyword: "lan"));
            Assert.Single(initial);

            UpdateSeriesPinyin(database.ConnectionString, seriesId, "cang hai");
            UpdatePersonPinyin(database.ConnectionString, personId, "cang mu");

            var oldTerm = await service.SearchAsync(new SeriesSearchQuery(TitleKeyword: "lan"));
            var newTerm = await service.SearchAsync(new SeriesSearchQuery(TitleKeyword: "cang", AuthorKeyword: "cang"));

            Assert.Empty(oldTerm);
            Assert.Single(newTerm);
            Assert.Equal(seriesId, newTerm[0].SeriesId);
        }
        finally
        {
            CleanupDatabaseFiles(dbPath);
        }
    }

    private static long InsertSeries(string connectionString, string title, string createdAt, string? titlePinyin = null)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO series (title, normalized_title, title_pinyin, created_at)
            VALUES ($title, $normalizedTitle, $titlePinyin, $createdAt);
            """;
        insert.Parameters.AddWithValue("$title", title);
        insert.Parameters.AddWithValue("$normalizedTitle", title.Trim().ToLowerInvariant());
        insert.Parameters.AddWithValue("$titlePinyin", titlePinyin ?? string.Empty);
        insert.Parameters.AddWithValue("$createdAt", createdAt);
        insert.ExecuteNonQuery();

        using var lastId = connection.CreateCommand();
        lastId.CommandText = "SELECT last_insert_rowid();";
        return Convert.ToInt64(lastId.ExecuteScalar());
    }

    private static long InsertVolume(string connectionString, long seriesId, string volumeTitle, string createdAt)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        var sourcePath = $"/tmp/{seriesId}/{volumeTitle}";

        using (var insertSource = connection.CreateCommand())
        {
            insertSource.CommandText = """
                INSERT INTO source (source_path, source_type, created_at)
                VALUES ($sourcePath, 'local_path', $createdAt);
                """;
            insertSource.Parameters.AddWithValue("$sourcePath", sourcePath);
            insertSource.Parameters.AddWithValue("$createdAt", createdAt);
            insertSource.ExecuteNonQuery();
        }

        long sourceId;
        using (var querySource = connection.CreateCommand())
        {
            querySource.CommandText = "SELECT source_id FROM source WHERE source_path = $sourcePath LIMIT 1;";
            querySource.Parameters.AddWithValue("$sourcePath", sourcePath);
            sourceId = Convert.ToInt64(querySource.ExecuteScalar());
        }

        using (var insertVolume = connection.CreateCommand())
        {
            insertVolume.CommandText = """
                INSERT INTO volume (source_id, series_id, title, created_at)
                VALUES ($sourceId, $seriesId, $title, $createdAt);
                """;
            insertVolume.Parameters.AddWithValue("$sourceId", sourceId);
            insertVolume.Parameters.AddWithValue("$seriesId", seriesId);
            insertVolume.Parameters.AddWithValue("$title", volumeTitle);
            insertVolume.Parameters.AddWithValue("$createdAt", createdAt);
            insertVolume.ExecuteNonQuery();
        }

        using var lastId = connection.CreateCommand();
        lastId.CommandText = "SELECT last_insert_rowid();";
        return Convert.ToInt64(lastId.ExecuteScalar());
    }

    private static long InsertPerson(string connectionString, string name, string? namePinyin = null)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO person (name, normalized_name, name_pinyin)
            VALUES ($name, $normalizedName, $namePinyin);
            """;
        insert.Parameters.AddWithValue("$name", name);
        insert.Parameters.AddWithValue("$normalizedName", name.Trim().ToLowerInvariant());
        insert.Parameters.AddWithValue("$namePinyin", namePinyin ?? string.Empty);
        insert.ExecuteNonQuery();

        using var lastId = connection.CreateCommand();
        lastId.CommandText = "SELECT last_insert_rowid();";
        return Convert.ToInt64(lastId.ExecuteScalar());
    }

    private static long InsertTag(string connectionString, string name, string category)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO tag (name, normalized_name, category)
            VALUES ($name, $normalizedName, $category);
            """;
        insert.Parameters.AddWithValue("$name", name);
        insert.Parameters.AddWithValue("$normalizedName", name.Trim().ToLowerInvariant());
        insert.Parameters.AddWithValue("$category", category);
        insert.ExecuteNonQuery();

        using var lastId = connection.CreateCommand();
        lastId.CommandText = "SELECT last_insert_rowid();";
        return Convert.ToInt64(lastId.ExecuteScalar());
    }

    private static void LinkSeriesPerson(string connectionString, long seriesId, long personId, string role)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO series_person (series_id, person_id, role)
            VALUES ($seriesId, $personId, $role);
            """;
        insert.Parameters.AddWithValue("$seriesId", seriesId);
        insert.Parameters.AddWithValue("$personId", personId);
        insert.Parameters.AddWithValue("$role", role);
        insert.ExecuteNonQuery();
    }

    private static void LinkSeriesTag(string connectionString, long seriesId, long tagId)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO series_tag (series_id, tag_id)
            VALUES ($seriesId, $tagId);
            """;
        insert.Parameters.AddWithValue("$seriesId", seriesId);
        insert.Parameters.AddWithValue("$tagId", tagId);
        insert.ExecuteNonQuery();
    }

    private static void InsertReadingProgress(string connectionString, long volumeId, bool completed)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO reading_progress (volume_id, current_page, max_page_reached, completed, last_read_at, updated_at)
            VALUES ($volumeId, 1, 1, $completed, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z');
            """;
        insert.Parameters.AddWithValue("$volumeId", volumeId);
        insert.Parameters.AddWithValue("$completed", completed ? 1 : 0);
        insert.ExecuteNonQuery();
    }

    private static void UpdateSeriesPinyin(string connectionString, long seriesId, string pinyin)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE series SET title_pinyin = $titlePinyin WHERE series_id = $seriesId;";
        command.Parameters.AddWithValue("$titlePinyin", pinyin);
        command.Parameters.AddWithValue("$seriesId", seriesId);
        command.ExecuteNonQuery();
    }

    private static void UpdatePersonPinyin(string connectionString, long personId, string pinyin)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE person SET name_pinyin = $namePinyin WHERE person_id = $personId;";
        command.Parameters.AddWithValue("$namePinyin", pinyin);
        command.Parameters.AddWithValue("$personId", personId);
        command.ExecuteNonQuery();
    }

    private static string CreateTempDbPath()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "nbreader-db-tests", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        return dbPath;
    }

    private static void CleanupDatabaseFiles(string dbPath)
    {
        TryDelete(dbPath);
        TryDelete(dbPath + "-wal");
        TryDelete(dbPath + "-shm");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

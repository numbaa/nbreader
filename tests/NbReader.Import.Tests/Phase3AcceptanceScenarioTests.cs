using Microsoft.Data.Sqlite;
using NbReader.Infrastructure;
using NbReader.Search;

namespace NbReader.Import.Tests;

public sealed class Phase3AcceptanceScenarioTests
{
    [Fact]
    public async Task Scenario1_SearchByTitleAuthorTagYear_ShouldLocateSeries()
    {
        var dbPath = CreateTempDbPath();

        try
        {
            var database = new AppDatabase(dbPath);
            database.Initialize();

            var seriesId = InsertSeries(database.ConnectionString, "Blue Hero", "2025-01-01T00:00:00Z");
            var volumeId = InsertVolume(database.ConnectionString, seriesId, "Vol.01", "2025-03-01T00:00:00Z");

            var metadata = new SeriesMetadataEditService(database.ConnectionString);
            await metadata.ReplaceSeriesMetadataAsync(seriesId, ["Alice"], ["Action"]);

            InsertReadingProgress(database.ConnectionString, volumeId, completed: false);

            var search = new SeriesSearchService(database.ConnectionString);
            var rows = await search.SearchAsync(new SeriesSearchQuery(
                TitleKeyword: "Blue",
                AuthorKeyword: "Alice",
                TagKeyword: "Action",
                Year: 2025));

            Assert.Single(rows);
            Assert.Equal(seriesId, rows[0].SeriesId);
        }
        finally
        {
            CleanupDatabaseFiles(dbPath);
        }
    }

    [Fact]
    public async Task Scenario2_ChaoticImportCanBeCorrectedViaServices()
    {
        var dbPath = CreateTempDbPath();

        try
        {
            var database = new AppDatabase(dbPath);
            database.Initialize();

            var wrongSeries = InsertSeries(database.ConnectionString, "Wrong Name", "2026-01-01T00:00:00Z");
            var targetSeries = InsertSeries(database.ConnectionString, "Right Name", "2026-01-02T00:00:00Z");
            var wrongVolume = InsertVolume(database.ConnectionString, wrongSeries, "Vol.X", "2026-01-03T00:00:00Z");

            var correction = new SeriesCorrectionService(database.ConnectionString);
            Assert.True(await correction.RenameSeriesAsync(wrongSeries, "Fixed Name"));
            Assert.True(await correction.UpdateVolumeNumberAsync(wrongVolume, 1));
            Assert.True(await correction.MergeSeriesAsync(wrongSeries, targetSeries));

            var search = new SeriesSearchService(database.ConnectionString);
            var oldRows = await search.SearchAsync(new SeriesSearchQuery(TitleKeyword: "Wrong"));
            var mergedRows = await search.SearchAsync(new SeriesSearchQuery(TitleKeyword: "Right"));

            Assert.Empty(oldRows);
            Assert.Single(mergedRows);
            Assert.Equal(1, mergedRows[0].VolumeCount);
        }
        finally
        {
            CleanupDatabaseFiles(dbPath);
        }
    }

    [Fact]
    public async Task Scenario3_FailedImportTaskCanBeRetried()
    {
        var dbPath = CreateTempDbPath();

        try
        {
            var database = new AppDatabase(dbPath);
            database.Initialize();

            var taskId = Guid.NewGuid();
            InsertImportTask(database.ConnectionString, taskId, @"C:\\tmp\\broken.zip", "c:/tmp/broken.zip", "ZipFile", "Failed", "2026-01-05T00:00:00Z");
            InsertImportTaskEvent(database.ConnectionString, taskId, "Failed", "analyze_failed", "broken file", "2026-01-05T00:00:00Z");

            var maintenance = new LibraryMaintenanceService(database.ConnectionString);
            var retried = await maintenance.RetryFailedTaskAsync(taskId);
            Assert.True(retried);

            var failed = await maintenance.GetFailedImportTasksAsync();
            Assert.Empty(failed);
        }
        finally
        {
            CleanupDatabaseFiles(dbPath);
        }
    }

    [Fact]
    public async Task Scenario4_AggregationShouldTakeEffectImmediatelyAfterCorrection()
    {
        var dbPath = CreateTempDbPath();

        try
        {
            var database = new AppDatabase(dbPath);
            database.Initialize();

            var sourceSeries = InsertSeries(database.ConnectionString, "Series A", "2026-01-01T00:00:00Z");
            var targetSeries = InsertSeries(database.ConnectionString, "Series B", "2026-01-02T00:00:00Z");

            InsertVolume(database.ConnectionString, sourceSeries, "Vol.01", "2026-01-03T00:00:00Z");
            InsertVolume(database.ConnectionString, targetSeries, "Vol.02", "2026-01-04T00:00:00Z");

            var correction = new SeriesCorrectionService(database.ConnectionString);
            Assert.True(await correction.MergeSeriesAsync(sourceSeries, targetSeries));

            var search = new SeriesSearchService(database.ConnectionString);
            var rows = await search.SearchAsync(new SeriesSearchQuery(TitleKeyword: "Series B"));

            Assert.Single(rows);
            Assert.Equal(2, rows[0].VolumeCount);
        }
        finally
        {
            CleanupDatabaseFiles(dbPath);
        }
    }

    private static long InsertSeries(string connectionString, string title, string createdAt)
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
        insert.Parameters.AddWithValue("$titlePinyin", title.Trim().ToLowerInvariant());
        insert.Parameters.AddWithValue("$createdAt", createdAt);
        insert.ExecuteNonQuery();

        using var lastId = connection.CreateCommand();
        lastId.CommandText = "SELECT last_insert_rowid();";
        return Convert.ToInt64(lastId.ExecuteScalar());
    }

    private static long InsertVolume(string connectionString, long seriesId, string title, string createdAt)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        var sourcePath = $"/tmp/{Guid.NewGuid():N}/{title}";

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
            insertVolume.Parameters.AddWithValue("$title", title);
            insertVolume.Parameters.AddWithValue("$createdAt", createdAt);
            insertVolume.ExecuteNonQuery();
        }

        using var lastId = connection.CreateCommand();
        lastId.CommandText = "SELECT last_insert_rowid();";
        return Convert.ToInt64(lastId.ExecuteScalar());
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

    private static void InsertImportTask(
        string connectionString,
        Guid taskId,
        string rawInput,
        string normalizedLocator,
        string inputKind,
        string status,
        string timestamp)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO import_task (task_id, raw_input, normalized_locator, input_kind, status, created_at, updated_at)
            VALUES ($taskId, $rawInput, $normalizedLocator, $inputKind, $status, $createdAt, $updatedAt);
            """;
        insert.Parameters.AddWithValue("$taskId", taskId.ToString());
        insert.Parameters.AddWithValue("$rawInput", rawInput);
        insert.Parameters.AddWithValue("$normalizedLocator", normalizedLocator);
        insert.Parameters.AddWithValue("$inputKind", inputKind);
        insert.Parameters.AddWithValue("$status", status);
        insert.Parameters.AddWithValue("$createdAt", timestamp);
        insert.Parameters.AddWithValue("$updatedAt", timestamp);
        insert.ExecuteNonQuery();
    }

    private static void InsertImportTaskEvent(
        string connectionString,
        Guid taskId,
        string status,
        string eventType,
        string message,
        string timestamp)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO import_task_event (task_id, status, event_type, message, occurred_at)
            VALUES ($taskId, $status, $eventType, $message, $occurredAt);
            """;
        insert.Parameters.AddWithValue("$taskId", taskId.ToString());
        insert.Parameters.AddWithValue("$status", status);
        insert.Parameters.AddWithValue("$eventType", eventType);
        insert.Parameters.AddWithValue("$message", message);
        insert.Parameters.AddWithValue("$occurredAt", timestamp);
        insert.ExecuteNonQuery();
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

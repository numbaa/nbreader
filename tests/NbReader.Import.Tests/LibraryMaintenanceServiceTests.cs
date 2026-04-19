using Microsoft.Data.Sqlite;
using NbReader.Infrastructure;
using NbReader.Search;

namespace NbReader.Import.Tests;

public sealed class LibraryMaintenanceServiceTests
{
    [Fact]
    public async Task GetUnorganizedVolumesAsync_ShouldReturnVolumesWithoutSeries()
    {
        var dbPath = CreateTempDbPath();

        try
        {
            var database = new AppDatabase(dbPath);
            database.Initialize();

            var groupedSeriesId = InsertSeries(database.ConnectionString, "Grouped", "2026-01-01T00:00:00Z");
            InsertVolume(database.ConnectionString, groupedSeriesId, "Grouped Vol.01", "2026-01-01T00:00:00Z");
            var unorganizedVolumeId = InsertVolume(database.ConnectionString, null, "Loose Vol.01", "2026-01-02T00:00:00Z");

            var service = new LibraryMaintenanceService(database.ConnectionString);
            var rows = await service.GetUnorganizedVolumesAsync();

            Assert.Single(rows);
            Assert.Equal(unorganizedVolumeId, rows[0].VolumeId);
            Assert.Equal("Loose Vol.01", rows[0].VolumeTitle);
        }
        finally
        {
            CleanupDatabaseFiles(dbPath);
        }
    }

    [Fact]
    public async Task RetryFailedTaskAsync_ShouldMoveTaskToPending_AndAppendEvent()
    {
        var dbPath = CreateTempDbPath();

        try
        {
            var database = new AppDatabase(dbPath);
            database.Initialize();

            var failedTaskId = Guid.NewGuid();
            InsertImportTask(
                database.ConnectionString,
                failedTaskId,
                @"C:\\tmp\\bad.zip",
                @"c:/tmp/bad.zip",
                "ZipFile",
                "Failed",
                "2026-01-03T00:00:00Z");
            InsertImportTaskEvent(database.ConnectionString, failedTaskId, "Failed", "analyze_failed", "broken archive", "2026-01-03T00:00:00Z");

            var service = new LibraryMaintenanceService(database.ConnectionString);
            var retried = await service.RetryFailedTaskAsync(failedTaskId);
            Assert.True(retried);

            var tasks = await service.GetFailedImportTasksAsync();
            Assert.Empty(tasks);

            using var connection = new SqliteConnection(database.ConnectionString);
            await connection.OpenAsync();

            using var statusCommand = connection.CreateCommand();
            statusCommand.CommandText = "SELECT status FROM import_task WHERE task_id = $taskId LIMIT 1;";
            statusCommand.Parameters.AddWithValue("$taskId", failedTaskId.ToString());
            var status = Convert.ToString(await statusCommand.ExecuteScalarAsync());
            Assert.Equal("Pending", status);

            using var eventCommand = connection.CreateCommand();
            eventCommand.CommandText = "SELECT event_type FROM import_task_event WHERE task_id = $taskId ORDER BY event_id DESC LIMIT 1;";
            eventCommand.Parameters.AddWithValue("$taskId", failedTaskId.ToString());
            var eventType = Convert.ToString(await eventCommand.ExecuteScalarAsync());
            Assert.Equal("retry_requested", eventType);
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
            INSERT INTO series (title, normalized_title, created_at)
            VALUES ($title, $normalizedTitle, $createdAt);
            """;
        insert.Parameters.AddWithValue("$title", title);
        insert.Parameters.AddWithValue("$normalizedTitle", title.Trim().ToLowerInvariant());
        insert.Parameters.AddWithValue("$createdAt", createdAt);
        insert.ExecuteNonQuery();

        using var lastId = connection.CreateCommand();
        lastId.CommandText = "SELECT last_insert_rowid();";
        return Convert.ToInt64(lastId.ExecuteScalar());
    }

    private static long InsertVolume(string connectionString, long? seriesId, string title, string createdAt)
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
            insertVolume.Parameters.AddWithValue("$seriesId", (object?)seriesId ?? DBNull.Value);
            insertVolume.Parameters.AddWithValue("$title", title);
            insertVolume.Parameters.AddWithValue("$createdAt", createdAt);
            insertVolume.ExecuteNonQuery();
        }

        using var lastId = connection.CreateCommand();
        lastId.CommandText = "SELECT last_insert_rowid();";
        return Convert.ToInt64(lastId.ExecuteScalar());
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

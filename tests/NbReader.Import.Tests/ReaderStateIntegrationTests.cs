using Microsoft.Data.Sqlite;
using NbReader.Catalog;
using NbReader.Infrastructure;
using NbReader.Reader;

namespace NbReader.Import.Tests;

public sealed class ReaderStateIntegrationTests
{
    [Fact]
    public async Task OpenNavigateAndSwitchVolume_ShouldFollowExpectedLifecycle()
    {
        var dbPath = CreateTempDbPath();

        try
        {
            var database = new AppDatabase(dbPath);
            database.Initialize();

            var seriesId = InsertSeries(database.ConnectionString, "Series A", "2026-03-01T00:00:00Z");
            var volume1 = InsertVolume(database.ConnectionString, seriesId, "Vol.01", "2026-03-02T00:00:00Z", ["1.jpg", "2.jpg", "3.jpg"]);
            var volume2 = InsertVolume(database.ConnectionString, seriesId, "Vol.02", "2026-03-03T00:00:00Z", ["1.jpg", "2.jpg"]);

            var volumeQueryService = new VolumeQueryService(database.ConnectionString);
            var context1 = await volumeQueryService.GetVolumeReaderContextAsync(volume1);
            Assert.NotNull(context1);

            var state = ReaderStateMachine.OpenVolume(context1!.VolumeId, context1.VolumeTitle, context1.SourcePath, context1.PageLocators);
            Assert.Equal(ReaderLifecycle.VolumeReady, state.Lifecycle);
            Assert.Equal(0, state.CurrentPageIndex);

            state = ReaderStateMachine.NavigateTo(state, 2);
            Assert.Equal(ReaderLifecycle.PageLoading, state.Lifecycle);
            Assert.Equal(2, state.CurrentPageIndex);

            state = ReaderStateMachine.MarkPageReady(state);
            Assert.Equal(ReaderLifecycle.PageReady, state.Lifecycle);
            Assert.True(state.CanMovePrevious);
            Assert.False(state.CanMoveNext);

            var nextVolumeId = await volumeQueryService.GetNextVolumeIdAsync(volume1);
            Assert.Equal(volume2, nextVolumeId);

            var context2 = await volumeQueryService.GetVolumeReaderContextAsync(nextVolumeId!.Value);
            Assert.NotNull(context2);

            var switched = ReaderStateMachine.OpenVolume(context2!.VolumeId, context2.VolumeTitle, context2.SourcePath, context2.PageLocators);
            Assert.Equal(ReaderLifecycle.VolumeReady, switched.Lifecycle);
            Assert.Equal(volume2, switched.VolumeId);
            Assert.Equal(0, switched.CurrentPageIndex);
            Assert.Equal(2, switched.TotalPages);
        }
        finally
        {
            CleanupDatabaseFiles(dbPath);
        }
    }

    [Fact]
    public async Task SwitchVolume_WhenNoNextVolume_ShouldReturnNull()
    {
        var dbPath = CreateTempDbPath();

        try
        {
            var database = new AppDatabase(dbPath);
            database.Initialize();

            var seriesId = InsertSeries(database.ConnectionString, "Series B", "2026-03-01T00:00:00Z");
            var onlyVolume = InsertVolume(database.ConnectionString, seriesId, "Vol.01", "2026-03-02T00:00:00Z", ["1.jpg"]);

            var volumeQueryService = new VolumeQueryService(database.ConnectionString);
            var nextVolumeId = await volumeQueryService.GetNextVolumeIdAsync(onlyVolume);

            Assert.Null(nextVolumeId);
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

    private static long InsertVolume(
        string connectionString,
        long seriesId,
        string volumeTitle,
        string createdAt,
        IReadOnlyList<string> pageLocators)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var transaction = connection.BeginTransaction();

        var sourcePath = $"/tmp/{seriesId}/{volumeTitle}";
        using (var insertSource = connection.CreateCommand())
        {
            insertSource.Transaction = transaction;
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
            querySource.Transaction = transaction;
            querySource.CommandText = "SELECT source_id FROM source WHERE source_path = $sourcePath LIMIT 1;";
            querySource.Parameters.AddWithValue("$sourcePath", sourcePath);
            sourceId = Convert.ToInt64(querySource.ExecuteScalar());
        }

        long volumeId;
        using (var insertVolume = connection.CreateCommand())
        {
            insertVolume.Transaction = transaction;
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

        using (var queryLastId = connection.CreateCommand())
        {
            queryLastId.Transaction = transaction;
            queryLastId.CommandText = "SELECT last_insert_rowid();";
            volumeId = Convert.ToInt64(queryLastId.ExecuteScalar());
        }

        for (var pageNumber = 1; pageNumber <= pageLocators.Count; pageNumber++)
        {
            using var insertPage = connection.CreateCommand();
            insertPage.Transaction = transaction;
            insertPage.CommandText = """
                INSERT INTO page (volume_id, page_number, page_locator, created_at)
                VALUES ($volumeId, $pageNumber, $locator, $createdAt);
                """;
            insertPage.Parameters.AddWithValue("$volumeId", volumeId);
            insertPage.Parameters.AddWithValue("$pageNumber", pageNumber);
            insertPage.Parameters.AddWithValue("$locator", pageLocators[pageNumber - 1]);
            insertPage.Parameters.AddWithValue("$createdAt", createdAt);
            insertPage.ExecuteNonQuery();
        }

        transaction.Commit();
        return volumeId;
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
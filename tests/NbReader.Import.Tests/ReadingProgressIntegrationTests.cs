using Microsoft.Data.Sqlite;
using NbReader.Catalog;
using NbReader.Infrastructure;

namespace NbReader.Import.Tests;

public sealed class ReadingProgressIntegrationTests
{
    [Fact]
    public async Task UpsertAndGetProgress_ShouldPersistAndRestoreSnapshot()
    {
        var dbPath = CreateTempDbPath();

        try
        {
            var database = new AppDatabase(dbPath);
            database.Initialize();

            var volumeId = InsertVolumeWithPages(database.ConnectionString, "Series A", "Vol.01", pageCount: 5, createdAt: "2026-01-01T00:00:00Z");
            var service = new ReadingProgressService(database.ConnectionString);

            var now = DateTimeOffset.UtcNow;
            await service.UpsertProgressAsync(new ReadingProgressSnapshot(
                VolumeId: volumeId,
                CurrentPageIndex: 2,
                MaxPageReached: 2,
                Completed: false,
                LastReadAt: now,
                ReadingMode: "double",
                ReadingDirection: "rtl",
                UpdatedAt: now));

            var restored = await service.GetProgressAsync(volumeId);

            Assert.NotNull(restored);
            Assert.Equal(2, restored!.CurrentPageIndex);
            Assert.Equal(2, restored.MaxPageReached);
            Assert.False(restored.Completed);
            Assert.Equal("double", restored.ReadingMode);
            Assert.Equal("rtl", restored.ReadingDirection);
        }
        finally
        {
            CleanupDatabaseFiles(dbPath);
        }
    }

    [Fact]
    public async Task UpsertProgress_ShouldKeepMaxPageReachedAsGreatestValue()
    {
        var dbPath = CreateTempDbPath();

        try
        {
            var database = new AppDatabase(dbPath);
            database.Initialize();

            var volumeId = InsertVolumeWithPages(database.ConnectionString, "Series A", "Vol.01", pageCount: 10, createdAt: "2026-01-01T00:00:00Z");
            var service = new ReadingProgressService(database.ConnectionString);

            var first = DateTimeOffset.UtcNow.AddMinutes(-2);
            var second = DateTimeOffset.UtcNow;

            await service.UpsertProgressAsync(new ReadingProgressSnapshot(
                VolumeId: volumeId,
                CurrentPageIndex: 6,
                MaxPageReached: 6,
                Completed: false,
                LastReadAt: first,
                ReadingMode: "single",
                ReadingDirection: "ltr",
                UpdatedAt: first));

            await service.UpsertProgressAsync(new ReadingProgressSnapshot(
                VolumeId: volumeId,
                CurrentPageIndex: 3,
                MaxPageReached: 3,
                Completed: false,
                LastReadAt: second,
                ReadingMode: "single",
                ReadingDirection: "ltr",
                UpdatedAt: second));

            var restored = await service.GetProgressAsync(volumeId);

            Assert.NotNull(restored);
            Assert.Equal(3, restored!.CurrentPageIndex);
            Assert.Equal(6, restored.MaxPageReached);
        }
        finally
        {
            CleanupDatabaseFiles(dbPath);
        }
    }

    [Fact]
    public async Task GetRecentReadingsAndNextVolume_ShouldFollowExpectedOrder()
    {
        var dbPath = CreateTempDbPath();

        try
        {
            var database = new AppDatabase(dbPath);
            database.Initialize();

            var volumeA = InsertVolumeWithPages(database.ConnectionString, "Series A", "Vol.01", pageCount: 5, createdAt: "2026-01-01T00:00:00Z");
            var volumeB = InsertVolumeWithPages(database.ConnectionString, "Series A", "Vol.02", pageCount: 5, createdAt: "2026-01-02T00:00:00Z");
            var volumeC = InsertVolumeWithPages(database.ConnectionString, "Series A", "Vol.03", pageCount: 5, createdAt: "2026-01-03T00:00:00Z");

            var progressService = new ReadingProgressService(database.ConnectionString);
            await progressService.UpsertProgressAsync(new ReadingProgressSnapshot(
                VolumeId: volumeA,
                CurrentPageIndex: 1,
                MaxPageReached: 1,
                Completed: false,
                LastReadAt: DateTimeOffset.Parse("2026-02-01T00:00:00Z"),
                ReadingMode: "single",
                ReadingDirection: "ltr",
                UpdatedAt: DateTimeOffset.Parse("2026-02-01T00:00:00Z")));

            await progressService.UpsertProgressAsync(new ReadingProgressSnapshot(
                VolumeId: volumeC,
                CurrentPageIndex: 4,
                MaxPageReached: 4,
                Completed: true,
                LastReadAt: DateTimeOffset.Parse("2026-02-03T00:00:00Z"),
                ReadingMode: "double",
                ReadingDirection: "rtl",
                UpdatedAt: DateTimeOffset.Parse("2026-02-03T00:00:00Z")));

            var recent = await progressService.GetRecentReadingsAsync(limit: 5);
            Assert.Equal(2, recent.Count);
            Assert.Equal(volumeC, recent[0].VolumeId);
            Assert.Equal(volumeA, recent[1].VolumeId);

            var volumeQueryService = new VolumeQueryService(database.ConnectionString);
            var nextOfA = await volumeQueryService.GetNextVolumeIdAsync(volumeA);
            var nextOfC = await volumeQueryService.GetNextVolumeIdAsync(volumeC);

            Assert.Equal(volumeB, nextOfA);
            Assert.Null(nextOfC);
        }
        finally
        {
            CleanupDatabaseFiles(dbPath);
        }
    }

    private static long InsertVolumeWithPages(string connectionString, string seriesTitle, string volumeTitle, int pageCount, string createdAt)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var transaction = connection.BeginTransaction();

        var normalizedTitle = seriesTitle.Trim().ToLowerInvariant();
        using (var insertSeries = connection.CreateCommand())
        {
            insertSeries.Transaction = transaction;
            insertSeries.CommandText = """
                INSERT INTO series (title, normalized_title, created_at)
                VALUES ($title, $normalized, $createdAt)
                ON CONFLICT(normalized_title) DO NOTHING;
                """;
            insertSeries.Parameters.AddWithValue("$title", seriesTitle);
            insertSeries.Parameters.AddWithValue("$normalized", normalizedTitle);
            insertSeries.Parameters.AddWithValue("$createdAt", createdAt);
            insertSeries.ExecuteNonQuery();
        }

        long seriesId;
        using (var querySeries = connection.CreateCommand())
        {
            querySeries.Transaction = transaction;
            querySeries.CommandText = "SELECT series_id FROM series WHERE normalized_title = $normalized LIMIT 1;";
            querySeries.Parameters.AddWithValue("$normalized", normalizedTitle);
            seriesId = Convert.ToInt64(querySeries.ExecuteScalar());
        }

        var sourcePath = $"/tmp/{seriesTitle}/{volumeTitle}";
        using (var insertSource = connection.CreateCommand())
        {
            insertSource.Transaction = transaction;
            insertSource.CommandText = """
                INSERT INTO source (source_path, source_type, created_at)
                VALUES ($path, 'local_path', $createdAt);
                """;
            insertSource.Parameters.AddWithValue("$path", sourcePath);
            insertSource.Parameters.AddWithValue("$createdAt", createdAt);
            insertSource.ExecuteNonQuery();
        }

        long sourceId;
        using (var querySource = connection.CreateCommand())
        {
            querySource.Transaction = transaction;
            querySource.CommandText = "SELECT source_id FROM source WHERE source_path = $path LIMIT 1;";
            querySource.Parameters.AddWithValue("$path", sourcePath);
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

        for (var pageNumber = 1; pageNumber <= pageCount; pageNumber++)
        {
            using var insertPage = connection.CreateCommand();
            insertPage.Transaction = transaction;
            insertPage.CommandText = """
                INSERT INTO page (volume_id, page_number, page_locator, created_at)
                VALUES ($volumeId, $pageNumber, $locator, $createdAt);
                """;
            insertPage.Parameters.AddWithValue("$volumeId", volumeId);
            insertPage.Parameters.AddWithValue("$pageNumber", pageNumber);
            insertPage.Parameters.AddWithValue("$locator", $"{pageNumber}.jpg");
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

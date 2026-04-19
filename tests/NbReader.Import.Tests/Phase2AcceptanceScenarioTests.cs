using Microsoft.Data.Sqlite;
using NbReader.Catalog;
using NbReader.Infrastructure;
using NbReader.Reader;

namespace NbReader.Import.Tests;

public sealed class Phase2AcceptanceScenarioTests
{
    [Fact]
    public async Task Scenario1_UserCanEnterVolumeAndFlipPages()
    {
        var workspace = CreateTempWorkspace();
        var dbPath = Path.Combine(workspace, "app.db");

        try
        {
            var volumeDir = Path.Combine(workspace, "Series-A", "Vol.01");
            Directory.CreateDirectory(volumeDir);
            File.WriteAllText(Path.Combine(volumeDir, "1.jpg"), "p1");
            File.WriteAllText(Path.Combine(volumeDir, "2.jpg"), "p2");
            File.WriteAllText(Path.Combine(volumeDir, "3.jpg"), "p3");

            var database = new AppDatabase(dbPath);
            database.Initialize();

            var seriesId = InsertSeries(database.ConnectionString, "Series A", "2026-04-01T00:00:00Z");
            var volumeId = InsertVolume(database.ConnectionString, seriesId, "Vol.01", volumeDir, "2026-04-02T00:00:00Z", ["1.jpg", "2.jpg", "3.jpg"]);

            var volumeQuery = new VolumeQueryService(database.ConnectionString);
            var context = await volumeQuery.GetVolumeReaderContextAsync(volumeId);
            Assert.NotNull(context);

            var state = ReaderStateMachine.OpenVolume(context!.VolumeId, context.VolumeTitle, context.SourcePath, context.PageLocators);
            Assert.Equal(ReaderLifecycle.VolumeReady, state.Lifecycle);

            var pageSource = new UnifiedVolumePageSource();
            using (var first = pageSource.OpenPageStream(context.SourcePath, context.PageLocators[0]))
            {
                Assert.NotNull(first);
            }

            state = ReaderStateMachine.NavigateTo(state, 1);
            state = ReaderStateMachine.MarkPageReady(state);
            Assert.Equal(ReaderLifecycle.PageReady, state.Lifecycle);
            Assert.Equal(1, state.CurrentPageIndex);

            using (var second = pageSource.OpenPageStream(context.SourcePath, context.PageLocators[state.CurrentPageIndex]))
            {
                Assert.NotNull(second);
            }
        }
        finally
        {
            CleanupDirectory(workspace);
        }
    }

    [Fact]
    public async Task Scenario2_RestoreLastPageAfterReopen()
    {
        var dbPath = CreateTempDbPath();

        try
        {
            var database = new AppDatabase(dbPath);
            database.Initialize();

            var seriesId = InsertSeries(database.ConnectionString, "Series A", "2026-04-01T00:00:00Z");
            var volumeId = InsertVolume(database.ConnectionString, seriesId, "Vol.01", "/tmp/series-a/vol-01", "2026-04-02T00:00:00Z", ["1.jpg", "2.jpg", "3.jpg", "4.jpg"]);

            var progressService = new ReadingProgressService(database.ConnectionString);
            var now = DateTimeOffset.UtcNow;
            await progressService.UpsertProgressAsync(new ReadingProgressSnapshot(
                VolumeId: volumeId,
                CurrentPageIndex: 2,
                MaxPageReached: 2,
                Completed: false,
                LastReadAt: now,
                ReadingMode: "single",
                ReadingDirection: "ltr",
                UpdatedAt: now));

            var restored = await progressService.GetProgressAsync(volumeId);
            Assert.NotNull(restored);
            Assert.Equal(2, restored!.CurrentPageIndex);
        }
        finally
        {
            CleanupDatabaseFiles(dbPath);
        }
    }

    [Fact]
    public void Scenario3_DualPageDirectionSwitch_ShouldMapCorrectly()
    {
        var ltr = ReaderSpreadRules.BuildSpread(
            anchorPageIndex: 1,
            totalPages: 6,
            mode: ReaderDisplayMode.DualPage,
            direction: ReaderReadingDirection.LeftToRight,
            coverSinglePage: true);

        var rtl = ReaderSpreadRules.BuildSpread(
            anchorPageIndex: 1,
            totalPages: 6,
            mode: ReaderDisplayMode.DualPage,
            direction: ReaderReadingDirection.RightToLeft,
            coverSinglePage: true);

        Assert.Equal(1, ltr.LeftPageIndex);
        Assert.Equal(2, ltr.RightPageIndex);
        Assert.Equal(2, rtl.LeftPageIndex);
        Assert.Equal(1, rtl.RightPageIndex);
    }

    [Fact]
    public async Task Scenario4_EndOfCurrentVolume_ShouldMoveToNextVolume()
    {
        var dbPath = CreateTempDbPath();

        try
        {
            var database = new AppDatabase(dbPath);
            database.Initialize();

            var seriesId = InsertSeries(database.ConnectionString, "Series B", "2026-04-01T00:00:00Z");
            var volume1 = InsertVolume(database.ConnectionString, seriesId, "Vol.01", "/tmp/series-b/vol-01", "2026-04-02T00:00:00Z", ["1.jpg", "2.jpg"]);
            var volume2 = InsertVolume(database.ConnectionString, seriesId, "Vol.02", "/tmp/series-b/vol-02", "2026-04-03T00:00:00Z", ["1.jpg"]);

            var volumeQuery = new VolumeQueryService(database.ConnectionString);
            var next = await volumeQuery.GetNextVolumeIdAsync(volume1);

            Assert.Equal(volume2, next);
        }
        finally
        {
            CleanupDatabaseFiles(dbPath);
        }
    }

    [Fact]
    public async Task Scenario5_RecentReadingAndContinueReading_ShouldBeUsable()
    {
        var dbPath = CreateTempDbPath();

        try
        {
            var database = new AppDatabase(dbPath);
            database.Initialize();

            var seriesId = InsertSeries(database.ConnectionString, "Series C", "2026-04-01T00:00:00Z");
            var volumeA = InsertVolume(database.ConnectionString, seriesId, "Vol.01", "/tmp/series-c/vol-01", "2026-04-02T00:00:00Z", ["1.jpg", "2.jpg", "3.jpg"]);
            var volumeB = InsertVolume(database.ConnectionString, seriesId, "Vol.02", "/tmp/series-c/vol-02", "2026-04-03T00:00:00Z", ["1.jpg", "2.jpg", "3.jpg"]);

            var progressService = new ReadingProgressService(database.ConnectionString);
            await progressService.UpsertProgressAsync(new ReadingProgressSnapshot(
                VolumeId: volumeA,
                CurrentPageIndex: 1,
                MaxPageReached: 1,
                Completed: false,
                LastReadAt: DateTimeOffset.Parse("2026-04-10T10:00:00Z"),
                ReadingMode: "single",
                ReadingDirection: "ltr",
                UpdatedAt: DateTimeOffset.Parse("2026-04-10T10:00:00Z")));

            await progressService.UpsertProgressAsync(new ReadingProgressSnapshot(
                VolumeId: volumeB,
                CurrentPageIndex: 2,
                MaxPageReached: 2,
                Completed: true,
                LastReadAt: DateTimeOffset.Parse("2026-04-10T11:00:00Z"),
                ReadingMode: "double",
                ReadingDirection: "rtl",
                UpdatedAt: DateTimeOffset.Parse("2026-04-10T11:00:00Z")));

            var recent = await progressService.GetRecentReadingsAsync(limit: 8);
            Assert.Equal(2, recent.Count);
            Assert.Equal(volumeB, recent[0].VolumeId);
            Assert.Equal(volumeA, recent[1].VolumeId);

            var continueReading = recent.FirstOrDefault(x => !x.Completed) ?? recent.FirstOrDefault();
            Assert.NotNull(continueReading);
            Assert.Equal(volumeA, continueReading!.VolumeId);
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
        string sourcePath,
        string createdAt,
        IReadOnlyList<string> pageLocators)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var transaction = connection.BeginTransaction();

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

    private static string CreateTempWorkspace()
    {
        var path = Path.Combine(Path.GetTempPath(), "nbreader-phase2-acceptance", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
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

    private static void CleanupDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
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
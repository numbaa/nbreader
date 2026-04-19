using Microsoft.Data.Sqlite;
using NbReader.Catalog;
using NbReader.Infrastructure;
using NbReader.Search;

namespace NbReader.Import.Tests;

public sealed class SeriesCorrectionServiceTests
{
    [Fact]
    public async Task RenameSeriesAsync_ShouldUpdateTitle_AndRemainSearchable()
    {
        var dbPath = CreateTempDbPath();

        try
        {
            var database = new AppDatabase(dbPath);
            database.Initialize();

            var seriesId = InsertSeries(database.ConnectionString, "Old Name", "2026-01-01T00:00:00Z");
            InsertVolume(database.ConnectionString, seriesId, "Vol.01", "2026-01-01T00:00:00Z");

            var correction = new SeriesCorrectionService(database.ConnectionString);
            var search = new SeriesSearchService(database.ConnectionString);

            var renamed = await correction.RenameSeriesAsync(seriesId, "New Name");
            Assert.True(renamed);

            var rows = await search.SearchAsync(new SeriesSearchQuery(TitleKeyword: "New"));
            Assert.Single(rows);
            Assert.Equal(seriesId, rows[0].SeriesId);
            Assert.Equal("New Name", rows[0].Title);
        }
        finally
        {
            CleanupDatabaseFiles(dbPath);
        }
    }

    [Fact]
    public async Task MergeSeriesAsync_ShouldMoveVolumes_AndDeleteSource()
    {
        var dbPath = CreateTempDbPath();

        try
        {
            var database = new AppDatabase(dbPath);
            database.Initialize();

            var sourceId = InsertSeries(database.ConnectionString, "Source", "2026-01-01T00:00:00Z");
            var targetId = InsertSeries(database.ConnectionString, "Target", "2026-01-02T00:00:00Z");

            InsertVolume(database.ConnectionString, sourceId, "S-Vol.01", "2026-01-03T00:00:00Z");
            InsertVolume(database.ConnectionString, targetId, "T-Vol.01", "2026-01-04T00:00:00Z");

            var correction = new SeriesCorrectionService(database.ConnectionString);
            var merged = await correction.MergeSeriesAsync(sourceId, targetId);
            Assert.True(merged);

            var search = new SeriesSearchService(database.ConnectionString);
            var sourceRows = await search.SearchAsync(new SeriesSearchQuery(TitleKeyword: "Source"));
            Assert.Empty(sourceRows);

            var targetRows = await search.SearchAsync(new SeriesSearchQuery(TitleKeyword: "Target"));
            Assert.Single(targetRows);
            Assert.Equal(2, targetRows[0].VolumeCount);
        }
        finally
        {
            CleanupDatabaseFiles(dbPath);
        }
    }

    [Fact]
    public async Task UpdateVolumeNumberAsync_ShouldAffectVolumeOrdering()
    {
        var dbPath = CreateTempDbPath();

        try
        {
            var database = new AppDatabase(dbPath);
            database.Initialize();

            var seriesId = InsertSeries(database.ConnectionString, "Series", "2026-01-01T00:00:00Z");
            var laterVolumeId = InsertVolume(database.ConnectionString, seriesId, "Vol.02", "2026-01-02T00:00:00Z");
            var earlierVolumeId = InsertVolume(database.ConnectionString, seriesId, "Vol.01", "2026-01-01T00:00:00Z");

            var correction = new SeriesCorrectionService(database.ConnectionString);
            Assert.True(await correction.UpdateVolumeNumberAsync(laterVolumeId, 1));
            Assert.True(await correction.UpdateVolumeNumberAsync(earlierVolumeId, 2));

            var volumes = await new VolumeQueryService(database.ConnectionString).GetVolumesBySeriesAsync(seriesId);
            Assert.Equal(2, volumes.Count);
            Assert.Equal(laterVolumeId, volumes[0].VolumeId);
            Assert.Equal(1, volumes[0].VolumeNumber);
            Assert.Equal(earlierVolumeId, volumes[1].VolumeId);
            Assert.Equal(2, volumes[1].VolumeNumber);
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

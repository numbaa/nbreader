using Microsoft.Data.Sqlite;
using NbReader.Catalog;
using NbReader.Infrastructure;

namespace NbReader.Import.Tests;

public sealed class SeriesQueryServiceTests
{
    [Fact]
    public async Task GetSeriesListAsync_ShouldAggregateVolumeCount_AndSortByLatestUpdatedAt()
    {
        var dbPath = CreateTempDbPath();

        try
        {
            var database = new AppDatabase(dbPath);
            database.Initialize();

            var seriesA = InsertSeries(database.ConnectionString, "Series A", "2026-01-01T00:00:00Z");
            var seriesB = InsertSeries(database.ConnectionString, "Series B", "2026-01-05T00:00:00Z");
            var seriesC = InsertSeries(database.ConnectionString, "Series C", "2026-01-09T00:00:00Z");

            InsertVolume(database.ConnectionString, seriesA, "Vol.01", "2026-01-10T00:00:00Z");
            InsertVolume(database.ConnectionString, seriesA, "Vol.02", "2026-01-11T00:00:00Z");
            InsertVolume(database.ConnectionString, seriesB, "Vol.01", "2026-01-08T00:00:00Z");

            var service = new SeriesQueryService(database.ConnectionString);
            var list = await service.GetSeriesListAsync();

            Assert.Equal(3, list.Count);
            Assert.Equal("Series A", list[0].Title);
            Assert.Equal(2, list[0].VolumeCount);
            Assert.Equal("Series C", list[1].Title);
            Assert.Equal(0, list[1].VolumeCount);
            Assert.Equal("Series B", list[2].Title);
            Assert.Equal(1, list[2].VolumeCount);
        }
        finally
        {
            CleanupDatabaseFiles(dbPath);
        }
    }

    [Fact]
    public async Task GetSeriesListAsync_ShouldRespectLimit()
    {
        var dbPath = CreateTempDbPath();

        try
        {
            var database = new AppDatabase(dbPath);
            database.Initialize();

            var seriesA = InsertSeries(database.ConnectionString, "Series A", "2026-01-01T00:00:00Z");
            var seriesB = InsertSeries(database.ConnectionString, "Series B", "2026-01-02T00:00:00Z");

            InsertVolume(database.ConnectionString, seriesA, "Vol.01", "2026-01-10T00:00:00Z");
            InsertVolume(database.ConnectionString, seriesB, "Vol.01", "2026-01-11T00:00:00Z");

            var service = new SeriesQueryService(database.ConnectionString);
            var list = await service.GetSeriesListAsync(limit: 1);

            Assert.Single(list);
            Assert.Equal("Series B", list[0].Title);
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
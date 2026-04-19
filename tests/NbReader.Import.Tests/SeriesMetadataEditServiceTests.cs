using Microsoft.Data.Sqlite;
using NbReader.Infrastructure;
using NbReader.Search;

namespace NbReader.Import.Tests;

public sealed class SeriesMetadataEditServiceTests
{
    [Fact]
    public async Task ReplaceSeriesMetadataAsync_ShouldPersistAuthorsAndTags_AndSupportSearch()
    {
        var dbPath = CreateTempDbPath();

        try
        {
            var database = new AppDatabase(dbPath);
            database.Initialize();

            var seriesId = InsertSeries(database.ConnectionString, "Series A", "2026-04-01T00:00:00Z");
            InsertVolume(database.ConnectionString, seriesId, "Vol.01", "2026-04-02T00:00:00Z");

            var metadataService = new SeriesMetadataEditService(database.ConnectionString);
            var updated = await metadataService.ReplaceSeriesMetadataAsync(seriesId, ["Author One", "Author Two"], ["Action", "Drama"]);
            Assert.True(updated);

            var snapshot = await metadataService.GetSeriesMetadataAsync(seriesId);
            Assert.Equal(2, snapshot.Authors.Count);
            Assert.Contains("Author One", snapshot.Authors);
            Assert.Contains("Action", snapshot.Tags);

            var search = new SeriesSearchService(database.ConnectionString);
            var byAuthor = await search.SearchAsync(new SeriesSearchQuery(AuthorKeyword: "Author"));
            var byTag = await search.SearchAsync(new SeriesSearchQuery(TagKeyword: "Action"));

            Assert.Single(byAuthor);
            Assert.Single(byTag);
            Assert.Equal(seriesId, byAuthor[0].SeriesId);
            Assert.Equal(seriesId, byTag[0].SeriesId);
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

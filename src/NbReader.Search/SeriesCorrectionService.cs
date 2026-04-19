using Microsoft.Data.Sqlite;

namespace NbReader.Search;

public sealed class SeriesCorrectionService
{
    private readonly string _connectionString;

    public SeriesCorrectionService(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    public async Task<bool> RenameSeriesAsync(long seriesId, string newTitle, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newTitle))
        {
            return false;
        }

        var normalized = Normalize(newTitle);

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE series
            SET title = $title,
                normalized_title = $normalizedTitle,
                title_pinyin = COALESCE(NULLIF(title_pinyin, ''), $normalizedTitle)
            WHERE series_id = $seriesId;
            """;
        command.Parameters.AddWithValue("$title", newTitle.Trim());
        command.Parameters.AddWithValue("$normalizedTitle", normalized);
        command.Parameters.AddWithValue("$seriesId", seriesId);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0;
    }

    public async Task<bool> MergeSeriesAsync(long sourceSeriesId, long targetSeriesId, CancellationToken cancellationToken = default)
    {
        if (sourceSeriesId <= 0 || targetSeriesId <= 0 || sourceSeriesId == targetSeriesId)
        {
            return false;
        }

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        using var sourceExists = connection.CreateCommand();
        sourceExists.Transaction = transaction;
        sourceExists.CommandText = "SELECT COUNT(1) FROM series WHERE series_id = $seriesId;";
        sourceExists.Parameters.AddWithValue("$seriesId", sourceSeriesId);

        using var targetExists = connection.CreateCommand();
        targetExists.Transaction = transaction;
        targetExists.CommandText = "SELECT COUNT(1) FROM series WHERE series_id = $seriesId;";
        targetExists.Parameters.AddWithValue("$seriesId", targetSeriesId);

        var hasSource = Convert.ToInt32(await sourceExists.ExecuteScalarAsync(cancellationToken)) > 0;
        var hasTarget = Convert.ToInt32(await targetExists.ExecuteScalarAsync(cancellationToken)) > 0;
        if (!hasSource || !hasTarget)
        {
            transaction.Rollback();
            return false;
        }

        using (var moveVolumes = connection.CreateCommand())
        {
            moveVolumes.Transaction = transaction;
            moveVolumes.CommandText = """
                UPDATE volume
                SET series_id = $targetSeriesId
                WHERE series_id = $sourceSeriesId;
                """;
            moveVolumes.Parameters.AddWithValue("$targetSeriesId", targetSeriesId);
            moveVolumes.Parameters.AddWithValue("$sourceSeriesId", sourceSeriesId);
            await moveVolumes.ExecuteNonQueryAsync(cancellationToken);
        }

        using (var copyPeople = connection.CreateCommand())
        {
            copyPeople.Transaction = transaction;
            copyPeople.CommandText = """
                INSERT INTO series_person (series_id, person_id, role, sort_order, source)
                SELECT $targetSeriesId, sp.person_id, sp.role, sp.sort_order, sp.source
                FROM series_person sp
                WHERE sp.series_id = $sourceSeriesId
                ON CONFLICT(series_id, person_id, role) DO NOTHING;
                """;
            copyPeople.Parameters.AddWithValue("$targetSeriesId", targetSeriesId);
            copyPeople.Parameters.AddWithValue("$sourceSeriesId", sourceSeriesId);
            await copyPeople.ExecuteNonQueryAsync(cancellationToken);
        }

        using (var copyTags = connection.CreateCommand())
        {
            copyTags.Transaction = transaction;
            copyTags.CommandText = """
                INSERT INTO series_tag (series_id, tag_id, source)
                SELECT $targetSeriesId, st.tag_id, st.source
                FROM series_tag st
                WHERE st.series_id = $sourceSeriesId
                ON CONFLICT(series_id, tag_id) DO NOTHING;
                """;
            copyTags.Parameters.AddWithValue("$targetSeriesId", targetSeriesId);
            copyTags.Parameters.AddWithValue("$sourceSeriesId", sourceSeriesId);
            await copyTags.ExecuteNonQueryAsync(cancellationToken);
        }

        using (var deleteSource = connection.CreateCommand())
        {
            deleteSource.Transaction = transaction;
            deleteSource.CommandText = "DELETE FROM series WHERE series_id = $sourceSeriesId;";
            deleteSource.Parameters.AddWithValue("$sourceSeriesId", sourceSeriesId);
            await deleteSource.ExecuteNonQueryAsync(cancellationToken);
        }

        transaction.Commit();
        return true;
    }

    public async Task<bool> UpdateVolumeNumberAsync(long volumeId, int? volumeNumber, CancellationToken cancellationToken = default)
    {
        if (volumeId <= 0)
        {
            return false;
        }

        if (volumeNumber is <= 0)
        {
            volumeNumber = null;
        }

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE volume
            SET volume_number = $volumeNumber
            WHERE volume_id = $volumeId;
            """;
        command.Parameters.AddWithValue("$volumeNumber", (object?)volumeNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("$volumeId", volumeId);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0;
    }

    private static string Normalize(string text)
    {
        return text.Trim().ToLowerInvariant();
    }
}

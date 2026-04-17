using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.Data.Sqlite;
using NbReader.Import;

namespace NbReader.Infrastructure;

public sealed class ImportWriteService
{
    private readonly string _connectionString;
    private readonly AppLogger? _logger;

    public ImportWriteService(AppDatabase database, AppLogger? logger = null)
    {
        _connectionString = database.ConnectionString;
        _logger = logger;
    }

    public ImportPersistResult Persist(ImportPlan plan, string sourceType = "local_path")
    {
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        var validationError = ValidatePlan(plan);
        if (validationError is not null)
        {
            return ImportPersistResult.CreateFailure(
                validationError.Value,
                ImportErrorCatalog.GetMessage(validationError.Value),
                startedAt,
                TimeSpan.Zero);
        }

        _logger?.Info($"task_id={plan.TaskId} stage=importing action=start volume_count={plan.VolumePlans.Count}");

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();

            var insertedOrUpdatedVolumes = 0;
            var skippedVolumes = 0;
            var writtenPages = 0;

            foreach (var volume in plan.VolumePlans)
            {
                if (volume.PageLocators.Count == 0)
                {
                    skippedVolumes++;
                    continue;
                }

                var sourceId = UpsertSource(connection, transaction, volume.SourceLocator, sourceType);
                var seriesTitle = volume.SeriesCandidate ?? plan.SeriesCandidate ?? "Unsorted";
                var seriesId = UpsertSeries(connection, transaction, seriesTitle);
                var volumeId = UpsertVolume(connection, transaction, sourceId, seriesId, volume.DisplayName);

                ClearPages(connection, transaction, volumeId);
                writtenPages += InsertPages(connection, transaction, volumeId, volume.PageLocators);
                insertedOrUpdatedVolumes++;
            }

            transaction.Commit();
            stopwatch.Stop();

            var summary = new ImportWriteSummary(insertedOrUpdatedVolumes, skippedVolumes, writtenPages, plan.VolumePlans.Count);
            _logger?.Info($"task_id={plan.TaskId} stage=importing action=completed elapsed_ms={stopwatch.ElapsedMilliseconds} volumes={insertedOrUpdatedVolumes} pages={writtenPages}");
            return ImportPersistResult.CreateSuccess(summary, startedAt, stopwatch.Elapsed);
        }
        catch (SqliteException exception)
        {
            stopwatch.Stop();
            var errorCode = exception.SqliteErrorCode == 19
                ? ImportErrorCode.DbConstraintFailed
                : ImportErrorCode.DbWriteFailed;

            _logger?.Error($"task_id={plan.TaskId} stage=importing action=failed elapsed_ms={stopwatch.ElapsedMilliseconds} error_code={errorCode}", exception);
            return ImportPersistResult.CreateFailure(errorCode, ImportErrorCatalog.Format(errorCode, exception.Message), startedAt, stopwatch.Elapsed);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            _logger?.Error($"task_id={plan.TaskId} stage=importing action=failed elapsed_ms={stopwatch.ElapsedMilliseconds} error_code={ImportErrorCode.Unknown}", exception);
            return ImportPersistResult.CreateFailure(
                ImportErrorCode.Unknown,
                ImportErrorCatalog.Format(ImportErrorCode.Unknown, exception.Message),
                startedAt,
                stopwatch.Elapsed);
        }
    }

    private static ImportErrorCode? ValidatePlan(ImportPlan plan)
    {
        if (string.IsNullOrWhiteSpace(plan.InputLocator))
        {
            return ImportErrorCode.InputEmpty;
        }

        if (plan.InputKind == ImportInputKind.Unknown)
        {
            return ImportErrorCode.UnsupportedInputType;
        }

        if (plan.RequiresConfirmation && !plan.IsConfirmed)
        {
            return ImportErrorCode.ConfirmationRequired;
        }

        if (plan.WarningList.Contains("mixed_directory_layout", StringComparer.Ordinal) && !plan.IsConfirmed)
        {
            return ImportErrorCode.MixedDirectoryLayout;
        }

        if (plan.VolumePlans.All(volume => volume.PageLocators.Count == 0))
        {
            return ImportErrorCode.NoValidImage;
        }

        return null;
    }

    private static long UpsertSource(SqliteConnection connection, SqliteTransaction transaction, string sourcePath, string sourceType)
    {
        using var upsert = connection.CreateCommand();
        upsert.Transaction = transaction;
        upsert.CommandText = """
            INSERT INTO source (source_path, source_type)
            VALUES ($path, $type)
            ON CONFLICT(source_path) DO UPDATE SET source_type = excluded.source_type;
            """;
        upsert.Parameters.AddWithValue("$path", sourcePath);
        upsert.Parameters.AddWithValue("$type", sourceType);
        upsert.ExecuteNonQuery();

        using var select = connection.CreateCommand();
        select.Transaction = transaction;
        select.CommandText = "SELECT source_id FROM source WHERE source_path = $path LIMIT 1;";
        select.Parameters.AddWithValue("$path", sourcePath);
        return Convert.ToInt64(select.ExecuteScalar());
    }

    private static long UpsertSeries(SqliteConnection connection, SqliteTransaction transaction, string title)
    {
        var normalized = title.Trim().ToLowerInvariant();

        using var upsert = connection.CreateCommand();
        upsert.Transaction = transaction;
        upsert.CommandText = """
            INSERT INTO series (title, normalized_title)
            VALUES ($title, $normalized)
            ON CONFLICT(normalized_title) DO UPDATE SET title = excluded.title;
            """;
        upsert.Parameters.AddWithValue("$title", title);
        upsert.Parameters.AddWithValue("$normalized", normalized);
        upsert.ExecuteNonQuery();

        using var select = connection.CreateCommand();
        select.Transaction = transaction;
        select.CommandText = "SELECT series_id FROM series WHERE normalized_title = $normalized LIMIT 1;";
        select.Parameters.AddWithValue("$normalized", normalized);
        return Convert.ToInt64(select.ExecuteScalar());
    }

    private static long UpsertVolume(SqliteConnection connection, SqliteTransaction transaction, long sourceId, long seriesId, string title)
    {
        using var upsert = connection.CreateCommand();
        upsert.Transaction = transaction;
        upsert.CommandText = """
            INSERT INTO volume (source_id, series_id, title)
            VALUES ($sourceId, $seriesId, $title)
            ON CONFLICT(source_id, title) DO UPDATE SET series_id = excluded.series_id;
            """;
        upsert.Parameters.AddWithValue("$sourceId", sourceId);
        upsert.Parameters.AddWithValue("$seriesId", seriesId);
        upsert.Parameters.AddWithValue("$title", title);
        upsert.ExecuteNonQuery();

        using var select = connection.CreateCommand();
        select.Transaction = transaction;
        select.CommandText = "SELECT volume_id FROM volume WHERE source_id = $sourceId AND title = $title LIMIT 1;";
        select.Parameters.AddWithValue("$sourceId", sourceId);
        select.Parameters.AddWithValue("$title", title);
        return Convert.ToInt64(select.ExecuteScalar());
    }

    private static void ClearPages(SqliteConnection connection, SqliteTransaction transaction, long volumeId)
    {
        using var clear = connection.CreateCommand();
        clear.Transaction = transaction;
        clear.CommandText = "DELETE FROM page WHERE volume_id = $volumeId;";
        clear.Parameters.AddWithValue("$volumeId", volumeId);
        clear.ExecuteNonQuery();
    }

    private static int InsertPages(SqliteConnection connection, SqliteTransaction transaction, long volumeId, IReadOnlyList<string> pageLocators)
    {
        var inserted = 0;
        for (var index = 0; index < pageLocators.Count; index++)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO page (volume_id, page_number, page_locator)
                VALUES ($volumeId, $pageNumber, $locator);
                """;
            insert.Parameters.AddWithValue("$volumeId", volumeId);
            insert.Parameters.AddWithValue("$pageNumber", index + 1);
            insert.Parameters.AddWithValue("$locator", (object?)pageLocators[index] ?? DBNull.Value);
            inserted += insert.ExecuteNonQuery();
        }

        return inserted;
    }
}

public sealed record ImportWriteSummary(
    int InsertedOrUpdatedVolumes,
    int SkippedVolumes,
    int InsertedPages,
    int RequestedVolumes);

public sealed record ImportPersistResult(
    bool Succeeded,
    ImportWriteSummary? Summary,
    ImportErrorCode? ErrorCode,
    string? ErrorMessage,
    DateTimeOffset StartedAt,
    TimeSpan Duration)
{
    public static ImportPersistResult CreateSuccess(ImportWriteSummary summary, DateTimeOffset startedAt, TimeSpan duration)
    {
        return new ImportPersistResult(true, summary, null, null, startedAt, duration);
    }

    public static ImportPersistResult CreateFailure(ImportErrorCode code, string message, DateTimeOffset startedAt, TimeSpan duration)
    {
        return new ImportPersistResult(false, null, code, message, startedAt, duration);
    }
}

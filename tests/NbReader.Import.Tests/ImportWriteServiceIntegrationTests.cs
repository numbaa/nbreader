using System;
using System.IO;
using Microsoft.Data.Sqlite;
using NbReader.Infrastructure;

namespace NbReader.Import.Tests;

public class ImportWriteServiceIntegrationTests
{
    [Fact]
    public void Persist_ShouldBeIdempotent_ForSamePlan()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "nbreader-db-tests", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        try
        {
            var database = new AppDatabase(dbPath);
            database.Initialize();
            var service = new ImportWriteService(database);

            var plan = BuildConfirmedPlan(
                inputLocator: "C:/library/series/vol1",
                sourceLocator: "C:/library/series/vol1",
                seriesTitle: "Series A",
                volumeTitle: "Vol.01",
                pages: ["1.jpg", "2.jpg", "3.jpg"]);

            var first = service.Persist(plan);
            var second = service.Persist(plan);

            Assert.True(first.Succeeded);
            Assert.True(second.Succeeded);
            Assert.Equal(1, CountRows(database.ConnectionString, "source"));
            Assert.Equal(1, CountRows(database.ConnectionString, "series"));
            Assert.Equal(1, CountRows(database.ConnectionString, "volume"));
            Assert.Equal(3, CountRows(database.ConnectionString, "page"));
        }
        finally
        {
            CleanupDatabaseFiles(dbPath);
        }
    }

    [Fact]
    public void Persist_ShouldRollbackTransaction_WhenInsertFails()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "nbreader-db-tests", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        try
        {
            var database = new AppDatabase(dbPath);
            database.Initialize();
            var service = new ImportWriteService(database);

            var brokenPlan = BuildConfirmedPlan(
                inputLocator: "C:/library/series/vol1",
                sourceLocator: "C:/library/series/vol1",
                seriesTitle: "Series A",
                volumeTitle: "Vol.01",
                pages: ["1.jpg", null!]);

            var result = service.Persist(brokenPlan);

            Assert.False(result.Succeeded);
            Assert.Equal(ImportErrorCode.DbConstraintFailed, result.ErrorCode);
            Assert.Equal(0, CountRows(database.ConnectionString, "source"));
            Assert.Equal(0, CountRows(database.ConnectionString, "series"));
            Assert.Equal(0, CountRows(database.ConnectionString, "volume"));
            Assert.Equal(0, CountRows(database.ConnectionString, "page"));
        }
        finally
        {
            CleanupDatabaseFiles(dbPath);
        }
    }

    [Fact]
    public void Persist_ShouldReturnConfirmationRequired_WhenPlanNotConfirmed()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "nbreader-db-tests", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        try
        {
            var database = new AppDatabase(dbPath);
            database.Initialize();
            var service = new ImportWriteService(database);

            var unconfirmed = BuildConfirmedPlan(
                inputLocator: "C:/library/series/vol1",
                sourceLocator: "C:/library/series/vol1",
                seriesTitle: "Series A",
                volumeTitle: "Vol.01",
                pages: ["1.jpg"])
                with
                {
                    RequiresConfirmation = true,
                    IsConfirmed = false,
                };

            var result = service.Persist(unconfirmed);

            Assert.False(result.Succeeded);
            Assert.Equal(ImportErrorCode.ConfirmationRequired, result.ErrorCode);
        }
        finally
        {
            CleanupDatabaseFiles(dbPath);
        }
    }

    private static ImportPlan BuildConfirmedPlan(
        string inputLocator,
        string sourceLocator,
        string seriesTitle,
        string volumeTitle,
        IReadOnlyList<string> pages)
    {
        return new ImportPlan(
            Guid.NewGuid(),
            ImportInputKind.ImageDirectory,
            inputLocator,
            seriesTitle,
            [
                new VolumePlan(
                    sourceLocator,
                    volumeTitle,
                    seriesTitle,
                    1,
                    pages,
                    pages.Count > 0 ? pages[0] : null,
                    [],
                    []),
            ],
            [],
            new ConflictReport([], [], false),
            RequiresConfirmation: false,
            IsConfirmed: true);
    }

    private static int CountRows(string connectionString, string table)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(1) FROM {table};";
        return Convert.ToInt32(command.ExecuteScalar());
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
            // Ignore temp cleanup failures caused by short-lived file locks.
        }
        catch (UnauthorizedAccessException)
        {
            // Ignore temp cleanup failures caused by short-lived file locks.
        }
    }
}

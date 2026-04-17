using System;
using System.IO;
using System.IO.Compression;
using Microsoft.Data.Sqlite;
using NbReader.Infrastructure;

namespace NbReader.Import.Tests;

public class Phase1AcceptanceScenarioTests
{
    [Fact]
    public void Scenario1_SingleZip_ShouldPersistOneVolumeAndPages()
    {
        var workspace = CreateTempWorkspace();
        var dbPath = Path.Combine(workspace, "app.db");

        try
        {
            var zipPath = Path.Combine(workspace, "Vol.01.zip");
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                AddTextEntry(zip, "1.jpg", "a");
                AddTextEntry(zip, "2.jpg", "b");
                AddTextEntry(zip, "3.jpg", "c");
                AddTextEntry(zip, "notes.txt", "ignored");
            }

            var store = new InMemoryImportTaskStore();
            var orchestrator = new ImportOrchestrator(store);
            var task = orchestrator.CreateOrReuseTask(zipPath);
            var plan = orchestrator.AnalyzeTask(task);

            var database = new AppDatabase(dbPath);
            database.Initialize();
            var writeService = new ImportWriteService(database);
            var result = writeService.Persist(plan);

            Assert.True(result.Succeeded);
            Assert.Equal(1, CountRows(database.ConnectionString, "volume"));
            Assert.Equal(3, CountRows(database.ConnectionString, "page"));
        }
        finally
        {
            CleanupDirectory(workspace);
        }
    }

    [Fact]
    public void Scenario2_SeriesDirectory_ShouldPersistMultipleVolumes()
    {
        var workspace = CreateTempWorkspace();
        var dbPath = Path.Combine(workspace, "app.db");
        var seriesRoot = Path.Combine(workspace, "Series-A");

        try
        {
            CreateVolumeDir(seriesRoot, "Vol.01", 3);
            CreateVolumeDir(seriesRoot, "Vol.02", 3);

            var store = new InMemoryImportTaskStore();
            var orchestrator = new ImportOrchestrator(store);
            var task = orchestrator.CreateOrReuseTask(seriesRoot);
            var plan = orchestrator.AnalyzeTask(task);

            var database = new AppDatabase(dbPath);
            database.Initialize();
            var writeService = new ImportWriteService(database);
            var result = writeService.Persist(plan);

            Assert.True(result.Succeeded);
            Assert.Equal(2, CountRows(database.ConnectionString, "volume"));
            Assert.Equal(6, CountRows(database.ConnectionString, "page"));
        }
        finally
        {
            CleanupDirectory(workspace);
        }
    }

    [Fact]
    public void Scenario3_MixedDirectory_ShouldRequireConfirmation()
    {
        var workspace = CreateTempWorkspace();
        var mixedRoot = Path.Combine(workspace, "Mixed-Series");

        try
        {
            Directory.CreateDirectory(mixedRoot);
            File.WriteAllText(Path.Combine(mixedRoot, "root.jpg"), "root");
            CreateVolumeDir(mixedRoot, "Vol.01", 3);

            var store = new InMemoryImportTaskStore();
            var orchestrator = new ImportOrchestrator(store);
            var task = orchestrator.CreateOrReuseTask(mixedRoot);
            var plan = orchestrator.AnalyzeTask(task);

            Assert.True(plan.RequiresConfirmation);
            Assert.Contains("mixed_directory_layout", plan.WarningList);
            Assert.Contains(store.Events, e => e.Status == ImportTaskStatus.AwaitingConfirmation && e.EventType == "awaiting_confirmation");
        }
        finally
        {
            CleanupDirectory(workspace);
        }
    }

    [Fact]
    public void Scenario4_RepeatedImportSamePath_ShouldNotCreateDuplicates()
    {
        var workspace = CreateTempWorkspace();
        var dbPath = Path.Combine(workspace, "app.db");
        var volumeDir = Path.Combine(workspace, "Vol.01");

        try
        {
            Directory.CreateDirectory(volumeDir);
            File.WriteAllText(Path.Combine(volumeDir, "1.jpg"), "a");
            File.WriteAllText(Path.Combine(volumeDir, "2.jpg"), "b");
            File.WriteAllText(Path.Combine(volumeDir, "3.jpg"), "c");

            var store = new InMemoryImportTaskStore();
            var orchestrator = new ImportOrchestrator(store);
            var taskA = orchestrator.CreateOrReuseTask(volumeDir);
            var taskB = orchestrator.CreateOrReuseTask(volumeDir + Path.DirectorySeparatorChar);

            Assert.Equal(taskA.TaskId, taskB.TaskId);

            var plan = orchestrator.AnalyzeTask(taskA);
            var database = new AppDatabase(dbPath);
            database.Initialize();
            var writeService = new ImportWriteService(database);

            var first = writeService.Persist(plan);
            var second = writeService.Persist(plan);

            Assert.True(first.Succeeded);
            Assert.True(second.Succeeded);
            Assert.Equal(1, CountRows(database.ConnectionString, "source"));
            Assert.Equal(1, CountRows(database.ConnectionString, "volume"));
            Assert.Equal(3, CountRows(database.ConnectionString, "page"));
        }
        finally
        {
            CleanupDirectory(workspace);
        }
    }

    [Fact]
    public void Scenario5_ImportFailure_ShouldReturnClearErrorReason()
    {
        var workspace = CreateTempWorkspace();
        var dbPath = Path.Combine(workspace, "app.db");

        try
        {
            var database = new AppDatabase(dbPath);
            database.Initialize();
            var writeService = new ImportWriteService(database);

            var notConfirmedPlan = new ImportPlan(
                Guid.NewGuid(),
                ImportInputKind.ImageDirectory,
                "C:/placeholder/input",
                "Series",
                [
                    new VolumePlan("C:/placeholder/input", "Vol.01", "Series", 1, ["1.jpg"], "1.jpg", [], []),
                ],
                [],
                new ConflictReport([], [], false),
                RequiresConfirmation: true,
                IsConfirmed: false);

            var result = writeService.Persist(notConfirmedPlan);

            Assert.False(result.Succeeded);
            Assert.Equal(ImportErrorCode.ConfirmationRequired, result.ErrorCode);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("requires user confirmation", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupDirectory(workspace);
        }
    }

    private static string CreateTempWorkspace()
    {
        var path = Path.Combine(Path.GetTempPath(), "nbreader-phase1-acceptance", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CreateVolumeDir(string seriesRoot, string volumeName, int pageCount)
    {
        var volumePath = Path.Combine(seriesRoot, volumeName);
        Directory.CreateDirectory(volumePath);
        for (var i = 1; i <= pageCount; i++)
        {
            File.WriteAllText(Path.Combine(volumePath, $"{i}.jpg"), $"p{i}");
        }
    }

    private static void AddTextEntry(ZipArchive zip, string entryName, string content)
    {
        var entry = zip.CreateEntry(entryName);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write(content);
    }

    private static int CountRows(string connectionString, string table)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(1) FROM {table};";
        return Convert.ToInt32(command.ExecuteScalar());
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
            // Temporary folder cleanup failures can happen due to file locks in CI.
        }
        catch (UnauthorizedAccessException)
        {
            // Temporary folder cleanup failures can happen due to file locks in CI.
        }
    }

    private sealed class InMemoryImportTaskStore : IImportTaskStore
    {
        private readonly Dictionary<string, ImportTask> _tasksByLocator = new(StringComparer.Ordinal);

        public List<ImportTaskEvent> Events { get; } = [];

        public ImportTask? FindByNormalizedLocator(string normalizedLocator)
        {
            return _tasksByLocator.TryGetValue(normalizedLocator, out var task)
                ? task
                : null;
        }

        public void UpsertTask(ImportTask task)
        {
            _tasksByLocator[task.NormalizedLocator] = task;
        }

        public void AppendEvent(ImportTaskEvent taskEvent)
        {
            Events.Add(taskEvent);
        }
    }
}

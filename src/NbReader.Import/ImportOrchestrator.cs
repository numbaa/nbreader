using System;

namespace NbReader.Import;

public sealed class ImportOrchestrator
{
    private readonly IImportTaskStore _taskStore;

    public ImportOrchestrator(IImportTaskStore taskStore)
    {
        _taskStore = taskStore;
    }

    public ImportTask CreateOrReuseTask(string inputPath)
    {
        var normalizedLocator = PathNormalizer.NormalizeLocator(inputPath);
        var existingTask = _taskStore.FindByNormalizedLocator(normalizedLocator);
        if (existingTask is not null)
        {
            return existingTask;
        }

        var now = DateTimeOffset.UtcNow;
        var inputKind = InputTypeDetector.Detect(normalizedLocator);

        var pendingTask = new ImportTask(
            Guid.NewGuid(),
            inputPath,
            normalizedLocator,
            inputKind,
            ImportTaskStatus.Pending,
            now,
            now);

        _taskStore.UpsertTask(pendingTask);
        _taskStore.AppendEvent(new ImportTaskEvent(
            pendingTask.TaskId,
            ImportTaskStatus.Pending,
            "task_created",
            "Import task created.",
            now));

        var scanningTask = pendingTask with
        {
            Status = ImportTaskStatus.Scanning,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _taskStore.UpsertTask(scanningTask);
        _taskStore.AppendEvent(new ImportTaskEvent(
            scanningTask.TaskId,
            ImportTaskStatus.Scanning,
            "scanning_started",
            "Scanning phase started.",
            scanningTask.UpdatedAt));

        return scanningTask;
    }
}

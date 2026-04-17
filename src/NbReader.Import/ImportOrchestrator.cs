using System;

namespace NbReader.Import;

public sealed class ImportOrchestrator
{
    private readonly IImportTaskStore _taskStore;
    private readonly ImportPlanAnalyzer _planAnalyzer;

    public ImportOrchestrator(IImportTaskStore taskStore)
        : this(taskStore, new ImportPlanAnalyzer())
    {
    }

    public ImportOrchestrator(IImportTaskStore taskStore, ImportPlanAnalyzer planAnalyzer)
    {
        _taskStore = taskStore;
        _planAnalyzer = planAnalyzer;
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

    public ImportPlan AnalyzeTask(ImportTask task)
    {
        var analyzingTask = task with
        {
            Status = ImportTaskStatus.Analyzing,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _taskStore.UpsertTask(analyzingTask);
        _taskStore.AppendEvent(new ImportTaskEvent(
            analyzingTask.TaskId,
            ImportTaskStatus.Analyzing,
            "analyzing_started",
            "Analyzing phase started.",
            analyzingTask.UpdatedAt));

        return _planAnalyzer.Analyze(analyzingTask);
    }
}

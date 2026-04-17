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

        var plan = _planAnalyzer.Analyze(analyzingTask);
        if (plan.RequiresConfirmation)
        {
            var awaitingTask = analyzingTask with
            {
                Status = ImportTaskStatus.AwaitingConfirmation,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            _taskStore.UpsertTask(awaitingTask);
            _taskStore.AppendEvent(new ImportTaskEvent(
                awaitingTask.TaskId,
                ImportTaskStatus.AwaitingConfirmation,
                "awaiting_confirmation",
                "Plan requires user confirmation.",
                awaitingTask.UpdatedAt));
        }

        return plan;
    }

    public ImportPlan ConfirmPlan(ImportTask task, ImportPlan plan, ImportConfirmationRequest request)
    {
        var awaitingTask = task with
        {
            Status = ImportTaskStatus.AwaitingConfirmation,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _taskStore.UpsertTask(awaitingTask);
        _taskStore.AppendEvent(new ImportTaskEvent(
            awaitingTask.TaskId,
            ImportTaskStatus.AwaitingConfirmation,
            "confirmation_started",
            "User confirmation started.",
            awaitingTask.UpdatedAt));

        var confirmedPlan = ImportPlanConfirmation.ApplyConfirmation(plan, request);
        var importingTask = awaitingTask with
        {
            Status = ImportTaskStatus.Importing,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _taskStore.UpsertTask(importingTask);
        _taskStore.AppendEvent(new ImportTaskEvent(
            importingTask.TaskId,
            ImportTaskStatus.Importing,
            "confirmation_applied",
            "Confirmation applied, task is ready for importing.",
            importingTask.UpdatedAt));

        return confirmedPlan;
    }
}

using System;
using System.Collections.Generic;

namespace NbReader.Import.Tests;

public class ImportOrchestratorTests
{
    [Fact]
    public void CreateOrReuseTask_ShouldReturnExistingTask_ForSameNormalizedPath()
    {
        var store = new InMemoryImportTaskStore();
        var orchestrator = new ImportOrchestrator(store);

        var root = Path.Combine(Path.GetTempPath(), "nbreader-orch-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var first = orchestrator.CreateOrReuseTask(root);
            var second = orchestrator.CreateOrReuseTask(root + Path.DirectorySeparatorChar);

            Assert.Equal(first.TaskId, second.TaskId);
            Assert.Equal(2, store.Events.Count);
            Assert.Equal(ImportTaskStatus.Pending, store.Events[0].Status);
            Assert.Equal(ImportTaskStatus.Scanning, store.Events[1].Status);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AnalyzeTask_ShouldAppendAnalyzingEvent_AndReturnPlan()
    {
        var store = new InMemoryImportTaskStore();
        var orchestrator = new ImportOrchestrator(store);

        var root = Path.Combine(Path.GetTempPath(), "nbreader-orch-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            File.WriteAllText(Path.Combine(root, "1.jpg"), "a");

            var task = orchestrator.CreateOrReuseTask(root);
            var plan = orchestrator.AnalyzeTask(task);

            Assert.Equal(task.TaskId, plan.TaskId);
            Assert.Equal(ImportInputKind.ImageDirectory, plan.InputKind);
            Assert.Contains(store.Events, e => e.Status == ImportTaskStatus.Analyzing && e.EventType == "analyzing_started");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AnalyzeTask_ShouldSwitchToAwaitingConfirmation_WhenPlanRequiresConfirmation()
    {
        var store = new InMemoryImportTaskStore();
        var orchestrator = new ImportOrchestrator(store);

        var root = Path.Combine(Path.GetTempPath(), "nbreader-orch-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            File.WriteAllText(Path.Combine(root, "0001.jpg"), "root-image");
            var vol = Path.Combine(root, "Vol.01");
            Directory.CreateDirectory(vol);
            File.WriteAllText(Path.Combine(vol, "1.jpg"), "a");

            var task = orchestrator.CreateOrReuseTask(root);
            var seriesTask = task with { InputKind = ImportInputKind.SeriesDirectory };
            var plan = orchestrator.AnalyzeTask(seriesTask);

            Assert.True(plan.RequiresConfirmation);
            Assert.Contains(store.Events, e => e.Status == ImportTaskStatus.AwaitingConfirmation && e.EventType == "awaiting_confirmation");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ConfirmPlan_ShouldMoveTaskToImporting_AndReturnConfirmedPlan()
    {
        var store = new InMemoryImportTaskStore();
        var orchestrator = new ImportOrchestrator(store);

        var task = new ImportTask(
            Guid.NewGuid(),
            "C:/input",
            "C:/input",
            ImportInputKind.SeriesDirectory,
            ImportTaskStatus.AwaitingConfirmation,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        var plan = new ImportPlan(
            task.TaskId,
            ImportInputKind.SeriesDirectory,
            task.NormalizedLocator,
            "Series",
            [
                new VolumePlan("C:/input/vol1", "Vol.01", "Series", 1, ["1.jpg"], "1.jpg", [], []),
            ],
            ["low_page_count"],
            new ConflictReport([], [], false),
            RequiresConfirmation: true,
            IsConfirmed: false);

        var confirmed = orchestrator.ConfirmPlan(task, plan, new ImportConfirmationRequest(
            SeriesNameOverride: "Series Confirmed",
            VolumeOverrides: [],
            SkipDuplicateVolumes: false,
            IgnoreWarnings: true));

        Assert.True(confirmed.IsConfirmed);
        Assert.False(confirmed.RequiresConfirmation);
        Assert.Equal("Series Confirmed", confirmed.SeriesCandidate);
        Assert.Contains(store.Events, e => e.Status == ImportTaskStatus.AwaitingConfirmation && e.EventType == "confirmation_started");
        Assert.Contains(store.Events, e => e.Status == ImportTaskStatus.Importing && e.EventType == "confirmation_applied");
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

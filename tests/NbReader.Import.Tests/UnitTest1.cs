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

using System;
using System.IO;
using System.Linq;

namespace NbReader.Import.Tests;

public class ImportPlanAnalyzerTests
{
    [Fact]
    public void Analyze_ShouldRequireConfirmation_ForMixedSeriesDirectoryLayout()
    {
        var root = Path.Combine(Path.GetTempPath(), "nbreader-plan-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            File.WriteAllText(Path.Combine(root, "0001.jpg"), "root-image");
            var volumeA = Path.Combine(root, "Vol.01");
            Directory.CreateDirectory(volumeA);
            File.WriteAllText(Path.Combine(volumeA, "1.jpg"), "a");

            var task = new ImportTask(
                Guid.NewGuid(),
                root,
                PathNormalizer.NormalizeLocator(root),
                ImportInputKind.SeriesDirectory,
                ImportTaskStatus.Analyzing,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow);

            var analyzer = new ImportPlanAnalyzer();
            var plan = analyzer.Analyze(task);

            Assert.True(plan.RequiresConfirmation);
            Assert.False(plan.IsConfirmed);
            Assert.Contains("mixed_directory_layout", plan.WarningList);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Analyze_ShouldBuildConflictReport_ForDuplicateVolumeNumberCandidates()
    {
        var root = Path.Combine(Path.GetTempPath(), "nbreader-plan-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var volumeA = Path.Combine(root, "Vol.01 A");
            var volumeB = Path.Combine(root, "Vol.01 B");
            Directory.CreateDirectory(volumeA);
            Directory.CreateDirectory(volumeB);
            File.WriteAllText(Path.Combine(volumeA, "1.jpg"), "a");
            File.WriteAllText(Path.Combine(volumeB, "1.jpg"), "b");

            var task = new ImportTask(
                Guid.NewGuid(),
                root,
                PathNormalizer.NormalizeLocator(root),
                ImportInputKind.SeriesDirectory,
                ImportTaskStatus.Analyzing,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow);

            var analyzer = new ImportPlanAnalyzer();
            var plan = analyzer.Analyze(task);

            Assert.True(plan.ConflictReport.HasConflicts);
            Assert.Contains("volume_number:1", plan.ConflictReport.DuplicateVolumeNumberKeys);
            Assert.True(plan.RequiresConfirmation);
            Assert.False(plan.IsConfirmed);
            Assert.Equal(2, plan.VolumePlans.Count);
            Assert.All(plan.VolumePlans, volume => Assert.Single(volume.PageLocators));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

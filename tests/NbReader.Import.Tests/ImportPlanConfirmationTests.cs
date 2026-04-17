using System;
using System.Collections.Generic;

namespace NbReader.Import.Tests;

public class ImportPlanConfirmationTests
{
    [Fact]
    public void ApplyConfirmation_ShouldOverrideSeriesAndVolumeAndClearWarnings_WhenIgnoreWarningsIsTrue()
    {
        var plan = new ImportPlan(
            Guid.NewGuid(),
            ImportInputKind.SeriesDirectory,
            "/library/series",
            "Original Series",
            new List<VolumePlan>
            {
                new(
                    "/library/series/Vol.01",
                    "Vol.01",
                    "Original Series",
                    1,
                    ["1.jpg"],
                    "1.jpg",
                    ["low_page_count"],
                    []),
            },
            ["low_page_count"],
            new ConflictReport([], [], false),
            RequiresConfirmation: true,
            IsConfirmed: false);

        var request = new ImportConfirmationRequest(
            SeriesNameOverride: "Confirmed Series",
            VolumeOverrides:
            [
                new VolumeConfirmationOverride(
                    SourceLocator: "/library/series/Vol.01",
                    DisplayNameOverride: "第01卷",
                    VolumeNumberOverride: 11,
                    Skip: false),
            ],
            SkipDuplicateVolumes: false,
            IgnoreWarnings: true);

        var confirmed = ImportPlanConfirmation.ApplyConfirmation(plan, request);

        Assert.True(confirmed.IsConfirmed);
        Assert.False(confirmed.RequiresConfirmation);
        Assert.Equal("Confirmed Series", confirmed.SeriesCandidate);
        Assert.Empty(confirmed.WarningList);
        Assert.Single(confirmed.VolumePlans);
        Assert.Equal("第01卷", confirmed.VolumePlans[0].DisplayName);
        Assert.Equal(11, confirmed.VolumePlans[0].VolumeNumberCandidate);
    }

    [Fact]
    public void ApplyConfirmation_ShouldSkipDuplicateVolumeNumbers_WhenSkipDuplicateVolumesIsTrue()
    {
        var plan = new ImportPlan(
            Guid.NewGuid(),
            ImportInputKind.SeriesDirectory,
            "/library/series",
            "Series",
            new List<VolumePlan>
            {
                new(
                    "/library/series/Vol.01a",
                    "Vol.01a",
                    "Series",
                    1,
                    ["1.jpg"],
                    "1.jpg",
                    [],
                    []),
                new(
                    "/library/series/Vol.01b",
                    "Vol.01b",
                    "Series",
                    1,
                    ["1.jpg"],
                    "1.jpg",
                    [],
                    []),
            },
            [],
            new ConflictReport(["volume_number:1"], ["duplicate_candidate_volume_number:1"], true),
            RequiresConfirmation: true,
            IsConfirmed: false);

        var request = new ImportConfirmationRequest(
            SeriesNameOverride: null,
            VolumeOverrides: [],
            SkipDuplicateVolumes: true,
            IgnoreWarnings: false);

        var confirmed = ImportPlanConfirmation.ApplyConfirmation(plan, request);

        Assert.Single(confirmed.VolumePlans);
        Assert.False(confirmed.ConflictReport.HasConflicts);
        Assert.True(confirmed.IsConfirmed);
    }
}

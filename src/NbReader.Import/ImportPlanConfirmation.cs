using System;
using System.Collections.Generic;
using System.Linq;

namespace NbReader.Import;

public static class ImportPlanConfirmation
{
    public static ImportPlan ApplyConfirmation(ImportPlan plan, ImportConfirmationRequest request)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(request);

        var overrideMap = request.VolumeOverrides
            .GroupBy(overrideItem => overrideItem.SourceLocator, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);

        var updatedVolumes = new List<VolumePlan>();
        foreach (var volume in plan.VolumePlans)
        {
            if (!overrideMap.TryGetValue(volume.SourceLocator, out var volumeOverride))
            {
                updatedVolumes.Add(volume);
                continue;
            }

            if (volumeOverride.Skip)
            {
                continue;
            }

            var updatedDisplayName = string.IsNullOrWhiteSpace(volumeOverride.DisplayNameOverride)
                ? volume.DisplayName
                : volumeOverride.DisplayNameOverride;
            var updatedNumber = volumeOverride.VolumeNumberOverride ?? volume.VolumeNumberCandidate;

            updatedVolumes.Add(volume with
            {
                DisplayName = updatedDisplayName,
                VolumeNumberCandidate = updatedNumber,
            });
        }

        var updatedConflict = BuildConflictReport(updatedVolumes);
        var warnings = request.IgnoreWarnings ? [] : plan.WarningList;

        if (request.SkipDuplicateVolumes)
        {
            updatedVolumes = SkipDuplicateVolumes(updatedVolumes);
            updatedConflict = BuildConflictReport(updatedVolumes);
        }

        return plan with
        {
            SeriesCandidate = string.IsNullOrWhiteSpace(request.SeriesNameOverride)
                ? plan.SeriesCandidate
                : request.SeriesNameOverride,
            VolumePlans = updatedVolumes,
            WarningList = warnings,
            ConflictReport = updatedConflict,
            RequiresConfirmation = false,
            IsConfirmed = true,
        };
    }

    private static List<VolumePlan> SkipDuplicateVolumes(IReadOnlyList<VolumePlan> volumes)
    {
        var seen = new HashSet<int>();
        var result = new List<VolumePlan>();

        foreach (var volume in volumes)
        {
            if (!volume.VolumeNumberCandidate.HasValue)
            {
                result.Add(volume);
                continue;
            }

            if (seen.Add(volume.VolumeNumberCandidate.Value))
            {
                result.Add(volume);
            }
        }

        return result;
    }

    private static ConflictReport BuildConflictReport(IReadOnlyList<VolumePlan> volumePlans)
    {
        var duplicateKeys = volumePlans
            .Where(plan => plan.VolumeNumberCandidate.HasValue)
            .GroupBy(plan => plan.VolumeNumberCandidate!.Value)
            .Where(group => group.Count() > 1)
            .Select(group => $"volume_number:{group.Key}")
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        var details = duplicateKeys
            .Select(key => $"duplicate_candidate_{key}")
            .ToArray();

        return new ConflictReport(duplicateKeys, details, duplicateKeys.Length > 0);
    }
}

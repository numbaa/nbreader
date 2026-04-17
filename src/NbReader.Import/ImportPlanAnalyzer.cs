using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace NbReader.Import;

public sealed class ImportPlanAnalyzer
{
    private static readonly Regex VolumeNumberRegex = new(@"(?:vol(?:ume)?|第)?\s*0*(\d{1,4})(?:卷)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ZipImageEnumerator _zipImageEnumerator;
    private readonly DirectoryImageEnumerator _directoryImageEnumerator;

    public ImportPlanAnalyzer()
        : this(new ZipImageEnumerator(), new DirectoryImageEnumerator())
    {
    }

    public ImportPlanAnalyzer(ZipImageEnumerator zipImageEnumerator, DirectoryImageEnumerator directoryImageEnumerator)
    {
        _zipImageEnumerator = zipImageEnumerator;
        _directoryImageEnumerator = directoryImageEnumerator;
    }

    public ImportPlan Analyze(ImportTask task)
    {
        var warningList = new List<string>();
        var volumePlans = new List<VolumePlan>();
        string? seriesCandidate = null;

        switch (task.InputKind)
        {
            case ImportInputKind.ZipFile:
                volumePlans.Add(BuildZipVolume(task.NormalizedLocator, warningList));
                seriesCandidate = Path.GetFileName(Path.GetDirectoryName(task.NormalizedLocator));
                break;
            case ImportInputKind.ImageDirectory:
                volumePlans.Add(BuildDirectoryVolume(task.NormalizedLocator, warningList));
                seriesCandidate = Path.GetFileName(Path.GetDirectoryName(task.NormalizedLocator));
                break;
            case ImportInputKind.SeriesDirectory:
                BuildSeriesVolumes(task.NormalizedLocator, warningList, volumePlans);
                seriesCandidate = Path.GetFileName(task.NormalizedLocator);
                break;
            default:
                warningList.Add("unsupported_input_kind");
                break;
        }

        var conflictReport = BuildConflictReport(volumePlans);
        var compactWarnings = warningList.Distinct(StringComparer.Ordinal).ToArray();
        var requiresConfirmation = ShouldRequireConfirmation(task.InputKind, volumePlans, compactWarnings, conflictReport);

        return new ImportPlan(
            task.TaskId,
            task.InputKind,
            task.NormalizedLocator,
            seriesCandidate,
            volumePlans,
            compactWarnings,
            conflictReport,
            requiresConfirmation,
            IsConfirmed: false);
    }

    private VolumePlan BuildZipVolume(string zipPath, List<string> warningList)
    {
        var pages = _zipImageEnumerator.Enumerate(zipPath)
            .Select(entry => entry.EntryPath)
            .ToArray();
        var displayName = Path.GetFileNameWithoutExtension(zipPath);
        var warnings = BuildVolumeWarnings(pages.Length, warningList);

        return new VolumePlan(
            zipPath,
            displayName,
            Path.GetFileName(Path.GetDirectoryName(zipPath)),
            ExtractVolumeNumberCandidate(displayName),
            pages,
            pages.FirstOrDefault(),
            warnings,
            []);
    }

    private VolumePlan BuildDirectoryVolume(string directoryPath, List<string> warningList)
    {
        var pages = _directoryImageEnumerator.Enumerate(directoryPath);
        var displayName = Path.GetFileName(directoryPath);
        var warnings = BuildVolumeWarnings(pages.Count, warningList);

        return new VolumePlan(
            directoryPath,
            displayName,
            Path.GetFileName(Path.GetDirectoryName(directoryPath)),
            ExtractVolumeNumberCandidate(displayName),
            pages,
            pages.FirstOrDefault(),
            warnings,
            []);
    }

    private void BuildSeriesVolumes(string seriesDirectoryPath, List<string> warningList, List<VolumePlan> volumePlans)
    {
        var rootImages = _directoryImageEnumerator.Enumerate(seriesDirectoryPath);
        if (rootImages.Count > 0)
        {
            warningList.Add("mixed_directory_layout");
        }

        var children = Directory.EnumerateDirectories(seriesDirectoryPath)
            .OrderBy(path => Path.GetFileName(path), NaturalStringComparer.Instance)
            .ToArray();

        foreach (var child in children)
        {
            var pages = _directoryImageEnumerator.Enumerate(child);
            var displayName = Path.GetFileName(child);
            var warnings = BuildVolumeWarnings(pages.Count, warningList);

            volumePlans.Add(new VolumePlan(
                child,
                displayName,
                Path.GetFileName(seriesDirectoryPath),
                ExtractVolumeNumberCandidate(displayName),
                pages,
                pages.FirstOrDefault(),
                warnings,
                []));
        }
    }

    private static List<string> BuildVolumeWarnings(int pageCount, List<string> sharedWarningList)
    {
        var warnings = new List<string>();
        if (pageCount == 0)
        {
            warnings.Add("no_valid_image");
            sharedWarningList.Add("no_valid_image");
        }
        else if (pageCount < 3)
        {
            warnings.Add("low_page_count");
            sharedWarningList.Add("low_page_count");
        }

        return warnings;
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

    private static bool ShouldRequireConfirmation(
        ImportInputKind inputKind,
        IReadOnlyList<VolumePlan> volumePlans,
        IReadOnlyList<string> warningList,
        ConflictReport conflictReport)
    {
        if (conflictReport.HasConflicts)
        {
            return true;
        }

        if (warningList.Contains("mixed_directory_layout", StringComparer.Ordinal) ||
            warningList.Contains("no_valid_image", StringComparer.Ordinal))
        {
            return true;
        }

        if (inputKind == ImportInputKind.SeriesDirectory && volumePlans.Any(plan => !plan.VolumeNumberCandidate.HasValue))
        {
            return true;
        }

        return false;
    }

    private static int? ExtractVolumeNumberCandidate(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = VolumeNumberRegex.Match(text);
        if (!match.Success)
        {
            return null;
        }

        return int.TryParse(match.Groups[1].Value, out var number)
            ? number
            : null;
    }
}
using System;
using System.Collections.Generic;

namespace NbReader.Import;

public sealed record ImportPlan(
    Guid TaskId,
    ImportInputKind InputKind,
    string InputLocator,
    string? SeriesCandidate,
    IReadOnlyList<VolumePlan> VolumePlans,
    IReadOnlyList<string> WarningList,
    ConflictReport ConflictReport,
    bool RequiresConfirmation,
    bool IsConfirmed);

public sealed record VolumePlan(
    string SourceLocator,
    string DisplayName,
    string? SeriesCandidate,
    int? VolumeNumberCandidate,
    IReadOnlyList<string> PageLocators,
    string? CoverCandidate,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> DuplicateHints);

public sealed record ConflictReport(
    IReadOnlyList<string> DuplicateVolumeNumberKeys,
    IReadOnlyList<string> DetailMessages,
    bool HasConflicts);

public sealed record ImportConfirmationRequest(
    string? SeriesNameOverride,
    IReadOnlyList<VolumeConfirmationOverride> VolumeOverrides,
    bool SkipDuplicateVolumes,
    bool IgnoreWarnings);

public sealed record VolumeConfirmationOverride(
    string SourceLocator,
    string? DisplayNameOverride,
    int? VolumeNumberOverride,
    bool Skip);
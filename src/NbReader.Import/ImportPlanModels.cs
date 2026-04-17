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
    bool RequiresConfirmation);

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
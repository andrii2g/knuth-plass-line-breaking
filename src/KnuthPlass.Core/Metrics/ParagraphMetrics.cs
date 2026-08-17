using System.Collections.Immutable;

namespace KnuthPlass.Core.Metrics;

/// <summary>
/// Contains paragraph-level measurements derived from a reconstructed layout.
/// </summary>
public sealed record ParagraphMetrics(
    int LineCount,
    double? TotalBadness,
    double? TotalDemerits,
    double? WorstLineBadness,
    double? MeanAbsoluteAdjustmentRatio,
    double MaximumStretch,
    double MaximumShrink,
    int EvaluatedCandidates,
    int RejectedCandidates,
    int FeasibleCandidates,
    ImmutableArray<int> SelectedBreakpointIds);

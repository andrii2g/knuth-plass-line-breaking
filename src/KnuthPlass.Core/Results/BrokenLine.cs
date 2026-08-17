using System.Collections.Immutable;
using KnuthPlass.Core.Metrics;
using KnuthPlass.Core.Model;

namespace KnuthPlass.Core.Results;

/// <summary>
/// Contains one selected line and its source boxes and shared measurements.
/// </summary>
public sealed record BrokenLine(
    int LineNumber,
    LineMetrics Metrics,
    ImmutableArray<Box> Boxes,
    double? LineDemerits,
    double? AccumulatedDemerits,
    bool IsOverfull,
    ImmutableArray<ParagraphItem> LayoutItems = default,
    Penalty? SelectedPenalty = null);

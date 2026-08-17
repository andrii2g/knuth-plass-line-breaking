using KnuthPlass.Core.Breaking;
using KnuthPlass.Core.Model;

namespace KnuthPlass.Core.Metrics;

/// <summary>
/// Contains one candidate line's normalized boundaries, measurements, and feasibility.
/// </summary>
public sealed record LineMetrics(
    Breakpoint Start,
    Breakpoint End,
    int StartItemIndex,
    int EndItemIndexExclusive,
    double NaturalWidth,
    double Stretch,
    double Shrink,
    double TargetWidth,
    double? AdjustmentRatio,
    double? Badness,
    FitnessClass? Fitness,
    int BreakPenalty,
    bool IsFlagged,
    bool IsForced,
    bool IsLast,
    bool IsFeasible,
    LineRejectionReason? RejectionReason);

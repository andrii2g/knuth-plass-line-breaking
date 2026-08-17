using KnuthPlass.Core.Metrics;

namespace KnuthPlass.Core.Breaking;

/// <summary>
/// Describes a measured candidate and the transition context used to score it.
/// </summary>
public sealed record CandidateLine(
    LineMetrics Metrics,
    FitnessClass? PreviousFitness,
    bool PreviousBreakWasFlagged);

using KnuthPlass.Core.Breaking;

namespace KnuthPlass.Core.Results;

/// <summary>
/// Captures one finite feasible DP edge for deterministic graph rendering.
/// </summary>
public sealed record BreakpointGraphEdge(
    int StartBreakpointId,
    FitnessClass? StartFitness,
    int EndBreakpointId,
    FitnessClass EndFitness,
    double LineDemerits,
    double? AccumulatedDemerits);

using System.Collections.Immutable;
using KnuthPlass.Core.Breaking;

namespace KnuthPlass.Core.Tracing;

/// <summary>
/// Base type for deterministic algorithm decision events.
/// </summary>
public abstract record TraceEvent;

public sealed record CandidateEvaluated(
    CandidateLine Candidate,
    double? LineDemerits = null,
    double? AccumulatedCandidateDemerits = null) : TraceEvent;

public sealed record CandidateRejected(
    CandidateLine Candidate,
    CandidateRejectionKind Reason = CandidateRejectionKind.Measurement) : TraceEvent;

public sealed record StateUpdated(
    CandidateLine Candidate,
    double TotalDemerits,
    int LineCount,
    double LineDemerits = 0) : TraceEvent;

public sealed record StateRetained(
    CandidateLine Candidate,
    double CandidateTotalDemerits,
    double RetainedTotalDemerits,
    int RetainedLineCount,
    double LineDemerits = 0) : TraceEvent;

public sealed record FinalStateSelected(
    int BreakpointId,
    FitnessClass Fitness,
    double TotalDemerits,
    int LineCount) : TraceEvent;

public sealed record PathReconstructed(
    ImmutableArray<int> BreakpointIds) : TraceEvent;

public enum CandidateRejectionKind
{
    Measurement = 0,
    NonFiniteDemerits = 1,
}

using KnuthPlass.Core.Breaking;

namespace KnuthPlass.Core.Tracing;

/// <summary>
/// Base type for deterministic algorithm decision events.
/// </summary>
public abstract record TraceEvent;

public sealed record CandidateEvaluated(CandidateLine Candidate) : TraceEvent;

public sealed record CandidateRejected(CandidateLine Candidate) : TraceEvent;

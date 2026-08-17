namespace KnuthPlass.Core.Tracing;

/// <summary>
/// Associates a trace event with its stable, one-based orchestration sequence.
/// </summary>
public sealed record SequencedTraceEvent(long Sequence, TraceEvent Event);

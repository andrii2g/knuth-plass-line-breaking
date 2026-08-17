namespace KnuthPlass.Core.Tracing;

/// <summary>
/// Assigns and returns the orchestration sequence used for a trace event.
/// </summary>
public interface ISequencedTraceSink : ITraceSink
{
    SequencedTraceEvent WriteSequenced(TraceEvent traceEvent);
}

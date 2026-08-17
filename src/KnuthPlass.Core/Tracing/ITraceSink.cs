namespace KnuthPlass.Core.Tracing;

/// <summary>
/// Receives deterministic typed decision events from line breakers.
/// </summary>
public interface ITraceSink
{
    void Write(TraceEvent traceEvent);
}

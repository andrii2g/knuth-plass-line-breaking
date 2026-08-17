using System.Collections.Immutable;

namespace KnuthPlass.Core.Tracing;

/// <summary>
/// Captures trace events in emission order and assigns monotonic sequence numbers.
/// </summary>
public sealed class InMemoryTraceSink : ISequencedTraceSink
{
    private readonly object gate = new();
    private readonly List<SequencedTraceEvent> events = [];
    private long nextSequence = 1;

    public ImmutableArray<SequencedTraceEvent> Events
    {
        get
        {
            lock (gate)
            {
                return [.. events];
            }
        }
    }

    public void Write(TraceEvent traceEvent) => WriteSequenced(traceEvent);

    public SequencedTraceEvent WriteSequenced(TraceEvent traceEvent)
    {
        ArgumentNullException.ThrowIfNull(traceEvent);

        lock (gate)
        {
            var sequenced = new SequencedTraceEvent(nextSequence, traceEvent);
            events.Add(sequenced);
            nextSequence = checked(nextSequence + 1);
            return sequenced;
        }
    }
}

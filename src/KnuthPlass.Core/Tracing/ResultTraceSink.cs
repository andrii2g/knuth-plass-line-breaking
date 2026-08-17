using System.Collections.Immutable;

namespace KnuthPlass.Core.Tracing;

internal sealed class ResultTraceSink(ITraceSink downstream) : ITraceSink
{
    private readonly object gate = new();
    private readonly List<SequencedTraceEvent> captured = [];
    private long nextLocalSequence = 1;

    public ImmutableArray<SequencedTraceEvent> Events
    {
        get
        {
            lock (gate)
            {
                return [.. captured];
            }
        }
    }

    public void Write(TraceEvent traceEvent)
    {
        ArgumentNullException.ThrowIfNull(traceEvent);

        lock (gate)
        {
            var sequenced = downstream is ISequencedTraceSink sequencedSink
                ? sequencedSink.WriteSequenced(traceEvent)
                : new SequencedTraceEvent(nextLocalSequence++, traceEvent);

            if (downstream is not ISequencedTraceSink)
            {
                downstream.Write(traceEvent);
            }

            captured.Add(sequenced);
        }
    }

    public TraceDocument CreateDocument(
        Core.Model.Paragraph paragraph,
        Core.Breaking.LineBreakingOptions options) =>
        new(options, paragraph.Breakpoints, Events);
}

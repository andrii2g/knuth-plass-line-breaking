using System.Collections.Immutable;
using KnuthPlass.Core.Breaking;
using KnuthPlass.Core.Model;
using KnuthPlass.Core.Tracing;

namespace KnuthPlass.Core.Tests.Tracing;

public sealed class InMemoryTraceSinkTests
{
    [Fact]
    public void SinkAssignsStrictlyIncreasingOneBasedSequenceNumbers()
    {
        var sink = new InMemoryTraceSink();
        sink.Write(new PathReconstructed(ImmutableArray.Create(0, 1)));
        sink.Write(new PathReconstructed(ImmutableArray.Create(0, 2)));

        Assert.Equal([1L, 2L], sink.Events.Select(item => item.Sequence));
        Assert.All(sink.Events, item => Assert.IsType<PathReconstructed>(item.Event));
    }

    [Fact]
    public void RepeatedSolverRunsProduceIdenticalTypedEventOrder()
    {
        var paragraph = new Paragraph(
        [
            new Box("aa", 2),
            new Glue(1, 1, 1),
            new Box("bb", 2),
            new Glue(1, 1, 1),
            new Box("cc", 2),
            new Penalty(0, Penalty.ForcedBreak, false),
        ]);
        var options = new LineBreakingOptions(5);
        var firstSink = new InMemoryTraceSink();
        var secondSink = new InMemoryTraceSink();

        var firstResult = new KnuthPlassLineBreaker().Break(paragraph, options, firstSink);
        var secondResult = new KnuthPlassLineBreaker().Break(paragraph, options, secondSink);

        Assert.Equal(
            firstSink.Events.Select(item => item.Sequence),
            secondSink.Events.Select(item => item.Sequence));
        Assert.Equal(
            firstSink.Events.Select(item => item.Sequence),
            firstResult.TraceEvents.Select(item => item.Sequence));
        Assert.Equal(
            secondSink.Events.Select(item => item.Event.GetType()),
            secondResult.TraceEvents.Select(item => item.Event.GetType()));
        Assert.Equal(
            firstSink.Events.Select(item => item.Event.GetType()),
            secondSink.Events.Select(item => item.Event.GetType()));

        foreach (var (first, second) in firstSink.Events.Zip(secondSink.Events))
        {
            if (first.Event is PathReconstructed firstPath
                && second.Event is PathReconstructed secondPath)
            {
                Assert.Equal(
                    firstPath.BreakpointIds.ToArray(),
                    secondPath.BreakpointIds.ToArray());
            }
            else
            {
                Assert.Equal(first.Event, second.Event);
            }
        }
        Assert.Equal(
            Enumerable.Range(1, firstSink.Events.Length).Select(value => (long)value),
            firstSink.Events.Select(item => item.Sequence));
    }
    [Fact]
    public void ReusedSequencedSinkPreservesOneOrchestrationSequenceInResults()
    {
        var paragraph = new Paragraph(
        [
            new Box("word", 4),
            new Penalty(0, Penalty.ForcedBreak, false),
        ]);
        var options = new LineBreakingOptions(4);
        var sharedSink = new InMemoryTraceSink();

        var greedy = new GreedyLineBreaker().Break(paragraph, options, sharedSink);
        var optimal = new KnuthPlassLineBreaker().Break(paragraph, options, sharedSink);

        Assert.NotEmpty(greedy.TraceEvents);
        Assert.NotEmpty(optimal.TraceEvents);
        Assert.Equal(greedy.TraceEvents[^1].Sequence + 1, optimal.TraceEvents[0].Sequence);
        Assert.Equal(
            sharedSink.Events.Select(item => item.Sequence),
            greedy.TraceEvents
                .Concat(optimal.TraceEvents)
                .Select(item => item.Sequence));
    }

}

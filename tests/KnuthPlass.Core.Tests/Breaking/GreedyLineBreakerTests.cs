using KnuthPlass.Core.Breaking;
using KnuthPlass.Core.Metrics;
using KnuthPlass.Core.Model;
using KnuthPlass.Core.Results;
using KnuthPlass.Core.Tracing;

namespace KnuthPlass.Core.Tests.Breaking;

public sealed class GreedyLineBreakerTests
{
    [Fact]
    public void BreakSelectsFarthestFeasibleBreakpointAndReconstructsWords()
    {
        var paragraph = ThreeWordParagraph();
        var options = new LineBreakingOptions(5);

        var result = new GreedyLineBreaker().Break(paragraph, options);

        Assert.True(result.IsSuccess);
        Assert.Equal([0, 2, 3], result.SelectedBreakpointIds.ToArray());
        Assert.Equal(["aa", "bb", "cc"],
            result.Lines.SelectMany(line => line.Boxes).Select(box => box.Text));
        Assert.Equal(2, result.Lines.Length);
        Assert.Equal([0, 1], result.Lines.Select(line => line.LineNumber));
        Assert.All(result.Lines, line => Assert.False(line.IsOverfull));
        Assert.NotNull(result.TotalDemerits);

        var measurement = new LineMeasurement(paragraph);
        foreach (var line in result.Lines)
        {
            Assert.Equal(
                measurement.Measure(line.Metrics.Start, line.Metrics.End, options),
                line.Metrics);
        }
    }

    [Fact]
    public void BreakStopsAtAndHonorsForcedBreaks()
    {
        var paragraph = new Paragraph(
        [
            new Box("left", 4),
            new Penalty(0, Penalty.ForcedBreak, false),
            new Box("end", 3),
            new Penalty(0, Penalty.ForcedBreak, false),
        ]);

        var result = new GreedyLineBreaker().Break(
            paragraph,
            new LineBreakingOptions(4));

        Assert.True(result.IsSuccess);
        Assert.Equal([0, 1, 2], result.SelectedBreakpointIds.ToArray());
        Assert.Equal(["left"], result.Lines[0].Boxes.Select(box => box.Text));
        Assert.Equal(["end"], result.Lines[1].Boxes.Select(box => box.Text));
        Assert.True(result.Lines[0].Metrics.IsForced);
        Assert.True(result.Lines[1].Metrics.IsForced);
    }

    [Fact]
    public void LongUnbreakableWordTerminatesAsExplicitOverfullLine()
    {
        var paragraph = new Paragraph(
        [
            new Box("abcdefghij", 10),
            new Penalty(0, Penalty.ForcedBreak, false),
        ]);

        var result = new GreedyLineBreaker().Break(
            paragraph,
            new LineBreakingOptions(4));

        Assert.True(result.IsSuccess);
        Assert.Equal([0, 1], result.SelectedBreakpointIds.ToArray());
        var line = Assert.Single(result.Lines);
        Assert.True(line.IsOverfull);
        Assert.False(line.Metrics.IsFeasible);
        Assert.Equal(10, line.Metrics.NaturalWidth);
        Assert.Null(line.LineDemerits);
        Assert.Null(line.AccumulatedDemerits);
        Assert.Null(result.TotalDemerits);
    }

    [Fact]
    public void UnreachableUnderfullForcedLineReturnsFailureWithoutPartialLayout()
    {
        var paragraph = new Paragraph(
        [
            new Box("aa", 2),
            new Penalty(0, Penalty.ForcedBreak, false),
            new Box("tail", 4),
            new Penalty(0, Penalty.ForcedBreak, false),
        ]);

        var result = new GreedyLineBreaker().Break(
            paragraph,
            new LineBreakingOptions(4));

        Assert.False(result.IsSuccess);
        Assert.Equal(FailureReason.NoFeasibleLayout, result.FailureReason);
        Assert.Empty(result.Lines);
        Assert.Empty(result.SelectedBreakpointIds);
        Assert.Null(result.TotalDemerits);
    }

    [Fact]
    public void BreakReturnsInvalidOptionsInsteadOfThrowing()
    {
        var result = new GreedyLineBreaker().Break(
            ThreeWordParagraph(),
            new LineBreakingOptions(double.NaN));

        Assert.False(result.IsSuccess);
        Assert.Equal(FailureReason.InvalidOptions, result.FailureReason);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void CandidateTraceUsesMeasuredCandidateSchemaAndStableOrder()
    {
        var paragraph = ThreeWordParagraph();
        var sink = new RecordingTraceSink();

        var result = new GreedyLineBreaker().Break(
            paragraph,
            new LineBreakingOptions(5),
            sink);

        Assert.True(result.IsSuccess);
        Assert.Equal(result.EvaluatedCandidates,
            sink.Events.OfType<CandidateEvaluated>().Count());
        Assert.Equal(result.RejectedCandidates,
            sink.Events.OfType<CandidateRejected>().Count());
        Assert.Equal(
            [(0, 1), (0, 2), (0, 3), (2, 3)],
            sink.Events
                .OfType<CandidateEvaluated>()
                .Select(item =>
                    (item.Candidate.Metrics.Start.Id, item.Candidate.Metrics.End.Id)));
        Assert.All(
            sink.Events.OfType<CandidateRejected>(),
            item => Assert.False(item.Candidate.Metrics.IsFeasible));
    }

    [Fact]
    public void NonFiniteAccumulationReturnsTypedFailure()
    {
        var paragraph = new Paragraph(
        [
            new Box("four", 4),
            new Penalty(0, Penalty.ForcedBreak, false),
        ]);

        var result = new GreedyLineBreaker().Break(
            paragraph,
            new LineBreakingOptions(4, LinePenalty: double.MaxValue));

        Assert.False(result.IsSuccess);
        Assert.Equal(FailureReason.NonFiniteDemerits, result.FailureReason);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void ResultDemeritsMatchIndependentPathRecalculation()
    {
        var paragraph = new Paragraph(
        [
            new Box("aaa", 3),
            new Glue(1, 0, 1),
            new Box("b", 1),
            new Penalty(0, 5, true),
            new Box("c", 1),
            new Glue(1, 2, 0),
            new Box("d", 0.5),
            new Penalty(0, Penalty.ForcedBreak, true),
        ]);
        var options = new LineBreakingOptions(
            4,
            LinePenalty: 10,
            FitnessDemerit: 70,
            FlaggedDemerit: 30,
            LastLineMode: LastLineMode.Justified);

        var result = new GreedyLineBreaker().Break(paragraph, options);

        Assert.True(result.IsSuccess);
        Assert.Equal([0, 2, 4], result.SelectedBreakpointIds.ToArray());
        Assert.Equal(FitnessClass.VeryTight, result.Lines[0].Metrics.Fitness);
        Assert.Equal(FitnessClass.Loose, result.Lines[1].Metrics.Fitness);

        var firstExpected = Math.Pow(
            options.LinePenalty + result.Lines[0].Metrics.Badness!.Value,
            2) + 25;
        var secondExpected = Math.Pow(
            options.LinePenalty + result.Lines[1].Metrics.Badness!.Value,
            2) + options.FitnessDemerit + options.FlaggedDemerit;

        Assert.Equal(firstExpected, result.Lines[0].LineDemerits!.Value, 10);
        Assert.Equal(firstExpected, result.Lines[0].AccumulatedDemerits!.Value, 10);
        Assert.Equal(secondExpected, result.Lines[1].LineDemerits!.Value, 10);
        Assert.Equal(
            firstExpected + secondExpected,
            result.Lines[1].AccumulatedDemerits!.Value,
            10);
        Assert.Equal(firstExpected + secondExpected, result.TotalDemerits!.Value, 10);
    }

    [Fact]
    public void OverfullAfterFeasiblePrefixPreservesCompleteReconstruction()
    {
        var paragraph = new Paragraph(
        [
            new Box("fits", 4),
            new Penalty(0, 0, false),
            new Box("toolong", 10),
            new Penalty(0, Penalty.ForcedBreak, false),
        ]);

        var result = new GreedyLineBreaker().Break(
            paragraph,
            new LineBreakingOptions(4));

        Assert.True(result.IsSuccess);
        Assert.Equal([0, 1, 2], result.SelectedBreakpointIds.ToArray());
        Assert.Equal(["fits", "toolong"],
            result.Lines.SelectMany(line => line.Boxes).Select(box => box.Text));
        Assert.False(result.Lines[0].IsOverfull);
        Assert.NotNull(result.Lines[0].LineDemerits);
        Assert.True(result.Lines[1].IsOverfull);
        Assert.Null(result.Lines[1].LineDemerits);
        Assert.Null(result.Lines[1].AccumulatedDemerits);
        Assert.Null(result.TotalDemerits);
        Assert.Equal(3, result.EvaluatedCandidates);
        Assert.Equal(2, result.RejectedCandidates);
        Assert.Equal(1, result.FeasibleCandidates);
    }

    private static Paragraph ThreeWordParagraph() =>
        new(
        [
            new Box("aa", 2),
            new Glue(1, 0.5, 1d / 3),
            new Box("bb", 2),
            new Glue(1, 0.5, 1d / 3),
            new Box("cc", 2),
            new Penalty(0, Penalty.ForcedBreak, false),
        ]);

    private sealed class RecordingTraceSink : ITraceSink
    {
        public List<TraceEvent> Events { get; } = [];

        public void Write(TraceEvent traceEvent) => Events.Add(traceEvent);
    }
}

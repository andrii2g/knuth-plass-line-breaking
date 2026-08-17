using KnuthPlass.Core.Breaking;
using KnuthPlass.Core.Metrics;
using KnuthPlass.Core.Model;
using KnuthPlass.Core.Results;

namespace KnuthPlass.Core.Tests.Metrics;

public sealed class ParagraphMetricsTests
{
    [Fact]
    public void SuccessfulMetricsAreDerivedFromReconstructedLines()
    {
        var paragraph = ThreeWordParagraph();
        var result = new KnuthPlassLineBreaker().Break(
            paragraph,
            new LineBreakingOptions(5));

        Assert.True(result.IsSuccess);
        var metrics = Assert.IsType<ParagraphMetrics>(result.Metrics);

        var expectedBadness = result.Lines.Sum(line => line.Metrics.Badness!.Value);
        var expectedDemerits = result.Lines.Sum(line => line.LineDemerits!.Value);
        var expectedWorst = result.Lines.Max(line => line.Metrics.Badness!.Value);
        var expectedMeanRatio = result.Lines.Average(
            line => Math.Abs(line.Metrics.AdjustmentRatio!.Value));

        Assert.Equal(result.Lines.Length, metrics.LineCount);
        Assert.Equal(expectedBadness, metrics.TotalBadness!.Value, 12);
        Assert.Equal(expectedDemerits, metrics.TotalDemerits!.Value, 12);
        Assert.Equal(metrics.TotalDemerits, result.TotalDemerits);
        Assert.Equal(expectedWorst, metrics.WorstLineBadness!.Value, 12);
        Assert.Equal(expectedMeanRatio, metrics.MeanAbsoluteAdjustmentRatio!.Value, 12);
        Assert.Equal(result.Lines.Max(line => line.Metrics.Stretch), metrics.MaximumStretch);
        Assert.Equal(result.Lines.Max(line => line.Metrics.Shrink), metrics.MaximumShrink);
        Assert.Equal(result.EvaluatedCandidates, metrics.EvaluatedCandidates);
        Assert.Equal(result.RejectedCandidates, metrics.RejectedCandidates);
        Assert.Equal(result.FeasibleCandidates, metrics.FeasibleCandidates);
        Assert.Equal(result.SelectedBreakpointIds, metrics.SelectedBreakpointIds);
        Assert.Equal(metrics, MetricsCalculator.Calculate(result));
    }

    [Fact]
    public void OverfullLayoutKeepsCapacityMetricsButDoesNotInventScores()
    {
        var paragraph = new Paragraph(
        [
            new Box("toolong", 10),
            new Penalty(0, Penalty.ForcedBreak, false),
        ]);

        var result = new GreedyLineBreaker().Break(
            paragraph,
            new LineBreakingOptions(4));

        Assert.True(result.IsSuccess);
        var metrics = Assert.IsType<ParagraphMetrics>(result.Metrics);
        Assert.Equal(1, metrics.LineCount);
        Assert.Null(metrics.TotalBadness);
        Assert.Null(metrics.TotalDemerits);
        Assert.Null(result.TotalDemerits);
        Assert.Null(metrics.WorstLineBadness);
        Assert.Null(metrics.MeanAbsoluteAdjustmentRatio);
        Assert.Equal(0, metrics.MaximumStretch);
        Assert.Equal(0, metrics.MaximumShrink);
    }

    [Fact]
    public void FailedLayoutHasNoMetricsAndCannotBeAggregated()
    {
        var result = new KnuthPlassLineBreaker().Break(
            ThreeWordParagraph(),
            new LineBreakingOptions(double.NaN));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Metrics);
        Assert.Throws<InvalidOperationException>(() => MetricsCalculator.Calculate(result));
    }

    [Fact]
    public void SuccessfulFactoryRejectsInconsistentLineScoreStates()
    {
        var valid = new KnuthPlassLineBreaker().Break(
            ThreeWordParagraph(),
            new LineBreakingOptions(5));
        var incompleteFeasible = valid.Lines.SetItem(
            0,
            valid.Lines[0] with { LineDemerits = null });

        Assert.Throws<InvalidOperationException>(() => LineBreakResult.Succeeded(
            "test",
            incompleteFeasible,
            valid.SelectedBreakpointIds,
            valid.EvaluatedCandidates,
            valid.RejectedCandidates,
            valid.FeasibleCandidates));

        var overfullParagraph = new Paragraph(
        [
            new Box("toolong", 10),
            new Penalty(0, Penalty.ForcedBreak, false),
        ]);
        var overfull = new GreedyLineBreaker().Break(
            overfullParagraph,
            new LineBreakingOptions(4));
        var scoredOverfull = overfull.Lines.SetItem(
            0,
            overfull.Lines[0] with
            {
                LineDemerits = 1,
                AccumulatedDemerits = 1,
            });

        Assert.Throws<InvalidOperationException>(() => LineBreakResult.Succeeded(
            "test",
            scoredOverfull,
            overfull.SelectedBreakpointIds,
            overfull.EvaluatedCandidates,
            overfull.RejectedCandidates,
            overfull.FeasibleCandidates));
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
}

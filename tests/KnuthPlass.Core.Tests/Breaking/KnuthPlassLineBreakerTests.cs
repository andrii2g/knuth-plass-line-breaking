using KnuthPlass.Core.Breaking;
using KnuthPlass.Core.Model;
using KnuthPlass.Core.Parsing;
using KnuthPlass.Core.Results;
using KnuthPlass.Core.Tracing;

namespace KnuthPlass.Core.Tests.Breaking;

public sealed class KnuthPlassLineBreakerTests
{
    [Fact]
    public void SolverMatchesIndependentExhaustiveOracleOnSmallParagraphs()
    {
        var cases = new[]
        {
            (ThreeWordParagraph(), new LineBreakingOptions(5)),
            (FiveWordParagraph(), new LineBreakingOptions(
                6,
                LastLineMode: LastLineMode.Justified)),
            (PenaltyAndFitnessParagraph(), new LineBreakingOptions(
                4,
                FitnessDemerit: 70,
                FlaggedDemerit: 30,
                LastLineMode: LastLineMode.Justified)),
        };

        foreach (var (paragraph, options) in cases)
        {
            var expected = ExhaustiveLineBreakOracle.Solve(paragraph, options);
            var actual = new KnuthPlassLineBreaker().Break(paragraph, options);

            Assert.Equal(expected.IsSuccess, actual.IsSuccess);
            Assert.True(actual.IsSuccess);
            Assert.Equal(expected.TotalDemerits, actual.TotalDemerits!.Value, 10);
            Assert.Equal(expected.BreakpointIds, actual.SelectedBreakpointIds.ToArray());
            Assert.Equal(
                expected.Fitnesses,
                actual.Lines.Select(line => line.Metrics.Fitness!.Value).ToArray());
        }
    }

    [Fact]
    public void UnreachableFinalReturnsFailureWithoutPartialLayout()
    {
        var paragraph = new Paragraph(
        [
            new Box("aa", 2),
            new Penalty(0, Penalty.ForcedBreak, false),
            new Box("tail", 4),
            new Penalty(0, Penalty.ForcedBreak, false),
        ]);

        var result = new KnuthPlassLineBreaker().Break(
            paragraph,
            new LineBreakingOptions(4));

        Assert.False(result.IsSuccess);
        Assert.Equal(FailureReason.NoFeasibleLayout, result.FailureReason);
        Assert.Empty(result.Lines);
        Assert.Empty(result.SelectedBreakpointIds);
    }

    [Fact]
    public void InvalidOptionsAndNonFiniteDemeritsReturnTypedFailures()
    {
        var paragraph = new Paragraph(
        [
            new Box("four", 4),
            new Penalty(0, Penalty.ForcedBreak, false),
        ]);
        var breaker = new KnuthPlassLineBreaker();

        var invalid = breaker.Break(
            paragraph,
            new LineBreakingOptions(double.NaN));
        var nonFinite = breaker.Break(
            paragraph,
            new LineBreakingOptions(4, LinePenalty: double.MaxValue));

        Assert.Equal(FailureReason.InvalidOptions, invalid.FailureReason);
        Assert.Equal(FailureReason.NonFiniteDemerits, nonFinite.FailureReason);
        Assert.Empty(invalid.Lines);
        Assert.Empty(nonFinite.Lines);
        Assert.Equal(1, nonFinite.EvaluatedCandidates);
        Assert.Equal(1, nonFinite.RejectedCandidates);
        Assert.Equal(0, nonFinite.FeasibleCandidates);
        Assert.Equal(nonFinite.EvaluatedCandidates, nonFinite.RejectedCandidates + nonFinite.FeasibleCandidates);
    }

    [Fact]
    public void TraceRecordsStateDecisionsAndCompleteReconstructedPath()
    {
        var paragraph = ThreeWordParagraph();
        var sink = new RecordingTraceSink();

        var result = new KnuthPlassLineBreaker().Break(
            paragraph,
            new LineBreakingOptions(5),
            sink);

        Assert.True(result.IsSuccess);
        Assert.Equal(result.EvaluatedCandidates,
            sink.Events.OfType<CandidateEvaluated>().Count());
        Assert.NotEmpty(sink.Events.OfType<StateUpdated>());
        Assert.Single(sink.Events.OfType<FinalStateSelected>());
        var reconstructed = Assert.Single(sink.Events.OfType<PathReconstructed>());
        Assert.Equal(
            result.SelectedBreakpointIds.ToArray(),
            reconstructed.BreakpointIds.ToArray());
        Assert.IsType<PathReconstructed>(sink.Events[^1]);
    }

    [Fact]
    public void FlagshipParagraphProducesACompleteDeterministicLayout()
    {
        const string text = "Global optimization may accept a slightly looser early line " +
            "when that choice prevents an awkward and expensive line near the end of the paragraph.";
        var paragraph = new ParagraphTokenizer().Tokenize(text);
        var options = new LineBreakingOptions(32);
        var breaker = new KnuthPlassLineBreaker();

        var first = breaker.Break(paragraph, options);
        var second = breaker.Break(paragraph, options);

        Assert.True(first.IsSuccess);
        Assert.Equal(
            first.SelectedBreakpointIds.ToArray(),
            second.SelectedBreakpointIds.ToArray());
        Assert.Equal(first.TotalDemerits, second.TotalDemerits);
        Assert.Equal(
            paragraph.Words.ToArray(),
            first.Lines.SelectMany(line => line.Boxes).Select(box => box.Text));
    }

    [Fact]
    public void GlobalOptimizationBeatsGreedyOnFlagshipParagraph()
    {
        const string text = "Global optimization may accept a slightly looser early line " +
            "when that choice prevents an awkward and expensive line near the end of the paragraph.";
        var paragraph = new ParagraphTokenizer().Tokenize(text);
        var options = new LineBreakingOptions(32);

        var greedy = new GreedyLineBreaker().Break(paragraph, options);
        var optimal = new KnuthPlassLineBreaker().Break(paragraph, options);

        Assert.True(greedy.IsSuccess);
        Assert.True(optimal.IsSuccess);
        Assert.NotNull(greedy.TotalDemerits);
        Assert.NotEqual(
            greedy.SelectedBreakpointIds.ToArray(),
            optimal.SelectedBreakpointIds.ToArray());
        Assert.True(optimal.TotalDemerits < greedy.TotalDemerits);
    }

    [Fact]
    public void EqualCostLayoutsChooseEarlierFinalPredecessor()
    {
        var paragraph = CreateWordParagraph(
            [1, 1, 1, 1, 1],
            glueStretch: 2,
            glueShrink: 0.25);
        var options = new LineBreakingOptions(
            5,
            LastLineMode: LastLineMode.Justified);
        var allLayouts = ExhaustiveLineBreakOracle.EnumerateAll(paragraph, options);
        var expected = ExhaustiveLineBreakOracle.Solve(paragraph, options);

        var tiedBest = allLayouts.Count(layout =>
            Math.Abs(layout.TotalDemerits - expected.TotalDemerits) <= options.Epsilon &&
            layout.Fitnesses.Count == expected.Fitnesses.Count);
        var actual = new KnuthPlassLineBreaker().Break(paragraph, options);

        Assert.True(tiedBest >= 2);
        Assert.Equal([0, 2, 5], expected.BreakpointIds);
        Assert.Equal(expected.BreakpointIds, actual.SelectedBreakpointIds.ToArray());
        Assert.Equal(expected.TotalDemerits, actual.TotalDemerits!.Value, 10);
    }

    [Fact]
    public void FitnessAwareStateBeatsOneStatePerBreakpointApproximation()
    {
        var paragraph = CreateWordParagraph(
            [3, 1, 1, 1, 3, 1, 1],
            glueStretch: 3,
            glueShrink: 1);
        var options = new LineBreakingOptions(
            5,
            FitnessDemerit: 5000,
            LastLineMode: LastLineMode.Justified);
        var expected = ExhaustiveLineBreakOracle.Solve(paragraph, options);
        var collapsed = ExhaustiveLineBreakOracle.SolveWithCollapsedFitness(
            paragraph,
            options);

        var actual = new KnuthPlassLineBreaker().Break(paragraph, options);

        Assert.True(expected.IsSuccess);
        Assert.True(collapsed.IsSuccess);
        Assert.True(actual.IsSuccess);
        Assert.Equal([0, 3, 5, 7], expected.BreakpointIds);
        Assert.Equal([0, 2, 4, 7], collapsed.BreakpointIds);
        Assert.Equal(expected.TotalDemerits, actual.TotalDemerits!.Value, 10);
        Assert.Equal(expected.BreakpointIds, actual.SelectedBreakpointIds.ToArray());
        Assert.Equal(
            expected.Fitnesses,
            actual.Lines.Select(line => line.Metrics.Fitness!.Value).ToArray());
        Assert.True(actual.TotalDemerits < collapsed.TotalDemerits);
    }

    [Fact]
    public void SolverHonorsSuccessfulInternalForcedBreak()
    {
        var paragraph = new Paragraph(
        [
            new Box("left", 4),
            new Penalty(0, Penalty.ForcedBreak, false),
            new Box("end", 3),
            new Penalty(0, Penalty.ForcedBreak, false),
        ]);
        var forced = paragraph.Breakpoints[1];
        var sink = new RecordingTraceSink();

        var result = new KnuthPlassLineBreaker().Break(
            paragraph,
            new LineBreakingOptions(4),
            sink);

        Assert.True(result.IsSuccess);
        Assert.Equal([0, 1, 2], result.SelectedBreakpointIds.ToArray());
        Assert.Equal(["left", "end"],
            result.Lines.SelectMany(line => line.Boxes).Select(box => box.Text));
        Assert.All(result.Lines, line => Assert.True(line.Metrics.IsForced));
        Assert.Equal(2, result.EvaluatedCandidates);
        Assert.All(
            sink.Events
                .OfType<CandidateEvaluated>()
                .Where(item => item.Candidate.Metrics.End.Id > forced.Id),
            item => Assert.True(item.Candidate.Metrics.Start.Id >= forced.Id));
        Assert.Equal(
            result.EvaluatedCandidates,
            result.RejectedCandidates + result.FeasibleCandidates);
    }

    [Fact]
    public void FiveHundredWordsRespectTheTheoreticalCandidateBound()
    {
        var paragraph = new ParagraphTokenizer().Tokenize(
            string.Join(' ', Enumerable.Repeat("a", 500)));

        var result = new KnuthPlassLineBreaker().Break(
            paragraph,
            new LineBreakingOptions(32));

        var breakpointCount = paragraph.Breakpoints.Length;
        var theoreticalMaximum =
            (long)breakpointCount * (breakpointCount - 1) / 2 * 4;

        Assert.True(result.IsSuccess);
        Assert.InRange(result.EvaluatedCandidates, 1, theoreticalMaximum);
        Assert.Equal(500,
            result.Lines.Sum(line => line.Boxes.Length));
        Assert.Equal(paragraph.End.Id, result.SelectedBreakpointIds[^1]);
    }

    private static Paragraph CreateWordParagraph(
        IReadOnlyList<int> widths,
        double glueStretch,
        double glueShrink)
    {
        var items = new List<ParagraphItem>();
        for (var index = 0; index < widths.Count; index++)
        {
            if (index > 0)
            {
                items.Add(new Glue(1, glueStretch, glueShrink));
            }

            items.Add(new Box($"w{index}", widths[index]));
        }

        items.Add(new Penalty(0, Penalty.ForcedBreak, false));
        return new Paragraph(items);
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

    private static Paragraph FiveWordParagraph() =>
        new(
        [
            new Box("a", 1),
            new Glue(1, 2, 1),
            new Box("bbb", 3),
            new Glue(1, 2, 1),
            new Box("cc", 2),
            new Glue(1, 2, 1),
            new Box("dddd", 4),
            new Glue(1, 2, 1),
            new Box("e", 1),
            new Penalty(0, Penalty.ForcedBreak, false),
        ]);

    private static Paragraph PenaltyAndFitnessParagraph() =>
        new(
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

    private sealed class RecordingTraceSink : ITraceSink
    {
        public List<TraceEvent> Events { get; } = [];

        public void Write(TraceEvent traceEvent) => Events.Add(traceEvent);
    }
}

using KnuthPlass.Core.Breaking;
using KnuthPlass.Core.Metrics;
using KnuthPlass.Core.Model;

namespace KnuthPlass.Core.Tests.Breaking;

public sealed class LineDemeritCalculatorTests
{
    [Theory]
    [InlineData(5, 169)]
    [InlineData(-5, 119)]
    [InlineData(Penalty.ForcedBreak, 144)]
    public void PenaltyFormulaHandlesPositiveNegativeAndForcedValues(
        int penalty,
        double expected)
    {
        var calculated = LineDemeritCalculator.TryCalculate(
            Metrics(badness: 2, FitnessClass.Tight, penalty),
            null,
            false,
            new LineBreakingOptions(10),
            out var demerits);

        Assert.True(calculated);
        Assert.Equal(expected, demerits);
    }

    [Fact]
    public void FitnessJumpAndConsecutiveFlagsAddConfiguredDemerits()
    {
        var calculated = LineDemeritCalculator.TryCalculate(
            Metrics(badness: 2, FitnessClass.Loose, Penalty.ForcedBreak, flagged: true),
            FitnessClass.VeryTight,
            true,
            new LineBreakingOptions(10, FitnessDemerit: 70, FlaggedDemerit: 30),
            out var demerits);

        Assert.True(calculated);
        Assert.Equal(244, demerits);
    }

    [Fact]
    public void AdjacentFitnessClassesAndSingleFlagAddNothing()
    {
        var calculated = LineDemeritCalculator.TryCalculate(
            Metrics(badness: 0, FitnessClass.Loose, Penalty.ForcedBreak, flagged: true),
            FitnessClass.Tight,
            false,
            new LineBreakingOptions(10, FitnessDemerit: 70, FlaggedDemerit: 30),
            out var demerits);

        Assert.True(calculated);
        Assert.Equal(100, demerits);
    }

    [Fact]
    public void RejectedOrOverflowingCalculationIsNotStored()
    {
        Assert.False(LineDemeritCalculator.TryCalculate(
            Metrics(badness: 0, FitnessClass.Tight, 0) with
            {
                IsFeasible = false,
                Badness = null,
                Fitness = null,
            },
            null,
            false,
            new LineBreakingOptions(10),
            out _));

        Assert.False(LineDemeritCalculator.TryCalculate(
            Metrics(badness: 0, FitnessClass.Tight, 0),
            null,
            false,
            new LineBreakingOptions(10, LinePenalty: double.MaxValue),
            out _));
    }

    [Fact]
    public void InvalidOptionsAndForbiddenMetricsAreRejected()
    {
        var metrics = Metrics(badness: 2, FitnessClass.Loose, 0);

        Assert.False(LineDemeritCalculator.TryCalculate(
            metrics,
            FitnessClass.VeryTight,
            false,
            new LineBreakingOptions(10, FitnessDemerit: -1),
            out _));
        Assert.False(LineDemeritCalculator.TryCalculate(
            metrics,
            FitnessClass.VeryTight,
            false,
            new LineBreakingOptions(10, FlaggedDemerit: double.NaN),
            out _));
        Assert.False(LineDemeritCalculator.TryCalculate(
            metrics,
            null,
            false,
            null!,
            out _));
        Assert.False(LineDemeritCalculator.TryCalculate(
            Metrics(badness: 2, FitnessClass.Tight, Penalty.ForbiddenBreak),
            null,
            false,
            new LineBreakingOptions(10),
            out _));
        Assert.False(LineDemeritCalculator.TryCalculate(
            metrics with { Badness = -1 },
            null,
            false,
            new LineBreakingOptions(10),
            out _));
        Assert.False(LineDemeritCalculator.TryCalculate(
            metrics with { Badness = double.NaN },
            null,
            false,
            new LineBreakingOptions(10),
            out _));
        Assert.False(LineDemeritCalculator.TryCalculate(
            metrics with { Fitness = (FitnessClass)999 },
            null,
            false,
            new LineBreakingOptions(10),
            out _));
        Assert.False(LineDemeritCalculator.TryCalculate(
            metrics,
            (FitnessClass)int.MinValue,
            false,
            new LineBreakingOptions(10),
            out _));
    }

    private static LineMetrics Metrics(
        double badness,
        FitnessClass fitness,
        int penalty,
        bool flagged = false) =>
        new(
            new Breakpoint(0, -1, true, false, false),
            new Breakpoint(1, 0, false, penalty <= Penalty.ForcedBreak, flagged),
            0,
            0,
            10,
            0,
            0,
            10,
            0,
            badness,
            fitness,
            penalty,
            flagged,
            penalty <= Penalty.ForcedBreak,
            true,
            true,
            null);
}

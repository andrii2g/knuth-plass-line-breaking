using KnuthPlass.Core.Breaking;
using KnuthPlass.Core.Metrics;
using KnuthPlass.Core.Model;

namespace KnuthPlass.Core.Tests.Metrics;

public sealed class LineMeasurementTests
{
    [Theory]
    [InlineData(6, 0, 0, FitnessClass.Tight)]
    [InlineData(7, 0.5, 12.5, FitnessClass.Tight)]
    [InlineData(8, 1, 100, FitnessClass.Loose)]
    [InlineData(10, 2, 800, FitnessClass.VeryLoose)]
    public void MeasureCalculatesRatioBadnessAndFitness(
        double targetWidth,
        double expectedRatio,
        double expectedBadness,
        FitnessClass expectedFitness)
    {
        var paragraph = CreateFlexibleParagraph();
        var measurement = new LineMeasurement(paragraph);

        var result = measurement.Measure(
            paragraph.Start,
            paragraph.End,
            Options(targetWidth));

        Assert.True(result.IsFeasible);
        Assert.Equal(expectedRatio, result.AdjustmentRatio!.Value, 12);
        Assert.Equal(expectedBadness, result.Badness!.Value, 12);
        Assert.Equal(expectedFitness, result.Fitness);
        Assert.Equal(6, result.NaturalWidth);
        Assert.Equal(2, result.Stretch);
        Assert.Equal(1, result.Shrink);
    }

    [Theory]
    [InlineData(5, true, null)]
    [InlineData(4.9, false, LineRejectionReason.AdjustmentRatioTooLow)]
    [InlineData(12, true, null)]
    [InlineData(12.1, false, LineRejectionReason.AdjustmentRatioTooHigh)]
    public void MeasureAppliesOrdinaryFeasibilityBounds(
        double targetWidth,
        bool expectedFeasible,
        LineRejectionReason? expectedReason)
    {
        var paragraph = CreateFlexibleParagraph();
        var result = new LineMeasurement(paragraph).Measure(
            paragraph.Start,
            paragraph.End,
            Options(targetWidth));

        Assert.Equal(expectedFeasible, result.IsFeasible);
        Assert.Equal(expectedReason, result.RejectionReason);
    }

    [Fact]
    public void MeasureUsesEpsilonOnlyForBoundaryComparison()
    {
        var paragraph = CreateFlexibleParagraph();
        var measurement = new LineMeasurement(paragraph);
        const double epsilon = 1e-9;

        var high = measurement.Measure(
            paragraph.Start,
            paragraph.End,
            Options(12.000000001, epsilon));
        var low = measurement.Measure(
            paragraph.Start,
            paragraph.End,
            Options(4.9999999995, epsilon));

        Assert.True(high.IsFeasible);
        Assert.True(high.AdjustmentRatio > 3);
        Assert.True(low.IsFeasible);
        Assert.True(low.AdjustmentRatio < -1);
    }

    [Fact]
    public void RaggedLastLineHasZeroRatioWhenItFits()
    {
        var paragraph = CreateFlexibleParagraph();
        var result = new LineMeasurement(paragraph).Measure(
            paragraph.Start,
            paragraph.End,
            new LineBreakingOptions(20));

        Assert.True(result.IsFeasible);
        Assert.Equal(0, result.AdjustmentRatio);
        Assert.Equal(0, result.Badness);
        Assert.Equal(FitnessClass.Tight, result.Fitness);
    }

    [Fact]
    public void RaggedLastLineRejectsNaturalOverflowEvenWhenShrinkCouldFit()
    {
        var paragraph = CreateFlexibleParagraph();
        var result = new LineMeasurement(paragraph).Measure(
            paragraph.Start,
            paragraph.End,
            new LineBreakingOptions(5));

        Assert.False(result.IsFeasible);
        Assert.Equal(
            LineRejectionReason.OverfullRaggedLastLine,
            result.RejectionReason);
        Assert.Null(result.AdjustmentRatio);
    }

    [Fact]
    public void MeasureDiscardsBoundaryGlueAndAddsOnlySelectedPenaltyWidth()
    {
        var paragraph = new Paragraph(
        [
            new Box("aaaa", 4),
            new Glue(1, 4, 2),
            new Penalty(2, -50, true),
            new Glue(10, 10, 10),
            new Glue(20, 20, 20),
            new Box("bbb", 3),
            new Penalty(0, Penalty.ForcedBreak, false),
        ]);
        var measurement = new LineMeasurement(paragraph);
        var optionalBreak = paragraph.Breakpoints.Single(point => point.ItemIndex == 2);

        var first = measurement.Measure(
            paragraph.Start,
            optionalBreak,
            Options(6));
        var second = measurement.Measure(
            optionalBreak,
            paragraph.End,
            new LineBreakingOptions(3));

        Assert.True(first.IsFeasible);
        Assert.Equal(6, first.NaturalWidth);
        Assert.Equal(0, first.Stretch);
        Assert.Equal(0, first.Shrink);
        Assert.Equal(-50, first.BreakPenalty);
        Assert.True(first.IsFlagged);
        Assert.Equal((0, 1), (first.StartItemIndex, first.EndItemIndexExclusive));

        Assert.True(second.IsFeasible);
        Assert.Equal(3, second.NaturalWidth);
        Assert.Equal((5, 6), (second.StartItemIndex, second.EndItemIndexExclusive));
    }

    [Theory]
    [InlineData(-0.5000001, FitnessClass.VeryTight)]
    [InlineData(-0.5, FitnessClass.Tight)]
    [InlineData(0.5, FitnessClass.Tight)]
    [InlineData(0.5000001, FitnessClass.Loose)]
    [InlineData(1, FitnessClass.Loose)]
    [InlineData(1.0000001, FitnessClass.VeryLoose)]
    public void ClassifyFitnessHonorsAllBoundaries(
        double ratio,
        FitnessClass expected)
    {
        Assert.Equal(expected, LineMeasurement.ClassifyFitness(ratio));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.5, 12.5)]
    [InlineData(-0.5, 12.5)]
    [InlineData(1, 100)]
    [InlineData(2, 800)]
    [InlineData(100, 10000)]
    public void CalculateBadnessIsSymmetricCubicAndCapped(
        double ratio,
        double expected)
    {
        Assert.Equal(expected, LineMeasurement.CalculateBadness(ratio), 12);
        Assert.Equal(expected, LineMeasurement.CalculateBadness(-ratio), 12);
    }

    [Fact]
    public void MeasureRejectsMissingAdjustmentCapacity()
    {
        var paragraph = new Paragraph(
        [
            new Box("fixed", 5),
            new Penalty(0, Penalty.ForcedBreak, false),
        ]);
        var measurement = new LineMeasurement(paragraph);

        var stretch = measurement.Measure(
            paragraph.Start,
            paragraph.End,
            Options(6));
        var shrink = measurement.Measure(
            paragraph.Start,
            paragraph.End,
            Options(4));

        Assert.Equal(LineRejectionReason.InsufficientStretch, stretch.RejectionReason);
        Assert.Equal(LineRejectionReason.InsufficientShrink, shrink.RejectionReason);
        Assert.Null(stretch.AdjustmentRatio);
        Assert.Null(shrink.AdjustmentRatio);
    }

    [Fact]
    public void MeasureRejectsAnEdgeThatSkipsAForcedBreak()
    {
        var paragraph = new Paragraph(
        [
            new Box("left", 4),
            new Penalty(0, Penalty.ForcedBreak, false),
            new Box("right", 3),
            new Penalty(0, Penalty.ForcedBreak, false),
        ]);
        var measurement = new LineMeasurement(paragraph);
        var internalForcedBreak = paragraph.Breakpoints[1];

        var skipped = measurement.Measure(
            paragraph.Start,
            paragraph.End,
            Options(7));
        var first = measurement.Measure(
            paragraph.Start,
            internalForcedBreak,
            Options(4));
        var second = measurement.Measure(
            internalForcedBreak,
            paragraph.End,
            Options(3));

        Assert.False(skipped.IsFeasible);
        Assert.Equal(
            LineRejectionReason.ForcedBreakSkipped,
            skipped.RejectionReason);
        Assert.True(first.IsFeasible);
        Assert.True(second.IsFeasible);
    }

    [Fact]
    public void ForbiddenPenaltyIsNotAMeasurableParagraphBreakpoint()
    {
        var paragraph = new Paragraph(
        [
            new Box("left", 4),
            new Penalty(0, Penalty.ForbiddenBreak, false),
            new Box("right", 3),
            new Penalty(0, Penalty.ForcedBreak, false),
        ]);
        var measurement = new LineMeasurement(paragraph);
        var fabricatedForbidden = new Breakpoint(
            1,
            1,
            false,
            false,
            false);

        Assert.DoesNotContain(
            paragraph.Breakpoints,
            point => point.ItemIndex == 1);
        Assert.Throws<ArgumentException>(
            () => measurement.Measure(
                paragraph.Start,
                fabricatedForbidden,
                Options(4)));
    }

    [Fact]
    public void MeasureValidatesOptionsAndBreakpointOwnership()
    {
        var paragraph = CreateFlexibleParagraph();
        var measurement = new LineMeasurement(paragraph);
        var other = CreateFlexibleParagraph();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => measurement.Measure(
                paragraph.Start,
                paragraph.End,
                Options(double.NaN)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => measurement.Measure(
                paragraph.Start,
                paragraph.End,
                Options(6) with { MaxAdjustmentRatio = -1 }));
        Assert.Throws<ArgumentException>(
            () => measurement.Measure(
                other.Start,
                paragraph.End,
                Options(6)));
        Assert.Throws<ArgumentException>(
            () => measurement.Measure(
                paragraph.End,
                paragraph.Start,
                Options(6)));
    }

    private static Paragraph CreateFlexibleParagraph() =>
        new(
        [
            new Box("aaa", 3),
            new Glue(1, 2, 1),
            new Box("bb", 2),
            new Penalty(0, Penalty.ForcedBreak, false),
        ]);

    private static LineBreakingOptions Options(
        double targetWidth,
        double epsilon = 1e-9) =>
        new(
            targetWidth,
            MaxAdjustmentRatio: 3,
            LastLineMode: LastLineMode.Justified,
            Epsilon: epsilon);
}

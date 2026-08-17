using KnuthPlass.Core.Model;

namespace KnuthPlass.Core.Tests.Model;

public sealed class ParagraphModelTests
{
    [Fact]
    public void ParagraphAssignsContiguousSourceIndicesInItemOrder()
    {
        var paragraph = new Paragraph(
        [
            new Box("first", 5, 7),
            new Glue(1, 0.5, 0.25),
            new Box("second", 6),
            new Penalty(0, Penalty.ForcedBreak, false),
        ]);

        var boxes = paragraph.Items.OfType<Box>().ToArray();
        Assert.Equal(["first", "second"], paragraph.Words.ToArray());
        Assert.Equal([0, 1], boxes.Select(box => box.SourceWordIndex).ToArray());
    }

    [Fact]
    public void ItemRecordsSupportDocumentedNamedArgumentsAndDeconstruction()
    {
        var box = new Box(Text: "word", Width: 4);
        var glue = new Glue(Width: 1, Stretch: 0.5, Shrink: 0.25);
        var penalty = new Penalty(Width: 2, Value: -50, Flagged: true);

        var (text, boxWidth) = box;
        var (glueWidth, stretch, shrink) = glue;
        var (penaltyWidth, value, flagged) = penalty;

        Assert.Equal(("word", 4), (text, boxWidth));
        Assert.Equal((1, 0.5, 0.25), (glueWidth, stretch, shrink));
        Assert.Equal((2, -50, true), (penaltyWidth, value, flagged));
    }

    [Fact]
    public void PenaltySentinelsAreThresholds()
    {
        Assert.True(new Penalty(0, -10001, false).IsForced);
        Assert.True(new Penalty(0, 10001, false).IsForbidden);
    }

    [Fact]
    public void BreakpointDiscoveryIncludesOnlyLegalItemsInSourceOrder()
    {
        var paragraph = new Paragraph(
        [
            new Glue(1, 1, 1),
            new Box("alpha", 5),
            new Penalty(0, 50, true),
            new Glue(1, 1, 1),
            new Box("beta", 4),
            new Penalty(0, -10001, false),
            new Box("gamma", 5),
            new Penalty(0, Penalty.ForcedBreak, false),
        ]);

        Assert.Equal([-1, 2, 5, 7], paragraph.Breakpoints.Select(point => point.ItemIndex));
        Assert.False(paragraph.Breakpoints[1].IsForced);
        Assert.True(paragraph.Breakpoints[1].IsFlagged);
        Assert.True(paragraph.Breakpoints[2].IsForced);
        Assert.True(paragraph.Breakpoints[3].IsForced);
    }

    [Fact]
    public void PublicModelRejectsInvalidValues()
    {
        Assert.Throws<ArgumentException>(() => new Box(" ", 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Box("word", double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Box("word", -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Box("word", 1, -2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Glue(-1, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Glue(1, double.PositiveInfinity, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Penalty(-1, 0, false));
    }

    [Fact]
    public void ParagraphRequiresItemsAndAForcedFinalBreak()
    {
        Assert.Throws<ArgumentException>(() => new Paragraph([]));
        Assert.Throws<ArgumentException>(
            () => new Paragraph([new Box("word", 4), new Penalty(0, 0, false)]));
    }
}

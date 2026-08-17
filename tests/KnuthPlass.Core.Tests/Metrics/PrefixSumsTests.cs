using KnuthPlass.Core.Metrics;
using KnuthPlass.Core.Model;

namespace KnuthPlass.Core.Tests.Metrics;

public sealed class PrefixSumsTests
{
    [Fact]
    public void GetRangeMatchesDirectItemTotalsAndExcludesPenaltyWidths()
    {
        var paragraph = CreateParagraph();
        var sums = new PrefixSums(paragraph);

        Assert.Equal(new RangeTotals(6, 2, 0.5), sums.GetRange(0, 4));
        Assert.Equal(new RangeTotals(9, 3, 1.5), sums.GetRange(0, 7));
        Assert.Equal(new RangeTotals(0, 0, 0), sums.GetRange(3, 4));
        Assert.Equal(new RangeTotals(3, 1, 1), sums.GetRange(4, 6));
    }

    [Fact]
    public void GlueBoundaryLookupsTrimInConstantTime()
    {
        var sums = new PrefixSums(CreateParagraph());

        Assert.Equal(2, sums.SkipLeadingGlue(1));
        Assert.Equal(5, sums.SkipLeadingGlue(4));
        Assert.Equal(7, sums.SkipLeadingGlue(7));

        Assert.Equal(1, sums.TrimTrailingGlue(2));
        Assert.Equal(4, sums.TrimTrailingGlue(5));
        Assert.Equal(6, sums.TrimTrailingGlue(6));
    }

    [Fact]
    public void ConstructorRejectsAggregateWidthOverflow()
    {
        var paragraph = new Paragraph(
        [
            new Box("huge", double.MaxValue),
            new Glue(double.MaxValue, 0, 0),
            new Box("tail", 1),
            new Penalty(0, Penalty.ForcedBreak, false),
        ]);

        Assert.Throws<ArgumentException>(() => new PrefixSums(paragraph));
    }

    [Fact]
    public void GetRangeRejectsInvalidBounds()
    {
        var sums = new PrefixSums(CreateParagraph());

        Assert.Throws<ArgumentOutOfRangeException>(() => sums.GetRange(-1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => sums.GetRange(0, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => sums.GetRange(8, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => sums.GetRange(0, -1));
        Assert.Throws<ArgumentException>(() => sums.GetRange(4, 3));
    }

    [Fact]
    public void EveryRangeMatchesIndependentDirectSummation()
    {
        var paragraph = CreateParagraph();
        var sums = new PrefixSums(paragraph);

        for (var start = 0; start <= paragraph.Items.Length; start++)
        {
            for (var end = start; end <= paragraph.Items.Length; end++)
            {
                var width = 0d;
                var stretch = 0d;
                var shrink = 0d;

                for (var index = start; index < end; index++)
                {
                    switch (paragraph.Items[index])
                    {
                        case Box box:
                            width += box.Width;
                            break;
                        case Glue glue:
                            width += glue.Width;
                            stretch += glue.Stretch;
                            shrink += glue.Shrink;
                            break;
                    }
                }

                Assert.Equal(
                    new RangeTotals(width, stretch, shrink),
                    sums.GetRange(start, end));
            }
        }
    }

    [Fact]
    public void CountForcedBreaksUsesHalfOpenRanges()
    {
        var sums = new PrefixSums(CreateParagraph());

        Assert.Equal(0, sums.CountForcedBreaks(0, 6));
        Assert.Equal(1, sums.CountForcedBreaks(0, 7));
        Assert.Equal(1, sums.CountForcedBreaks(6, 7));
        Assert.Equal(0, sums.CountForcedBreaks(7, 7));
    }

    private static Paragraph CreateParagraph() =>
        new(
        [
            new Box("aa", 2),
            new Glue(1, 2, 0.5),
            new Box("bbb", 3),
            new Penalty(4, -50, false),
            new Glue(2, 1, 1),
            new Box("c", 1),
            new Penalty(0, Penalty.ForcedBreak, false),
        ]);
}

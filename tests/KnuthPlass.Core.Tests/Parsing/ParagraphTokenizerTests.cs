using KnuthPlass.Core.Model;
using KnuthPlass.Core.Parsing;

namespace KnuthPlass.Core.Tests.Parsing;

public sealed class ParagraphTokenizerTests
{
    private readonly ParagraphTokenizer tokenizer = new();

    [Fact]
    public void TokenizeNormalizesWhitespaceAndBuildsCanonicalItems()
    {
        var paragraph = tokenizer.Tokenize("  alpha\tbeta\r\n gamma  ");

        Assert.Equal(["alpha", "beta", "gamma"], paragraph.Words.ToArray());
        Assert.True(paragraph.HadLineBreaks);
        Assert.Collection(
            paragraph.Items,
            item => AssertBox(item, "alpha", 5, 0),
            item => AssertGlue(item, 1, 0.5, 1d / 3d),
            item => AssertBox(item, "beta", 4, 1),
            item => AssertGlue(item, 1, 0.5, 1d / 3d),
            item => AssertBox(item, "gamma", 5, 2),
            item => Assert.Equal(
                new Penalty(0, Penalty.ForcedBreak, false),
                Assert.IsType<Penalty>(item)));
    }

    [Fact]
    public void TokenizeDiscoversStableLegalBreakpoints()
    {
        var paragraph = tokenizer.Tokenize("one two three");

        Assert.Collection(
            paragraph.Breakpoints,
            point =>
            {
                Assert.Equal(0, point.Id);
                Assert.Equal(-1, point.ItemIndex);
                Assert.True(point.IsSyntheticStart);
            },
            point => Assert.Equal((1, 1, false), (point.Id, point.ItemIndex, point.IsForced)),
            point => Assert.Equal((2, 3, false), (point.Id, point.ItemIndex, point.IsForced)),
            point => Assert.Equal((3, 5, true), (point.Id, point.ItemIndex, point.IsForced)));

        Assert.Equal(paragraph.Breakpoints[^1], paragraph.End);
    }

    [Fact]
    public void TokenizeKeepsPunctuationAndCountsUnicodeScalars()
    {
        var paragraph = tokenizer.Tokenize("hi, 😀");

        var boxes = paragraph.Items.OfType<Box>().ToArray();
        Assert.Equal("hi,", boxes[0].Text);
        Assert.Equal(3, boxes[0].Width);
        Assert.Equal("😀", boxes[1].Text);
        Assert.Equal(1, boxes[1].Width);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void TokenizeRejectsEmptyNormalizedInput(string text)
    {
        var exception = Assert.Throws<ArgumentException>(() => tokenizer.Tokenize(text));

        Assert.Equal("text", exception.ParamName);
    }

    [Fact]
    public void ParagraphExcludesForbiddenPenaltiesFromBreakpoints()
    {
        var paragraph = new Paragraph(
        [
            new Box("alpha", 5, 0),
            new Penalty(0, Penalty.ForbiddenBreak, false),
            new Box("beta", 4, 1),
            new Penalty(0, Penalty.ForcedBreak, false),
        ]);

        Assert.Equal(2, paragraph.Breakpoints.Length);
        Assert.Equal(-1, paragraph.Start.ItemIndex);
        Assert.Equal(3, paragraph.End.ItemIndex);
    }

    [Fact]
    public void TokenizerOptionsRejectNonFiniteDimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TokenizerOptions(spaceWidth: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TokenizerOptions(stretch: double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TokenizerOptions(shrink: -0.1));
    }

    private static void AssertBox(
        ParagraphItem item,
        string text,
        double width,
        int sourceWordIndex)
    {
        var box = Assert.IsType<Box>(item);
        Assert.Equal(text, box.Text);
        Assert.Equal(width, box.Width);
        Assert.Equal(sourceWordIndex, box.SourceWordIndex);
    }

    private static void AssertGlue(
        ParagraphItem item,
        double width,
        double stretch,
        double shrink)
    {
        var glue = Assert.IsType<Glue>(item);
        Assert.Equal(width, glue.Width);
        Assert.Equal(stretch, glue.Stretch);
        Assert.Equal(shrink, glue.Shrink);
    }
}

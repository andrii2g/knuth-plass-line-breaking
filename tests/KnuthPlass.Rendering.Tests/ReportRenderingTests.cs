using System.Xml.Linq;
using KnuthPlass.Core.Breaking;
using KnuthPlass.Core.Model;
using KnuthPlass.Core.Parsing;
using KnuthPlass.Core.Results;
using KnuthPlass.Core.Tracing;
using KnuthPlass.Rendering.Html;
using KnuthPlass.Rendering.Svg;

namespace KnuthPlass.Rendering.Tests;

public sealed class ReportRenderingTests
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    [Fact]
    public void HtmlAndSvgAreWellFormedEscapedAndStructurallyStable()
    {
        var paragraph = SpecialParagraph();
        var options = new LineBreakingOptions(6, LastLineMode: LastLineMode.Justified);
        var first = BreakBoth(paragraph, options);
        var second = BreakBoth(paragraph, options);
        var htmlRenderer = new HtmlComparisonRenderer();
        var layoutRenderer = new LayoutComparisonSvgRenderer();
        var graphRenderer = new BreakpointGraphSvgRenderer();

        var html = htmlRenderer.Render(paragraph, options, first, includeTraceLink: true);
        var layout = layoutRenderer.Render(options, first);
        var graph = graphRenderer.Render(first.Single(result =>
            result.AlgorithmName == KnuthPlassLineBreaker.Name));

        Assert.Equal(html, htmlRenderer.Render(paragraph, options, second, includeTraceLink: true));
        Assert.Equal(layout, layoutRenderer.Render(options, second));
        Assert.Equal(graph, graphRenderer.Render(second.Single(result =>
            result.AlgorithmName == KnuthPlassLineBreaker.Name)));

        var htmlDocument = XDocument.Parse(html, LoadOptions.PreserveWhitespace);
        var layoutDocument = XDocument.Parse(layout, LoadOptions.PreserveWhitespace);
        var graphDocument = XDocument.Parse(graph, LoadOptions.PreserveWhitespace);

        Assert.Equal("en", htmlDocument.Root!.Attribute("lang")!.Value);
        Assert.Equal(2, htmlDocument.Descendants("section")
            .Count(element => (string?)element.Attribute("class") == "algorithm"));
        Assert.Contains("&lt;&gt;&amp;&quot;&apos;", html, StringComparison.Ordinal);
        Assert.Contains("\u0416", html, StringComparison.Ordinal);
        Assert.Contains("\uFFFD", html, StringComparison.Ordinal);
        Assert.Contains("\uFFFD", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("\u0001", html, StringComparison.Ordinal);
        Assert.DoesNotContain("\u0001", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("<>&\"'", html, StringComparison.Ordinal);
        Assert.All(
            htmlDocument.Descendants("a"),
            anchor => Assert.DoesNotContain("://", anchor.Attribute("href")!.Value));

        Assert.Equal("layout-title", layoutDocument.Root!.Element(Svg + "title")!.Attribute("id")!.Value);
        var glue = layoutDocument.Descendants(Svg + "rect")
            .First(element => (string?)element.Attribute("class") == "glue");
        Assert.Equal("1", glue.Attribute("data-natural-width")!.Value);
        Assert.Equal("2", glue.Attribute("data-adjusted-width")!.Value);
        Assert.Contains(layoutDocument.Descendants(Svg + "text"), element => element.Value.Contains('\u0416'));

        Assert.Equal("graph-title", graphDocument.Root!.Element(Svg + "title")!.Attribute("id")!.Value);
        Assert.Contains(
            graphDocument.Descendants(Svg + "path"),
            element => ((string?)element.Attribute("class"))?.Contains("selected", StringComparison.Ordinal) == true);

        Assert.Equal(
            ["summary", "(none)", "algorithm", "algorithm", "legend"],
            htmlDocument.Descendants("section")
                .Select(element => (string?)element.Attribute("class") ?? "(none)")
                .ToArray());
        Assert.Equal(
            ["layout-comparison.svg", "breakpoint-graph.svg", "summary.json", "trace.txt"],
            htmlDocument.Descendants("a")
                .Select(element => element.Attribute("href")!.Value)
                .ToArray());
        Assert.Equal(2, layoutDocument.Descendants(Svg + "g")
            .Count(element => element.Attribute("id")?.Value.StartsWith(
                "algorithm-", StringComparison.Ordinal) == true));
        Assert.Equal(2, layoutDocument.Descendants(Svg + "line")
            .Count(element => (string?)element.Attribute("class") == "ruler"));
        Assert.Equal(4, layoutDocument.Descendants(Svg + "rect")
            .Count(element => (string?)element.Attribute("class") == "box"));
        Assert.Equal(2, layoutDocument.Descendants(Svg + "rect")
            .Count(element => (string?)element.Attribute("class") == "glue"));
        Assert.Single(
            graphDocument.Descendants(Svg + "path"),
            element => ((string?)element.Attribute("class"))?.StartsWith(
                "edge", StringComparison.Ordinal) == true);
        Assert.Equal(2, graphDocument.Descendants(Svg + "circle").Count());

        AssertUniqueIds(layoutDocument);
        AssertUniqueIds(graphDocument);
    }

    [Fact]
    public void OverfullLayoutUsesVisibleLabelAndHatch()
    {
        var paragraph = new Paragraph(
        [
            new Box("toolong", 10),
            new Penalty(0, Penalty.ForcedBreak, false),
        ]);
        var options = new LineBreakingOptions(4);
        var results = new LineBreakResult[]
        {
            new GreedyLineBreaker().Break(paragraph, options),
            new KnuthPlassLineBreaker().Break(paragraph, options),
        };

        var svg = new LayoutComparisonSvgRenderer().Render(options, results);
        var document = XDocument.Parse(svg);

        Assert.Contains("Overfull - no comparable score", svg, StringComparison.Ordinal);
        Assert.Contains(
            document.Descendants(Svg + "rect"),
            element => (string?)element.Attribute("class") == "overfull-outline");
        Assert.Contains("url(#overfull-hatch)", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void BreakpointGraphPrunesDeterministicallyWithoutDroppingSelectedPath()
    {
        const string text = "Global optimization may accept a slightly looser early line " +
            "when that choice prevents an awkward and expensive line near the end of the paragraph.";
        var paragraph = new ParagraphTokenizer().Tokenize(text);
        var options = new LineBreakingOptions(32);
        var result = new KnuthPlassLineBreaker().Break(
            paragraph,
            options,
            new InMemoryTraceSink());
        var renderer = new BreakpointGraphSvgRenderer();
        var renderOptions = new BreakpointGraphRenderOptions(1, 0);

        var first = renderer.Render(result, renderOptions);
        var second = renderer.Render(result, renderOptions);
        var document = XDocument.Parse(first);
        var selectedEdges = document.Descendants(Svg + "path")
            .Count(element => ((string?)element.Attribute("class"))?.Contains("selected", StringComparison.Ordinal) == true);
        var selectedNodes = document.Descendants(Svg + "circle")
            .Count(element => ((string?)element.Attribute("class"))?.Contains("selected", StringComparison.Ordinal) == true);

        Assert.Equal(first, second);
        Assert.Contains("Graph pruned:", first, StringComparison.Ordinal);
        Assert.Equal(result.Lines.Length, selectedEdges);
        Assert.Equal(result.Lines.Length + 1, selectedNodes);
    }

    [Fact]
    public void SelectedPenaltyAndNegativeGluePreserveExactGeometry()
    {
        var penaltyParagraph = new Paragraph(
        [
            new Box("hy", 2),
            new Penalty(1, 0, true),
            new Box("tail", 2),
            new Penalty(0, Penalty.ForcedBreak, false),
        ]);
        var penaltyOptions = new LineBreakingOptions(3);
        var penaltyResult = new GreedyLineBreaker().Break(penaltyParagraph, penaltyOptions);

        Assert.Equal(1, penaltyResult.Lines[0].SelectedPenalty!.Width);
        var penaltyDocument = XDocument.Parse(
            new LayoutComparisonSvgRenderer().Render(penaltyOptions, [penaltyResult]));
        var penaltyRect = Assert.Single(
            penaltyDocument.Descendants(Svg + "rect"),
            element => (string?)element.Attribute("class") == "penalty");
        Assert.Equal("20", penaltyRect.Attribute("width")!.Value);
        Assert.Equal("210", penaltyRect.Attribute("x")!.Value);

        var negativeGlueParagraph = new Paragraph(
        [
            new Box("a", 1),
            new Glue(1, 0, 2),
            new Box("b", 1),
            new Glue(5, 0, 0),
            new Box("c", 1),
            new Penalty(0, Penalty.ForcedBreak, false),
        ]);
        var negativeGlueOptions = new LineBreakingOptions(
            7,
            LastLineMode: LastLineMode.Justified);
        var negativeGlueResult = new GreedyLineBreaker().Break(
            negativeGlueParagraph,
            negativeGlueOptions);
        var negativeGlueDocument = XDocument.Parse(
            new LayoutComparisonSvgRenderer().Render(
                negativeGlueOptions,
                [negativeGlueResult]));

        var negativeGlue = Assert.Single(
            negativeGlueDocument.Descendants(Svg + "rect"),
            element => ((string?)element.Attribute("class"))?.Contains(
                "negative-glue", StringComparison.Ordinal) == true);
        Assert.Equal("-1", negativeGlue.Attribute("data-adjusted-width")!.Value);
        Assert.Equal("20", negativeGlue.Attribute("width")!.Value);
        Assert.Equal("170", negativeGlue.Attribute("x")!.Value);
        Assert.Equal(
            "170",
            negativeGlueDocument.Descendants(Svg + "rect")
                .Single(element => element.Attribute("id")!.Value.EndsWith(
                    "-box-2", StringComparison.Ordinal))
                .Attribute("x")!.Value);
    }

    [Fact]
    public void RenderersRejectMismatchedOrInvalidInputs()
    {
        var paragraph = SpecialParagraph();
        var options = new LineBreakingOptions(6, LastLineMode: LastLineMode.Justified);
        var results = BreakBoth(paragraph, options);
        var mismatchedTarget = options with { TargetWidth = 7 };
        var mismatchedCost = options with { LinePenalty = 11 };
        var htmlRenderer = new HtmlComparisonRenderer();
        var layoutRenderer = new LayoutComparisonSvgRenderer();

        Assert.Throws<ArgumentException>(() =>
            htmlRenderer.Render(paragraph, mismatchedTarget, results));
        Assert.Throws<ArgumentException>(() =>
            layoutRenderer.Render(mismatchedTarget, results));
        Assert.Throws<ArgumentException>(() =>
            htmlRenderer.Render(paragraph, mismatchedCost, results));
        Assert.Throws<ArgumentException>(() =>
            layoutRenderer.Render(mismatchedCost, results));
        Assert.Throws<ArgumentException>(() =>
            htmlRenderer.Render(
                new Paragraph(
                [
                    new Box("<>&\"'\u0001\u0416", 2),
                    new Glue(1, 3, 0.5),
                    new Box("tail", 2),
                    new Penalty(0, Penalty.ForcedBreak, false),
                ]),
                options,
                results));
        Assert.Throws<ArgumentException>(() =>
            htmlRenderer.Render(
                new Paragraph(paragraph.Items, hadLineBreaks: true),
                options,
                results));

        var failingOptions = options with { LinePenalty = double.MaxValue };
        var failed = new KnuthPlassLineBreaker().Break(paragraph, failingOptions);
        Assert.False(failed.IsSuccess);
        Assert.Throws<ArgumentException>(() =>
            htmlRenderer.Render(paragraph, options, [failed]));
        Assert.Throws<ArgumentException>(() =>
            layoutRenderer.Render(options, [failed]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            layoutRenderer.Render(options with { Epsilon = 0 }, []));
    }

    [Fact]
    public void GraphRequiresCapturedKnuthPlassTrace()
    {
        var paragraph = SpecialParagraph();
        var options = new LineBreakingOptions(6, LastLineMode: LastLineMode.Justified);
        var untraced = new KnuthPlassLineBreaker().Break(paragraph, options);
        var greedy = new GreedyLineBreaker().Break(
            paragraph,
            options,
            new InMemoryTraceSink());
        var traced = new KnuthPlassLineBreaker().Break(
            paragraph,
            options,
            new InMemoryTraceSink());
        var renderer = new BreakpointGraphSvgRenderer();

        Assert.Throws<InvalidOperationException>(() => renderer.Render(untraced));
        Assert.Throws<ArgumentException>(() => renderer.Render(greedy));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            renderer.Render(traced, new BreakpointGraphRenderOptions(0)));
    }

    private static Paragraph SpecialParagraph() =>
        new(
        [
            new Box("<>&\"'\u0001\u0416", 2),
            new Glue(1, 2, 0.5),
            new Box("tail", 2),
            new Penalty(0, Penalty.ForcedBreak, false),
        ]);

    private static LineBreakResult[] BreakBoth(
        Paragraph paragraph,
        LineBreakingOptions options) =>
    [
        new KnuthPlassLineBreaker().Break(paragraph, options, new InMemoryTraceSink()),
        new GreedyLineBreaker().Break(paragraph, options, new InMemoryTraceSink()),
    ];

    private static void AssertUniqueIds(XDocument document)
    {
        var ids = document.Descendants().Attributes("id")
            .Select(attribute => attribute.Value).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }
}

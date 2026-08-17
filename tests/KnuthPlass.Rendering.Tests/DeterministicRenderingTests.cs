using System.Globalization;
using System.Text.Json;
using KnuthPlass.Core.Breaking;
using KnuthPlass.Core.Model;
using KnuthPlass.Core.Parsing;
using KnuthPlass.Core.Tracing;
using KnuthPlass.Rendering.Json;
using KnuthPlass.Rendering.Text;

namespace KnuthPlass.Rendering.Tests;

public sealed class DeterministicRenderingTests
{
    private const string FlagshipText =
        "Global optimization may accept a slightly looser early line " +
        "when that choice prevents an awkward and expensive line near the end of the paragraph.";

    [Fact]
    public void SummaryJsonIsByteStableAndInvariantCulture()
    {
        var paragraph = new ParagraphTokenizer().Tokenize(FlagshipText);
        var options = new LineBreakingOptions(32);
        var firstResults = new[]
        {
            new KnuthPlassLineBreaker().Break(paragraph, options),
            new GreedyLineBreaker().Break(paragraph, options),
        };
        var secondResults = new[]
        {
            new GreedyLineBreaker().Break(paragraph, options),
            new KnuthPlassLineBreaker().Break(paragraph, options),
        };
        var renderer = new SummaryJsonRenderer();

        var first = renderer.Render(paragraph, options, firstResults);
        var second = renderer.Render(paragraph, options, secondResults);
        var underFrenchCulture = UnderCulture(
            "fr-FR",
            () => renderer.Render(paragraph, options, secondResults));

        Assert.Equal(first, second);
        Assert.Equal(first, underFrenchCulture);
        Assert.EndsWith("\n", first, StringComparison.Ordinal);
        Assert.DoesNotContain("NaN", first, StringComparison.Ordinal);
        Assert.DoesNotContain("Infinity", first, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(first);
        var root = document.RootElement;
        Assert.Equal(
            ["schemaVersion", "input", "options", "algorithms", "comparison", "artifacts"],
            root.EnumerateObject().Select(property => property.Name));
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(paragraph.Words.Length, root.GetProperty("input").GetProperty("wordCount").GetInt32());
        var algorithms = root.GetProperty("algorithms");
        Assert.Equal(2, algorithms.GetArrayLength());
        Assert.Equal(GreedyLineBreaker.Name, algorithms[0].GetProperty("algorithm").GetString());
        Assert.Equal(KnuthPlassLineBreaker.Name, algorithms[1].GetProperty("algorithm").GetString());
        Assert.True(root.GetProperty("comparison").GetProperty("comparable").GetBoolean());
        Assert.Equal(JsonValueKind.Object, root.GetProperty("artifacts").ValueKind);
    }

    [Fact]
    public void TraceTextIsStableAndUsesMonotonicSequencePrefix()
    {
        var paragraph = new ParagraphTokenizer().Tokenize(FlagshipText);
        var options = new LineBreakingOptions(32);
        var sink = new InMemoryTraceSink();
        var result = new KnuthPlassLineBreaker().Break(paragraph, options, sink);
        var trace = Assert.IsType<TraceDocument>(result.Trace);
        var renderer = new TraceTextRenderer();

        var first = renderer.Render(trace);
        var second = UnderCulture(
            "uk-UA",
            () => renderer.Render(trace));

        Assert.Equal(first, second);
        Assert.StartsWith("Options targetWidth=32", first, StringComparison.Ordinal);
        Assert.Contains("Breakpoints count=", first, StringComparison.Ordinal);
        Assert.Contains("[000001] CandidateEvaluated", first, StringComparison.Ordinal);
        Assert.Contains(" target=32 ", first, StringComparison.Ordinal);
        Assert.Contains(" stretch=", first, StringComparison.Ordinal);
        Assert.Contains(" shrink=", first, StringComparison.Ordinal);
        Assert.Contains(" lineDemerits=", first, StringComparison.Ordinal);
        Assert.Contains(" accumulatedCandidateDemerits=", first, StringComparison.Ordinal);
        Assert.Contains(" action=", first, StringComparison.Ordinal);
        Assert.Contains("rejection=OverfullRaggedLastLine", first, StringComparison.Ordinal);
        Assert.Contains("PathReconstructed", first, StringComparison.Ordinal);
    }

    [Fact]
    public void TraceRendererRejectsNonMonotonicInput()
    {
        var events = new[]
        {
            new SequencedTraceEvent(2, new PathReconstructed([0, 1])),
            new SequencedTraceEvent(1, new PathReconstructed([0, 2])),
        };

        var paragraph = new ParagraphTokenizer().Tokenize("a");
        var options = new LineBreakingOptions(1);
        var trace = new TraceDocument(options, paragraph.Breakpoints, [.. events]);

        Assert.Throws<ArgumentException>(
            () => new TraceTextRenderer().Render(trace));
    }

    [Fact]
    public void SummaryJsonRepresentsOverfullFailureAndEscapedText()
    {
        var paragraph = new Paragraph(
        [
            new Box("quoted\"word\\path", 10),
            new Penalty(0, Penalty.ForcedBreak, false),
        ]);
        var options = new LineBreakingOptions(4);
        var renderer = new SummaryJsonRenderer();

        var json = renderer.Render(
            paragraph,
            options,
            [
                new KnuthPlassLineBreaker().Break(paragraph, options),
                new GreedyLineBreaker().Break(paragraph, options),
            ]);

        using var document = JsonDocument.Parse(json);
        var algorithms = document.RootElement.GetProperty("algorithms");
        var greedy = algorithms[0];
        var optimal = algorithms[1];

        Assert.Equal("success", greedy.GetProperty("status").GetString());
        Assert.Equal(
            "quoted\"word\\path",
            greedy.GetProperty("lines")[0].GetProperty("boxes")[0].GetProperty("text").GetString());
        Assert.Equal(
            JsonValueKind.Null,
            greedy.GetProperty("metrics").GetProperty("totalDemerits").ValueKind);
        Assert.Equal("failure", optimal.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, optimal.GetProperty("metrics").ValueKind);
        Assert.False(
            document.RootElement
                .GetProperty("comparison")
                .GetProperty("comparable")
                .GetBoolean());
    }

    [Fact]
    public void SummaryComparisonTreatsTotalsWithinEpsilonAsEqual()
    {
        var paragraph = new ParagraphTokenizer().Tokenize(FlagshipText);
        var options = new LineBreakingOptions(32, Epsilon: 1e100);
        var results = new[]
        {
            new GreedyLineBreaker().Break(paragraph, options),
            new KnuthPlassLineBreaker().Break(paragraph, options),
        };

        var json = new SummaryJsonRenderer().Render(paragraph, options, results);

        using var document = JsonDocument.Parse(json);
        var comparison = document.RootElement.GetProperty("comparison");
        Assert.True(comparison.GetProperty("comparable").GetBoolean());
        Assert.Equal(0, comparison.GetProperty("demeritDifference").GetDouble());
        Assert.Equal(JsonValueKind.Null, comparison.GetProperty("improvementPercent").ValueKind);
    }

    private static T UnderCulture<T>(string cultureName, Func<T> action)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }
}

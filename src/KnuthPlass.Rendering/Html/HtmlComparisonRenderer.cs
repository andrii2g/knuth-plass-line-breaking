using System.Text;
using KnuthPlass.Core.Breaking;
using KnuthPlass.Core.Metrics;
using KnuthPlass.Core.Model;
using KnuthPlass.Core.Results;

namespace KnuthPlass.Rendering.Html;

public sealed class HtmlComparisonRenderer
{
    public string Render(
        Paragraph paragraph,
        LineBreakingOptions options,
        IEnumerable<LineBreakResult> results,
        bool includeTraceLink = false)
    {
        ArgumentNullException.ThrowIfNull(paragraph);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(results);

        var materialized = results.ToArray();
        if (materialized.Any(result => result is null))
        {
            throw new ArgumentException("Results cannot contain null entries.", nameof(results));
        }

        if (materialized
            .GroupBy(result => result.AlgorithmName, StringComparer.Ordinal)
            .Any(group => group.Count() > 1))
        {
            throw new ArgumentException("Algorithm names must be unique.", nameof(results));
        }

        RenderInputValidation.Validate(options, materialized, paragraph);

        var ordered = materialized
            .OrderBy(ResultRank)
            .ThenBy(result => result.AlgorithmName, StringComparer.Ordinal)
            .ToArray();

        var builder = new StringBuilder();
        builder.Append("""
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8"/>
<meta name="viewport" content="width=device-width, initial-scale=1"/>
<title>Knuth-Plass line-breaking comparison</title>
<style>
:root{color-scheme:light;--ink:#172033;--muted:#566278;--panel:#f7f9fc;--line:#c9d2e3;--accent:#174ea6;--good:#176b3a;--warn:#9a3412}
*{box-sizing:border-box}body{margin:0;font:16px/1.5 system-ui,sans-serif;color:var(--ink);background:white}
main{max-width:1180px;margin:auto;padding:1.5rem}.summary,.algorithm,.legend{border:1px solid var(--line);border-radius:.6rem;padding:1rem;background:var(--panel)}
.options,.metrics{display:grid;grid-template-columns:repeat(auto-fit,minmax(12rem,1fr));gap:.4rem 1rem}.options div,.metrics div{display:flex;justify-content:space-between;gap:1rem}
.comparison-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:1rem;margin:1rem 0}.line-card{background:white;border:1px solid var(--line);border-left:.35rem solid var(--accent);padding:.75rem;margin:.75rem 0}.overfull{border-left-color:var(--warn)}
.words{overflow-wrap:anywhere}.path{font-family:ui-monospace,monospace}.status-success{color:var(--good)}.status-failure,.overfull-label{color:var(--warn);font-weight:700}
a{color:var(--accent);text-underline-offset:.18em}a:focus-visible{outline:3px solid #f59e0b;outline-offset:3px}
@media(max-width:760px){.comparison-grid{grid-template-columns:1fr}}
</style>
</head>
<body>
<main>
<header><h1>Knuth-Plass line-breaking comparison</h1>
<p>Deterministic synthetic-width comparison of greedy and globally optimized line breaking.</p></header>
<section class="summary" aria-labelledby="input-heading"><h2 id="input-heading">Input and options</h2>
""");
        builder.Append("<p>Words: ").Append(paragraph.Words.Length)
            .Append("; breakpoints: ").Append(paragraph.Breakpoints.Length)
            .Append("; normalized line breaks: ").Append(paragraph.HadLineBreaks ? "yes" : "no")
            .Append("</p><div class=\"options\">");
        AppendOption(builder, "Target width", options.TargetWidth);
        AppendOption(builder, "Line penalty", options.LinePenalty);
        AppendOption(builder, "Fitness demerit", options.FitnessDemerit);
        AppendOption(builder, "Flagged demerit", options.FlaggedDemerit);
        AppendOption(builder, "Maximum ratio", options.MaxAdjustmentRatio);
        AppendOption(builder, "Epsilon", options.Epsilon);
        builder.Append("<div><span>Last line</span><strong>").Append(options.LastLineMode)
            .Append("</strong></div></div></section>");

        AppendComparison(builder, ordered, options.Epsilon);
        builder.Append("<div class=\"comparison-grid\">");
        foreach (var result in ordered)
        {
            AppendAlgorithm(builder, result);
        }

        builder.Append("</div><section class=\"legend\" aria-labelledby=\"legend-heading\"><h2 id=\"legend-heading\">How to read the metrics</h2>")
            .Append("<p><strong>Badness</strong> is the cubic visual cost of stretching or shrinking one line. ")
            .Append("<strong>Demerits</strong> combine line badness, penalties, fitness transitions, and flagged-break costs across a path.</p>")
            .Append("<p>An overfull line is displayed for greedy termination but has no comparable score.</p></section>")
            .Append("<nav aria-label=\"Generated artifacts\"><h2>Artifacts</h2><ul>")
            .Append("<li><a href=\"layout-comparison.svg\">Scaled line layout SVG</a></li>");
        if (ordered.Any(result => result.AlgorithmName == KnuthPlassLineBreaker.Name))
        {
            builder.Append("<li><a href=\"breakpoint-graph.svg\">Breakpoint graph SVG</a></li>");
        }

        builder.Append("<li><a href=\"summary.json\">Machine-readable JSON summary</a></li>");
        if (includeTraceLink)
        {
            builder.Append("<li><a href=\"trace.txt\">Decision trace</a></li>");
        }

        builder.Append("</ul></nav></main></body></html>\n");
        return builder.ToString();
    }

    private static int ResultRank(LineBreakResult result) =>
        result.AlgorithmName == GreedyLineBreaker.Name ? 0 :
        result.AlgorithmName == KnuthPlassLineBreaker.Name ? 1 : 2;

    private static void AppendOption(StringBuilder builder, string label, double value) =>
        builder.Append("<div><span>").Append(label).Append("</span><strong>")
            .Append(RenderFormatting.Number(value)).Append("</strong></div>");

    private static void AppendComparison(
        StringBuilder builder,
        IReadOnlyList<LineBreakResult> results,
        double epsilon)
    {
        var greedy = results.FirstOrDefault(result => result.AlgorithmName == GreedyLineBreaker.Name);
        var optimal = results.FirstOrDefault(result => result.AlgorithmName == KnuthPlassLineBreaker.Name);
        builder.Append("<section aria-labelledby=\"comparison-heading\"><h2 id=\"comparison-heading\">Summary comparison</h2><p>");

        if (greedy?.TotalDemerits is not { } greedyTotal
            || optimal?.TotalDemerits is not { } optimalTotal)
        {
            builder.Append("The results are not cost-comparable; each status is reported separately.");
        }
        else
        {
            var rawDifference = greedyTotal - optimalTotal;
            var difference = Math.Abs(rawDifference) <= epsilon ? 0 : rawDifference;
            if (difference == 0)
            {
                builder.Append("Totals are equal within epsilon; no improvement is claimed.");
            }
            else if (difference > 0)
            {
                builder.Append("Knuth-Plass reduces total demerits by ")
                    .Append(RenderFormatting.Number(difference));
                if (greedyTotal > 0)
                {
                    builder.Append(" (")
                        .Append(RenderFormatting.Number(difference / greedyTotal * 100))
                        .Append("%)");
                }

                builder.Append('.');
            }
            else
            {
                builder.Append("Greedy has lower total demerits by ")
                    .Append(RenderFormatting.Number(-difference)).Append('.');
            }
        }

        builder.Append("</p></section>");
    }

    private static void AppendAlgorithm(StringBuilder builder, LineBreakResult result)
    {
        var id = result.AlgorithmName == GreedyLineBreaker.Name ? "greedy" :
            result.AlgorithmName == KnuthPlassLineBreaker.Name ? "knuth-plass" : "algorithm";
        builder.Append("<section class=\"algorithm\" aria-labelledby=\"").Append(id)
            .Append("-heading\"><h2 id=\"").Append(id).Append("-heading\">")
            .Append(RenderFormatting.Xml(result.AlgorithmName)).Append("</h2><p class=\"")
            .Append(result.IsSuccess ? "status-success" : "status-failure").Append("\">Status: ")
            .Append(result.IsSuccess ? "success" : "failure").Append("</p>");

        if (!result.IsSuccess)
        {
            builder.Append("<p>Failure: ")
                .Append(RenderFormatting.Xml(result.FailureReason?.ToString() ?? "unknown"))
                .Append("</p></section>");
            return;
        }

        builder.Append("<p class=\"path\">Break path: ")
            .Append(string.Join(" -&gt; ", result.SelectedBreakpointIds.Select(value => $"B{value:D2}")))
            .Append("</p>");
        AppendMetrics(builder, result.Metrics);

        foreach (var line in result.Lines)
        {
            builder.Append("<article class=\"line-card")
                .Append(line.IsOverfull ? " overfull" : string.Empty)
                .Append("\"><h3>Line ").Append(line.LineNumber + 1)
                .Append("</h3><p class=\"words\">")
                .Append(RenderFormatting.Xml(string.Join(' ', line.Boxes.Select(box => box.Text))))
                .Append("</p>");
            if (line.IsOverfull)
            {
                builder.Append("<p class=\"overfull-label\">Overfull and intentionally unscored</p>");
            }

            builder.Append("<dl class=\"metrics\">");
            AppendMetric(builder, "Natural width", RenderFormatting.Number(line.Metrics.NaturalWidth));
            AppendMetric(builder, "Target width", RenderFormatting.Number(line.Metrics.TargetWidth));
            AppendMetric(builder, "Ratio", RenderFormatting.NullableNumber(line.Metrics.AdjustmentRatio));
            AppendMetric(builder, "Badness", RenderFormatting.NullableNumber(line.Metrics.Badness));
            AppendMetric(builder, "Fitness", line.Metrics.Fitness?.ToString() ?? "not available");
            AppendMetric(builder, "Line demerits", RenderFormatting.NullableNumber(line.LineDemerits));
            builder.Append("</dl></article>");
        }

        builder.Append("</section>");
    }

    private static void AppendMetrics(StringBuilder builder, ParagraphMetrics? metrics)
    {
        if (metrics is null)
        {
            return;
        }

        builder.Append("<dl class=\"metrics\">");
        AppendMetric(builder, "Lines", metrics.LineCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendMetric(builder, "Total badness", RenderFormatting.NullableNumber(metrics.TotalBadness));
        AppendMetric(builder, "Total demerits", RenderFormatting.NullableNumber(metrics.TotalDemerits));
        AppendMetric(builder, "Worst badness", RenderFormatting.NullableNumber(metrics.WorstLineBadness));
        AppendMetric(builder, "Mean absolute ratio", RenderFormatting.NullableNumber(metrics.MeanAbsoluteAdjustmentRatio));
        builder.Append("</dl>");
    }

    private static void AppendMetric(StringBuilder builder, string name, string value) =>
        builder.Append("<div><dt>").Append(name).Append("</dt><dd>")
            .Append(RenderFormatting.Xml(value)).Append("</dd></div>");
}

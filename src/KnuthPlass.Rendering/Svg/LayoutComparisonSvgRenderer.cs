using System.Text;
using KnuthPlass.Core.Breaking;
using KnuthPlass.Core.Model;
using KnuthPlass.Core.Results;

namespace KnuthPlass.Rendering.Svg;

public sealed class LayoutComparisonSvgRenderer
{
    private const double ContentLeft = 170;
    private const double MaximumPlotWidth = 900;
    private const double LineHeight = 82;

    public string Render(
        LineBreakingOptions options,
        IEnumerable<LineBreakResult> results)
    {
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

        RenderInputValidation.Validate(options, materialized);

        var ordered = materialized
            .OrderBy(ResultRank)
            .ThenBy(result => result.AlgorithmName, StringComparer.Ordinal)
            .ToArray();

        var lines = ordered.Where(result => result.IsSuccess)
            .SelectMany(result => result.Lines).ToArray();
        if (lines.Any(line => line.LayoutItems.IsDefault))
        {
            throw new InvalidOperationException(
                "Layout rendering requires exact line items captured during reconstruction.");
        }

        var maximumUnits = Math.Max(
            options.TargetWidth,
            lines.Length == 0 ? options.TargetWidth : lines.Max(RenderedWidth));
        var scale = Math.Min(20, MaximumPlotWidth / Math.Max(maximumUnits, options.Epsilon));
        var plotWidth = Math.Max(500, maximumUnits * scale);
        var width = ContentLeft + plotWidth + 50;
        var height = 70 + ordered.Sum(result =>
            result.IsSuccess ? 42 + Math.Max(1, result.Lines.Length) * LineHeight : 82);

        var builder = new StringBuilder();
        builder.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" role=\"img\" aria-labelledby=\"layout-title layout-desc\" viewBox=\"0 0 ")
            .Append(RenderFormatting.Number(width)).Append(' ')
            .Append(RenderFormatting.Number(height)).Append("\">\n")
            .Append("<title id=\"layout-title\">Greedy and Knuth-Plass scaled line layouts</title>\n")
            .Append("<desc id=\"layout-desc\">Boxes use synthetic word widths; blue gaps show adjusted glue; dashed rulers show target width.</desc>\n")
            .Append("""
<defs><pattern id="overfull-hatch" width="8" height="8" patternUnits="userSpaceOnUse"><path d="M0 8L8 0" stroke="#b45309" stroke-width="2"/></pattern></defs>
<style>.heading{font:700 18px system-ui,sans-serif;fill:#172033}.label{font:13px system-ui,sans-serif;fill:#334155}.metric{font:12px ui-monospace,monospace;fill:#475569}.box{fill:#dbeafe;stroke:#1d4ed8}.glue{fill:#bfdbfe;stroke:#60a5fa}.negative-glue{fill:#fed7aa;stroke:#c2410c}.penalty{fill:#fde68a;stroke:#a16207}.ruler{stroke:#64748b;stroke-dasharray:5 4}.overfull-outline{fill:url(#overfull-hatch);stroke:#b45309;stroke-width:2}</style>
""");

        double y = 34;
        for (var algorithmIndex = 0; algorithmIndex < ordered.Length; algorithmIndex++)
        {
            var result = ordered[algorithmIndex];
            builder.Append("<g id=\"algorithm-").Append(algorithmIndex).Append("\">\n")
                .Append("<text class=\"heading\" x=\"20\" y=\"")
                .Append(RenderFormatting.Number(y)).Append("\">")
                .Append(RenderFormatting.Xml(result.AlgorithmName)).Append("</text>\n");
            y += 32;

            if (!result.IsSuccess)
            {
                builder.Append("<text class=\"label\" x=\"20\" y=\"")
                    .Append(RenderFormatting.Number(y)).Append("\">Failure: ")
                    .Append(RenderFormatting.Xml(result.FailureReason?.ToString() ?? "unknown"))
                    .Append("</text>\n</g>\n");
                y += 50;
                continue;
            }

            foreach (var line in result.Lines)
            {
                AppendLine(builder, algorithmIndex, line, options.TargetWidth, scale, y);
                y += LineHeight;
            }

            builder.Append("</g>\n");
            y += 10;
        }

        builder.Append("</svg>\n");
        return builder.ToString();
    }

    private static int ResultRank(LineBreakResult result) =>
        result.AlgorithmName == GreedyLineBreaker.Name ? 0 :
        result.AlgorithmName == KnuthPlassLineBreaker.Name ? 1 : 2;

    private static void AppendLine(
        StringBuilder builder,
        int algorithmIndex,
        BrokenLine line,
        double targetWidth,
        double scale,
        double y)
    {
        var lineId = $"a{algorithmIndex}-line-{line.LineNumber}";
        var boxY = y + 12;
        const double boxHeight = 28;
        var targetX = ContentLeft + targetWidth * scale;
        builder.Append("<g id=\"").Append(lineId).Append("\">\n")
            .Append("<text class=\"label\" x=\"20\" y=\"")
            .Append(RenderFormatting.Number(boxY + 19)).Append("\">Line ")
            .Append(line.LineNumber + 1).Append("</text>\n")
            .Append("<line class=\"ruler\" x1=\"").Append(RenderFormatting.Number(targetX))
            .Append("\" y1=\"").Append(RenderFormatting.Number(boxY - 5))
            .Append("\" x2=\"").Append(RenderFormatting.Number(targetX))
            .Append("\" y2=\"").Append(RenderFormatting.Number(boxY + boxHeight + 5))
            .Append("\"/>\n");

        var x = ContentLeft;
        for (var itemIndex = 0; itemIndex < line.LayoutItems.Length; itemIndex++)
        {
            switch (line.LayoutItems[itemIndex])
            {
                case Box box:
                    var boxWidth = box.Width * scale;
                    builder.Append("<rect id=\"").Append(lineId).Append("-box-").Append(itemIndex)
                        .Append("\" class=\"box\" x=\"").Append(RenderFormatting.Number(x))
                        .Append("\" y=\"").Append(RenderFormatting.Number(boxY))
                        .Append("\" width=\"").Append(RenderFormatting.Number(boxWidth))
                        .Append("\" height=\"").Append(RenderFormatting.Number(boxHeight))
                        .Append("\" data-source-word-index=\"").Append(box.SourceWordIndex)
                        .Append("\" data-width=\"").Append(RenderFormatting.Number(box.Width)).Append("\"/>\n")
                        .Append("<text class=\"label\" x=\"").Append(RenderFormatting.Number(x + 3))
                        .Append("\" y=\"").Append(RenderFormatting.Number(boxY + 19)).Append("\">")
                        .Append(RenderFormatting.Xml(box.Text)).Append("</text>\n");
                    x += boxWidth;
                    break;
                case Glue glue:
                    var adjusted = AdjustedGlueWidth(glue, line.Metrics.AdjustmentRatio);
                    var adjustedPixels = adjusted * scale;
                    var glueX = adjustedPixels < 0 ? x + adjustedPixels : x;
                    builder.Append("<rect id=\"").Append(lineId).Append("-glue-").Append(itemIndex)
                        .Append(adjustedPixels < 0
                            ? "\" class=\"glue negative-glue\" x=\""
                            : "\" class=\"glue\" x=\"")
                        .Append(RenderFormatting.Number(glueX))
                        .Append("\" y=\"").Append(RenderFormatting.Number(boxY + 8))
                        .Append("\" width=\"").Append(RenderFormatting.Number(Math.Abs(adjustedPixels)))
                        .Append("\" height=\"12\" data-natural-width=\"")
                        .Append(RenderFormatting.Number(glue.Width))
                        .Append("\" data-adjusted-width=\"").Append(RenderFormatting.Number(adjusted))
                        .Append("\"/>\n");
                    x += adjustedPixels;
                    break;
            }
        }

        if (line.SelectedPenalty is { Width: > 0 } selectedPenalty)
        {
            var penaltyWidth = selectedPenalty.Width * scale;
            builder.Append("<rect id=\"").Append(lineId).Append("-penalty\" class=\"penalty\" x=\"")
                .Append(RenderFormatting.Number(x)).Append("\" y=\"")
                .Append(RenderFormatting.Number(boxY)).Append("\" width=\"")
                .Append(RenderFormatting.Number(penaltyWidth)).Append("\" height=\"")
                .Append(RenderFormatting.Number(boxHeight)).Append("\"/>\n");
            x += penaltyWidth;
        }

        if (line.IsOverfull)
        {
            builder.Append("<rect class=\"overfull-outline\" x=\"").Append(RenderFormatting.Number(ContentLeft))
                .Append("\" y=\"").Append(RenderFormatting.Number(boxY))
                .Append("\" width=\"").Append(RenderFormatting.Number(Math.Max(0, x - ContentLeft)))
                .Append("\" height=\"").Append(RenderFormatting.Number(boxHeight))
                .Append("\"/><text class=\"label\" x=\"").Append(RenderFormatting.Number(ContentLeft))
                .Append("\" y=\"").Append(RenderFormatting.Number(boxY + 47))
                .Append("\">Overfull - no comparable score</text>\n");
        }

        builder.Append("<text class=\"metric\" x=\"").Append(RenderFormatting.Number(ContentLeft))
            .Append("\" y=\"").Append(RenderFormatting.Number(boxY + (line.IsOverfull ? 64 : 47)))
            .Append("\">ratio=").Append(RenderFormatting.Xml(RenderFormatting.NullableNumber(line.Metrics.AdjustmentRatio)))
            .Append(" badness=").Append(RenderFormatting.Xml(RenderFormatting.NullableNumber(line.Metrics.Badness)))
            .Append(" fitness=").Append(line.Metrics.Fitness?.ToString() ?? "not available")
            .Append("</text>\n</g>\n");
    }

    private static double RenderedWidth(BrokenLine line)
    {
        double total = 0;
        for (var index = 0; index < line.LayoutItems.Length; index++)
        {
            total += line.LayoutItems[index] switch
            {
                Box box => box.Width,
                Glue glue => AdjustedGlueWidth(glue, line.Metrics.AdjustmentRatio),
                _ => 0,
            };
        }

        return total + (line.SelectedPenalty?.Width ?? 0);
    }

    private static double AdjustedGlueWidth(Glue glue, double? ratio) =>
        ratio is not { } value ? glue.Width :
        value >= 0 ? glue.Width + value * glue.Stretch : glue.Width + value * glue.Shrink;
}

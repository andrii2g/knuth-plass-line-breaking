using System.Buffers;
using System.Text;
using System.Text.Json;
using KnuthPlass.Core.Breaking;
using KnuthPlass.Core.Metrics;
using KnuthPlass.Core.Model;
using KnuthPlass.Core.Results;

namespace KnuthPlass.Rendering.Json;

/// <summary>
/// Produces the deterministic, machine-readable comparison summary.
/// </summary>
public sealed class SummaryJsonRenderer
{
    public string Render(
        Paragraph paragraph,
        LineBreakingOptions options,
        IEnumerable<LineBreakResult> results)
    {
        ArgumentNullException.ThrowIfNull(paragraph);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(results);

        var orderedResults = results.ToArray();
        if (orderedResults.Any(result => result is null))
        {
            throw new ArgumentException("Results cannot contain null entries.", nameof(results));
        }

        if (orderedResults
            .GroupBy(result => result.AlgorithmName, StringComparer.Ordinal)
            .Any(group => group.Count() > 1))
        {
            throw new ArgumentException("Algorithm names must be unique.", nameof(results));
        }

        RenderInputValidation.Validate(options, orderedResults, paragraph);

        orderedResults = orderedResults
            .OrderBy(result => result.AlgorithmName == GreedyLineBreaker.Name ? 0
                : result.AlgorithmName == KnuthPlassLineBreaker.Name ? 1 : 2)
            .ThenBy(result => result.AlgorithmName, StringComparer.Ordinal)
            .ToArray();

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(
            buffer,
            new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            WriteInput(writer, paragraph, options);
            WriteOptions(writer, options);

            writer.WriteStartArray("algorithms");
            foreach (var result in orderedResults)
            {
                WriteResult(writer, result);
            }

            writer.WriteEndArray();
            WriteComparison(writer, orderedResults, options.Epsilon);
            writer.WriteStartObject("artifacts");
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan) + "\n";
    }

    private static void WriteInput(
        Utf8JsonWriter writer,
        Paragraph paragraph,
        LineBreakingOptions options)
    {
        writer.WriteStartObject("input");
        WriteNumber(writer, "targetWidth", options.TargetWidth);
        writer.WriteNumber("wordCount", paragraph.Words.Length);
        writer.WriteNumber("breakpointCount", paragraph.Breakpoints.Length);
        writer.WriteBoolean("hadLineBreaks", paragraph.HadLineBreaks);
        writer.WriteEndObject();
    }

    private static void WriteOptions(Utf8JsonWriter writer, LineBreakingOptions options)
    {
        writer.WriteStartObject("options");
        WriteNumber(writer, "linePenalty", options.LinePenalty);
        WriteNumber(writer, "fitnessDemerit", options.FitnessDemerit);
        WriteNumber(writer, "flaggedDemerit", options.FlaggedDemerit);
        WriteNumber(writer, "maxAdjustmentRatio", options.MaxAdjustmentRatio);
        writer.WriteString("lastLineMode", EnumToken(options.LastLineMode));
        WriteNumber(writer, "epsilon", options.Epsilon);
        writer.WriteEndObject();
    }

    private static void WriteResult(Utf8JsonWriter writer, LineBreakResult result)
    {
        writer.WriteStartObject();
        writer.WriteString("algorithm", result.AlgorithmName);
        writer.WriteString("status", result.IsSuccess ? "success" : "failure");

        writer.WriteStartArray("breakPath");
        foreach (var breakpointId in result.SelectedBreakpointIds)
        {
            writer.WriteNumberValue(breakpointId);
        }

        writer.WriteEndArray();

        writer.WriteStartArray("lines");
        foreach (var line in result.Lines)
        {
            WriteLine(writer, line);
        }

        writer.WriteEndArray();
        WriteMetrics(writer, result.Metrics);

        writer.WriteStartObject("counters");
        writer.WriteNumber("evaluatedCandidates", result.EvaluatedCandidates);
        writer.WriteNumber("rejectedCandidates", result.RejectedCandidates);
        writer.WriteNumber("feasibleCandidates", result.FeasibleCandidates);
        writer.WriteEndObject();

        if (result.FailureReason is { } failureReason)
        {
            writer.WriteString("failure", EnumToken(failureReason));
        }
        else
        {
            writer.WriteNull("failure");
        }

        writer.WriteEndObject();
    }

    private static void WriteLine(Utf8JsonWriter writer, BrokenLine line)
    {
        var metrics = line.Metrics;
        writer.WriteStartObject();
        writer.WriteNumber("lineNumber", line.LineNumber);
        writer.WriteNumber("startBreakpointId", metrics.Start.Id);
        writer.WriteNumber("endBreakpointId", metrics.End.Id);
        WriteNumber(writer, "naturalWidth", metrics.NaturalWidth);
        WriteNumber(writer, "targetWidth", metrics.TargetWidth);
        WriteNumber(writer, "stretch", metrics.Stretch);
        WriteNumber(writer, "shrink", metrics.Shrink);
        WriteNullableNumber(writer, "adjustmentRatio", metrics.AdjustmentRatio);
        WriteNullableNumber(writer, "badness", metrics.Badness);

        if (metrics.Fitness is { } fitness)
        {
            writer.WriteString("fitness", EnumToken(fitness));
        }
        else
        {
            writer.WriteNull("fitness");
        }

        writer.WriteNumber("breakPenalty", metrics.BreakPenalty);
        writer.WriteBoolean("flagged", metrics.IsFlagged);
        writer.WriteBoolean("forced", metrics.IsForced);
        writer.WriteBoolean("last", metrics.IsLast);
        writer.WriteBoolean("feasible", metrics.IsFeasible);
        writer.WriteBoolean("overfull", line.IsOverfull);
        WriteNullableNumber(writer, "lineDemerits", line.LineDemerits);
        WriteNullableNumber(writer, "accumulatedDemerits", line.AccumulatedDemerits);

        writer.WriteStartArray("boxes");
        foreach (var box in line.Boxes)
        {
            writer.WriteStartObject();
            writer.WriteNumber("sourceWordIndex", box.SourceWordIndex);
            writer.WriteString("text", box.Text);
            WriteNumber(writer, "width", box.Width);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteMetrics(Utf8JsonWriter writer, ParagraphMetrics? metrics)
    {
        if (metrics is null)
        {
            writer.WriteNull("metrics");
            return;
        }

        writer.WriteStartObject("metrics");
        writer.WriteNumber("lineCount", metrics.LineCount);
        WriteNullableNumber(writer, "totalBadness", metrics.TotalBadness);
        WriteNullableNumber(writer, "totalDemerits", metrics.TotalDemerits);
        WriteNullableNumber(writer, "worstLineBadness", metrics.WorstLineBadness);
        WriteNullableNumber(
            writer,
            "meanAbsoluteAdjustmentRatio",
            metrics.MeanAbsoluteAdjustmentRatio);
        WriteNumber(writer, "maximumStretch", metrics.MaximumStretch);
        WriteNumber(writer, "maximumShrink", metrics.MaximumShrink);
        writer.WriteEndObject();
    }

    private static void WriteComparison(
        Utf8JsonWriter writer,
        IReadOnlyList<LineBreakResult> results,
        double epsilon)
    {
        var greedy = results.FirstOrDefault(result =>
            string.Equals(result.AlgorithmName, GreedyLineBreaker.Name, StringComparison.Ordinal));
        var optimal = results.FirstOrDefault(result =>
            string.Equals(result.AlgorithmName, KnuthPlassLineBreaker.Name, StringComparison.Ordinal));
        var greedyDemerits = greedy?.Metrics?.TotalDemerits;
        var optimalDemerits = optimal?.Metrics?.TotalDemerits;
        var comparable = greedyDemerits.HasValue && optimalDemerits.HasValue;

        writer.WriteStartObject("comparison");
        writer.WriteBoolean("comparable", comparable);

        if (comparable)
        {
            writer.WriteString("baselineAlgorithm", greedy!.AlgorithmName);
            writer.WriteString("optimizedAlgorithm", optimal!.AlgorithmName);
            var rawDifference = greedyDemerits!.Value - optimalDemerits!.Value;
            var difference = Math.Abs(rawDifference) <= epsilon ? 0 : rawDifference;
            WriteNumber(writer, "demeritDifference", difference);
            if (difference != 0 && greedyDemerits.Value > 0)
            {
                WriteNumber(writer, "improvementPercent", difference / greedyDemerits.Value * 100);
            }
            else
            {
                writer.WriteNull("improvementPercent");
            }
        }
        else
        {
            writer.WriteNull("baselineAlgorithm");
            writer.WriteNull("optimizedAlgorithm");
            writer.WriteNull("demeritDifference");
            writer.WriteNull("improvementPercent");
        }

        writer.WriteEndObject();
    }

    private static void WriteNullableNumber(
        Utf8JsonWriter writer,
        string propertyName,
        double? value)
    {
        if (value is { } number)
        {
            WriteNumber(writer, propertyName, number);
        }
        else
        {
            writer.WriteNull(propertyName);
        }
    }

    private static void WriteNumber(
        Utf8JsonWriter writer,
        string propertyName,
        double value)
    {
        if (!double.IsFinite(value))
        {
            throw new InvalidOperationException(
                $"JSON numeric property '{propertyName}' must be finite.");
        }

        writer.WriteNumber(propertyName, value == 0 ? 0 : value);
    }

    private static string EnumToken<T>(T value)
        where T : struct, Enum
    {
        var name = value.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}

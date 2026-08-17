using System.Collections.Immutable;
using KnuthPlass.Core.Metrics;
using KnuthPlass.Core.Model;
using KnuthPlass.Core.Results;
using KnuthPlass.Core.Tracing;

namespace KnuthPlass.Core.Breaking;

/// <summary>
/// Selects the farthest feasible next breakpoint, honoring forced breaks.
/// </summary>
public sealed class GreedyLineBreaker : ILineBreaker
{
    public const string Name = "Greedy";

    public LineBreakResult Break(
        Paragraph paragraph,
        LineBreakingOptions options,
        ITraceSink? trace = null)
    {
        ArgumentNullException.ThrowIfNull(paragraph);

        if (!TryValidateOptions(options))
        {
            return LineBreakResult.Failed(Name, FailureReason.InvalidOptions);
        }

        var measurement = new LineMeasurement(paragraph);
        var lines = ImmutableArray.CreateBuilder<BrokenLine>();
        var selectedBreakpoints = ImmutableArray.CreateBuilder<int>();
        selectedBreakpoints.Add(paragraph.Start.Id);

        var current = paragraph.Start;
        FitnessClass? previousFitness = null;
        var previousBreakWasFlagged = false;
        var accumulatedDemerits = 0d;
        var hasOverfullLine = false;
        var evaluatedCandidates = 0;
        var rejectedCandidates = 0;
        var feasibleCandidates = 0;

        while (current != paragraph.End)
        {
            LineMetrics? farthestFeasible = null;
            LineMetrics? earliestOverflow = null;

            for (var endId = current.Id + 1; endId < paragraph.Breakpoints.Length; endId++)
            {
                var end = paragraph.Breakpoints[endId];
                var metrics = measurement.Measure(current, end, options);
                var candidate = new CandidateLine(
                    metrics,
                    previousFitness,
                    previousBreakWasFlagged);

                evaluatedCandidates++;
                trace?.Write(new CandidateEvaluated(candidate));

                if (metrics.IsFeasible)
                {
                    feasibleCandidates++;
                    farthestFeasible = metrics;
                }
                else
                {
                    rejectedCandidates++;
                    trace?.Write(new CandidateRejected(candidate));

                    if (earliestOverflow is null &&
                        metrics.NaturalWidth > options.TargetWidth + options.Epsilon &&
                        ContainsBox(paragraph, metrics))
                    {
                        earliestOverflow = metrics;
                    }
                }

                if (end.IsForced)
                {
                    break;
                }
            }

            var isOverfull = farthestFeasible is null;
            var selected = farthestFeasible ?? earliestOverflow;
            if (selected is null)
            {
                return LineBreakResult.Failed(
                    Name,
                    FailureReason.NoFeasibleLayout,
                    evaluatedCandidates,
                    rejectedCandidates,
                    feasibleCandidates);
            }

            selected = measurement.Measure(current, selected.End, options);
            double? lineDemerits = null;
            double? lineAccumulatedDemerits = null;

            if (isOverfull)
            {
                hasOverfullLine = true;
                previousFitness = null;
            }
            else
            {
                if (!LineDemeritCalculator.TryCalculate(
                        selected,
                        previousFitness,
                        previousBreakWasFlagged,
                        options,
                        out var calculatedDemerits))
                {
                    return LineBreakResult.Failed(
                        Name,
                        FailureReason.NonFiniteDemerits,
                        evaluatedCandidates,
                        rejectedCandidates,
                        feasibleCandidates);
                }

                lineDemerits = calculatedDemerits;
                previousFitness = selected.Fitness;

                if (!hasOverfullLine)
                {
                    accumulatedDemerits += calculatedDemerits;
                    if (!double.IsFinite(accumulatedDemerits))
                    {
                        return LineBreakResult.Failed(
                            Name,
                            FailureReason.NonFiniteDemerits,
                            evaluatedCandidates,
                            rejectedCandidates,
                            feasibleCandidates);
                    }

                    lineAccumulatedDemerits = accumulatedDemerits;
                }
            }

            previousBreakWasFlagged = selected.IsFlagged;
            selectedBreakpoints.Add(selected.End.Id);
            lines.Add(new BrokenLine(
                lines.Count,
                selected,
                GetBoxes(paragraph, selected),
                lineDemerits,
                lineAccumulatedDemerits,
                isOverfull));
            current = selected.End;
        }

        return LineBreakResult.Succeeded(
            Name,
            lines.ToImmutable(),
            selectedBreakpoints.ToImmutable(),
            hasOverfullLine ? null : accumulatedDemerits,
            evaluatedCandidates,
            rejectedCandidates,
            feasibleCandidates);
    }

    private static bool TryValidateOptions(LineBreakingOptions? options)
    {
        if (options is null)
        {
            return false;
        }

        try
        {
            LineMeasurement.ValidateOptions(options);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool ContainsBox(Paragraph paragraph, LineMetrics metrics)
    {
        for (var index = metrics.StartItemIndex;
             index < metrics.EndItemIndexExclusive;
             index++)
        {
            if (paragraph.Items[index] is Box)
            {
                return true;
            }
        }

        return false;
    }

    private static ImmutableArray<Box> GetBoxes(
        Paragraph paragraph,
        LineMetrics metrics)
    {
        var boxes = ImmutableArray.CreateBuilder<Box>();
        for (var index = metrics.StartItemIndex;
             index < metrics.EndItemIndexExclusive;
             index++)
        {
            if (paragraph.Items[index] is Box box)
            {
                boxes.Add(box);
            }
        }

        return boxes.ToImmutable();
    }
}

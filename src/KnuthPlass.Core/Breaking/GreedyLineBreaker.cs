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

        var resultTrace = trace is null ? null : new ResultTraceSink(trace);
        trace = resultTrace;

        if (!TryValidateOptions(options))
        {
            return LineBreakResult.Failed(
                Name,
                FailureReason.InvalidOptions,
                trace: resultTrace?.CreateDocument(paragraph, options));
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
                    previousBreakWasFlagged,
                    lines.Count);

                evaluatedCandidates++;

                double? traceLineDemerits = null;
                double? traceAccumulatedDemerits = null;
                if (metrics.IsFeasible
                    && LineDemeritCalculator.TryCalculate(
                        metrics,
                        previousFitness,
                        previousBreakWasFlagged,
                        options,
                        out var calculatedTraceDemerits))
                {
                    traceLineDemerits = calculatedTraceDemerits;
                    var candidateTotal = accumulatedDemerits + calculatedTraceDemerits;
                    if (!hasOverfullLine && double.IsFinite(candidateTotal))
                    {
                        traceAccumulatedDemerits = candidateTotal;
                    }
                }

                trace?.Write(new CandidateEvaluated(
                    candidate,
                    traceLineDemerits,
                    traceAccumulatedDemerits));

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
                    feasibleCandidates,
                    resultTrace?.CreateDocument(paragraph, options));
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
                var selectedCandidate = new CandidateLine(
                    selected,
                    previousFitness,
                    previousBreakWasFlagged,
                    lines.Count);
                if (!LineDemeritCalculator.TryCalculate(
                        selected,
                        previousFitness,
                        previousBreakWasFlagged,
                        options,
                        out var calculatedDemerits))
                {
                    feasibleCandidates--;
                    rejectedCandidates++;
                    trace?.Write(new CandidateRejected(
                        selectedCandidate,
                        CandidateRejectionKind.NonFiniteDemerits));
                    return LineBreakResult.Failed(
                        Name,
                        FailureReason.NonFiniteDemerits,
                        evaluatedCandidates,
                        rejectedCandidates,
                        feasibleCandidates,
                        resultTrace?.CreateDocument(paragraph, options));
                }

                lineDemerits = calculatedDemerits;

                if (!hasOverfullLine)
                {
                    accumulatedDemerits += calculatedDemerits;
                    if (!double.IsFinite(accumulatedDemerits))
                    {
                        feasibleCandidates--;
                        rejectedCandidates++;
                        trace?.Write(new CandidateRejected(
                            selectedCandidate,
                            CandidateRejectionKind.NonFiniteDemerits));
                        return LineBreakResult.Failed(
                            Name,
                            FailureReason.NonFiniteDemerits,
                            evaluatedCandidates,
                            rejectedCandidates,
                            feasibleCandidates,
                            resultTrace?.CreateDocument(paragraph, options));
                    }

                    lineAccumulatedDemerits = accumulatedDemerits;
                }

                previousFitness = selected.Fitness;
            }

            previousBreakWasFlagged = selected.IsFlagged;
            selectedBreakpoints.Add(selected.End.Id);
            lines.Add(BrokenLineFactory.Create(
                lines.Count,
                paragraph,
                selected,
                lineDemerits,
                lineAccumulatedDemerits,
                isOverfull));
            current = selected.End;
        }

        return LineBreakResult.Succeeded(
            Name,
            lines.ToImmutable(),
            selectedBreakpoints.ToImmutable(),
            evaluatedCandidates,
            rejectedCandidates,
            feasibleCandidates,
            resultTrace?.CreateDocument(paragraph, options));
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

}

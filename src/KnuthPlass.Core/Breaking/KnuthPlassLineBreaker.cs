using System.Collections.Immutable;
using KnuthPlass.Core.Metrics;
using KnuthPlass.Core.Model;
using KnuthPlass.Core.Results;
using KnuthPlass.Core.Tracing;

namespace KnuthPlass.Core.Breaking;

/// <summary>
/// Finds the minimum-demerit complete layout while retaining fitness context.
/// </summary>
public sealed class KnuthPlassLineBreaker : ILineBreaker
{
    public const string Name = "Knuth-Plass";
    private const int FitnessClassCount = 4;

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
                paragraph,
                options,
                trace: resultTrace?.CreateDocument(paragraph, options));
        }

        var measurement = new LineMeasurement(paragraph);
        var states = new ActiveNode?[paragraph.Breakpoints.Length, FitnessClassCount];
        var startState = new ActiveNode(
            paragraph.Start,
            FitnessClass.Tight,
            0,
            0,
            null,
            null,
            0);
        states[paragraph.Start.Id, (int)FitnessClass.Tight] = startState;

        var evaluatedCandidates = 0;
        var rejectedCandidates = 0;
        var feasibleCandidates = 0;
        var encounteredNonFiniteDemerits = false;

        var activeStartId = paragraph.Start.Id;
        for (var endId = 1; endId < paragraph.Breakpoints.Length; endId++)
        {
            var end = paragraph.Breakpoints[endId];

            for (var startId = activeStartId; startId < endId; startId++)
            {
                for (var fitnessIndex = 0;
                     fitnessIndex < FitnessClassCount;
                     fitnessIndex++)
                {
                    var predecessor = states[startId, fitnessIndex];
                    if (predecessor is null)
                    {
                        continue;
                    }

                    FitnessClass? previousFitness = predecessor.LineCount == 0
                        ? null
                        : predecessor.Fitness;
                    var previousFlagged = predecessor.LineCount > 0 &&
                        predecessor.Breakpoint.IsFlagged;
                    var metrics = measurement.Measure(
                        predecessor.Breakpoint,
                        end,
                        options);
                    var candidate = new CandidateLine(
                        metrics,
                        previousFitness,
                        previousFlagged,
                        predecessor.LineCount);

                    evaluatedCandidates++;

                    if (!metrics.IsFeasible)
                    {
                        trace?.Write(new CandidateEvaluated(candidate));
                        rejectedCandidates++;
                        trace?.Write(new CandidateRejected(
                            candidate,
                            CandidateRejectionKind.Measurement));
                        continue;
                    }

                    if (!LineDemeritCalculator.TryCalculate(
                            metrics,
                            previousFitness,
                            previousFlagged,
                            options,
                            out var lineDemerits))
                    {
                        trace?.Write(new CandidateEvaluated(candidate));
                        encounteredNonFiniteDemerits = true;
                        rejectedCandidates++;
                        trace?.Write(new CandidateRejected(
                            candidate,
                            CandidateRejectionKind.NonFiniteDemerits));
                        continue;
                    }

                    var totalDemerits = predecessor.TotalDemerits + lineDemerits;
                    if (!double.IsFinite(totalDemerits))
                    {
                        trace?.Write(new CandidateEvaluated(candidate, lineDemerits));
                        encounteredNonFiniteDemerits = true;
                        rejectedCandidates++;
                        trace?.Write(new CandidateRejected(
                            candidate,
                            CandidateRejectionKind.NonFiniteDemerits));
                        continue;
                    }
                    trace?.Write(new CandidateEvaluated(
                        candidate,
                        lineDemerits,
                        totalDemerits));
                    feasibleCandidates++;

                    var fitness = metrics.Fitness!.Value;
                    var next = new ActiveNode(
                        end,
                        fitness,
                        totalDemerits,
                        predecessor.LineCount + 1,
                        predecessor,
                        metrics,
                        lineDemerits);
                    var incumbent = states[endId, (int)fitness];

                    if (incumbent is null ||
                        ActiveNodeComparer.IsBetter(next, incumbent, options.Epsilon))
                    {
                        states[endId, (int)fitness] = next;
                        trace?.Write(new StateUpdated(
                            candidate,
                            totalDemerits,
                            next.LineCount,
                            lineDemerits));
                    }
                    else
                    {
                        trace?.Write(new StateRetained(
                            candidate,
                            totalDemerits,
                            incumbent.TotalDemerits,
                            incumbent.LineCount,
                            lineDemerits));
                    }
                }
            }

            if (end.IsForced)
            {
                activeStartId = endId;
            }
        }

        var final = SelectFinalState(states, paragraph.End.Id, options.Epsilon);
        if (final is null)
        {
            return LineBreakResult.Failed(
                Name,
                encounteredNonFiniteDemerits
                    ? FailureReason.NonFiniteDemerits
                    : FailureReason.NoFeasibleLayout,
                paragraph,
                options,
                evaluatedCandidates,
                rejectedCandidates,
                feasibleCandidates,
                resultTrace?.CreateDocument(paragraph, options));
        }

        trace?.Write(new FinalStateSelected(
            final.Breakpoint.Id,
            final.Fitness,
            final.TotalDemerits,
            final.LineCount));

        if (!TryReconstruct(
                paragraph,
                startState,
                final,
                out var lines,
                out var selectedBreakpointIds))
        {
            return LineBreakResult.Failed(
                Name,
                FailureReason.InvalidReconstruction,
                paragraph,
                options,
                evaluatedCandidates,
                rejectedCandidates,
                feasibleCandidates,
                resultTrace?.CreateDocument(paragraph, options));
        }

        trace?.Write(new PathReconstructed(selectedBreakpointIds));
        return LineBreakResult.Succeeded(
            Name,
            lines,
            selectedBreakpointIds,
            evaluatedCandidates,
            rejectedCandidates,
            feasibleCandidates,
            paragraph,
            options,
            resultTrace?.CreateDocument(paragraph, options));
    }

    private static ActiveNode? SelectFinalState(
        ActiveNode?[,] states,
        int finalBreakpointId,
        double epsilon)
    {
        ActiveNode? selected = null;
        for (var fitnessIndex = 0;
             fitnessIndex < FitnessClassCount;
             fitnessIndex++)
        {
            var candidate = states[finalBreakpointId, fitnessIndex];
            if (candidate is not null &&
                (selected is null ||
                 ActiveNodeComparer.IsBetter(candidate, selected, epsilon)))
            {
                selected = candidate;
            }
        }

        return selected;
    }

    private static bool TryReconstruct(
        Paragraph paragraph,
        ActiveNode start,
        ActiveNode final,
        out ImmutableArray<BrokenLine> lines,
        out ImmutableArray<int> selectedBreakpointIds)
    {
        var reversed = new List<ActiveNode>();
        var cursor = final;

        while (cursor.Predecessor is not null)
        {
            if (cursor.SelectedLine is null)
            {
                lines = [];
                selectedBreakpointIds = [];
                return false;
            }

            reversed.Add(cursor);
            cursor = cursor.Predecessor;
        }

        if (!ReferenceEquals(cursor, start))
        {
            lines = [];
            selectedBreakpointIds = [];
            return false;
        }

        reversed.Reverse();
        var lineBuilder = ImmutableArray.CreateBuilder<BrokenLine>(reversed.Count);
        var breakpointBuilder = ImmutableArray.CreateBuilder<int>(reversed.Count + 1);
        breakpointBuilder.Add(paragraph.Start.Id);

        for (var index = 0; index < reversed.Count; index++)
        {
            var node = reversed[index];
            var metrics = node.SelectedLine!;
            lineBuilder.Add(BrokenLineFactory.Create(
                index,
                paragraph,
                metrics,
                node.LineDemerits,
                node.TotalDemerits,
                false));
            breakpointBuilder.Add(node.Breakpoint.Id);
        }

        lines = lineBuilder.MoveToImmutable();
        selectedBreakpointIds = breakpointBuilder.MoveToImmutable();
        return IsCompleteReconstruction(paragraph, lines, selectedBreakpointIds);
    }

    private static bool IsCompleteReconstruction(
        Paragraph paragraph,
        ImmutableArray<BrokenLine> lines,
        ImmutableArray<int> selectedBreakpointIds)
    {
        if (selectedBreakpointIds.Length != lines.Length + 1 ||
            selectedBreakpointIds.IsEmpty ||
            selectedBreakpointIds[0] != paragraph.Start.Id ||
            selectedBreakpointIds[^1] != paragraph.End.Id)
        {
            return false;
        }

        for (var index = 1; index < selectedBreakpointIds.Length; index++)
        {
            if (selectedBreakpointIds[index - 1] >= selectedBreakpointIds[index])
            {
                return false;
            }
        }

        var sourceIndices = lines
            .SelectMany(line => line.Boxes)
            .Select(box => box.SourceWordIndex)
            .ToArray();
        return sourceIndices.SequenceEqual(Enumerable.Range(0, paragraph.Words.Length));
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
}

using KnuthPlass.Core.Breaking;
using KnuthPlass.Core.Model;

namespace KnuthPlass.Core.Tests.Breaking;

internal static class ExhaustiveLineBreakOracle
{
    public static OracleResult Solve(
        Paragraph paragraph,
        LineBreakingOptions options)
    {
        var completed = EnumerateAll(paragraph, options);
        OracleResult? selected = null;
        foreach (var candidate in completed)
        {
            if (selected is null || IsBetter(candidate, selected, options.Epsilon))
            {
                selected = candidate;
            }
        }

        return selected ?? OracleResult.Failed;
    }

    public static IReadOnlyList<OracleResult> EnumerateAll(
        Paragraph paragraph,
        LineBreakingOptions options)
    {
        var completed = new List<OracleResult>();
        Search(
            paragraph,
            options,
            paragraph.Start,
            null,
            false,
            0,
            [paragraph.Start.Id],
            [],
            completed);
        return completed;
    }

    public static OracleResult SolveWithCollapsedFitness(
        Paragraph paragraph,
        LineBreakingOptions options)
    {
        var states = new OracleState?[paragraph.Breakpoints.Length];
        states[paragraph.Start.Id] = new OracleState(
            paragraph.Start,
            FitnessClass.Tight,
            0,
            0,
            false,
            [paragraph.Start.Id],
            [],
            -1);

        for (var endId = 1; endId < paragraph.Breakpoints.Length; endId++)
        {
            OracleState? retained = null;
            for (var startId = 0; startId < endId; startId++)
            {
                var predecessor = states[startId];
                if (predecessor is null)
                {
                    continue;
                }

                var line = Measure(
                    paragraph,
                    predecessor.Breakpoint,
                    paragraph.Breakpoints[endId],
                    options);
                if (line is null)
                {
                    continue;
                }

                FitnessClass? previousFitness = predecessor.LineCount == 0
                    ? null
                    : predecessor.Fitness;
                var lineDemerits = CalculateDemerits(
                    line,
                    previousFitness,
                    predecessor.LineCount > 0 && predecessor.Flagged,
                    options);
                var totalDemerits = predecessor.TotalDemerits + lineDemerits;
                if (!double.IsFinite(totalDemerits))
                {
                    continue;
                }

                var candidate = new OracleState(
                    paragraph.Breakpoints[endId],
                    line.Fitness,
                    totalDemerits,
                    predecessor.LineCount + 1,
                    line.Flagged,
                    [.. predecessor.BreakpointIds, endId],
                    [.. predecessor.Fitnesses, line.Fitness],
                    startId);

                if (retained is null ||
                    IsBetterCollapsed(candidate, retained, options.Epsilon))
                {
                    retained = candidate;
                }
            }

            states[endId] = retained;
        }

        var final = states[paragraph.End.Id];
        return final is null
            ? OracleResult.Failed
            : new OracleResult(
                true,
                final.TotalDemerits,
                final.BreakpointIds,
                final.Fitnesses);
    }

    private static bool IsBetterCollapsed(
        OracleState candidate,
        OracleState incumbent,
        double epsilon)
    {
        if (candidate.TotalDemerits < incumbent.TotalDemerits - epsilon)
        {
            return true;
        }

        if (candidate.TotalDemerits > incumbent.TotalDemerits + epsilon)
        {
            return false;
        }

        if (candidate.LineCount != incumbent.LineCount)
        {
            return candidate.LineCount < incumbent.LineCount;
        }

        if (candidate.PredecessorId != incumbent.PredecessorId)
        {
            return candidate.PredecessorId < incumbent.PredecessorId;
        }

        return candidate.Fitness < incumbent.Fitness;
    }

    private static void Search(
        Paragraph paragraph,
        LineBreakingOptions options,
        Breakpoint start,
        FitnessClass? previousFitness,
        bool previousFlagged,
        double totalDemerits,
        List<int> breakpointIds,
        List<FitnessClass> fitnesses,
        List<OracleResult> completed)
    {
        if (start == paragraph.End)
        {
            completed.Add(new OracleResult(
                true,
                totalDemerits,
                [.. breakpointIds],
                [.. fitnesses]));
            return;
        }

        for (var endId = start.Id + 1;
             endId < paragraph.Breakpoints.Length;
             endId++)
        {
            var end = paragraph.Breakpoints[endId];
            var line = Measure(paragraph, start, end, options);
            if (line is not null)
            {
                var lineDemerits = CalculateDemerits(
                    line,
                    previousFitness,
                    previousFlagged,
                    options);
                var nextTotal = totalDemerits + lineDemerits;

                if (double.IsFinite(lineDemerits) && double.IsFinite(nextTotal))
                {
                    breakpointIds.Add(end.Id);
                    fitnesses.Add(line.Fitness);
                    Search(
                        paragraph,
                        options,
                        end,
                        line.Fitness,
                        end.IsFlagged,
                        nextTotal,
                        breakpointIds,
                        fitnesses,
                        completed);
                    fitnesses.RemoveAt(fitnesses.Count - 1);
                    breakpointIds.RemoveAt(breakpointIds.Count - 1);
                }
            }

            if (end.IsForced)
            {
                break;
            }
        }
    }

    private static OracleLine? Measure(
        Paragraph paragraph,
        Breakpoint start,
        Breakpoint end,
        LineBreakingOptions options)
    {
        for (var index = start.ItemIndex + 1; index < end.ItemIndex; index++)
        {
            if (paragraph.Items[index] is Penalty { IsForced: true })
            {
                return null;
            }
        }

        var startIndex = start.ItemIndex + 1;
        while (startIndex < paragraph.Items.Length &&
               paragraph.Items[startIndex] is Glue)
        {
            startIndex++;
        }

        var endExclusive = end.ItemIndex;
        while (endExclusive > startIndex &&
               paragraph.Items[endExclusive - 1] is Glue)
        {
            endExclusive--;
        }

        var naturalWidth = 0d;
        var stretch = 0d;
        var shrink = 0d;
        for (var index = startIndex; index < endExclusive; index++)
        {
            switch (paragraph.Items[index])
            {
                case Box box:
                    naturalWidth += box.Width;
                    break;
                case Glue glue:
                    naturalWidth += glue.Width;
                    stretch += glue.Stretch;
                    shrink += glue.Shrink;
                    break;
            }
        }

        var penalty = paragraph.Items[end.ItemIndex] as Penalty;
        naturalWidth += penalty?.Width ?? 0;

        if (!double.IsFinite(naturalWidth) ||
            !double.IsFinite(stretch) ||
            !double.IsFinite(shrink))
        {
            return null;
        }

        double ratio;
        if (end == paragraph.End && options.LastLineMode == LastLineMode.Ragged)
        {
            if (naturalWidth > options.TargetWidth + options.Epsilon)
            {
                return null;
            }

            ratio = 0;
        }
        else
        {
            var difference = options.TargetWidth - naturalWidth;
            if (Math.Abs(difference) <= options.Epsilon)
            {
                ratio = 0;
            }
            else if (difference > 0)
            {
                if (stretch == 0)
                {
                    return null;
                }

                ratio = difference / stretch;
            }
            else
            {
                if (shrink == 0)
                {
                    return null;
                }

                ratio = difference / shrink;
            }

            if (!double.IsFinite(ratio) ||
                ratio < -1 - options.Epsilon ||
                ratio > options.MaxAdjustmentRatio + options.Epsilon)
            {
                return null;
            }
        }

        var badness = Math.Min(10000, 100 * Math.Pow(Math.Abs(ratio), 3));
        var fitness = ratio switch
        {
            < -0.5 => FitnessClass.VeryTight,
            <= 0.5 => FitnessClass.Tight,
            <= 1 => FitnessClass.Loose,
            _ => FitnessClass.VeryLoose,
        };

        return new OracleLine(
            badness,
            fitness,
            penalty?.Value ?? 0,
            end.IsFlagged);
    }

    private static double CalculateDemerits(
        OracleLine line,
        FitnessClass? previousFitness,
        bool previousFlagged,
        LineBreakingOptions options)
    {
        var baseAmount = options.LinePenalty + line.Badness;
        var result = baseAmount * baseAmount;
        var penaltyMagnitude = (double)line.Penalty * line.Penalty;

        if (line.Penalty >= 0 && line.Penalty < Penalty.ForbiddenBreak)
        {
            result += penaltyMagnitude;
        }
        else if (line.Penalty < 0 && line.Penalty > Penalty.ForcedBreak)
        {
            result -= penaltyMagnitude;
        }

        result = Math.Max(0, result);
        if (previousFitness is FitnessClass prior &&
            Math.Abs((int)prior - (int)line.Fitness) > 1)
        {
            result += options.FitnessDemerit;
        }

        if (previousFlagged && line.Flagged)
        {
            result += options.FlaggedDemerit;
        }

        return result;
    }

    private static bool IsBetter(
        OracleResult candidate,
        OracleResult incumbent,
        double epsilon)
    {
        if (candidate.TotalDemerits < incumbent.TotalDemerits - epsilon)
        {
            return true;
        }

        if (candidate.TotalDemerits > incumbent.TotalDemerits + epsilon)
        {
            return false;
        }

        if (candidate.Fitnesses.Count != incumbent.Fitnesses.Count)

        {
            return candidate.Fitnesses.Count < incumbent.Fitnesses.Count;
        }

        var candidatePredecessor = candidate.BreakpointIds[^2];
        var incumbentPredecessor = incumbent.BreakpointIds[^2];
        if (candidatePredecessor != incumbentPredecessor)
        {
            return candidatePredecessor < incumbentPredecessor;
        }

        if (candidate.Fitnesses[^1] != incumbent.Fitnesses[^1])
        {
            return candidate.Fitnesses[^1] < incumbent.Fitnesses[^1];
        }

        for (var index = candidate.Fitnesses.Count - 2; index >= 0; index--)
        {
            if (candidate.Fitnesses[index] != incumbent.Fitnesses[index])
            {
                return candidate.Fitnesses[index] < incumbent.Fitnesses[index];
            }

            if (candidate.BreakpointIds[index] != incumbent.BreakpointIds[index])
            {
                return candidate.BreakpointIds[index] < incumbent.BreakpointIds[index];
            }
        }

        return false;
    }

    private sealed record OracleState(
        Breakpoint Breakpoint,
        FitnessClass Fitness,
        double TotalDemerits,
        int LineCount,
        bool Flagged,
        IReadOnlyList<int> BreakpointIds,
        IReadOnlyList<FitnessClass> Fitnesses,
        int PredecessorId);

    private sealed record OracleLine(
        double Badness,
        FitnessClass Fitness,
        int Penalty,
        bool Flagged);
}

internal sealed record OracleResult(
    bool IsSuccess,
    double TotalDemerits,
    IReadOnlyList<int> BreakpointIds,
    IReadOnlyList<FitnessClass> Fitnesses)
{
    public static OracleResult Failed { get; } =
        new(false, 0, [], []);
}

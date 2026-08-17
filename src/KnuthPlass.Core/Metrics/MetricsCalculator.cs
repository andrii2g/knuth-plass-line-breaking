using KnuthPlass.Core.Results;

namespace KnuthPlass.Core.Metrics;

/// <summary>
/// Derives paragraph metrics exclusively from reconstructed selected lines.
/// </summary>
public static class MetricsCalculator
{
    public static ParagraphMetrics Calculate(LineBreakResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException("Metrics cannot be calculated for a failed layout.");
        }

        if (result.Lines.IsEmpty)
        {
            throw new InvalidOperationException("A successful layout must contain at least one line.");
        }

        if (result.SelectedBreakpointIds.Length != result.Lines.Length + 1)
        {
            throw new InvalidOperationException("The selected break path must bound every reconstructed line.");
        }

        double totalBadness = 0;
        double totalDemerits = 0;
        double worstLineBadness = 0;
        double totalAbsoluteRatio = 0;
        double maximumStretch = 0;
        double maximumShrink = 0;
        var hasCompleteScores = true;

        foreach (var line in result.Lines)
        {
            maximumStretch = Math.Max(maximumStretch, RequireFiniteNonNegative(line.Metrics.Stretch, "stretch"));
            maximumShrink = Math.Max(maximumShrink, RequireFiniteNonNegative(line.Metrics.Shrink, "shrink"));

            if (line.IsOverfull)
            {
                if (line.Metrics.IsFeasible
                    || line.Metrics.Badness is not null
                    || line.Metrics.AdjustmentRatio is not null
                    || line.Metrics.Fitness is not null
                    || line.LineDemerits is not null
                    || line.AccumulatedDemerits is not null)
                {
                    throw new InvalidOperationException(
                        "An overfull reconstructed line cannot contain feasible scores.");
                }

                hasCompleteScores = false;
                continue;
            }

            if (!line.Metrics.IsFeasible
                || line.Metrics.Badness is not { } badness
                || line.Metrics.AdjustmentRatio is not { } ratio
                || line.Metrics.Fitness is null
                || line.LineDemerits is not { } demerits
                || line.AccumulatedDemerits is null)
            {
                throw new InvalidOperationException(
                    "A non-overfull reconstructed line must contain complete feasible scores.");
            }

            badness = RequireFiniteNonNegative(badness, "badness");
            demerits = RequireFiniteNonNegative(demerits, "line demerits");
            if (!double.IsFinite(ratio))
            {
                throw new InvalidOperationException("A reconstructed adjustment ratio must be finite.");
            }

            totalBadness += badness;
            totalDemerits += demerits;
            worstLineBadness = Math.Max(worstLineBadness, badness);
            totalAbsoluteRatio += Math.Abs(ratio);
        }

        if (hasCompleteScores
            && (!double.IsFinite(totalBadness)
                || !double.IsFinite(totalDemerits)
                || !double.IsFinite(totalAbsoluteRatio)))
        {
            throw new InvalidOperationException("Aggregate paragraph metrics must be finite.");
        }

        return new ParagraphMetrics(
            result.Lines.Length,
            hasCompleteScores ? totalBadness : null,
            hasCompleteScores ? totalDemerits : null,
            hasCompleteScores ? worstLineBadness : null,
            hasCompleteScores ? totalAbsoluteRatio / result.Lines.Length : null,
            maximumStretch,
            maximumShrink,
            result.EvaluatedCandidates,
            result.RejectedCandidates,
            result.FeasibleCandidates,
            result.SelectedBreakpointIds);
    }

    private static double RequireFiniteNonNegative(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new InvalidOperationException($"Reconstructed {name} must be finite and non-negative.");
        }

        return value;
    }
}

using KnuthPlass.Core.Metrics;
using KnuthPlass.Core.Model;

namespace KnuthPlass.Core.Breaking;

/// <summary>
/// Applies the shared line, penalty, fitness, and flagged-break demerit formula.
/// </summary>
public static class LineDemeritCalculator
{
    public static bool TryCalculate(
        LineMetrics metrics,
        FitnessClass? previousFitness,
        bool previousBreakWasFlagged,
        LineBreakingOptions options,
        out double demerits)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        if (options is null)
        {
            demerits = 0;
            return false;
        }

        try
        {
            LineMeasurement.ValidateOptions(options);
        }
        catch (ArgumentException)
        {
            demerits = 0;
            return false;
        }

        demerits = 0;
        if (!metrics.IsFeasible || metrics.RejectionReason is not null ||
            metrics.BreakPenalty >= Penalty.ForbiddenBreak ||
            metrics.Badness is not double badness || !double.IsFinite(badness) ||
            badness < 0 ||
            metrics.Fitness is not FitnessClass fitness || !Enum.IsDefined(fitness) ||
            previousFitness is FitnessClass providedPrior && !Enum.IsDefined(providedPrior))
        {
            return false;
        }

        var baseAmount = options.LinePenalty + badness;
        var lineDemerits = baseAmount * baseAmount;
        if (!double.IsFinite(lineDemerits))
        {
            return false;
        }

        var penaltyMagnitude = (double)metrics.BreakPenalty * metrics.BreakPenalty;
        if (metrics.BreakPenalty >= 0 && metrics.BreakPenalty < Penalty.ForbiddenBreak)
        {
            lineDemerits += penaltyMagnitude;
        }
        else if (metrics.BreakPenalty < 0 && metrics.BreakPenalty > Penalty.ForcedBreak)
        {
            lineDemerits -= penaltyMagnitude;
        }

        lineDemerits = Math.Max(0, lineDemerits);

        if (previousFitness is FitnessClass prior &&
            Math.Abs((int)prior - (int)fitness) > 1)
        {
            lineDemerits += options.FitnessDemerit;
        }

        if (previousBreakWasFlagged && metrics.IsFlagged)
        {
            lineDemerits += options.FlaggedDemerit;
        }

        if (!double.IsFinite(lineDemerits))
        {
            return false;
        }

        demerits = lineDemerits;
        return true;
    }
}

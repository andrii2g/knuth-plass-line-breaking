using KnuthPlass.Core.Breaking;
using KnuthPlass.Core.Model;

namespace KnuthPlass.Core.Metrics;

/// <summary>
/// Applies the shared boundary, ratio, feasibility, badness, and fitness rules.
/// </summary>
public sealed class LineMeasurement
{
    private readonly Paragraph paragraph;
    private readonly PrefixSums prefixSums;

    public LineMeasurement(Paragraph paragraph)
    {
        ArgumentNullException.ThrowIfNull(paragraph);
        this.paragraph = paragraph;
        prefixSums = new PrefixSums(paragraph);
    }

    /// <summary>
    /// Measures one candidate line between two legal paragraph breakpoints.
    /// </summary>
    public LineMetrics Measure(
        Breakpoint start,
        Breakpoint end,
        LineBreakingOptions options)
    {
        ValidateBreakpoint(start, nameof(start));
        ValidateBreakpoint(end, nameof(end));
        ValidateOptions(options);

        if (start.Id >= end.Id || start.ItemIndex >= end.ItemIndex)
        {
            throw new ArgumentException(
                "The end breakpoint must occur after the start breakpoint.");
        }

        var startItemIndex = prefixSums.SkipLeadingGlue(start.ItemIndex + 1);
        var endItemIndex = prefixSums.TrimTrailingGlue(end.ItemIndex);
        endItemIndex = Math.Max(startItemIndex, endItemIndex);

        var totals = prefixSums.GetRange(startItemIndex, endItemIndex);
        var selectedPenalty = paragraph.Items[end.ItemIndex] as Penalty;
        var naturalWidth = totals.Width + (selectedPenalty?.Width ?? 0);
        var breakPenalty = selectedPenalty?.Value ?? 0;
        var isLast = end == paragraph.End;

        if (!double.IsFinite(naturalWidth))
        {
            return Rejected(
                start,
                end,
                startItemIndex,
                endItemIndex,
                totals,
                options.TargetWidth,
                breakPenalty,
                isLast,
                LineRejectionReason.NonFiniteCalculation);
        }
        if (prefixSums.CountForcedBreaks(start.ItemIndex + 1, end.ItemIndex) > 0)
        {
            return Rejected(
                start,
                end,
                startItemIndex,
                endItemIndex,
                totals with { Width = naturalWidth },
                options.TargetWidth,
                breakPenalty,
                isLast,
                LineRejectionReason.ForcedBreakSkipped);
        }


        if (isLast && options.LastLineMode == LastLineMode.Ragged)
        {
            if (naturalWidth > options.TargetWidth + options.Epsilon)
            {
                return Rejected(
                    start,
                    end,
                    startItemIndex,
                    endItemIndex,
                    totals with { Width = naturalWidth },
                    options.TargetWidth,
                    breakPenalty,
                    true,
                    LineRejectionReason.OverfullRaggedLastLine);
            }

            return Feasible(
                start,
                end,
                startItemIndex,
                endItemIndex,
                totals with { Width = naturalWidth },
                options.TargetWidth,
                0,
                breakPenalty,
                true);
        }

        var difference = options.TargetWidth - naturalWidth;
        double ratio;

        if (Math.Abs(difference) <= options.Epsilon)
        {
            ratio = 0;
        }
        else if (difference > 0)
        {
            if (totals.Stretch == 0)
            {
                return Rejected(
                    start,
                    end,
                    startItemIndex,
                    endItemIndex,
                    totals with { Width = naturalWidth },
                    options.TargetWidth,
                    breakPenalty,
                    isLast,
                    LineRejectionReason.InsufficientStretch);
            }

            ratio = difference / totals.Stretch;
        }
        else
        {
            if (totals.Shrink == 0)
            {
                return Rejected(
                    start,
                    end,
                    startItemIndex,
                    endItemIndex,
                    totals with { Width = naturalWidth },
                    options.TargetWidth,
                    breakPenalty,
                    isLast,
                    LineRejectionReason.InsufficientShrink);
            }

            ratio = difference / totals.Shrink;
        }

        if (!double.IsFinite(ratio))
        {
            return Rejected(
                start,
                end,
                startItemIndex,
                endItemIndex,
                totals with { Width = naturalWidth },
                options.TargetWidth,
                breakPenalty,
                isLast,
                LineRejectionReason.NonFiniteCalculation);
        }

        if (ratio < -1 - options.Epsilon)
        {
            return Rejected(
                start,
                end,
                startItemIndex,
                endItemIndex,
                totals with { Width = naturalWidth },
                options.TargetWidth,
                breakPenalty,
                isLast,
                LineRejectionReason.AdjustmentRatioTooLow,
                ratio);
        }

        if (ratio > options.MaxAdjustmentRatio + options.Epsilon)
        {
            return Rejected(
                start,
                end,
                startItemIndex,
                endItemIndex,
                totals with { Width = naturalWidth },
                options.TargetWidth,
                breakPenalty,
                isLast,
                LineRejectionReason.AdjustmentRatioTooHigh,
                ratio);
        }

        return Feasible(
            start,
            end,
            startItemIndex,
            endItemIndex,
            totals with { Width = naturalWidth },
            options.TargetWidth,
            ratio,
            breakPenalty,
            isLast);
    }

    /// <summary>
    /// Calculates cubic badness for a finite adjustment ratio.
    /// </summary>
    public static double CalculateBadness(double adjustmentRatio)
    {
        if (!double.IsFinite(adjustmentRatio))
        {
            throw new ArgumentOutOfRangeException(nameof(adjustmentRatio));
        }

        return Math.Min(10000, 100 * Math.Pow(Math.Abs(adjustmentRatio), 3));
    }

    /// <summary>
    /// Classifies a finite adjustment ratio.
    /// </summary>
    public static FitnessClass ClassifyFitness(double adjustmentRatio)
    {
        if (!double.IsFinite(adjustmentRatio))
        {
            throw new ArgumentOutOfRangeException(nameof(adjustmentRatio));
        }

        return adjustmentRatio switch
        {
            < -0.5 => FitnessClass.VeryTight,
            <= 0.5 => FitnessClass.Tight,
            <= 1 => FitnessClass.Loose,
            _ => FitnessClass.VeryLoose,
        };
    }

    private static LineMetrics Feasible(
        Breakpoint start,
        Breakpoint end,
        int startItemIndex,
        int endItemIndex,
        RangeTotals totals,
        double targetWidth,
        double ratio,
        int breakPenalty,
        bool isLast) =>
        new(
            start,
            end,
            startItemIndex,
            endItemIndex,
            totals.Width,
            totals.Stretch,
            totals.Shrink,
            targetWidth,
            ratio,
            CalculateBadness(ratio),
            ClassifyFitness(ratio),
            breakPenalty,
            end.IsFlagged,
            end.IsForced,
            isLast,
            true,
            null);

    private static LineMetrics Rejected(
        Breakpoint start,
        Breakpoint end,
        int startItemIndex,
        int endItemIndex,
        RangeTotals totals,
        double targetWidth,
        int breakPenalty,
        bool isLast,
        LineRejectionReason reason,
        double? ratio = null) =>
        new(
            start,
            end,
            startItemIndex,
            endItemIndex,
            totals.Width,
            totals.Stretch,
            totals.Shrink,
            targetWidth,
            ratio,
            null,
            null,
            breakPenalty,
            end.IsFlagged,
            end.IsForced,
            isLast,
            false,
            reason);

    private void ValidateBreakpoint(Breakpoint breakpoint, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);

        if (breakpoint.Id < 0 ||
            breakpoint.Id >= paragraph.Breakpoints.Length ||
            !ReferenceEquals(paragraph.Breakpoints[breakpoint.Id], breakpoint))
        {
            throw new ArgumentException(
                "Breakpoint does not belong to this paragraph.",
                parameterName);
        }
    }

    private static void ValidateOptions(LineBreakingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        RequireFinitePositive(options.TargetWidth, nameof(options.TargetWidth));
        RequireFiniteNonNegative(options.LinePenalty, nameof(options.LinePenalty));
        RequireFiniteNonNegative(options.FitnessDemerit, nameof(options.FitnessDemerit));
        RequireFiniteNonNegative(options.FlaggedDemerit, nameof(options.FlaggedDemerit));
        RequireFiniteNonNegative(
            options.MaxAdjustmentRatio,
            nameof(options.MaxAdjustmentRatio));
        RequireFinitePositive(options.Epsilon, nameof(options.Epsilon));

        if (!Enum.IsDefined(options.LastLineMode))
        {
            throw new ArgumentOutOfRangeException(nameof(options.LastLineMode));
        }
    }

    private static void RequireFinitePositive(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Value must be finite and positive.");
        }
    }

    private static void RequireFiniteNonNegative(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Value must be finite and non-negative.");
        }
    }
}

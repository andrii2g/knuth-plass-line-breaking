using KnuthPlass.Core.Breaking;
using KnuthPlass.Core.Model;
using KnuthPlass.Core.Results;

namespace KnuthPlass.Rendering;

internal static class RenderInputValidation
{
    public static void Validate(
        LineBreakingOptions options,
        IReadOnlyList<LineBreakResult> results,
        Paragraph? paragraph = null)
    {
        ValidateOptions(options);

        foreach (var result in results)
        {
            if (result.Options != options)
            {
                throw new ArgumentException(
                    "Render options must exactly match the options captured by every result.",
                    nameof(options));
            }

            if (paragraph is not null &&
                (result.ParagraphHadLineBreaks != paragraph.HadLineBreaks ||
                 !result.ParagraphItems.SequenceEqual(paragraph.Items)))
            {
                throw new ArgumentException(
                    "The paragraph must exactly match the item sequence captured by every result.",
                    nameof(paragraph));
            }
        }
    }

    private static void ValidateOptions(LineBreakingOptions options)
    {
        if (!double.IsFinite(options.TargetWidth) || options.TargetWidth <= 0 ||
            !IsFiniteNonNegative(options.LinePenalty) ||
            !IsFiniteNonNegative(options.FitnessDemerit) ||
            !IsFiniteNonNegative(options.FlaggedDemerit) ||
            !IsFiniteNonNegative(options.MaxAdjustmentRatio) ||
            !double.IsFinite(options.Epsilon) || options.Epsilon <= 0 ||
            !Enum.IsDefined(options.LastLineMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Rendering requires valid finite line-breaking options.");
        }
    }

    private static bool IsFiniteNonNegative(double value) =>
        double.IsFinite(value) && value >= 0;
}

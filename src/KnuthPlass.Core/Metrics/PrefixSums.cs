using System.Collections.Immutable;
using KnuthPlass.Core.Model;

namespace KnuthPlass.Core.Metrics;

/// <summary>
/// Provides O(1) width, stretch, and shrink totals over immutable paragraph items.
/// </summary>
public sealed class PrefixSums
{
    private readonly ImmutableArray<ParagraphItem> items;
    private readonly double[] widths;
    private readonly double[] stretches;
    private readonly double[] shrinks;
    private readonly int[] nextNonGlue;
    private readonly int[] trimmedEnd;
    private readonly int[] forcedBreakCounts;

    /// <summary>
    /// Initializes cumulative sums and generic glue-boundary lookup tables.
    /// </summary>
    public PrefixSums(Paragraph paragraph)
    {
        ArgumentNullException.ThrowIfNull(paragraph);
        items = paragraph.Items;

        widths = new double[items.Length + 1];
        stretches = new double[items.Length + 1];
        shrinks = new double[items.Length + 1];
        nextNonGlue = new int[items.Length + 1];
        trimmedEnd = new int[items.Length + 1];
        forcedBreakCounts = new int[items.Length + 1];

        var maximumPenaltyWidth = 0d;
        for (var index = 0; index < items.Length; index++)
        {
            var (width, stretch, shrink) = items[index] switch
            {
                Box box => (box.Width, 0d, 0d),
                Glue glue => (glue.Width, glue.Stretch, glue.Shrink),
                Penalty penalty => (0d, 0d, 0d),
                _ => throw new ArgumentException(
                    $"Unsupported paragraph item type: {items[index].GetType().Name}.",
                    nameof(paragraph)),
            };

            if (items[index] is Penalty selectedPenalty)
            {
                maximumPenaltyWidth = Math.Max(maximumPenaltyWidth, selectedPenalty.Width);
            }

            widths[index + 1] = AddFinite(widths[index], width, nameof(paragraph));
            stretches[index + 1] = AddFinite(stretches[index], stretch, nameof(paragraph));
            shrinks[index + 1] = AddFinite(shrinks[index], shrink, nameof(paragraph));
            forcedBreakCounts[index + 1] = forcedBreakCounts[index] +
                (items[index] is Penalty { IsForced: true } ? 1 : 0);
            trimmedEnd[index + 1] = items[index] is Glue
                ? trimmedEnd[index]
                : index + 1;
        }

        _ = AddFinite(widths[^1], maximumPenaltyWidth, nameof(paragraph));

        nextNonGlue[^1] = items.Length;
        for (var index = items.Length - 1; index >= 0; index--)
        {
            nextNonGlue[index] = items[index] is Glue
                ? nextNonGlue[index + 1]
                : index;
        }
    }

    public int ItemCount => items.Length;

    /// <summary>
    /// Returns aggregate dimensions for the half-open item range.
    /// Penalty widths are intentionally excluded.
    /// </summary>
    public RangeTotals GetRange(int startInclusive, int endExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startInclusive);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startInclusive, items.Length);
        ArgumentOutOfRangeException.ThrowIfNegative(endExclusive);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(endExclusive, items.Length);

        if (startInclusive > endExclusive)
        {
            throw new ArgumentException("Range start must not exceed range end.");
        }

        return new RangeTotals(
            widths[endExclusive] - widths[startInclusive],
            stretches[endExclusive] - stretches[startInclusive],
            shrinks[endExclusive] - shrinks[startInclusive]);
    }

    /// <summary>
    /// Counts forced penalties in the half-open item range.
    /// </summary>
    public int CountForcedBreaks(int startInclusive, int endExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startInclusive);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startInclusive, items.Length);
        ArgumentOutOfRangeException.ThrowIfNegative(endExclusive);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(endExclusive, items.Length);

        if (startInclusive > endExclusive)
        {
            throw new ArgumentException("Range start must not exceed range end.");
        }

        return forcedBreakCounts[endExclusive] - forcedBreakCounts[startInclusive];
    }

    /// <summary>
    /// Returns the first non-glue index at or after the supplied index.
    /// </summary>
    public int SkipLeadingGlue(int startInclusive)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startInclusive);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startInclusive, items.Length);
        return nextNonGlue[startInclusive];
    }

    /// <summary>
    /// Returns an end-exclusive index with trailing glue removed.
    /// </summary>
    public int TrimTrailingGlue(int endExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(endExclusive);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(endExclusive, items.Length);
        return trimmedEnd[endExclusive];
    }

    private static double AddFinite(double left, double right, string parameterName)
    {
        var result = left + right;
        if (!double.IsFinite(result))
        {
            throw new ArgumentException(
                "Paragraph dimensions overflow finite line measurement.",
                parameterName);
        }

        return result;
    }
}

using System.Collections.Immutable;
using KnuthPlass.Core.Metrics;
using KnuthPlass.Core.Model;

namespace KnuthPlass.Core.Results;

internal static class BrokenLineFactory
{
    public static BrokenLine Create(
        int lineNumber,
        Paragraph paragraph,
        LineMetrics metrics,
        double? lineDemerits,
        double? accumulatedDemerits,
        bool isOverfull)
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

        return new BrokenLine(
            lineNumber,
            metrics,
            boxes.ToImmutable(),
            lineDemerits,
            accumulatedDemerits,
            isOverfull);
    }
}

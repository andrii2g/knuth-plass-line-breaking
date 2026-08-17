namespace KnuthPlass.Core.Metrics;

/// <summary>
/// Contains natural width, stretch, and shrink totals for an item range.
/// </summary>
public readonly record struct RangeTotals(
    double Width,
    double Stretch,
    double Shrink);

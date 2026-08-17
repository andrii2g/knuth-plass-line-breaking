namespace KnuthPlass.Core.Model;

/// <summary>
/// Represents a discretionary, forbidden, or forced line-break point.
/// </summary>
public sealed record Penalty : ParagraphItem
{
    public const int ForcedBreak = -10000;
    public const int ForbiddenBreak = 10000;

    /// <summary>
    /// Initializes a penalty item.
    /// </summary>
    public Penalty(double width, int value, bool flagged)
    {
        if (value is < ForcedBreak or > ForbiddenBreak)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"Penalty values must be between {ForcedBreak} and {ForbiddenBreak}.");
        }

        Width = ModelValidation.FiniteNonNegative(width, nameof(width));
        Value = value;
        Flagged = flagged;
    }

    public double Width { get; }
    public int Value { get; }
    public bool Flagged { get; }
    public bool IsForced => Value <= ForcedBreak;
    public bool IsForbidden => Value >= ForbiddenBreak;
}

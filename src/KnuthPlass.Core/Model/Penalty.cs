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
    public Penalty(double Width, int Value, bool Flagged)
    {
        this.Width = ModelValidation.FiniteNonNegative(Width, nameof(Width));
        this.Value = Value;
        this.Flagged = Flagged;
    }

    public double Width { get; }
    public int Value { get; }
    public bool Flagged { get; }
    public bool IsForced => Value <= ForcedBreak;
    public bool IsForbidden => Value >= ForbiddenBreak;

    /// <summary>
    /// Deconstructs the documented public penalty values.
    /// </summary>
    public void Deconstruct(out double Width, out int Value, out bool Flagged)
    {
        Width = this.Width;
        Value = this.Value;
        Flagged = this.Flagged;
    }
}

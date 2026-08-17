namespace KnuthPlass.Core.Model;

/// <summary>
/// Represents flexible whitespace with natural width, stretch, and shrink.
/// </summary>
public sealed record Glue : ParagraphItem
{
    /// <summary>
    /// Initializes a glue item.
    /// </summary>
    public Glue(double width, double stretch, double shrink)
    {
        Width = ModelValidation.FiniteNonNegative(width, nameof(width));
        Stretch = ModelValidation.FiniteNonNegative(stretch, nameof(stretch));
        Shrink = ModelValidation.FiniteNonNegative(shrink, nameof(shrink));
    }

    public double Width { get; }
    public double Stretch { get; }
    public double Shrink { get; }
}

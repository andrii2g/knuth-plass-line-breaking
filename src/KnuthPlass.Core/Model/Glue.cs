namespace KnuthPlass.Core.Model;

/// <summary>
/// Represents flexible whitespace with natural width, stretch, and shrink.
/// </summary>
public sealed record Glue : ParagraphItem
{
    /// <summary>
    /// Initializes a glue item.
    /// </summary>
    public Glue(double Width, double Stretch, double Shrink)
    {
        this.Width = ModelValidation.FiniteNonNegative(Width, nameof(Width));
        this.Stretch = ModelValidation.FiniteNonNegative(Stretch, nameof(Stretch));
        this.Shrink = ModelValidation.FiniteNonNegative(Shrink, nameof(Shrink));
    }

    public double Width { get; }
    public double Stretch { get; }
    public double Shrink { get; }

    /// <summary>
    /// Deconstructs the documented public glue values.
    /// </summary>
    public void Deconstruct(out double Width, out double Stretch, out double Shrink)
    {
        Width = this.Width;
        Stretch = this.Stretch;
        Shrink = this.Shrink;
    }
}

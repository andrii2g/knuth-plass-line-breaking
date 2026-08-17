namespace KnuthPlass.Core.Model;

/// <summary>
/// Represents fixed-width text that cannot break internally.
/// </summary>
public sealed record Box : ParagraphItem
{
    /// <summary>
    /// Initializes a box.
    /// </summary>
    /// <param name="Text">The nonblank source text.</param>
    /// <param name="Width">The finite, non-negative synthetic width.</param>
    /// <param name="SourceWordIndex">The zero-based source word index, or -1 before paragraph assignment.</param>
    public Box(string Text, double Width, int SourceWordIndex = -1)
    {
        if (string.IsNullOrWhiteSpace(Text))
        {
            throw new ArgumentException("Box text must not be blank.", nameof(Text));
        }

        if (SourceWordIndex < -1)
        {
            throw new ArgumentOutOfRangeException(nameof(SourceWordIndex));
        }

        this.Text = Text;
        this.Width = ModelValidation.FiniteNonNegative(Width, nameof(Width));
        this.SourceWordIndex = SourceWordIndex;
    }

    public string Text { get; }
    public double Width { get; }
    public int SourceWordIndex { get; }

    /// <summary>
    /// Deconstructs the documented public box values.
    /// </summary>
    public void Deconstruct(out string Text, out double Width)
    {
        Text = this.Text;
        Width = this.Width;
    }
}

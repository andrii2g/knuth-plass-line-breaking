namespace KnuthPlass.Core.Model;

/// <summary>
/// Represents fixed-width text that cannot break internally.
/// </summary>
public sealed record Box : ParagraphItem
{
    /// <summary>
    /// Initializes a box.
    /// </summary>
    /// <param name="text">The nonblank source text.</param>
    /// <param name="width">The finite, non-negative synthetic width.</param>
    /// <param name="sourceWordIndex">The zero-based source word index.</param>
    public Box(string text, double width, int sourceWordIndex = -1)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Box text must not be blank.", nameof(text));
        }

        if (sourceWordIndex < -1)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceWordIndex));
        }

        Text = text;
        Width = ModelValidation.FiniteNonNegative(width, nameof(width));
        SourceWordIndex = sourceWordIndex;
    }

    public string Text { get; }
    public double Width { get; }
    public int SourceWordIndex { get; }
}

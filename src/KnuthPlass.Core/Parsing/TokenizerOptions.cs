using KnuthPlass.Core.Model;

namespace KnuthPlass.Core.Parsing;

/// <summary>
/// Configures deterministic synthetic paragraph tokenization.
/// </summary>
public sealed record TokenizerOptions
{
    public TokenizerOptions(
        double spaceWidth = 1,
        double stretch = 0.5,
        double shrink = 1d / 3d,
        int maxInputLength = 100_000)
    {
        SpaceWidth = ModelValidation.FiniteNonNegative(spaceWidth, nameof(spaceWidth));
        Stretch = ModelValidation.FiniteNonNegative(stretch, nameof(stretch));
        Shrink = ModelValidation.FiniteNonNegative(shrink, nameof(shrink));

        if (maxInputLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxInputLength));
        }

        MaxInputLength = maxInputLength;
    }

    public double SpaceWidth { get; }
    public double Stretch { get; }
    public double Shrink { get; }
    public int MaxInputLength { get; }
}

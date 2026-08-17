namespace KnuthPlass.Core.Model;

/// <summary>
/// Represents one immutable item in the box, glue, and penalty paragraph model.
/// </summary>
public abstract record ParagraphItem;

internal static class ModelValidation
{
    public static double FiniteNonNegative(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "The value must be finite and non-negative.");
        }

        return value;
    }
}

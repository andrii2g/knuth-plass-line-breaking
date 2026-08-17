using System.Globalization;
using System.Security;
using System.Text;

namespace KnuthPlass.Rendering;

internal static class RenderFormatting
{
    public static string Number(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new InvalidOperationException("Rendered numeric values must be finite.");
        }

        return (value == 0 ? 0 : value).ToString("G17", CultureInfo.InvariantCulture);
    }

    public static string NullableNumber(double? value) =>
        value is { } number ? Number(number) : "not available";

    public static string Xml(string value)
    {
        var sanitized = new StringBuilder(value.Length);
        foreach (var rune in value.EnumerateRunes())
        {
            var scalar = rune.Value;
            sanitized.Append(
                scalar is 0x9 or 0xA or 0xD ||
                scalar is >= 0x20 and <= 0xD7FF ||
                scalar is >= 0xE000 and <= 0xFFFD ||
                scalar is >= 0x10000 and <= 0x10FFFF
                    ? rune.ToString()
                    : "\uFFFD");
        }

        return SecurityElement.Escape(sanitized.ToString()) ?? string.Empty;
    }
}

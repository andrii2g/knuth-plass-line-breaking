using System.Text;
using KnuthPlass.Core.Model;

namespace KnuthPlass.Core.Parsing;

/// <summary>
/// Converts one plain-text paragraph into deterministic boxes, glue, and a forced end.
/// </summary>
public sealed class ParagraphTokenizer
{
    /// <summary>
    /// Tokenizes source text using synthetic Unicode-scalar widths.
    /// </summary>
    public Paragraph Tokenize(string text, TokenizerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        options ??= new TokenizerOptions();

        if (text.Length > options.MaxInputLength)
        {
            throw new ArgumentException(
                $"Input exceeds the {options.MaxInputLength} character limit.",
                nameof(text));
        }

        var words = SplitWords(text);
        if (words.Count == 0)
        {
            throw new ArgumentException(
                "Input must contain at least one non-whitespace character.",
                nameof(text));
        }

        var items = new List<ParagraphItem>(words.Count * 2);
        for (var index = 0; index < words.Count; index++)
        {
            if (index > 0)
            {
                items.Add(new Glue(options.SpaceWidth, options.Stretch, options.Shrink));
            }

            var word = words[index];
            items.Add(new Box(word, word.EnumerateRunes().Count(), index));
        }

        items.Add(new Penalty(0, Penalty.ForcedBreak, false));
        var hadLineBreaks = text.Contains('\r') || text.Contains('\n');
        return new Paragraph(items, hadLineBreaks);
    }

    private static List<string> SplitWords(string text)
    {
        var words = new List<string>();
        var current = new StringBuilder();

        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                if (current.Length > 0)
                {
                    words.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            current.Append(character);
        }

        if (current.Length > 0)
        {
            words.Add(current.ToString());
        }

        return words;
    }
}

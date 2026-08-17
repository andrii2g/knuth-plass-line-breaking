using System.Collections.Immutable;

namespace KnuthPlass.Core.Model;

/// <summary>
/// Contains an immutable paragraph item sequence and its legal breakpoints.
/// </summary>
public sealed class Paragraph
{
    /// <summary>
    /// Initializes a paragraph and discovers its legal breakpoints.
    /// </summary>
    /// <param name="items">Items ending in a forced penalty.</param>
    /// <param name="hadLineBreaks">Whether source normalization removed line breaks.</param>
    public Paragraph(IEnumerable<ParagraphItem> items, bool hadLineBreaks = false)
    {
        ArgumentNullException.ThrowIfNull(items);

        var sourceItems = items.ToImmutableArray();
        if (sourceItems.IsEmpty)
        {
            throw new ArgumentException("A paragraph must contain items.", nameof(items));
        }

        if (sourceItems[^1] is not Penalty { IsForced: true })
        {
            throw new ArgumentException(
                "A paragraph must end in a forced penalty.",
                nameof(items));
        }

        Items = NormalizeBoxes(sourceItems);

        Breakpoints = DiscoverBreakpoints(Items);
        Words = Items
            .OfType<Box>()
            .Select(box => box.Text)
            .ToImmutableArray();
        HadLineBreaks = hadLineBreaks;
    }

    public ImmutableArray<ParagraphItem> Items { get; }
    public ImmutableArray<Breakpoint> Breakpoints { get; }
    public ImmutableArray<string> Words { get; }
    public bool HadLineBreaks { get; }
    public Breakpoint Start => Breakpoints[0];
    public Breakpoint End => Breakpoints[^1];

    private static ImmutableArray<ParagraphItem> NormalizeBoxes(
        ImmutableArray<ParagraphItem> items)
    {
        var normalized = ImmutableArray.CreateBuilder<ParagraphItem>(items.Length);
        var sourceWordIndex = 0;

        foreach (var item in items)
        {
            normalized.Add(item is Box box
                ? new Box(box.Text, box.Width, sourceWordIndex++)
                : item);
        }

        return normalized.MoveToImmutable();
    }

    private static ImmutableArray<Breakpoint> DiscoverBreakpoints(
        ImmutableArray<ParagraphItem> items)
    {
        var breakpoints = ImmutableArray.CreateBuilder<Breakpoint>();
        breakpoints.Add(new Breakpoint(0, -1, true, false, false));

        for (var index = 0; index < items.Length; index++)
        {
            switch (items[index])
            {
                case Glue when index > 0 && items[index - 1] is Box:
                    breakpoints.Add(new Breakpoint(
                        breakpoints.Count,
                        index,
                        false,
                        false,
                        false));
                    break;
                case Penalty { IsForbidden: false } penalty:
                    breakpoints.Add(new Breakpoint(
                        breakpoints.Count,
                        index,
                        false,
                        penalty.IsForced,
                        penalty.Flagged));
                    break;
            }
        }

        return breakpoints.ToImmutable();
    }
}

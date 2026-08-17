using KnuthPlass.Core.Model;
using KnuthPlass.Core.Results;
using KnuthPlass.Core.Tracing;

namespace KnuthPlass.Core.Breaking;

/// <summary>
/// Breaks an immutable paragraph according to a deterministic strategy.
/// </summary>
public interface ILineBreaker
{
    LineBreakResult Break(
        Paragraph paragraph,
        LineBreakingOptions options,
        ITraceSink? trace = null);
}

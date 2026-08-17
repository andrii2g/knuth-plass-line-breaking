namespace KnuthPlass.Core.Model;

/// <summary>
/// Identifies a legal position at which a line may end.
/// </summary>
/// <param name="Id">The stable breakpoint identifier in source order.</param>
/// <param name="ItemIndex">The break item index, or -1 for the synthetic start.</param>
/// <param name="IsSyntheticStart">Whether this is the synthetic paragraph start.</param>
/// <param name="IsForced">Whether every path reaching this position must break.</param>
/// <param name="IsFlagged">Whether the break participates in consecutive-flagged demerits.</param>
public sealed record Breakpoint(
    int Id,
    int ItemIndex,
    bool IsSyntheticStart,
    bool IsForced,
    bool IsFlagged);

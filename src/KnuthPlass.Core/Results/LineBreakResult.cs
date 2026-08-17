using System.Collections.Immutable;

namespace KnuthPlass.Core.Results;

/// <summary>
/// Contains either a complete selected layout or an explicit failure.
/// </summary>
public sealed class LineBreakResult
{
    private LineBreakResult(
        string algorithmName,
        bool isSuccess,
        ImmutableArray<BrokenLine> lines,
        ImmutableArray<int> selectedBreakpointIds,
        double? totalDemerits,
        FailureReason? failureReason,
        int evaluatedCandidates,
        int rejectedCandidates,
        int feasibleCandidates)
    {
        AlgorithmName = algorithmName;
        IsSuccess = isSuccess;
        Lines = lines;
        SelectedBreakpointIds = selectedBreakpointIds;
        TotalDemerits = totalDemerits;
        FailureReason = failureReason;
        EvaluatedCandidates = evaluatedCandidates;
        RejectedCandidates = rejectedCandidates;
        FeasibleCandidates = feasibleCandidates;
    }

    public string AlgorithmName { get; }
    public bool IsSuccess { get; }
    public ImmutableArray<BrokenLine> Lines { get; }
    public ImmutableArray<int> SelectedBreakpointIds { get; }
    public double? TotalDemerits { get; }
    public FailureReason? FailureReason { get; }
    public int EvaluatedCandidates { get; }
    public int RejectedCandidates { get; }
    public int FeasibleCandidates { get; }

    internal static LineBreakResult Succeeded(
        string algorithmName,
        ImmutableArray<BrokenLine> lines,
        ImmutableArray<int> selectedBreakpointIds,
        double? totalDemerits,
        int evaluatedCandidates,
        int rejectedCandidates,
        int feasibleCandidates) =>
        new(
            algorithmName,
            true,
            lines,
            selectedBreakpointIds,
            totalDemerits,
            null,
            evaluatedCandidates,
            rejectedCandidates,
            feasibleCandidates);

    internal static LineBreakResult Failed(
        string algorithmName,
        FailureReason failureReason,
        int evaluatedCandidates = 0,
        int rejectedCandidates = 0,
        int feasibleCandidates = 0) =>
        new(
            algorithmName,
            false,
            ImmutableArray<BrokenLine>.Empty,
            ImmutableArray<int>.Empty,
            null,
            failureReason,
            evaluatedCandidates,
            rejectedCandidates,
            feasibleCandidates);
}

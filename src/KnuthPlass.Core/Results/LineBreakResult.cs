using System.Collections.Immutable;
using KnuthPlass.Core.Breaking;
using KnuthPlass.Core.Metrics;
using KnuthPlass.Core.Model;
using KnuthPlass.Core.Tracing;

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
        FailureReason? failureReason,
        int evaluatedCandidates,
        int rejectedCandidates,
        int feasibleCandidates,
        Paragraph paragraph,
        LineBreakingOptions? options,
        TraceDocument? trace)
    {
        AlgorithmName = algorithmName;
        IsSuccess = isSuccess;
        Lines = lines;
        SelectedBreakpointIds = selectedBreakpointIds;
        FailureReason = failureReason;
        EvaluatedCandidates = evaluatedCandidates;
        RejectedCandidates = rejectedCandidates;
        FeasibleCandidates = feasibleCandidates;
        ParagraphItems = paragraph.Items;
        ParagraphHadLineBreaks = paragraph.HadLineBreaks;
        Options = options;
        Trace = trace;
    }

    public string AlgorithmName { get; }
    public bool IsSuccess { get; }
    public ImmutableArray<BrokenLine> Lines { get; }
    public ImmutableArray<int> SelectedBreakpointIds { get; }
    public double? TotalDemerits => Metrics?.TotalDemerits;
    public FailureReason? FailureReason { get; }
    public int EvaluatedCandidates { get; }
    public int RejectedCandidates { get; }
    public int FeasibleCandidates { get; }
    public ImmutableArray<ParagraphItem> ParagraphItems { get; }
    public bool ParagraphHadLineBreaks { get; }
    public LineBreakingOptions? Options { get; }
    public ParagraphMetrics? Metrics { get; private set; }
    public TraceDocument? Trace { get; }
    public ImmutableArray<SequencedTraceEvent> TraceEvents =>
        Trace?.Events ?? ImmutableArray<SequencedTraceEvent>.Empty;

    internal static LineBreakResult Succeeded(
        string algorithmName,
        ImmutableArray<BrokenLine> lines,
        ImmutableArray<int> selectedBreakpointIds,
        int evaluatedCandidates,
        int rejectedCandidates,
        int feasibleCandidates,
        Paragraph paragraph,
        LineBreakingOptions? options,
        TraceDocument? trace = null)
    {
        var result = new LineBreakResult(
            algorithmName,
            true,
            lines,
            selectedBreakpointIds,
            null,
            evaluatedCandidates,
            rejectedCandidates,
            feasibleCandidates,
            paragraph,
            options,
            trace);

        result.Metrics = MetricsCalculator.Calculate(result);
        return result;
    }

    internal static LineBreakResult Failed(
        string algorithmName,
        FailureReason failureReason,
        Paragraph paragraph,
        LineBreakingOptions? options,
        int evaluatedCandidates = 0,
        int rejectedCandidates = 0,
        int feasibleCandidates = 0,
        TraceDocument? trace = null) =>
        new(
            algorithmName,
            false,
            ImmutableArray<BrokenLine>.Empty,
            ImmutableArray<int>.Empty,
            failureReason,
            evaluatedCandidates,
            rejectedCandidates,
            feasibleCandidates,
            paragraph,
            options,
            trace);
}

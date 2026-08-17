using System.Globalization;
using System.Text;
using KnuthPlass.Core.Breaking;
using KnuthPlass.Core.Tracing;

namespace KnuthPlass.Rendering.Text;

/// <summary>
/// Renders sequenced typed trace events as stable invariant-culture text.
/// </summary>
public sealed class TraceTextRenderer
{
    public string Render(TraceDocument trace)
    {
        ArgumentNullException.ThrowIfNull(trace);

        var builder = new StringBuilder();
        AppendHeader(builder, trace);

        long previousSequence = 0;
        foreach (var item in trace.Events)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (item.Sequence <= previousSequence)
            {
                throw new ArgumentException(
                    "Trace sequence numbers must be strictly increasing.",
                    nameof(trace));
            }

            previousSequence = item.Sequence;
            builder.Append('[')
                .Append(item.Sequence.ToString("D6", CultureInfo.InvariantCulture))
                .Append("] ");
            AppendEvent(builder, item.Event);
            builder.Append('\n');
        }

        return builder.ToString();
    }

    private static void AppendHeader(
        StringBuilder builder,
        TraceDocument trace)
    {
        var options = trace.Options;
        builder.Append("Options targetWidth=")
            .Append(Number(options.TargetWidth))
            .Append(" linePenalty=")
            .Append(Number(options.LinePenalty))
            .Append(" fitnessDemerit=")
            .Append(Number(options.FitnessDemerit))
            .Append(" flaggedDemerit=")
            .Append(Number(options.FlaggedDemerit))
            .Append(" maxAdjustmentRatio=")
            .Append(Number(options.MaxAdjustmentRatio))
            .Append(" lastLineMode=")
            .Append(options.LastLineMode)
            .Append(" epsilon=")
            .Append(Number(options.Epsilon))
            .Append('\n')
            .Append("Breakpoints count=")
            .Append(trace.Breakpoints.Length)
            .Append('\n');

        foreach (var breakpoint in trace.Breakpoints)
        {
            builder.Append("  B")
                .Append(breakpoint.Id.ToString("D2", CultureInfo.InvariantCulture))
                .Append(" itemIndex=")
                .Append(breakpoint.ItemIndex)
                .Append(" synthetic=")
                .Append(breakpoint.IsSyntheticStart ? "true" : "false")
                .Append(" forced=")
                .Append(breakpoint.IsForced ? "true" : "false")
                .Append(" flagged=")
                .Append(breakpoint.IsFlagged ? "true" : "false")
                .Append('\n');
        }
    }

    private static void AppendEvent(
        StringBuilder builder,
        TraceEvent traceEvent)
    {
        ArgumentNullException.ThrowIfNull(traceEvent);

        switch (traceEvent)
        {
            case CandidateEvaluated evaluated:
                builder.Append("CandidateEvaluated ");
                AppendCandidate(
                    builder,
                    evaluated.Candidate,
                    "evaluated",
                    evaluated.LineDemerits,
                    evaluated.AccumulatedCandidateDemerits,
                    null);
                break;
            case CandidateRejected rejected:
                builder.Append("CandidateRejected ");
                var rejection = rejected.Reason == CandidateRejectionKind.Measurement
                    ? rejected.Candidate.Metrics.RejectionReason?.ToString() ?? "measurement"
                    : rejected.Reason.ToString();
                AppendCandidate(
                    builder,
                    rejected.Candidate,
                    "rejected",
                    null,
                    null,
                    rejection);
                break;
            case StateUpdated updated:
                builder.Append("StateUpdated ");
                AppendCandidate(
                    builder,
                    updated.Candidate,
                    "updated",
                    updated.LineDemerits,
                    updated.TotalDemerits,
                    null);
                break;
            case StateRetained retained:
                builder.Append("StateRetained ");
                AppendCandidate(
                    builder,
                    retained.Candidate,
                    "retained",
                    retained.LineDemerits,
                    retained.CandidateTotalDemerits,
                    null);
                builder.Append(" retainedTotalDemerits=")
                    .Append(Number(retained.RetainedTotalDemerits))
                    .Append(" retainedLineCount=")
                    .Append(retained.RetainedLineCount);
                break;
            case FinalStateSelected selected:
                builder.Append("FinalStateSelected breakpoint=B")
                    .Append(selected.BreakpointId.ToString("D2", CultureInfo.InvariantCulture))
                    .Append(" fitness=")
                    .Append(selected.Fitness)
                    .Append(" totalDemerits=")
                    .Append(Number(selected.TotalDemerits))
                    .Append(" lineCount=")
                    .Append(selected.LineCount);
                break;
            case PathReconstructed reconstructed:
                builder.Append("PathReconstructed breakpoints=")
                    .AppendJoin(',', reconstructed.BreakpointIds.Select(
                        id => $"B{id.ToString("D2", CultureInfo.InvariantCulture)}"));
                break;
            default:
                throw new ArgumentException(
                    $"Unsupported trace event type '{traceEvent.GetType().Name}'.",
                    nameof(traceEvent));
        }
    }

    private static void AppendCandidate(
        StringBuilder builder,
        CandidateLine candidate,
        string action,
        double? lineDemerits,
        double? accumulatedCandidateDemerits,
        string? rejectionReason)
    {
        var metrics = candidate.Metrics;

        builder.Append("start=B")
            .Append(metrics.Start.Id.ToString("D2", CultureInfo.InvariantCulture))
            .Append(" end=B")
            .Append(metrics.End.Id.ToString("D2", CultureInfo.InvariantCulture))
            .Append(" line=")
            .Append(candidate.LineNumber)
            .Append(" natural=")
            .Append(Number(metrics.NaturalWidth))
            .Append(" target=")
            .Append(Number(metrics.TargetWidth))
            .Append(" stretch=")
            .Append(Number(metrics.Stretch))
            .Append(" shrink=")
            .Append(Number(metrics.Shrink))
            .Append(" ratio=")
            .Append(NullableNumber(metrics.AdjustmentRatio))
            .Append(" rejection=")
            .Append(rejectionReason ?? "null")
            .Append(" badness=")
            .Append(NullableNumber(metrics.Badness))
            .Append(" penalty=")
            .Append(metrics.BreakPenalty)
            .Append(" fitness=")
            .Append(metrics.Fitness?.ToString() ?? "null")
            .Append(" lineDemerits=")
            .Append(NullableNumber(lineDemerits))
            .Append(" accumulatedCandidateDemerits=")
            .Append(NullableNumber(accumulatedCandidateDemerits))
            .Append(" action=")
            .Append(action);
    }

    private static string NullableNumber(double? value) =>
        value is { } number ? Number(number) : "null";

    private static string Number(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new InvalidOperationException("Trace numeric values must be finite.");
        }

        return (value == 0 ? 0 : value).ToString("G17", CultureInfo.InvariantCulture);
    }
}

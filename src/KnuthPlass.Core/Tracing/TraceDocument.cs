using System.Collections.Immutable;
using KnuthPlass.Core.Breaking;
using KnuthPlass.Core.Model;

namespace KnuthPlass.Core.Tracing;

/// <summary>
/// Captures the exact immutable inputs and sequenced events for one algorithm run.
/// </summary>
public sealed record TraceDocument(
    LineBreakingOptions Options,
    ImmutableArray<Breakpoint> Breakpoints,
    ImmutableArray<SequencedTraceEvent> Events);

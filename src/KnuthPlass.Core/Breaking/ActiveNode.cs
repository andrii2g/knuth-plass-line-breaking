using KnuthPlass.Core.Metrics;
using KnuthPlass.Core.Model;

namespace KnuthPlass.Core.Breaking;

internal sealed record ActiveNode(
    Breakpoint Breakpoint,
    FitnessClass Fitness,
    double TotalDemerits,
    int LineCount,
    ActiveNode? Predecessor,
    LineMetrics? SelectedLine,
    double LineDemerits);

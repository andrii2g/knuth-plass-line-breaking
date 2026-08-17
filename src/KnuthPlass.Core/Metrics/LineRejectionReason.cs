namespace KnuthPlass.Core.Metrics;

/// <summary>
/// Explains why a measured candidate line is not feasible.
/// </summary>
public enum LineRejectionReason
{
    InsufficientStretch = 0,
    InsufficientShrink = 1,
    AdjustmentRatioTooLow = 2,
    AdjustmentRatioTooHigh = 3,
    OverfullRaggedLastLine = 4,
    NonFiniteCalculation = 5,
    ForcedBreakSkipped = 6,
}

namespace KnuthPlass.Core.Breaking;

/// <summary>
/// Configures line feasibility and demerit calculations.
/// </summary>
/// <param name="TargetWidth">The finite positive target line width.</param>
/// <param name="LinePenalty">The finite non-negative base line penalty.</param>
/// <param name="FitnessDemerit">The finite non-negative adjacent-fitness demerit.</param>
/// <param name="FlaggedDemerit">The finite non-negative consecutive-flag demerit.</param>
/// <param name="MaxAdjustmentRatio">The finite non-negative maximum stretch ratio.</param>
/// <param name="LastLineMode">The mandatory final-line policy.</param>
/// <param name="Epsilon">The finite positive comparison tolerance.</param>
public sealed record LineBreakingOptions(
    double TargetWidth,
    double LinePenalty = 10,
    double FitnessDemerit = 100,
    double FlaggedDemerit = 100,
    double MaxAdjustmentRatio = 3,
    LastLineMode LastLineMode = LastLineMode.Ragged,
    double Epsilon = 1e-9);

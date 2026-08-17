namespace KnuthPlass.Core.Results;

/// <summary>
/// Classifies an expected line-breaking failure.
/// </summary>
public enum FailureReason
{
    InvalidOptions = 0,
    NoFeasibleLayout = 1,
    NonFiniteDemerits = 2,
    InvalidReconstruction = 3,
}

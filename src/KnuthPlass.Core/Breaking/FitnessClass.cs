namespace KnuthPlass.Core.Breaking;

/// <summary>
/// Classifies a line by its adjustment ratio for transition demerits.
/// </summary>
public enum FitnessClass
{
    VeryTight = 0,
    Tight = 1,
    Loose = 2,
    VeryLoose = 3,
}

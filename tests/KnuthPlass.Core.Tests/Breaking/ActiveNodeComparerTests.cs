using KnuthPlass.Core.Breaking;
using KnuthPlass.Core.Model;

namespace KnuthPlass.Core.Tests.Breaking;

public sealed class ActiveNodeComparerTests
{
    [Fact]
    public void CostOutsideEpsilonWinsBeforeAllTieBreakers()
    {
        var candidate = Node(99, 10, predecessorId: 5, FitnessClass.VeryLoose);
        var incumbent = Node(100, 1, predecessorId: 1, FitnessClass.VeryTight);

        Assert.True(ActiveNodeComparer.IsBetter(candidate, incumbent, 0.5));
        Assert.False(ActiveNodeComparer.IsBetter(incumbent, candidate, 0.5));
    }

    [Fact]
    public void FewerLinesWinWhenCostsAreWithinEpsilon()
    {
        var candidate = Node(100.05, 2, predecessorId: 5, FitnessClass.VeryLoose);
        var incumbent = Node(100, 3, predecessorId: 1, FitnessClass.VeryTight);

        Assert.True(ActiveNodeComparer.IsBetter(candidate, incumbent, 0.1));
    }

    [Fact]
    public void EarlierPredecessorWinsAfterEqualCostAndLineCount()
    {
        var candidate = Node(100, 2, predecessorId: 2, FitnessClass.VeryLoose);
        var incumbent = Node(100, 2, predecessorId: 3, FitnessClass.VeryTight);

        Assert.True(ActiveNodeComparer.IsBetter(candidate, incumbent, 1e-9));
    }

    [Fact]
    public void LowerFitnessWinsAfterOtherFieldsTie()
    {
        var candidate = Node(100, 2, predecessorId: 2, FitnessClass.Tight);
        var incumbent = Node(100, 2, predecessorId: 2, FitnessClass.Loose);

        Assert.True(ActiveNodeComparer.IsBetter(candidate, incumbent, 1e-9));
        Assert.False(ActiveNodeComparer.IsBetter(incumbent, candidate, 1e-9));
    }

    private static ActiveNode Node(
        double totalDemerits,
        int lineCount,
        int predecessorId,
        FitnessClass fitness)
    {
        var predecessor = new ActiveNode(
            new Breakpoint(predecessorId, predecessorId, false, false, false),
            FitnessClass.Tight,
            0,
            Math.Max(0, lineCount - 1),
            null,
            null,
            0);

        return new ActiveNode(
            new Breakpoint(10, 10, false, false, false),
            fitness,
            totalDemerits,
            lineCount,
            predecessor,
            null,
            0);
    }
}

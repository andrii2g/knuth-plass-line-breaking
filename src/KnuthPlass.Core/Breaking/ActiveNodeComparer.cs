namespace KnuthPlass.Core.Breaking;

internal static class ActiveNodeComparer
{
    public static bool IsBetter(
        ActiveNode candidate,
        ActiveNode incumbent,
        double epsilon)
    {
        if (candidate.TotalDemerits < incumbent.TotalDemerits - epsilon)
        {
            return true;
        }

        if (candidate.TotalDemerits > incumbent.TotalDemerits + epsilon)
        {
            return false;
        }

        if (candidate.LineCount != incumbent.LineCount)
        {
            return candidate.LineCount < incumbent.LineCount;
        }

        var candidatePredecessor = candidate.Predecessor?.Breakpoint.Id ?? -1;
        var incumbentPredecessor = incumbent.Predecessor?.Breakpoint.Id ?? -1;
        if (candidatePredecessor != incumbentPredecessor)
        {
            return candidatePredecessor < incumbentPredecessor;
        }

        return candidate.Fitness < incumbent.Fitness;
    }
}

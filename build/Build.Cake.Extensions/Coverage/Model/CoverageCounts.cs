namespace Build.Cake.Extensions.Coverage.Model;

// Line, branch and method counts of a class, a group of classes or a whole report. A method counts as covered
// ("called") once any of its lines ran.
public sealed record CoverageCounts(int CoveredLines, int Lines, int CoveredBranches, int Branches, int CoveredMethods, int Methods)
{
    public static CoverageCounts Zero { get; } = new(0, 0, 0, 0, 0, 0);

    public int MissedLines => Lines - CoveredLines;

    public int MissedBranches => Branches - CoveredBranches;

    public static CoverageCounts operator +(CoverageCounts left, CoverageCounts right) => new(
        left.CoveredLines + right.CoveredLines,
        left.Lines + right.Lines,
        left.CoveredBranches + right.CoveredBranches,
        left.Branches + right.Branches,
        left.CoveredMethods + right.CoveredMethods,
        left.Methods + right.Methods);

    public static CoverageCounts Sum(IEnumerable<CoverageCounts> counts) => counts.Aggregate(Zero, (total, next) => total + next);
}

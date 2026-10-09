using Build.Cake.Extensions.Coverage.Model;

namespace Build.Cake.Extensions.Coverage.Reporting;

public sealed record CoverageReport(IReadOnlyList<CoverageTable> Tables, CoverageSummary Summary)
{
    public const string Legend = "Sorted by missed lines. Gap: share of all missed lines. Method (called): methods entered at least once.";
}

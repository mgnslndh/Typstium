using Cake.Core.IO;

namespace Build.Cake.Extensions.Coverage;

// CoverageDirectory holds raw/ (the test runs' Cobertura files) and receives relative/, report/ and summary.json.
// SourceDirectory holds one folder per product (src/<assembly>/) with its PublicAPI.*.txt files.
public sealed record CoverageReportSettings(
    DirectoryPath CoverageDirectory,
    DirectoryPath SourceDirectory,
    IReadOnlyList<string> Products,
    string JobSummaryHeading,
    bool WriteJobSummary,
    int TopClasses = 10)
{
    public DirectoryPath RawDirectory => CoverageDirectory.Combine("raw");

    public FilePath SummaryPath => CoverageDirectory.CombineWithFilePath("summary.json");
}

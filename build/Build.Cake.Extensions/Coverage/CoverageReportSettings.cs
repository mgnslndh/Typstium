using Build.Cake.Extensions.Coverage.Cobertura;
using Cake.Core.IO;

namespace Build.Cake.Extensions.Coverage;

// Files: where the raw Cobertura files are read and the merged report and summary.json are written.
// Sources: how the repository lays out its code. SourceDirectory holds one folder per product (src/<assembly>/) with
// its PublicAPI.*.txt files. MissingCoverageHint is appended to the error when there are no raw files, telling the
// user how this build produces them.
public sealed record CoverageReportSettings(
    CoverageLayout Files,
    SourceLayout Sources,
    DirectoryPath SourceDirectory,
    IReadOnlyList<string> Products,
    string JobSummaryHeading,
    bool WriteJobSummary,
    string MissingCoverageHint,
    int TopClasses = 10);

// What makes the coverage report and comparison Typstium's: the products, the comment's marker and footer, and the
// CI workflow that keeps the baselines.
public static class TypstiumCoverage
{
    public const string Marker = "<!-- typstium-coverage -->";
    public const string Footer = "<sub>Unit and integration tests, Windows and Ubuntu merged. The full tables are in the CI run's coverage job summary.</sub>";
    public const string JobSummaryHeading = "### Code coverage (unit and integration tests)";
    public const string Workflow = "ci.yml";
    public const string SummaryArtifact = "coverage-summary";
    public const string CheckName = "Coverage";

    // The products are the projects under src/; anything under tests/ never counts.
    public static CoverageReportSettings ReportSettings(ICakeContext context, BuildParameters parameters) => new(
        parameters.CoverageDirectory,
        context.MakeAbsolute(context.Directory("src")),
        context.GetFiles("src/**/*.csproj").Select(project => project.GetFilenameWithoutExtension().FullPath).Order(StringComparer.Ordinal).ToList(),
        JobSummaryHeading,
        parameters.JobSummary);

    public static PullRequestCoverageSettings ComparisonSettings(BuildParameters parameters) => new(
        parameters.CoverageDirectory.CombineWithFilePath("summary.json"),
        Workflow,
        SummaryArtifact,
        CheckName,
        new CoverageComparisonOptions(Marker, parameters.MaxLineDecrease, Footer));
}

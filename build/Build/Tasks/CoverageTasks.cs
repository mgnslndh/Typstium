public static partial class Program
{
    static void RegisterCoverageTasks()
    {
        // In Default only with --coverage (after every test passed); as the target, it merges raw files already in
        // artifacts/coverage/raw/, e.g. CI's Windows and Ubuntu artifacts copied together.
        Task("CoverageReport")
            .WithCriteria<BuildParameters>(parameters => parameters.Coverage || parameters.Target == "CoverageReport")
            .Does<BuildParameters>((context, parameters) => context.ReportCoverage(TypstiumCoverage.ReportSettings(context, parameters)));

        // CI only, on pull requests: compares with the merge base, comments, and creates the Coverage check.
        Task("CoverageComparison")
            .Does<BuildParameters>((context, parameters) => context.PublishPullRequestCoverage(TypstiumCoverage.ComparisonSettings(parameters)));
    }
}

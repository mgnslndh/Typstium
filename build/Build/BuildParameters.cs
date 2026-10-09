using System.Globalization;

// The build's inputs, read once in Setup from the command line (--name=value).
public sealed record BuildParameters(
    string Target,
    string Configuration,
    bool Coverage,
    bool JobSummary,
    double MaxLineDecrease,
    CoverageLayout CoverageFiles)
{
    // Cake matches target names ignoring case, so --target=coveragereport selects the task too.
    public bool IsCoverageReportTarget => string.Equals(Target, "CoverageReport", StringComparison.OrdinalIgnoreCase);

    public static BuildParameters Create(ICakeContext context, string target) => new(
        target,
        context.Argument("configuration", "Release"),
        Flag(context, "coverage"),
        !Flag(context, "no-job-summary"),
        // Parsed here, not by Cake's converter, so --max-line-decrease=0.5 means the same on a Swedish machine.
        double.Parse(context.Argument("max-line-decrease", "0.5"), NumberStyles.Float, CultureInfo.InvariantCulture),
        new CoverageLayout(context.MakeAbsolute(context.Directory("artifacts/coverage"))));

    // --name alone means true (Cake reads it as an empty value); --name=false and --name=true are honored.
    private static bool Flag(ICakeContext context, string name) =>
        context.HasArgument(name) && context.Argument(name, "") is var value && (value.Length == 0 || bool.Parse(value));
}

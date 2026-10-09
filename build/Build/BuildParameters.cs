using System.Globalization;

// The build's inputs, read once in Setup from the command line (--name=value).
public sealed record BuildParameters(
    string Target,
    string Configuration,
    bool Coverage,
    bool JobSummary,
    double MaxLineDecrease,
    DirectoryPath CoverageDirectory)
{
    public DirectoryPath RawCoverageDirectory => CoverageDirectory.Combine("raw");

    public static BuildParameters Create(ICakeContext context, string target) => new(
        target,
        context.Argument("configuration", "Release"),
        context.HasArgument("coverage"),
        !context.HasArgument("no-job-summary"),
        // Parsed here, not by Cake's converter, so --max-line-decrease=0.5 means the same on a Swedish machine.
        double.Parse(context.Argument("max-line-decrease", "0.5"), NumberStyles.Float, CultureInfo.InvariantCulture),
        context.MakeAbsolute(context.Directory("artifacts/coverage")));
}

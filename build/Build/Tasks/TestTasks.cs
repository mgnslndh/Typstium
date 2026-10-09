public static partial class Program
{
    // The build's own tests first (fast), then the products': unit, integration, and the timing tests last and alone,
    // because they assert wall-clock bounds that other compiles running alongside would disturb.
    static void RegisterTestTasks()
    {
        Task("BuildTests")
            .Does<BuildParameters>(parameters =>
                DotNetTest("build/Build.Cake.Extensions.Tests", new DotNetTestSettings
                {
                    PathType = DotNetTestPathType.Project,
                    Configuration = parameters.Configuration,
                }));

        Task("UnitTests")
            .IsDependentOn("Build")
            .Does<BuildParameters>(parameters => RunProductTests("tests/Typstium.UnitTests", parameters));

        Task("IntegrationTests")
            .IsDependentOn("UnitTests")
            .Does<BuildParameters>(parameters => RunProductTests("tests/Typstium.IntegrationTests", parameters));

        // Never with coverage: instrumentation would distort the timings.
        Task("TimingTests")
            .IsDependentOn("IntegrationTests")
            .Does<BuildParameters>(parameters =>
                DotNetTest("tests/Typstium.TimingTests", new DotNetTestSettings
                {
                    PathType = DotNetTestPathType.Project,
                    Configuration = parameters.Configuration,
                    NoBuild = true,
                    ArgumentCustomization = arguments => arguments.Append("--max-parallel-test-modules").Append("1"),
                }));
    }

    // With --coverage each run writes Cobertura into the raw folder under a GUID name, so target frameworks of one
    // project don't overwrite each other.
    static void RunProductTests(string project, BuildParameters parameters)
    {
        var settings = new DotNetTestSettings { PathType = DotNetTestPathType.Project, Configuration = parameters.Configuration, NoBuild = true };
        var arguments = new ProcessArgumentBuilder();
        if (parameters.Coverage)
        {
            settings.ResultsDirectory = parameters.RawCoverageDirectory;
            arguments.Append("--coverage").Append("--coverage-output-format").Append("cobertura");
        }

        DotNetTest(project, arguments, settings);
    }
}

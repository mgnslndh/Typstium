using Cake.Core;
using Cake.Core.Configuration;
using Cake.Core.IO;
using Cake.Typstium.Utilities;
using Typstium;

namespace Cake.Typstium.Executable;

/// <summary>
/// The core's locator chain with Cake's tools folder in the bundled runtime's place, after TYPST_PATH (spec cake.md §5,
/// D5): ToolPath → TYPST_PATH → CakeTools → BundledRuntime → WinGet → WellKnownLocations → PATH.
/// </summary>
internal static class TypstCakeResolver
{
    public static TypstExecutableResolver Create(ICakeEnvironment environment, ICakeConfiguration configuration, FilePath? toolPath) =>
        new(Locators(environment, configuration, toolPath));

    public static IReadOnlyList<ITypstExecutableLocator> Locators(ICakeEnvironment environment, ICakeConfiguration configuration, FilePath? toolPath)
    {
        var locators = new List<ITypstExecutableLocator>();
        if (toolPath is not null)
        {
            locators.Add(TypstExecutableLocators.ExplicitPath(CakePaths.Native(toolPath, environment)));
        }

        // The folder #tool installs into: Paths_Tools when configured, otherwise ./tools.
        var tools = configuration.GetToolPath(environment.WorkingDirectory, environment);
        locators.Add(TypstExecutableLocators.EnvironmentVariable());
        locators.Add(new CakeToolsLocator(CakePaths.Native(tools, environment), HostRuntimeIdentifier.Candidates(HostRuntimeIdentifier.Current)));
        locators.Add(TypstExecutableLocators.BundledRuntime());
        locators.Add(TypstExecutableLocators.WinGet());
        locators.Add(TypstExecutableLocators.WellKnownLocations());
        locators.Add(TypstExecutableLocators.SearchPath());
        return locators;
    }
}

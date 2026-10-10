using Cake.Core;
using Cake.Core.Diagnostics;
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
    public static TypstExecutableResolver Create(ICakeContext context, FilePath? toolPath) => new(Locators(context, toolPath));

    public static IReadOnlyList<ITypstExecutableLocator> Locators(ICakeContext context, FilePath? toolPath)
    {
        var environment = context.Environment;
        var locators = new List<ITypstExecutableLocator>();
        if (toolPath is not null)
        {
            locators.Add(TypstExecutableLocators.ExplicitPath(CakePaths.Native(toolPath, environment)));
        }

        // The folder #tool installs into: Paths_Tools when configured, otherwise ./tools.
        var tools = context.Configuration.GetToolPath(environment.WorkingDirectory, environment);
        locators.Add(TypstExecutableLocators.EnvironmentVariable());
        locators.Add(new CakeToolsLocator(
            CakePaths.Native(tools, environment),
            HostRuntimeIdentifier.Candidates(HostRuntimeIdentifier.Current),
            // What #tool registered this run, read only when the chain gets here.
            executable => context.Tools.Resolve(executable) is { } registered ? CakePaths.Native(registered, environment) : null,
            message => context.Log.Warning("{0}", message)));
        locators.Add(TypstExecutableLocators.BundledRuntime());
        locators.Add(TypstExecutableLocators.WinGet());
        locators.Add(TypstExecutableLocators.WellKnownLocations());
        locators.Add(TypstExecutableLocators.SearchPath());
        return locators;
    }
}

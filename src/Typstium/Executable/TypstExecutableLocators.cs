using Typstium.Executable;

namespace Typstium;

/// <summary>Factories for the built-in locators, to compose a custom TypstExecutableResolver.</summary>
public static class TypstExecutableLocators
{
    public static ITypstExecutableLocator ExplicitPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new ExplicitPathLocator(path, SystemLocatorEnvironment.Instance);
    }

    public static ITypstExecutableLocator EnvironmentVariable() => EnvironmentVariable("TYPST_PATH");

    public static ITypstExecutableLocator EnvironmentVariable(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new EnvironmentVariableLocator(name, SystemLocatorEnvironment.Instance);
    }

    public static ITypstExecutableLocator BundledRuntime() => new BundledRuntimeLocator(SystemLocatorEnvironment.Instance);

    public static ITypstExecutableLocator WinGet() => new WinGetLocator(SystemLocatorEnvironment.Instance);

    public static ITypstExecutableLocator WellKnownLocations() => new WellKnownLocationsLocator(SystemLocatorEnvironment.Instance);

    public static ITypstExecutableLocator SearchPath() => new SearchPathLocator(SystemLocatorEnvironment.Instance);
}

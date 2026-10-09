using Typstium.Executable;

namespace Typstium;

/// <summary>Factories for the built-in locators, to compose a custom TypstExecutableResolver.</summary>
public static class TypstExecutableLocators
{
    /// <summary>A path you configure. A relative path is resolved against the current directory when the locator runs.</summary>
    public static ITypstExecutableLocator ExplicitPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new ExplicitPathLocator(path, SystemLocatorEnvironment.Instance);
    }

    /// <summary>The <c>TYPST_PATH</c> environment variable.</summary>
    public static ITypstExecutableLocator EnvironmentVariable() => EnvironmentVariable("TYPST_PATH");

    /// <summary>The given environment variable. Its value must be a fully qualified path; relative paths are ignored.</summary>
    public static ITypstExecutableLocator EnvironmentVariable(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new EnvironmentVariableLocator(name, SystemLocatorEnvironment.Instance);
    }

    /// <summary><c>typst/&lt;rid&gt;/</c> next to the app, or <c>bin/typst/&lt;rid&gt;/</c> for classic ASP.NET, as copied by the
    /// Typstium runtime packages. On Unix it sets the execute bit if it is missing.</summary>
    public static ITypstExecutableLocator BundledRuntime() => new BundledRuntimeLocator(SystemLocatorEnvironment.Instance);

    /// <summary>WinGet's <c>Links</c> folder, then its <c>Packages</c> folder, per user and machine-wide. Windows only.</summary>
    public static ITypstExecutableLocator WinGet() => new WinGetLocator(SystemLocatorEnvironment.Instance);

    /// <summary><c>~/.cargo/bin</c>; Scoop shims on Windows; <c>/opt/homebrew/bin</c> and <c>/usr/local/bin</c> elsewhere.</summary>
    public static ITypstExecutableLocator WellKnownLocations() => new WellKnownLocationsLocator(SystemLocatorEnvironment.Instance);

    /// <summary>The <c>PATH</c> entries that are fully qualified, never the current directory. On Windows only <c>typst.exe</c>
    /// is accepted, because <c>.cmd</c> and <c>.bat</c> shims can't be started without a shell.</summary>
    public static ITypstExecutableLocator SearchPath() => new SearchPathLocator(SystemLocatorEnvironment.Instance);
}

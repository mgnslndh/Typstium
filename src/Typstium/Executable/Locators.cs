using Typstium.Utilities;

namespace Typstium.Executable;

internal sealed class ExplicitPathLocator(string path, ILocatorEnvironment environment) : ITypstExecutableLocator
{
    public string Name => "ExplicitPath";

    public ValueTask<TypstLocatorResult> LocateAsync(CancellationToken cancellationToken)
    {
        var full = Path.GetFullPath(path);
        return new(environment.FileExists(full) ? TypstLocatorResult.Found(full) : TypstLocatorResult.NotFound(new[] { full }));
    }
}

internal sealed class EnvironmentVariableLocator(string variable, ILocatorEnvironment environment) : ITypstExecutableLocator
{
    public string Name => variable;

    public ValueTask<TypstLocatorResult> LocateAsync(CancellationToken cancellationToken)
    {
        var value = environment.GetVariable(variable);
        if (string.IsNullOrWhiteSpace(value))
        {
            return new(TypstLocatorResult.NotFound(new[] { $"%{variable}% (not set)" }));
        }

        if (!FullyQualifiedPath.IsFullyQualified(value!.Trim(), environment.IsWindows))
        {
            return new(TypstLocatorResult.NotFound(new[] { $"{value} (relative paths are not allowed)" }));
        }

        var full = Path.GetFullPath(value);
        return new(environment.FileExists(full) ? TypstLocatorResult.Found(full) : TypstLocatorResult.NotFound(new[] { full }));
    }
}

/// <summary>typst/&lt;rid&gt;/ next to the app, as copied by the Typstium runtime packages.</summary>
internal sealed class BundledRuntimeLocator(ILocatorEnvironment environment) : ITypstExecutableLocator
{
    public string Name => "BundledRuntime";

    public ValueTask<TypstLocatorResult> LocateAsync(CancellationToken cancellationToken)
    {
        var executable = environment.ExecutableFileName;
        var candidates = new[]
        {
            Path.Combine(environment.BaseDirectory, "typst", environment.RuntimeIdentifier, executable),
            Path.Combine(environment.BaseDirectory, "bin", "typst", environment.RuntimeIdentifier, executable),
        };

        foreach (var candidate in candidates)
        {
            if (environment.FileExists(candidate))
            {
                environment.EnsureExecutable(candidate);
                return new(TypstLocatorResult.Found(candidate));
            }
        }

        return new(TypstLocatorResult.NotFound(candidates));
    }
}

internal sealed class WinGetLocator(ILocatorEnvironment environment) : ITypstExecutableLocator
{
    public string Name => "WinGet";

    public ValueTask<TypstLocatorResult> LocateAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsWindows)
        {
            return new(TypstLocatorResult.NotFound(new[] { "(WinGet is Windows only)" }));
        }

        var searched = new List<string>();
        var roots = new[]
        {
            environment.GetVariable("LOCALAPPDATA") is { Length: > 0 } local ? Path.Combine(local, "Microsoft", "WinGet") : null,
            environment.GetVariable("ProgramFiles") is { Length: > 0 } programFiles ? Path.Combine(programFiles, "WinGet") : null,
        };

        foreach (var root in roots.OfType<string>())
        {
            var link = Path.Combine(root, "Links", TypstExecutableFileNames.Windows);
            searched.Add(link);
            if (environment.FileExists(link))
            {
                return new(TypstLocatorResult.Found(link));
            }
        }

        foreach (var root in roots.OfType<string>())
        {
            var packages = Path.Combine(root, "Packages");
            searched.Add(Path.Combine(packages, "Typst.Typst_*", "typst-*", TypstExecutableFileNames.Windows));
            foreach (var package in environment.EnumerateDirectories(packages, "Typst.Typst_*"))
            {
                foreach (var build in environment.EnumerateDirectories(package, "typst-*"))
                {
                    var candidate = Path.Combine(build, TypstExecutableFileNames.Windows);
                    if (environment.FileExists(candidate))
                    {
                        return new(TypstLocatorResult.Found(candidate));
                    }
                }
            }
        }

        return new(TypstLocatorResult.NotFound(searched));
    }
}

internal sealed class WellKnownLocationsLocator(ILocatorEnvironment environment) : ITypstExecutableLocator
{
    public string Name => "WellKnownLocations";

    public ValueTask<TypstLocatorResult> LocateAsync(CancellationToken cancellationToken)
    {
        var candidates = new List<string>();
        var home = environment.HomeDirectory;
        if (home is not null)
        {
            candidates.Add(Path.Combine(home, ".cargo", "bin", environment.ExecutableFileName));
        }

        if (environment.IsWindows)
        {
            if (home is not null)
            {
                candidates.Add(Path.Combine(home, "scoop", "shims", TypstExecutableFileNames.Windows));
            }
        }
        else
        {
            candidates.Add("/opt/homebrew/bin/" + TypstExecutableFileNames.Unix);
            candidates.Add("/usr/local/bin/" + TypstExecutableFileNames.Unix);
        }

        var found = candidates.FirstOrDefault(environment.FileExists);
        return new(found is not null ? TypstLocatorResult.Found(found) : TypstLocatorResult.NotFound(candidates));
    }
}

/// <summary>
/// PATH, fully qualified entries only (not drive- or root-relative ones): never the current directory. On Windows only typst.exe is accepted, because
/// .cmd/.bat shims can't be started without a shell.
/// </summary>
internal sealed class SearchPathLocator(ILocatorEnvironment environment) : ITypstExecutableLocator
{
    public string Name => "PATH";

    public ValueTask<TypstLocatorResult> LocateAsync(CancellationToken cancellationToken)
    {
        var executable = environment.ExecutableFileName;
        var searched = new List<string>();
        var entries = (environment.GetVariable("PATH") ?? string.Empty).Split(Path.PathSeparator);
        foreach (var entry in entries)
        {
            var directory = entry.Trim().Trim('"');
            if (directory.Length == 0 || !FullyQualifiedPath.IsFullyQualified(directory, environment.IsWindows))
            {
                continue;
            }

            var candidate = Path.Combine(directory, executable);
            searched.Add(candidate);
            if (environment.FileExists(candidate))
            {
                return new(TypstLocatorResult.Found(candidate));
            }
        }

        return new(TypstLocatorResult.NotFound(searched.Count > 0 ? searched : new List<string> { "(PATH has no absolute entries)" }));
    }
}

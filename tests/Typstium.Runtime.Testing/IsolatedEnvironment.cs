namespace Typstium.Runtime.Testing;

/// <summary>
/// Environment variables that give one end-to-end scenario its own copy of every per-user folder the .NET CLI and NuGet
/// write to, so scenarios can run in parallel without sharing state. Shared folders aren't safe even when each tool
/// locks its own files: <c>dotnet tool install</c> and a <c>dotnet</c> run reading the same resolver cache collided on
/// Windows (PR #6).
/// </summary>
public static class IsolatedEnvironment
{
    /// <summary>The variables <see cref="Create"/> points into the scenario's root.</summary>
    public static IReadOnlyList<string> Variables { get; } =
    [
        "NUGET_PACKAGES",            // extracted packages
        "NUGET_HTTP_CACHE_PATH",     // downloaded feed responses and .nupkg files
        "NUGET_SCRATCH",             // NuGet's temporary files
        "NUGET_PLUGINS_CACHE_PATH",  // credential plugin cache
        "DOTNET_CLI_HOME",           // ~/.dotnet: tool resolver cache, first-run marker, template cache
        "TMP",                       // temp folder on Windows (also Typstium's scratch TempRoot)
        "TEMP",
        "TMPDIR",                    // temp folder on Unix
    ];

    /// <summary>
    /// <paramref name="baseEnvironment"/> with every variable in <see cref="Variables"/> set to its own folder under
    /// <paramref name="root"/> (created). The same root always gives the same folders, so a scenario can ask again.
    /// </summary>
    public static Dictionary<string, string?> Create(string root, IReadOnlyDictionary<string, string?> baseEnvironment)
    {
        var environment = new Dictionary<string, string?>(baseEnvironment);
        var temp = Path.Combine(root, "temp");
        foreach (var variable in Variables)
        {
            var folder = variable is "TMP" or "TEMP" or "TMPDIR" ? temp : Path.Combine(root, variable.ToLowerInvariant());
            Directory.CreateDirectory(folder);
            environment[variable] = folder;
        }

        return environment;
    }
}

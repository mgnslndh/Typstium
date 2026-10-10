using System.Globalization;
using Typstium.Testing;

namespace Typstium.Runtime.Testing;

/// <summary>A local package feed and the isolated NuGet cache the generated projects restore with.</summary>
public sealed record Feed(string Directory, string Version, string NuGetPackages, IReadOnlyList<string> RuntimeIdentifiers)
{
    public IReadOnlyDictionary<string, string?> Environment => new Dictionary<string, string?> { ["NUGET_PACKAGES"] = NuGetPackages };
}

/// <summary>
/// Packs Typstium and fixture Typstium.Runtime.&lt;rid&gt; packages, with a text stub instead of Typst, into
/// artifacts/runtime-tests/feed-&lt;version&gt;/, once per test session. The version is unique per session, so the
/// persistent, isolated cache (artifacts/runtime-tests/nuget-packages) never serves a stale fixture.
/// </summary>
public static class PackageFeed
{
    public static readonly string[] AllRuntimeIdentifiers = ["win-x64", "win-arm64", "osx-x64", "osx-arm64", "linux-x64", "linux-arm64", "linux-arm"];

    private static readonly Lazy<Task<Feed>> Fixture = new(CreateFixtureAsync);

    public static Task<Feed> GetFixtureAsync() => Fixture.Value;

    public static string ArtifactsRoot => Path.Combine(RepositoryPaths.Root, "artifacts", "runtime-tests");

    /// <summary>The executable's file name in a runtime package for <paramref name="rid"/>.</summary>
    public static string ExecutableName(string rid) => rid.StartsWith("win-", StringComparison.Ordinal) ? "typst.exe" : "typst";

    private static async Task<Feed> CreateFixtureAsync()
    {
        var version = "0.0.0-t" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        var directory = Path.Combine(ArtifactsRoot, "feed-" + version);
        var cache = Path.Combine(ArtifactsRoot, "nuget-packages");
        Directory.CreateDirectory(directory);
        var environment = new Dictionary<string, string?> { ["NUGET_PACKAGES"] = cache };

        await PackAsync(environment, Path.Combine("src", "Typstium", "Typstium.csproj"), "-o", directory, "-p:Version=" + version);

        foreach (var rid in AllRuntimeIdentifiers)
        {
            var source = Path.Combine(directory, "sources", rid);
            Directory.CreateDirectory(source);
            await File.WriteAllTextAsync(Path.Combine(source, ExecutableName(rid)), $"stub typst for {rid}\n");
            await File.WriteAllTextAsync(Path.Combine(source, "LICENSE"), "stub license\n");
            await PackAsync(environment, Path.Combine("src", "Typstium.Runtime", "Typstium.Runtime.csproj"),
                "-o", directory, "-p:TypstiumRuntimeIdentifier=" + rid, "-p:TypstiumRuntimeSource=" + source, "-p:TypstVersion=" + version);
        }

        await PackAsync(environment, Path.Combine("src", "Typstium.Runtime.All", "Typstium.Runtime.All.csproj"),
            "-o", directory, "-p:TypstVersion=" + version, "-p:RestoreAdditionalProjectSources=" + directory);

        return new Feed(directory, version, cache, AllRuntimeIdentifiers);
    }

    private static async Task PackAsync(IReadOnlyDictionary<string, string?> environment, string project, params string[] arguments)
    {
        var run = await DotNet.RunAsync(RepositoryPaths.Root, environment, ["pack", project, "-c", "Release", "-nodeReuse:false", .. arguments]);
        if (run.ExitCode != 0)
        {
            throw new InvalidOperationException($"dotnet pack {project} failed:\n{run.Output}");
        }
    }
}

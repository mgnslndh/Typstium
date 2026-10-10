using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Typstium.Runtime.Testing;
using Typstium.Testing;
using Typstium.Testing.Fixtures;

namespace Cake.Typstium.EndToEndTests;

/// <summary>
/// Typstium and Cake.Typstium packed once per session with a unique version, next to the real runtime packages in
/// $TYPSTIUM_RUNTIME_PACKAGES, and a fresh NuGet cache (spec cake.md §8).
/// </summary>
internal static class CakeSession
{
    private static readonly Lazy<Task<Session>> Current = new(CreateAsync);

    private static bool Required => Environment.GetEnvironmentVariable("TYPSTIUM_REQUIRE_RUNTIME_PACKAGES") == "1";

    public static string HostRid { get; } =
        (OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux") + "-" + RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();

    public static Task<Session> GetAsync() => Current.Value;

    private static async Task<Session> CreateAsync()
    {
        var packages = Environment.GetEnvironmentVariable("TYPSTIUM_RUNTIME_PACKAGES");
        var all = packages is not null && Directory.Exists(packages)
            ? Directory.EnumerateFiles(packages, "Typstium.Runtime.All.*.nupkg").SingleOrDefault()
            : null;
        if (all is null)
        {
            if (Required)
            {
                throw new InvalidOperationException($"TYPSTIUM_REQUIRE_RUNTIME_PACKAGES=1, but '{packages}' holds no Typstium.Runtime.All package.");
            }

            Skip.Test("Set TYPSTIUM_RUNTIME_PACKAGES to a folder with the packed runtime packages (./build.ps1 --target=PackTypstRuntimes).");
        }

        var runtimeVersion = Path.GetFileNameWithoutExtension(all!)["Typstium.Runtime.All.".Length..];
        var version = "0.0.0-c" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        var feed = Path.Combine(PackageFeed.ArtifactsRoot, "cake-" + version);
        var environment = new Dictionary<string, string?>
        {
            ["NUGET_PACKAGES"] = Path.Combine(PackageFeed.ArtifactsRoot, "cake-cache-" + version),
            // Every scenario starts without TYPST_PATH; CK5 sets it on purpose.
            ["TYPST_PATH"] = null,
        };
        foreach (var project in new[] { Path.Combine("src", "Typstium", "Typstium.csproj"), Path.Combine("src", "Cake.Typstium", "Cake.Typstium.csproj") })
        {
            var pack = await DotNet.RunAsync(RepositoryPaths.Root, environment, "pack", project, "-c", "Release", "-o", feed, "-p:Version=" + version, "-nodeReuse:false");
            if (pack.ExitCode != 0)
            {
                throw new InvalidOperationException($"dotnet pack {project} failed:\n{pack.Output}");
            }
        }

        // Once per session, into a folder of its own: a local tool (manifest + dotnet tool install) per scenario shares
        // the per-user ~/.dotnet/toolResolverCache, which parallel installs and runs collide on (Windows locks it).
        var cakeTool = Path.Combine(PackageFeed.ArtifactsRoot, "cake-tool-" + version);
        var install = await DotNet.RunAsync(RepositoryPaths.Root, environment, "tool", "install", "Cake.Tool", "--version", "6.3.0", "--tool-path", cakeTool);
        if (install.ExitCode != 0)
        {
            throw new InvalidOperationException("dotnet tool install Cake.Tool failed:" + Environment.NewLine + install.Output);
        }

        var cake = Path.Combine(cakeTool, OperatingSystem.IsWindows() ? "dotnet-cake.exe" : "dotnet-cake");
        return new Session(feed, version, packages!, runtimeVersion, environment, cake);
    }

    /// <summary>Extracts the host's typst from its runtime package, for CK5's TYPST_PATH.</summary>
    public static string ExtractHostTypst(Session session, TemporaryDirectory into)
    {
        var package = Path.Combine(session.RuntimePackages, $"Typstium.Runtime.{HostRid}.{session.RuntimeVersion}.nupkg");
        var name = OperatingSystem.IsWindows() ? "typst.exe" : "typst";
        var target = into.Combine("typst-path", name);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        using (var zip = ZipFile.OpenRead(package))
        {
            zip.GetEntry($"tools/typst/{HostRid}/{name}")!.ExtractToFile(target);
        }

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(target, File.GetUnixFileMode(target) | UnixFileMode.UserExecute);
        }

        return target;
    }
}

internal sealed record Session(string Feed, string Version, string RuntimePackages, string RuntimeVersion, IReadOnlyDictionary<string, string?> Environment, string CakeExecutable)
{
    /// <summary>
    /// The session's environment with the scenario's own NuGet, .NET CLI and temp folders (<see cref="IsolatedEnvironment"/>),
    /// so scenarios running in parallel share nothing. Kept outside the scenario's folder so a project there never globs it;
    /// the same folder always gets the same environment.
    /// </summary>
    public IReadOnlyDictionary<string, string?> EnvironmentFor(TemporaryDirectory folder) =>
        IsolatedEnvironment.Create(
            System.IO.Path.Combine(PackageFeed.ArtifactsRoot, $"cake-iso-{Version}-{System.IO.Path.GetFileName(folder.Path)}"), Environment);

    /// <summary>A nuget.config with the session feed, the runtime packages and nuget.org (for Cake.Tool and Cake.Sdk).</summary>
    public string NuGetConfig => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <packageSources>
            <clear />
            <add key="typstium" value="{Feed}" />
            <add key="runtimes" value="{RuntimePackages}" />
            <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
          </packageSources>
        </configuration>
        """;
}

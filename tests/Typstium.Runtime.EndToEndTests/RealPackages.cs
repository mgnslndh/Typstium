using System.Globalization;
using Typstium.Runtime.Testing;
using Typstium.Testing;
using Typstium.Testing.Fixtures;

namespace Typstium.Runtime.EndToEndTests;

/// <summary>
/// Apps that reference a freshly packed Typstium and the real runtime packages in $TYPSTIUM_RUNTIME_PACKAGES, restored
/// into a fresh cache per session (the real packages keep their version between repacks).
/// </summary>
internal static class RealPackages
{
    private static readonly Lazy<Task<Session>> Current = new(CreateSessionAsync);

    private static bool Required => Environment.GetEnvironmentVariable("TYPSTIUM_REQUIRE_RUNTIME_PACKAGES") == "1";

    public static async Task<App> CreateAppAsync(string[]? runtimes = null, bool referenceAll = false, string targetFramework = "net10.0", Dictionary<string, string>? properties = null)
    {
        var session = await Current.Value;
        var ids = (runtimes ?? []).Select(rid => "Typstium.Runtime." + rid).Concat(referenceAll ? ["Typstium.Runtime.All"] : []);
        var references = string.Concat(ids.Select(id => $"    <PackageReference Include=\"{id}\" Version=\"{session.RuntimeVersion}\" />\n"));
        var allProperties = new Dictionary<string, string>(properties ?? []) { ["OutputType"] = "Exe", ["TargetFramework"] = targetFramework, ["LangVersion"] = "latest", ["ImplicitUsings"] = "enable" };
        var propertyLines = string.Concat(allProperties.Select(property => $"    <{property.Key}>{property.Value}</{property.Key}>\n"));

        var app = new App(session.Environment);
        app.Write("App.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n" + propertyLines + "  </PropertyGroup>\n  <ItemGroup>\n" +
            $"    <PackageReference Include=\"Typstium\" Version=\"{session.Version}\" />\n" + references + "  </ItemGroup>\n</Project>\n");
        app.Write("Program.cs", ProgramSource);
        app.Write("nuget.config", $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="typstium" value="{session.Feed}" />
                <add key="runtimes" value="{session.RuntimePackages}" />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
              </packageSources>
            </configuration>
            """);
        File.Copy(Path.Combine(RepositoryPaths.Root, "global.json"), Path.Combine(app.Directory, "global.json"));
        return app;
    }

    public static async Task RequireDockerAsync()
    {
        await Current.Value;
        if (!OperatingSystem.IsLinux() || await TryRunAsync("docker", "info") != 0)
        {
            if (Required && OperatingSystem.IsLinux())
            {
                throw new InvalidOperationException("TYPSTIUM_REQUIRE_RUNTIME_PACKAGES=1, but Docker isn't available.");
            }

            Skip.Test("Container tests need Docker on Linux.");
        }
    }

    private static async Task<int> TryRunAsync(string tool, params string[] arguments)
    {
        try
        {
            return (await DotNet.RunToolAsync(tool, Path.GetTempPath(), new Dictionary<string, string?>(), arguments)).ExitCode;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return -1;
        }
    }

    private static async Task<Session> CreateSessionAsync()
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
        var version = "0.0.0-e" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        var feed = Path.Combine(PackageFeed.ArtifactsRoot, "e2e-" + version);
        var environment = new Dictionary<string, string?> { ["NUGET_PACKAGES"] = Path.Combine(PackageFeed.ArtifactsRoot, "e2e-cache-" + version) };
        var pack = await DotNet.RunAsync(RepositoryPaths.Root, environment,
            "pack", Path.Combine("src", "Typstium", "Typstium.csproj"), "-c", "Release", "-o", feed, "-p:Version=" + version, "-nodeReuse:false");
        if (pack.ExitCode != 0)
        {
            throw new InvalidOperationException("dotnet pack src/Typstium failed:\n" + pack.Output);
        }

        return new Session(feed, version, packages!, runtimeVersion, environment);
    }

    private sealed record Session(string Feed, string Version, string RuntimePackages, string RuntimeVersion, IReadOnlyDictionary<string, string?> Environment);

    private const string ProgramSource = """
        using Typstium;

        var compiler = TypstCompiler.CreateDefault();
        var result = (await compiler.CompileAsync(TypstSource.Markup("= Hello from Typstium"), TypstOutput.Pdf())).EnsureSuccess();
        var pdf = ((TypstDocument.SingleFile)result.Document!).Content;
        Console.WriteLine($"found-by={result.Invocation.ExecutableFoundBy}");
        Console.WriteLine($"path={result.Invocation.ExecutablePath}");
        Console.WriteLine($"pdf={(pdf.Length > 4 && pdf.Span[0] == (byte)'%' && pdf.Span[1] == (byte)'P')}");
        Console.WriteLine($"process64={Environment.Is64BitProcess}");

        """;
}

/// <summary>One generated app in a temp folder.</summary>
internal sealed class App(IReadOnlyDictionary<string, string?> environment) : IDisposable
{
    private readonly TemporaryDirectory _temp = new("e2e");

    public string Directory => _temp.Path;

    public void Write(string relativePath, string content) => _temp.CreateFile(relativePath, content);

    public async Task PublishAsync(params string[] arguments)
    {
        var run = await DotNet.RunAsync(Directory, environment, ["publish", "-c", "Release", "-o", "pub", "-nodeReuse:false", .. arguments]);
        if (run.ExitCode != 0)
        {
            throw new InvalidOperationException("dotnet publish failed:\n" + run.Output);
        }
    }

    /// <summary>Builds (or publishes with <paramref name="publish"/>) and runs the app; throws on a non-zero exit.</summary>
    public async Task<DotNetRun> RunAppAsync(string[]? publish, string? workingDirectory = null)
    {
        string folder;
        if (publish is null)
        {
            var build = await DotNet.RunAsync(Directory, environment, "build", "-c", "Release", "-o", "out", "-nodeReuse:false");
            if (build.ExitCode != 0)
            {
                throw new InvalidOperationException("dotnet build failed:\n" + build.Output);
            }

            folder = Path.Combine(Directory, "out");
        }
        else
        {
            await PublishAsync(publish);
            folder = Path.Combine(Directory, "pub");
        }

        // The bundled runtime must be what the locator finds, so the app runs without TYPST_PATH.
        var appEnvironment = new Dictionary<string, string?> { ["TYPST_PATH"] = null };

        // The apphost where there is one (net48, single-file, UseAppHost), otherwise dotnet App.dll.
        var apphost = Path.Combine(folder, OperatingSystem.IsWindows() ? "App.exe" : "App");
        var run = File.Exists(apphost)
            ? await DotNet.RunToolAsync(apphost, workingDirectory ?? folder, appEnvironment)
            : await DotNet.RunAsync(workingDirectory ?? folder, appEnvironment, Path.Combine(folder, "App.dll"));
        if (run.ExitCode != 0)
        {
            throw new InvalidOperationException("The app failed:\n" + run.Output);
        }

        return run;
    }

    public void Dispose() => _temp.Dispose();
}

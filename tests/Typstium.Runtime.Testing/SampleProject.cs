using System.Text;
using Typstium.Testing.Fixtures;

namespace Typstium.Runtime.Testing;

/// <summary>
/// A generated SDK-style project in a temp folder (outside the repository, so its Directory.Build.props don't apply)
/// that references the feed's Typstium and runtime packages.
/// </summary>
public sealed class SampleProject : IDisposable
{
    private readonly TemporaryDirectory _temp;
    private readonly Feed _feed;

    private SampleProject(Feed feed, string folder)
    {
        _feed = feed;
        _temp = new TemporaryDirectory("runtime");
        Directory = _temp.CreateDirectory(folder);
    }

    /// <summary>The project's folder.</summary>
    public string Directory { get; }

    public string Name { get; private init; } = "App";

    public string Sdk { get; set; } = "Microsoft.NET.Sdk";

    public string TargetFrameworks { get; set; } = "net10.0";

    public string OutputType { get; set; } = "Exe";

    public List<string> Runtimes { get; } = [];

    public bool ReferenceAll { get; set; }

    public bool ReferenceTypstium { get; set; } = true;

    public Dictionary<string, string> Properties { get; } = new(StringComparer.Ordinal) { ["UseAppHost"] = "false" };

    public List<string> ExtraItems { get; } = [];

    public static async Task<SampleProject> CreateAsync(string name = "App", string folder = "App", params string[] runtimes)
    {
        var project = new SampleProject(await PackageFeed.GetFixtureAsync(), folder) { Name = name };
        project.Runtimes.AddRange(runtimes);
        return project;
    }

    /// <summary>Writes the project, Program.cs, nuget.config and global.json.</summary>
    public SampleProject Write()
    {
        var references = new StringBuilder();
        if (ReferenceTypstium)
        {
            references.AppendLine($"""    <PackageReference Include="Typstium" Version="{_feed.Version}" />""");
        }

        foreach (var rid in Runtimes)
        {
            references.AppendLine($"""    <PackageReference Include="Typstium.Runtime.{rid}" Version="{_feed.Version}" />""");
        }

        if (ReferenceAll)
        {
            references.AppendLine($"""    <PackageReference Include="Typstium.Runtime.All" Version="{_feed.Version}" />""");
        }

        var properties = string.Concat(Properties.Select(property => $"    <{property.Key}>{property.Value}</{property.Key}>\n"));
        File.WriteAllText(Path.Combine(Directory, Name + ".csproj"), $"""
            <Project Sdk="{Sdk}">
              <PropertyGroup>
                <{(TargetFrameworks.Contains(';') ? "TargetFrameworks" : "TargetFramework")}>{TargetFrameworks}</{(TargetFrameworks.Contains(';') ? "TargetFrameworks" : "TargetFramework")}>
                <OutputType>{OutputType}</OutputType>
            {properties}  </PropertyGroup>
              <ItemGroup>
            {references}{string.Concat(ExtraItems.Select(item => "    " + item + "\n"))}  </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(Directory, "Program.cs"), OutputType is "Exe" or "WinExe" ? "System.Console.WriteLine(\"hello\");\n" : "public static class Library { }\n");
        File.WriteAllText(Path.Combine(Directory, "nuget.config"), $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="feed" value="{_feed.Directory}" />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
              </packageSources>
            </configuration>
            """);
        File.Copy(Path.Combine(Typstium.Testing.RepositoryPaths.Root, "global.json"), Path.Combine(Directory, "global.json"), overwrite: true);
        return this;
    }

    /// <summary>Runs <c>dotnet &lt;command&gt;</c> in the project folder with the feed's cache.</summary>
    public Task<DotNetRun> RunAsync(params string[] arguments) =>
        DotNet.RunAsync(Directory, _feed.Environment, [.. arguments, "-nodeReuse:false"]);

    /// <summary>The Typst-related files under <paramref name="relativeFolder"/>: binaries and the manifest, '/'-separated, sorted.</summary>
    public string[] TypstFiles(string relativeFolder)
    {
        var root = Path.Combine(Directory, relativeFolder);
        return !System.IO.Directory.Exists(root)
            ? []
            : System.IO.Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Where(file => Path.GetFileName(file) is "typst" or "typst.exe" or "typstium.runtime.json")
                .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
                .Order(StringComparer.Ordinal)
                .ToArray();
    }

    public string ReadText(string relativePath) => File.ReadAllText(Path.Combine(Directory, relativePath)).ReplaceLineEndings("\n");

    public void Dispose() => _temp.Dispose();
}

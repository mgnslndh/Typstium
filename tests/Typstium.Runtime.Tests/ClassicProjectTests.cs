using Typstium.Runtime.Testing;
using Typstium.Testing.Fixtures;

namespace Typstium.Runtime.Tests;

public class ClassicProjectTests
{
    // vswhere ships with every Visual Studio and Build Tools install since 2017.
    private static string? FindMSBuild()
    {
        var vswhere = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "Installer", "vswhere.exe");
        if (!File.Exists(vswhere))
        {
            return null;
        }

        var run = DotNet.RunToolAsync(vswhere, Path.GetTempPath(), new Dictionary<string, string?>(), "-latest", "-requires", "Microsoft.Component.MSBuild", "-find", @"MSBuild\**\Bin\MSBuild.exe").GetAwaiter().GetResult();
        return run.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault(File.Exists);
    }

    private static async Task<string> RequireMSBuildAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("Classic projects build with msbuild.exe on Windows.");
        }

        var msbuild = FindMSBuild();
        if (msbuild is null)
        {
            Skip.Test("Visual Studio or Build Tools isn't installed.");
        }

        return await Task.FromResult(msbuild!);
    }

    [Test]
    [Property(Scenario.Key, "RS18")]
    public async Task A_packages_config_project_with_direct_references_gets_its_runtime()
    {
        var msbuild = await RequireMSBuildAsync();
        var feed = await PackageFeed.GetFixtureAsync();
        using var temp = new TemporaryDirectory("classic");
        var v = feed.Version;
        temp.CreateFile("nuget.config", $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <config><add key="repositoryPath" value="packages" /></config>
              <packageSources><clear /><add key="feed" value="{feed.Directory}" /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources>
            </configuration>
            """);
        temp.CreateFile("App/packages.config", $"""
            <?xml version="1.0" encoding="utf-8"?>
            <packages>
              <package id="Typstium" version="{v}" targetFramework="net472" />
              <package id="Typstium.Runtime.win-x64" version="{v}" targetFramework="net472" />
            </packages>
            """);
        // The Import lines NuGet adds when a package is installed: props at the top, targets at the bottom.
        temp.CreateFile("App/App.csproj", $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <Import Project="..\packages\Typstium.Runtime.win-x64.{v}\build\Typstium.Runtime.win-x64.props" Condition="Exists('..\packages\Typstium.Runtime.win-x64.{v}\build\Typstium.Runtime.win-x64.props')" />
              <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" />
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>
                <OutputPath>bin\</OutputPath>
                <AssemblyName>App</AssemblyName>
              </PropertyGroup>
              <ItemGroup>
                <Reference Include="System" />
                <Compile Include="Program.cs" />
                <None Include="packages.config" />
              </ItemGroup>
              <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
              <Import Project="..\packages\Typstium.{v}\build\Typstium.targets" Condition="Exists('..\packages\Typstium.{v}\build\Typstium.targets')" />
            </Project>
            """);
        temp.CreateFile("App/Program.cs", "static class Program { static void Main() { } }\n");
        var environment = feed.Environment;

        var restore = await DotNet.RunToolAsync(msbuild, temp.Path, environment, @"App\App.csproj", "-t:restore", "-p:RestorePackagesConfig=true", "-nodeReuse:false");
        var build = await DotNet.RunToolAsync(msbuild, temp.Path, environment, @"App\App.csproj", "-p:Configuration=Release", "-nodeReuse:false");

        await Assert.That(restore.ExitCode).IsEqualTo(0).Because(restore.Output);
        await Assert.That(build.ExitCode).IsEqualTo(0).Because(build.Output);
        await Assert.That(File.Exists(temp.Combine("App", "bin", "typst", "win-x64", "typst.exe"))).IsTrue();
        await Assert.That(File.Exists(temp.Combine("App", "bin", "typstium.runtime.json"))).IsTrue();
    }

    [Test]
    [Property(Scenario.Key, "RS17")]
    public async Task A_classic_web_application_publishes_its_runtime_under_bin()
    {
        var (temp, site, published) = await PublishClassicWebAsync("");
        using var _ = temp;

        await Assert.That(File.Exists(Path.Combine(site, "bin", "typst", "win-x64", "typst.exe"))).IsTrue().Because(published);
        await Assert.That(File.Exists(Path.Combine(site, "bin", "typst", "win-x64", "LICENSE"))).IsTrue().Because(published);
        await Assert.That(File.Exists(Path.Combine(site, "bin", "typst", "win-x64", "NOTICE"))).IsTrue().Because(published);
        await Assert.That(File.Exists(Path.Combine(site, "bin", "typstium.runtime.json"))).IsTrue().Because(published);
    }

    private static async Task<(TemporaryDirectory Temp, string Site, string Published)> PublishClassicWebAsync(string extraProperties)
    {
        var msbuild = await RequireMSBuildAsync();

        // The web targets live under the same Visual Studio install as MSBuild, in a folder named for its version (v17.0, v18.0, ...).
        var extensions = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(msbuild)!)!)!, "Microsoft", "VisualStudio");
        var webTargets = Directory.Exists(extensions)
            ? Directory.EnumerateDirectories(extensions, "v*").Select(folder => Path.Combine(folder, "WebApplications", "Microsoft.WebApplication.targets")).FirstOrDefault(File.Exists)
            : null;
        if (webTargets is null)
        {
            Skip.Test("The ASP.NET web development workload isn't installed.");
        }

        var visualStudioVersion = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(webTargets!))!)[1..];
        var feed = await PackageFeed.GetFixtureAsync();
        var temp = new TemporaryDirectory("classic-web");
        var v = feed.Version;
        temp.CreateFile("nuget.config", $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources><clear /><add key="feed" value="{feed.Directory}" /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources>
            </configuration>
            """);
        temp.CreateFile("Web/Web.config", "<configuration />\n");
        temp.CreateFile("Web/Web.csproj", $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" />
              <PropertyGroup>
                <ProjectTypeGuids>{349c5851-65df-11da-9384-00065b846f21};{fae04ec0-301f-11d3-bf4b-00c04f79efbc}</ProjectTypeGuids>
                <OutputType>Library</OutputType>
                <TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>
                <OutputPath>bin\</OutputPath>
                <AssemblyName>Web</AssemblyName>
                <VisualStudioVersion>{{visualStudioVersion}}</VisualStudioVersion>
                <VSToolsPath>$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)</VSToolsPath>
            {{extraProperties}}
              </PropertyGroup>
              <ItemGroup>
                <Reference Include="System" />
                <Reference Include="System.Web" />
                <Content Include="Web.config" />
                <PackageReference Include="Typstium" Version="{{v}}" />
                <PackageReference Include="Typstium.Runtime.win-x64" Version="{{v}}" />
              </ItemGroup>
              <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
              <Import Project="$(VSToolsPath)\WebApplications\Microsoft.WebApplication.targets" />
            </Project>
            """);
        var site = temp.Combine("site");
        var environment = feed.Environment;

        var restore = await DotNet.RunToolAsync(msbuild, temp.Path, environment, @"Web\Web.csproj", "-t:restore", "-nodeReuse:false");
        var publish = await DotNet.RunToolAsync(msbuild, temp.Path, environment, @"Web\Web.csproj", "-p:Configuration=Release",
            "-t:WebPublish", "-p:WebPublishMethod=FileSystem", "-p:PublishUrl=" + site, "-nodeReuse:false");

        var published = "Published files: " + string.Join(", ", Directory.Exists(site) ? Directory.EnumerateFiles(site, "*", SearchOption.AllDirectories).Select(file => Path.GetRelativePath(site, file)) : []);
        await Assert.That(restore.ExitCode).IsEqualTo(0).Because(restore.Output);
        await Assert.That(publish.ExitCode).IsEqualTo(0).Because(publish.Output);
        return (temp, site, published);
    }
}

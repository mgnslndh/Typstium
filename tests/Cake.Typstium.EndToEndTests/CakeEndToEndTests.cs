using System.Text.RegularExpressions;
using Typstium.Runtime.Testing;
using Typstium.Testing;
using Typstium.Testing.Fixtures;

namespace Cake.Typstium.EndToEndTests;

public partial class CakeEndToEndTests
{
    private const string Document = "= Hello from Cake\n\nCompiled by Cake.Typstium.\n";
    private const string BrokenDocument = "= Broken\n\n#let x = (1, 2\n";

    private static string OtherRid => CakeSession.HostRid.StartsWith("win", StringComparison.Ordinal) ? "linux-x64" : "win-x64";

    /// <summary>A folder with nuget.config, global.json, doc.typ and a local Cake.Tool 6.3.0.</summary>
    private static async Task<TemporaryDirectory> ScriptFolderAsync(Session session, string document = Document)
    {
        var folder = new TemporaryDirectory("cake-e2e");
        folder.CreateFile("nuget.config", session.NuGetConfig);
        folder.CreateFile("doc.typ", document);
        File.Copy(Path.Combine(RepositoryPaths.Root, "global.json"), folder.Combine("global.json"));
        var environment = session.EnvironmentFor(folder);
        Expect(await DotNet.RunAsync(folder.Path, environment, "new", "tool-manifest"), "dotnet new tool-manifest");
        Expect(await DotNet.RunAsync(folder.Path, environment, "tool", "install", "Cake.Tool", "--version", "6.3.0"), "dotnet tool install Cake.Tool");
        return folder;
    }

    private static string Script(Session session, bool loadDependencies, params string[] rids) =>
        $"#addin nuget:?package=Cake.Typstium&version={session.Version}{(loadDependencies ? "&loaddependencies=true" : "")}\n" +
        string.Concat(rids.Select(rid => $"#tool nuget:?package=Typstium.Runtime.{rid}&version={session.RuntimeVersion}{Include(rid)}\n")) +
        """
        Task("Default").Does(() =>
        {
            var result = TypstCompile("doc.typ", "out/doc.pdf");
            Information("found-by={0}", result.Invocation.ExecutableFoundBy);
            Information("path={0}", result.Invocation.ExecutablePath);
        });
        RunTarget("Default");
        """;

    // Cake's #tool registers only *.exe and *.dll files; the Linux and macOS binaries have no extension.
    private static string Include(string rid) => rid.StartsWith("win", StringComparison.Ordinal) ? "" : "&include=./**/typst";

    // Cake skips an addin it can't load and says why ("Could not load …Cake.Typstium.dll (missing Typstium, Version=…)");
    // the script then fails to compile with CS0103 for the alias.
    [GeneratedRegex(@"Could not load [^\r\n]*Cake\.Typstium\.dll \(missing Typstium, Version=[\s\S]*error CS0103: The name 'TypstCompile' does not exist")]
    private static partial Regex MissingTypstiumAssembly();

    [GeneratedRegex(@"\x1b\[[0-9;]*m")]
    private static partial Regex Ansi();

    /// <summary>Cake colors its log; the assertions read plain text.</summary>
    private static DotNetRun Plain(DotNetRun run) => run with { Output = Ansi().Replace(run.Output, "") };

    private static async Task<DotNetRun> RunCakeAsync(Session session, TemporaryDirectory folder, IReadOnlyDictionary<string, string?>? extra = null)
    {
        var environment = new Dictionary<string, string?>(session.EnvironmentFor(folder));
        foreach (var (name, value) in extra ?? new Dictionary<string, string?>())
        {
            environment[name] = value;
        }

        return Plain(await DotNet.RunAsync(folder.Path, environment, "cake", "build.cake"));
    }

    private static void Expect(DotNetRun run, string what)
    {
        if (run.ExitCode != 0)
        {
            throw new InvalidOperationException($"{what} failed:\n{run.Output}");
        }
    }

    [Test]
    [Property(Scenario.Key, "CK1")]
    public async Task A_script_compiles_with_typst_from_its_tool_directive()
    {
        var session = await CakeSession.GetAsync();
        using var folder = await ScriptFolderAsync(session);
        folder.CreateFile("build.cake", Script(session, loadDependencies: true, CakeSession.HostRid));

        var run = await RunCakeAsync(session, folder);

        Expect(run, "dotnet cake");
        await Assert.That(run.Output).Contains("found-by=CakeTools");
        await Assert.That(File.ReadAllBytes(folder.Combine("out", "doc.pdf"))[..4]).IsEquivalentTo("%PDF"u8.ToArray());
    }

    [Test]
    [Property(Scenario.Key, "CK2")]
    public async Task A_script_with_several_tool_directives_picks_the_hosts_binary()
    {
        var session = await CakeSession.GetAsync();
        using var folder = await ScriptFolderAsync(session);
        folder.CreateFile("build.cake", Script(session, loadDependencies: true, OtherRid, CakeSession.HostRid));

        var run = await RunCakeAsync(session, folder);

        Expect(run, "dotnet cake");
        await Assert.That(run.Output).Contains("found-by=CakeTools");
        await Assert.That(run.Output).Contains($"{Path.DirectorySeparatorChar}typst{Path.DirectorySeparatorChar}{CakeSession.HostRid}{Path.DirectorySeparatorChar}");
    }

    [Test]
    [Property(Scenario.Key, "CK3")]
    public async Task A_script_without_loaddependencies_fails_naming_the_typstium_assembly()
    {
        var session = await CakeSession.GetAsync();
        using var folder = await ScriptFolderAsync(session);
        folder.CreateFile("build.cake", Script(session, loadDependencies: false, CakeSession.HostRid));

        var run = await RunCakeAsync(session, folder);

        // Cake echoes the #addin directive, so "Typstium" alone would always match: require the compiler's error.
        await Assert.That(run.ExitCode).IsNotEqualTo(0);
        await Assert.That(MissingTypstiumAssembly().IsMatch(run.Output)).IsTrue().Because(run.Output);
        await Assert.That(run.Output).DoesNotContain("found-by=");
    }

    [Test]
    [Property(Scenario.Key, "CK4")]
    public async Task A_cake_sdk_project_finds_typst_through_a_runtime_package_reference()
    {
        var session = await CakeSession.GetAsync();
        using var folder = new TemporaryDirectory("cake-e2e");
        folder.CreateFile("nuget.config", session.NuGetConfig);
        folder.CreateFile("doc.typ", Document);
        File.Copy(Path.Combine(RepositoryPaths.Root, "global.json"), folder.Combine("global.json"));
        folder.CreateFile("Build.csproj", $"""
            <Project Sdk="Cake.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Cake.Typstium" Version="{session.Version}" />
                <PackageReference Include="Typstium.Runtime.{CakeSession.HostRid}" Version="{session.RuntimeVersion}" />
              </ItemGroup>
            </Project>
            """);
        folder.CreateFile("Program.cs", """
            using Typstium;

            Task("Default").Does(() =>
            {
                var result = TypstCompile("doc.typ", "out/doc.pdf");
                Information("found-by={0}", result.Invocation.ExecutableFoundBy);
            });
            RunTarget("Default");
            """);

        var run = Plain(await DotNet.RunAsync(folder.Path, session.EnvironmentFor(folder), "run", "--project", "Build.csproj"));

        Expect(run, "dotnet run (Cake.Sdk)");
        await Assert.That(run.Output).Contains("found-by=BundledRuntime");
        await Assert.That(File.Exists(folder.Combine("out", "doc.pdf"))).IsTrue();
    }

    [Test]
    [Property(Scenario.Key, "CK5")]
    public async Task TYPST_PATH_wins_over_a_tool_directive()
    {
        var session = await CakeSession.GetAsync();
        using var folder = await ScriptFolderAsync(session);
        folder.CreateFile("build.cake", Script(session, loadDependencies: true, CakeSession.HostRid));
        var typst = CakeSession.ExtractHostTypst(session, folder);

        var run = await RunCakeAsync(session, folder, new Dictionary<string, string?> { ["TYPST_PATH"] = typst });

        Expect(run, "dotnet cake");
        await Assert.That(run.Output).Contains("found-by=TYPST_PATH");
    }

    [Test]
    [Property(Scenario.Key, "CK6")]
    public async Task A_document_with_an_error_fails_the_build_with_a_canonical_error_line()
    {
        var session = await CakeSession.GetAsync();
        using var folder = await ScriptFolderAsync(session, BrokenDocument);
        folder.CreateFile("build.cake", Script(session, loadDependencies: true, CakeSession.HostRid));

        var run = await RunCakeAsync(session, folder);

        await Assert.That(run.ExitCode).IsNotEqualTo(0);
        await Assert.That(run.Output).Contains("doc.typ(");
        await Assert.That(run.Output).Contains("error TYPST");
    }

    [Test]
    [Property(Scenario.Key, "CK7")]
    public async Task A_pinned_rollback_wins_over_a_higher_version_left_in_the_tools_folder()
    {
        var session = await CakeSession.GetAsync();
        using var folder = await ScriptFolderAsync(session);
        folder.CreateFile("build.cake", Script(session, loadDependencies: true, CakeSession.HostRid));
        var executable = OperatingSystem.IsWindows() ? "typst.exe" : "typst";
        folder.CreateFile($"tools/Typstium.Runtime.{CakeSession.HostRid}.99.0.0/tools/typst/{CakeSession.HostRid}/{executable}", "stale");

        var run = await RunCakeAsync(session, folder);

        Expect(run, "dotnet cake");
        await Assert.That(run.Output).Contains($"Typstium.Runtime.{CakeSession.HostRid}.{session.RuntimeVersion}{Path.DirectorySeparatorChar}");
        await Assert.That(run.Output).Contains($"{session.RuntimeVersion}, 99.0.0; using {session.RuntimeVersion}, registered by #tool");
    }
}

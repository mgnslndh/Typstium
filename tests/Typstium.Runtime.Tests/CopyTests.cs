using Typstium.Runtime.Testing;

namespace Typstium.Runtime.Tests;

public class CopyTests
{
    private static async Task<SampleProject> BuiltAsync(SampleProject project, params string[] extra)
    {
        project.Write();
        var run = await project.RunAsync(["build", "-c", "Release", "-o", "out", .. extra]);
        await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
        return project;
    }

    [Test]
    [Property(Scenario.Key, "RS1")]
    [Property(Scenario.Key, "RS27")]
    public async Task A_build_without_a_runtime_identifier_copies_every_referenced_runtime()
    {
        using var project = await BuiltAsync(await SampleProject.CreateAsync(runtimes: ["linux-x64", "win-x64"]));

        await Assert.That(project.TypstFiles("out")).IsEquivalentTo(new[] { "typst/linux-x64/typst", "typst/win-x64/typst.exe", "typstium.runtime.json" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        var manifest = System.Text.Json.JsonDocument.Parse(project.ReadText("out/typstium.runtime.json")).RootElement;
        await Assert.That(manifest.GetProperty("version").GetInt32()).IsEqualTo(1);
        await Assert.That(manifest.GetProperty("runtimes").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString())).IsEquivalentTo(
            new Dictionary<string, string?> { ["linux-x64"] = "typst/linux-x64/typst", ["win-x64"] = "typst/win-x64/typst.exe" });
    }

    [Test]
    [Property(Scenario.Key, "RS1")]
    public async Task The_meta_package_brings_all_seven_runtimes()
    {
        var project = await SampleProject.CreateAsync();
        project.ReferenceAll = true;
        using var built = await BuiltAsync(project);

        await Assert.That(built.TypstFiles("out").Count(file => file.StartsWith("typst/", StringComparison.Ordinal))).IsEqualTo(7);
    }

    [Test]
    [Property(Scenario.Key, "RS2")]
    public async Task A_portable_publish_copies_every_referenced_runtime_and_the_manifest()
    {
        using var project = await SampleProject.CreateAsync(runtimes: ["linux-x64", "osx-arm64"]);
        project.Write();
        var run = await project.RunAsync("publish", "-c", "Release", "-o", "pub");

        await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
        await Assert.That(project.TypstFiles("pub")).IsEquivalentTo(new[] { "typst/linux-x64/typst", "typst/osx-arm64/typst", "typstium.runtime.json" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Property(Scenario.Key, "RS13")]
    [Property(Scenario.Key, "RS29")]
    public async Task Turning_the_copy_off_copies_nothing_and_writes_no_manifest()
    {
        var project = await SampleProject.CreateAsync(runtimes: ["linux-x64"]);
        project.Properties["TypstiumCopyRuntime"] = "false";
        using var built = await BuiltAsync(project);

        await Assert.That(built.TypstFiles("out")).IsEmpty();
    }

    [Test]
    [Property(Scenario.Key, "RS29")]
    public async Task Copying_on_publish_only_leaves_the_build_output_without_runtimes()
    {
        var project = await SampleProject.CreateAsync(runtimes: ["linux-x64"]);
        project.Properties["TypstiumCopyRuntimeOnBuild"] = "false";
        using var built = await BuiltAsync(project);
        var publish = await built.RunAsync("publish", "-c", "Release", "-o", "pub");

        await Assert.That(publish.ExitCode).IsEqualTo(0).Because(publish.Output);
        await Assert.That(built.TypstFiles("out")).IsEmpty();
        await Assert.That(built.TypstFiles("pub")).IsEquivalentTo(new[] { "typst/linux-x64/typst", "typstium.runtime.json" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Property(Scenario.Key, "RS29")]
    public async Task Copying_on_build_only_leaves_the_publish_folder_without_runtimes()
    {
        var project = await SampleProject.CreateAsync(runtimes: ["linux-x64"]);
        project.Properties["TypstiumCopyRuntimeOnPublish"] = "false";
        project.Write();
        var publish = await project.RunAsync("publish", "-c", "Release", "-o", "pub");

        await Assert.That(publish.ExitCode).IsEqualTo(0).Because(publish.Output);
        await Assert.That(project.TypstFiles("pub")).IsEmpty();
    }

    [Test]
    [Property(Scenario.Key, "RS33")]
    public async Task Design_time_builds_copy_nothing()
    {
        using var project = await BuiltAsync(await SampleProject.CreateAsync(runtimes: ["linux-x64"]), "-p:DesignTimeBuild=true");

        await Assert.That(project.TypstFiles("out")).IsEmpty();
    }

    [Test]
    [Property(Scenario.Key, "RS34")]
    public async Task An_unchanged_runtime_is_not_copied_again_and_clean_removes_everything()
    {
        using var project = await SampleProject.CreateAsync(runtimes: ["linux-x64"]);
        project.Write();
        var first = await project.RunAsync("build", "-c", "Release");
        await Assert.That(first.ExitCode).IsEqualTo(0).Because(first.Output);
        var output = Path.Combine(project.Directory, "bin", "Release", "net10.0");
        var binary = Path.Combine(output, "typst", "linux-x64", "typst");
        File.WriteAllText(binary, "marker");
        File.SetLastWriteTimeUtc(binary, DateTime.UtcNow.AddDays(1));

        var second = await project.RunAsync("build", "-c", "Release");
        await Assert.That(second.ExitCode).IsEqualTo(0).Because(second.Output);
        await Assert.That(File.ReadAllText(binary)).IsEqualTo("marker");

        var clean = await project.RunAsync("clean", "-c", "Release");
        await Assert.That(clean.ExitCode).IsEqualTo(0).Because(clean.Output);
        await Assert.That(project.TypstFiles(Path.Combine("bin", "Release", "net10.0"))).IsEmpty();
    }

    [Test]
    [Property(Scenario.Key, "RS25")]
    public async Task An_app_gets_the_runtime_through_a_library_and_publishes_without_conflicts()
    {
        using var library = await SampleProject.CreateAsync(name: "Lib", folder: "Lib", runtimes: ["linux-x64"]);
        library.OutputType = "Library";
        library.Write();
        using var app = await SampleProject.CreateAsync();
        app.ReferenceTypstium = false;
        app.ExtraItems.Add($"""<ProjectReference Include="{Path.Combine(library.Directory, "Lib.csproj")}" />""");
        app.Write();

        var build = await app.RunAsync("build", "-c", "Release", "-o", "out");
        var publish = await app.RunAsync("publish", "-c", "Release", "-o", "pub");

        await Assert.That(build.ExitCode).IsEqualTo(0).Because(build.Output);
        await Assert.That(publish.ExitCode).IsEqualTo(0).Because(publish.Output);
        await Assert.That(app.TypstFiles("out")).IsEquivalentTo(new[] { "typst/linux-x64/typst", "typstium.runtime.json" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(app.TypstFiles("pub")).IsEquivalentTo(new[] { "typst/linux-x64/typst", "typstium.runtime.json" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Property(Scenario.Key, "RS1")]
    public async Task A_runtime_identifier_declared_by_a_package_is_copied_and_listed()
    {
        var project = await SampleProject.CreateAsync(runtimes: ["linux-x64"]);
        project.ExtraItems.Add("""<TypstiumRuntime Include="linux-riscv64" Executable="$(MSBuildProjectDirectory)/Program.cs" ContractVersion="1" />""");
        using var built = await BuiltAsync(project);

        await Assert.That(File.Exists(Path.Combine(built.Directory, "out", "typst", "linux-riscv64", "Program.cs"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(built.Directory, "out", "typst", "linux-x64", "typst"))).IsTrue();
        var runtimes = System.Text.Json.JsonDocument.Parse(built.ReadText("out/typstium.runtime.json")).RootElement.GetProperty("runtimes");
        await Assert.That(runtimes.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray()).IsEquivalentTo(new[] { "linux-riscv64", "linux-x64" });
    }

    // Typst is Apache-2.0: its LICENSE and NOTICE travel with every copy of the binary (spec D18).
    [Test]
    [Property(Scenario.Key, "RS1")]
    [Property(Scenario.Key, "RS2")]
    [Property(Scenario.Key, "RS31")]
    [Arguments("", "typst/linux-x64")]
    [Arguments("tools/{rid}", "tools/linux-x64")]
    public async Task Each_runtime_carries_typsts_license_and_notice(string directory, string expectedFolder)
    {
        using var project = await SampleProject.CreateAsync(runtimes: ["linux-x64"]);
        if (directory.Length > 0)
        {
            project.Properties["TypstiumRuntimeDirectory"] = directory;
        }

        project.Write();
        var build = await project.RunAsync("build", "-c", "Release", "-o", "out");
        var publish = await project.RunAsync("publish", "-c", "Release", "-o", "pub");

        await Assert.That(build.ExitCode).IsEqualTo(0).Because(build.Output);
        await Assert.That(publish.ExitCode).IsEqualTo(0).Because(publish.Output);
        foreach (var folder in new[] { "out", "pub" })
        {
            var runtime = Path.Combine(project.Directory, folder, expectedFolder);
            await Assert.That(Directory.EnumerateFiles(runtime).Select(file => Path.GetFileName(file)!).Order(StringComparer.Ordinal).ToArray())
                .IsEquivalentTo(new[] { "LICENSE", "NOTICE", "typst" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
    }
}

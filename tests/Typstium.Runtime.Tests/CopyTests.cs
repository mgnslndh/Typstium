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
        await Assert.That(project.ReadText("out/typstium.runtime.json")).IsEqualTo("""
            {
              "version": 1,
              "runtimes": {
                "linux-x64": "typst/linux-x64/typst",
                "win-x64": "typst/win-x64/typst.exe"
              }
            }

            """);
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
        await project.RunAsync("build", "-c", "Release");
        var output = Path.Combine(project.Directory, "bin", "Release", "net10.0");
        var binary = Path.Combine(output, "typst", "linux-x64", "typst");
        File.WriteAllText(binary, "marker");
        File.SetLastWriteTimeUtc(binary, DateTime.UtcNow.AddDays(1));

        await project.RunAsync("build", "-c", "Release");
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
}

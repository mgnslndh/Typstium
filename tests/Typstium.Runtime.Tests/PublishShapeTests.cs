using System.Runtime.InteropServices;
using Typstium.Runtime.Testing;

namespace Typstium.Runtime.Tests;

public class PublishShapeTests
{
    // The machine's own RID, for publishes that need its runtime pack or apphost.
    private static string HostRid =>
        (OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux") + "-" +
        RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();

    [Test]
    [Property(Scenario.Key, "RS4")]
    public async Task A_single_file_publish_keeps_the_binary_and_the_manifest_next_to_the_app()
    {
        using var project = await SampleProject.CreateAsync(runtimes: [HostRid]);
        project.Properties["UseAppHost"] = "true";
        project.Write();
        var run = await project.RunAsync("publish", "-c", "Release", "-r", HostRid, "--self-contained", "false", "-p:PublishSingleFile=true", "-o", "pub");

        var executable = PackageFeed.ExecutableName(HostRid);
        await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
        await Assert.That(project.TypstFiles("pub")).IsEquivalentTo(new[] { $"typst/{HostRid}/{executable}", "typstium.runtime.json" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Property(Scenario.Key, "RS5")]
    public async Task A_trimmed_self_contained_publish_keeps_the_binary()
    {
        using var project = await SampleProject.CreateAsync(runtimes: [HostRid]);
        project.Properties["UseAppHost"] = "true";
        project.Write();
        var run = await project.RunAsync("publish", "-c", "Release", "-r", HostRid, "--self-contained", "true", "-p:PublishTrimmed=true", "-o", "pub");

        await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
        await Assert.That(project.TypstFiles("pub")).Contains($"typst/{HostRid}/{PackageFeed.ExecutableName(HostRid)}");
    }

    [Test]
    [Property(Scenario.Key, "RS6")]
    public async Task Each_target_framework_gets_its_own_copy()
    {
        using var project = await SampleProject.CreateAsync(runtimes: ["linux-x64"]);
        project.TargetFrameworks = "net10.0;net48";
        // net48 executables get an inferred RuntimeIdentifier (win-x86), which would select no linux-x64 runtime; an explicit list wins.
        project.Properties["TypstiumRuntimeIdentifiers"] = "linux-x64";
        // The sample uses top-level statements, which need a newer language version than net48's default.
        project.Properties["LangVersion"] = "latest";
        project.Write();
        var run = await project.RunAsync("build", "-c", "Release");

        await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
        await Assert.That(project.TypstFiles("bin/Release/net10.0")).Contains("typst/linux-x64/typst");
        await Assert.That(project.TypstFiles("bin/Release/net48")).Contains("typst/linux-x64/typst");
    }
}

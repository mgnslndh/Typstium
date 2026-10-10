using Typstium.Runtime.Testing;

namespace Typstium.Runtime.Tests;

public class SelectionTests
{
    private static async Task<(SampleProject Project, DotNetRun Run)> PublishAsync(string rid, string[] runtimes, params string[] extra)
    {
        var project = await SampleProject.CreateAsync(runtimes: runtimes);
        project.Write();
        var run = await project.RunAsync(["publish", "-c", "Release", "-r", rid, "--self-contained", "false", "-o", "pub", .. extra]);
        return (project, run);
    }

    private static string[] Typst(params string[] paths) => [.. paths, "typstium.runtime.json"];

    [Test]
    [Property(Scenario.Key, "RS3")]
    [Arguments("linux-x64", "typst/linux-x64/typst")]
    [Arguments("win-arm64", "typst/win-arm64/typst.exe")]
    [Arguments("osx-arm64", "typst/osx-arm64/typst")]
    public async Task A_publish_for_a_runtime_identifier_copies_only_its_runtime(string rid, string expected)
    {
        var (project, run) = await PublishAsync(rid, ["linux-x64", "win-arm64", "osx-arm64"]);
        using (project)
        {
            await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
            await Assert.That(project.TypstFiles("pub")).IsEquivalentTo(Typst(expected), TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
    }

    [Test]
    [Property(Scenario.Key, "RS3")]
    [Property(Scenario.Key, "RS11")]
    [Arguments("linux-musl-x64", "typst/linux-x64/typst")]
    [Arguments("linux-musl-arm64", "typst/linux-arm64/typst")]
    [Arguments("ubuntu.22.04-x64", "typst/linux-x64/typst")]
    [Arguments("win10-x64", "typst/win-x64/typst.exe")]
    [Arguments("osx.13-arm64", "typst/osx-arm64/typst")]
    public async Task Musl_distro_and_versioned_runtime_identifiers_map_to_the_shipped_ones(string rid, string expected)
    {
        var (project, run) = await PublishAsync(rid, ["linux-x64", "linux-arm64", "win-x64", "osx-arm64"], "-p:UseRidGraph=true");
        using (project)
        {
            await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
            await Assert.That(project.TypstFiles("pub")).IsEquivalentTo(Typst(expected), TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
    }

    [Test]
    [Property(Scenario.Key, "RS40")]
    public async Task A_32_bit_windows_app_gets_every_windows_runtime()
    {
        var (project, run) = await PublishAsync("win-x86", ["win-x64", "win-arm64", "linux-x64"]);
        using (project)
        {
            await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
            await Assert.That(project.TypstFiles("pub")).IsEquivalentTo(Typst("typst/win-arm64/typst.exe", "typst/win-x64/typst.exe"), TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
    }

    [Test]
    [Property(Scenario.Key, "RS3")]
    public async Task No_matching_runtime_warns_and_copies_nothing()
    {
        var (project, run) = await PublishAsync("linux-x64", ["win-x64"]);
        using (project)
        {
            await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
            await Assert.That(run.DiagnosticCodes).Contains("TYPSTIUM0001");
            await Assert.That(project.TypstFiles("pub")).IsEmpty();
        }
    }

    [Test]
    [Property(Scenario.Key, "RS7")]
    [Property(Scenario.Key, "RS8")]
    public async Task A_runtime_identifier_in_the_project_filters_a_plain_build()
    {
        using var project = await SampleProject.CreateAsync(runtimes: ["linux-x64", "win-x64"]);
        project.Properties["RuntimeIdentifier"] = "win-x64";
        project.Properties["SelfContained"] = "false";
        project.Write();
        var run = await project.RunAsync("build", "-c", "Release", "-o", "out");

        await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
        await Assert.That(project.TypstFiles("out")).IsEquivalentTo(Typst("typst/win-x64/typst.exe"), TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Property(Scenario.Key, "RS30")]
    public async Task An_explicit_list_overrides_the_runtime_identifier_and_warns_about_unknown_names()
    {
        var (project, run) = await PublishAsync("linux-x64", ["linux-x64", "win-x64", "osx-arm64"], "-p:TypstiumRuntimeIdentifiers=win-x64%3Bosx-arm64%3Blinux-riscv64");
        using (project)
        {
            await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
            await Assert.That(run.DiagnosticCodes).Contains("TYPSTIUM0004");
            await Assert.That(run.Output).Contains("linux-riscv64");
            await Assert.That(project.TypstFiles("pub")).IsEquivalentTo(Typst("typst/osx-arm64/typst", "typst/win-x64/typst.exe"), TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
    }

    [Test]
    public async Task Runtime_identifiers_are_matched_ignoring_case()
    {
        var (project, run) = await PublishAsync("Linux-X64", ["linux-x64", "win-x64"], "-p:TypstiumRuntimeIdentifiers=WIN-X64");
        using (project)
        {
            await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
            await Assert.That(project.TypstFiles("pub")).IsEquivalentTo(Typst("typst/win-x64/typst.exe"), TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }

        var (second, secondRun) = await PublishAsync("Linux-X64", ["linux-x64", "win-x64"]);
        using (second)
        {
            await Assert.That(secondRun.ExitCode).IsEqualTo(0).Because(secondRun.Output);
            await Assert.That(second.TypstFiles("pub")).IsEquivalentTo(Typst("typst/linux-x64/typst"), TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
    }

    [Test]
    public async Task Republishing_into_the_same_folder_rewrites_the_manifest_for_the_last_runtime()
    {
        using var project = await SampleProject.CreateAsync(runtimes: ["linux-x64", "win-x64"]);
        project.Write();
        foreach (var rid in new[] { "linux-x64", "win-x64", "linux-x64" })
        {
            var run = await project.RunAsync("publish", "-c", "Release", "-r", rid, "--self-contained", "false", "-o", "pub");
            await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
        }

        await Assert.That(project.ReadText("pub/typstium.runtime.json")).Contains("\"linux-x64\": \"typst/linux-x64/typst\"");
        await Assert.That(project.ReadText("pub/typstium.runtime.json")).DoesNotContain("win-x64");
    }

    [Test]
    public async Task An_unknown_contract_version_warns_and_the_runtime_is_ignored()
    {
        using var project = await SampleProject.CreateAsync(runtimes: ["linux-x64"]);
        project.ExtraItems.Add("""<TypstiumRuntime Include="win-x64" Executable="$(MSBuildProjectDirectory)/Program.cs" ContractVersion="2" />""");
        project.Write();
        var run = await project.RunAsync("build", "-c", "Release", "-o", "out");

        await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
        await Assert.That(run.DiagnosticCodes).Contains("TYPSTIUM0005");
        await Assert.That(project.TypstFiles("out")).IsEquivalentTo(Typst("typst/linux-x64/typst"), TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }
}

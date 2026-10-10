using Typstium.Runtime.Testing;

namespace Typstium.Runtime.Tests;

public class DirectoryTests
{
    private static async Task<(SampleProject Project, DotNetRun Run)> BuildAsync(string directory, params string[] runtimes)
    {
        var project = await SampleProject.CreateAsync(runtimes: runtimes);
        project.Properties["TypstiumRuntimeDirectory"] = directory;
        project.Write();
        return (project, await project.RunAsync("build", "-c", "Release", "-o", "out"));
    }

    [Test]
    [Property(Scenario.Key, "RS31")]
    public async Task A_custom_directory_is_used_and_described_in_the_manifest()
    {
        var (project, run) = await BuildAsync("tools/typst-{rid}", "linux-x64", "win-x64");
        using (project)
        {
            await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
            await Assert.That(project.TypstFiles("out")).IsEquivalentTo(new[] { "tools/typst-linux-x64/typst", "tools/typst-win-x64/typst.exe", "typstium.runtime.json" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(project.ReadText("out/typstium.runtime.json")).Contains("\"win-x64\": \"tools/typst-win-x64/typst.exe\"");
        }
    }

    [Test]
    [Property(Scenario.Key, "RS32")]
    public async Task A_pattern_without_rid_gives_a_flat_layout_for_one_runtime()
    {
        var (project, run) = await BuildAsync("tools", "linux-x64");
        using (project)
        {
            await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
            await Assert.That(project.TypstFiles("out")).IsEquivalentTo(new[] { "tools/typst", "typstium.runtime.json" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
    }

    [Test]
    [Property(Scenario.Key, "RS32")]
    public async Task A_pattern_without_rid_and_two_runtimes_is_an_error()
    {
        var (project, run) = await BuildAsync("tools", "linux-x64", "osx-x64");
        using (project)
        {
            await Assert.That(run.ExitCode).IsNotEqualTo(0);
            await Assert.That(run.DiagnosticCodes).Contains("TYPSTIUM0002");
        }
    }

    [Test]
    [Property(Scenario.Key, "RS31")]
    [Arguments("/opt/typst/{rid}")]
    [Arguments("C:/typst/{rid}")]
    [Arguments("C:typst/{rid}")]
    [Arguments("../typst/{rid}")]
    [Arguments("tools/../../{rid}")]
    [Arguments("tools/\"{rid}")]
    public async Task Directories_outside_the_output_folder_are_an_error(string directory)
    {
        var (project, run) = await BuildAsync(directory, "linux-x64");
        using (project)
        {
            await Assert.That(run.ExitCode).IsNotEqualTo(0);
            await Assert.That(run.DiagnosticCodes).Contains("TYPSTIUM0003");
        }
    }

    [Test]
    public async Task A_directory_with_backslashes_is_written_with_forward_slashes()
    {
        var (project, run) = await BuildAsync(@"tools\typst\{rid}\", "win-x64");
        using (project)
        {
            await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
            await Assert.That(project.ReadText("out/typstium.runtime.json")).Contains("\"win-x64\": \"tools/typst/win-x64/typst.exe\"");
            await Assert.That(project.TypstFiles("out")).Contains("tools/typst/win-x64/typst.exe");
        }
    }
}

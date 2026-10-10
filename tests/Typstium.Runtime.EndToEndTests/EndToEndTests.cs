using System.Runtime.InteropServices;
using Typstium.Runtime.Testing;

namespace Typstium.Runtime.EndToEndTests;

public class EndToEndTests
{
    private static string HostRid =>
        (OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux") + "-" + RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();

    [Test]
    [Property(Scenario.Key, "RS1")]
    [Property(Scenario.Key, "RS27")]
    public async Task An_app_referencing_every_runtime_compiles_with_the_native_binary()
    {
        using var app = await RealPackages.CreateAppAsync(referenceAll: true);
        var run = await app.RunAppAsync(publish: null);

        await Assert.That(run.Output).Contains("found-by=BundledRuntime");
        await Assert.That(run.Output).Contains($"typst{Path.DirectorySeparatorChar}{HostRid}{Path.DirectorySeparatorChar}");
        await Assert.That(run.Output).Contains("pdf=True");
    }

    [Test]
    [Property(Scenario.Key, "RS4")]
    public async Task A_single_file_app_compiles_with_the_binary_next_to_it()
    {
        using var app = await RealPackages.CreateAppAsync(runtimes: [HostRid]);
        var run = await app.RunAppAsync(publish: ["-r", HostRid, "--self-contained", "false", "-p:PublishSingleFile=true"]);

        await Assert.That(run.Output).Contains("found-by=BundledRuntime");
        await Assert.That(run.Output).Contains("pdf=True");
    }

    [Test]
    [Property(Scenario.Key, "RS31")]
    public async Task A_custom_directory_is_found_through_the_manifest_from_another_working_directory()
    {
        using var app = await RealPackages.CreateAppAsync(runtimes: [HostRid], properties: new() { ["TypstiumRuntimeDirectory"] = "tools/typst-{rid}" });
        var run = await app.RunAppAsync(publish: null, workingDirectory: Path.GetTempPath());

        await Assert.That(run.Output).Contains("found-by=BundledRuntime");
        await Assert.That(run.Output).Contains($"tools{Path.DirectorySeparatorChar}typst-{HostRid}");
    }

    [Test]
    [Property(Scenario.Key, "RS19")]
    public async Task A_32_bit_net_framework_app_runs_the_x64_binary()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test(".NET Framework runs on Windows only.");
            return;
        }

        using var app = await RealPackages.CreateAppAsync(runtimes: ["win-x64"], targetFramework: "net48",
            properties: new() { ["PlatformTarget"] = "AnyCPU", ["Prefer32Bit"] = "true", ["UseAppHost"] = "true" });
        var run = await app.RunAppAsync(publish: null);

        await Assert.That(run.Output).Contains("process64=False");
        await Assert.That(run.Output).Contains($"typst{Path.DirectorySeparatorChar}win-x64");
        await Assert.That(run.Output).Contains("pdf=True");
    }

    [Test]
    [Property(Scenario.Key, "RS41")]
    public async Task An_arm_machine_runs_the_x64_binary_under_emulation_when_only_it_is_referenced()
    {
        var emulated = HostRid switch { "osx-arm64" => "osx-x64", "win-arm64" => "win-x64", _ => null };
        if (emulated is null)
        {
            Skip.Test("Needs an Arm64 Mac (Rosetta) or Arm64 Windows.");
            return;
        }

        using var app = await RealPackages.CreateAppAsync(runtimes: [emulated]);
        var run = await app.RunAppAsync(publish: null);

        await Assert.That(run.Output).Contains($"typst{Path.DirectorySeparatorChar}{emulated}");
        await Assert.That(run.Output).Contains("pdf=True");
    }
}

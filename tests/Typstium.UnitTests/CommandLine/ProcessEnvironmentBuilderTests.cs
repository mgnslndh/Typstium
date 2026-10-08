using System.Diagnostics;
using Typstium.CommandLine;

namespace Typstium.UnitTests.CommandLine;

public class ProcessEnvironmentBuilderTests
{
    private static readonly string[] Inherited =
        ["PATH", "TYPST_FONT_PATHS", "typst_root", "https_proxy", "NO_PROXY", "SOURCE_DATE_EPOCH", "HOME"];

    [Test]
    public async Task Typst_variables_are_removed_and_the_network_is_blocked_by_default()
    {
        var changes = ProcessEnvironmentBuilder.Build(Inherited, TypstCompileOptions.Default);

        await Assert.That(changes.Remove).IsEquivalentTo(new[] { "TYPST_FONT_PATHS", "typst_root", "https_proxy", "NO_PROXY" });
        await Assert.That(changes.Set["HTTPS_PROXY"]).IsEqualTo(ProcessEnvironmentBuilder.DeadProxy);
        await Assert.That(changes.Set["HTTP_PROXY"]).IsEqualTo(ProcessEnvironmentBuilder.DeadProxy);
        await Assert.That(changes.Set["ALL_PROXY"]).IsEqualTo(ProcessEnvironmentBuilder.DeadProxy);
    }

    [Test]
    public async Task Allowed_network_keeps_proxies_and_now_removes_source_date_epoch()
    {
        var options = TypstCompileOptions.Default with
        {
            Packages = TypstPackageOptions.Default with { NetworkAccess = TypstNetworkAccess.Allowed },
            CreationTimestamp = TypstCreationTimestamp.Now,
        };
        var changes = ProcessEnvironmentBuilder.Build(Inherited, options);

        await Assert.That(changes.Remove).IsEquivalentTo(new[] { "TYPST_FONT_PATHS", "typst_root", "SOURCE_DATE_EPOCH" });
        await Assert.That(changes.Set.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Probe_environment_only_removes_typst_variables()
    {
        var changes = ProcessEnvironmentBuilder.BuildForProbe(Inherited);
        await Assert.That(changes.Remove).IsEquivalentTo(new[] { "TYPST_FONT_PATHS", "typst_root" });
        await Assert.That(changes.Set.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ApplyTo_removes_then_sets()
    {
        var startInfo = new ProcessStartInfo("x");
        startInfo.Environment["TYPST_ROOT"] = "/somewhere";
        var changes = ProcessEnvironmentBuilder.Build(new[] { "TYPST_ROOT" }, TypstCompileOptions.Default);
        changes.ApplyTo(startInfo);

        await Assert.That(startInfo.Environment.ContainsKey("TYPST_ROOT")).IsFalse();
        await Assert.That(startInfo.Environment["HTTPS_PROXY"]).IsEqualTo(ProcessEnvironmentBuilder.DeadProxy);
    }
}

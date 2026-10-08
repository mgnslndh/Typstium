using System.Diagnostics;
using System.Runtime.InteropServices;
using Typstium.Testing.Fixtures;
using Typstium.Testing.Typst;

namespace Typstium.IntegrationTests;

public class LimitsTests
{
    private const string NestedLoops = "#let n = 0\n#for i in range(100000) { for j in range(100000) { n += 1 } }\n#n\n";

    private static bool NoWorkspaces(string tempRoot) => Directory.GetDirectories(tempRoot, "ws-*").Length == 0;

    [Test]
    public async Task Heavy_templates_time_out()
    {
        using var temp = new TemporaryDirectory("it-limits");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path });
        var stopwatch = Stopwatch.StartNew();

        var result = await compiler.CompileMarkupAsync(NestedLoops, TypstOutput.Pdf(), TypstCompileOptions.Default with { Timeout = TimeSpan.FromSeconds(2) });

        await Assert.That(result.Status).IsEqualTo(TypstCompileStatus.TimedOut);
        await Assert.That(result.Diagnostics.Last().Id.Value).IsEqualTo("TYPST9002");
        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(15));
        await Assert.That(NoWorkspaces(temp.Path)).IsTrue();
    }

    [Test]
    public async Task Huge_output_hits_the_output_limit()
    {
        using var temp = new TemporaryDirectory("it-limits");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path });

        var result = await compiler.CompileMarkupAsync(
            "#for i in range(20000) [Page #i #pagebreak()]",
            TypstOutput.Pdf(),
            TypstCompileOptions.Default with { MaxOutputBytes = 1024 * 1024 });

        await Assert.That(result.Status).IsEqualTo(TypstCompileStatus.OutputLimitExceeded);
    }

    [Test]
    public async Task Caller_cancellation_stops_typst_and_cleans_up()
    {
        using var temp = new TemporaryDirectory("it-limits");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAsync<OperationCanceledException>(() => compiler.CompileMarkupAsync(NestedLoops, cancellation.Token));

        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(10));
        await Assert.That(NoWorkspaces(temp.Path)).IsTrue();
    }

    [Test]
    public async Task Windows_job_object_memory_cap_stops_memory_hungry_templates()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Skip.Test("The memory cap uses Windows job objects.");
        }

        using var temp = new TemporaryDirectory("it-limits");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path });
        var options = TypstCompileOptions.Default with { MemoryLimitBytes = 100L * 1024 * 1024, Timeout = TimeSpan.FromSeconds(30) };

        TypstCompileResult? result = null;
        try
        {
            result = await compiler.CompileMarkupAsync("#let a = range(50000000)\n#a.len()", TypstOutput.Pdf(), options);
        }
        catch (TypstProcessCrashedException)
        {
            // Expected: allocation fails under the cap and Typst aborts.
        }

        await Assert.That(result is null || !result.IsSuccess).IsTrue();
    }
}

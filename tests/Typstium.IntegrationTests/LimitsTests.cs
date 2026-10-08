using System.Diagnostics;
using System.Runtime.InteropServices;
using Typstium.Testing.Fixtures;
using Typstium.Testing.Typst;

namespace Typstium.IntegrationTests;

public class LimitsTests
{
    private const string NestedLoops = "#let n = 0\n#for i in range(100000) { for j in range(100000) { n += 1 } }\n#n\n";

    private static bool NoWorkspaces(string tempRoot) => Directory.GetDirectories(tempRoot, "ws-*").Length == 0;

    // [Timeout] catches a kill that never happens (the compile would hang); the duration assertions catch a slow kill.
    [Test]
    [Timeout(60_000)]
    public async Task Heavy_templates_time_out(CancellationToken cancellationToken)
    {
        using var temp = new TemporaryDirectory("it-limits");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path });
        var timeout = TimeSpan.FromSeconds(2);

        var result = await compiler.CompileMarkupAsync(NestedLoops, TypstOutput.Pdf(), TypstCompileOptions.Default with { Timeout = timeout }, cancellationToken);

        await Assert.That(result.Status).IsEqualTo(TypstCompileStatus.TimedOut);
        await Assert.That(result.Diagnostics.Last().Id.Value).IsEqualTo("TYPST9002");

        // ProcessDuration runs from process start to exit, so start-up and probing outside the process don't count.
        var invocation = result.Invocation;
        var timings = $"process {invocation.ProcessDuration.TotalMilliseconds:F0} ms, total {invocation.TotalDuration.TotalMilliseconds:F0} ms";
        await Assert.That(invocation.ProcessDuration).IsGreaterThanOrEqualTo(timeout).Because(timings);
        await Assert.That(invocation.ProcessDuration).IsLessThan(timeout + TimeSpan.FromSeconds(5)).Because(timings);
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
    [Timeout(60_000)]
    public async Task Caller_cancellation_stops_typst_and_cleans_up(CancellationToken cancellationToken)
    {
        using var temp = new TemporaryDirectory("it-limits");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path });
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        var cancelledAt = TimeSpan.Zero;
        using var registration = cancellation.Token.Register(() => cancelledAt = stopwatch.Elapsed);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(500));

        await Assert.ThrowsAsync<OperationCanceledException>(() => compiler.CompileMarkupAsync(NestedLoops, cancellation.Token));

        // Measured from the cancel, so a slow start before it doesn't count.
        var stopping = stopwatch.Elapsed - cancelledAt;
        await Assert.That(stopping).IsLessThan(TimeSpan.FromSeconds(5)).Because($"stopped {stopping.TotalMilliseconds:F0} ms after the cancel");
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

using System.Diagnostics;
using Typstium.Testing.Fixtures;
using Typstium.Testing.Typst;

namespace Typstium.TimingTests;

/// <summary>Typst must stop promptly when a compile times out or is cancelled, even with every concurrency slot busy.</summary>
public class TimeoutAndCancellationTests
{
    private const string NestedLoops = "#let n = 0\n#for i in range(100000) { for j in range(100000) { n += 1 } }\n#n\n";

    private static bool NoWorkspaces(string tempRoot) => Directory.GetDirectories(tempRoot, "ws-*").Length == 0;

    // [Timeout] catches a kill that never happens (the compile would hang); the duration assertions catch a slow kill.
    [Test]
    [Timeout(60_000)]
    public async Task Heavy_templates_time_out(CancellationToken cancellationToken)
    {
        using var temp = new TemporaryDirectory("timing");
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

    // Each running compile must not hold thread-pool threads while Typst runs: if it did, a full set of
    // concurrent compiles would starve the pool, and the timer that kills Typst would fire seconds late.
    [Test]
    [Timeout(120_000)]
    public async Task Timeouts_fire_on_time_when_every_concurrency_slot_is_busy(CancellationToken cancellationToken)
    {
        using var temp = new TemporaryDirectory("timing");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path });
        var timeout = TimeSpan.FromSeconds(2);
        var options = TypstCompileOptions.Default with { Timeout = timeout };

        var results = await Task.WhenAll(Enumerable.Range(0, Environment.ProcessorCount * 2)
            .Select(_ => compiler.CompileMarkupAsync(NestedLoops, TypstOutput.Pdf(), options, cancellationToken)));

        var durations = string.Join(", ", results.Select(result => $"{result.Invocation.ProcessDuration.TotalMilliseconds:F0}"));
        await Assert.That(results.All(result => result.Status == TypstCompileStatus.TimedOut)).IsTrue().Because($"statuses: {string.Join(", ", results.Select(r => r.Status))}");
        await Assert.That(results.Max(result => result.Invocation.ProcessDuration)).IsLessThan(timeout + TimeSpan.FromSeconds(5)).Because($"process ms: {durations}");
    }

    [Test]
    [Timeout(60_000)]
    public async Task Caller_cancellation_stops_typst_and_cleans_up(CancellationToken cancellationToken)
    {
        using var temp = new TemporaryDirectory("timing");
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
}

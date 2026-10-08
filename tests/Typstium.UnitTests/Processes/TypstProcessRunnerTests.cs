using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Typstium.Processes;
using Typstium.UnitTests.Testing;

namespace Typstium.UnitTests.Processes;

public class TypstProcessRunnerTests
{
    private readonly TypstProcessRunner _runner = new();

    [Test]
    public async Task Exit_code_stdout_and_stderr_are_captured()
    {
        var outcome = await _runner.RunAsync(ShellCommands.Spec(ShellCommands.Run("echo hello& echo oops 1>&2& exit 3", "echo hello; echo oops 1>&2; exit 3")), CancellationToken.None);

        var exited = (ProcessOutcome.Exited)outcome;
        await Assert.That(exited.ExitCode).IsEqualTo(3);
        await Assert.That(Encoding.UTF8.GetString(exited.StandardOutput.ToArray()).Trim()).IsEqualTo("hello");
        await Assert.That(exited.StandardError).Contains("oops");
    }

    [Test]
    public async Task Stdin_reaches_the_process()
    {
        var spec = ShellCommands.Spec(ShellCommands.Run("more", "cat"), stdin: Encoding.UTF8.GetBytes("from stdin\n"));
        var outcome = (ProcessOutcome.Exited)await _runner.RunAsync(spec, CancellationToken.None);
        await Assert.That(Encoding.UTF8.GetString(outcome.StandardOutput.ToArray())).Contains("from stdin");
    }

    [Test]
    public async Task Timeout_kills_the_process_and_reports_timed_out()
    {
        var stopwatch = Stopwatch.StartNew();
        var outcome = await _runner.RunAsync(ShellCommands.Spec(ShellCommands.Sleep30(), timeout: TimeSpan.FromMilliseconds(500)), CancellationToken.None);
        await Assert.That(outcome is ProcessOutcome.TimedOut).IsTrue();
        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task Output_limit_kills_the_process()
    {
        var endless = ShellCommands.Run("for /L %i in (1,0,2) do @echo yyyyyyyyyyyyyyyyyyyyyyyyyyyyyy", "yes");
        var outcome = await _runner.RunAsync(ShellCommands.Spec(endless, maxStdoutBytes: 64 * 1024), CancellationToken.None);
        var exceeded = outcome as ProcessOutcome.OutputLimitExceeded;
        await Assert.That(exceeded).IsNotNull();
        await Assert.That(exceeded!.LimitBytes).IsEqualTo(64L * 1024);
    }

    [Test]
    public async Task Caller_cancellation_throws_after_killing_the_process()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var stopwatch = Stopwatch.StartNew();
        await Assert.ThrowsAsync<OperationCanceledException>(() => _runner.RunAsync(ShellCommands.Spec(ShellCommands.Sleep30()), cancellation.Token));
        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task A_missing_executable_is_a_start_failure()
    {
        var spec = ShellCommands.Spec(TypstArguments.Of("compile")) with { Executable = Path.Combine(Path.GetTempPath(), "no-such-typst-" + Guid.NewGuid().ToString("N")) };
        var exception = await Assert.ThrowsAsync<TypstProcessStartException>(() => _runner.RunAsync(spec, CancellationToken.None));
        await Assert.That(exception!.ExecutablePath).IsEqualTo(spec.Executable);
    }

    [Test]
    public async Task Stderr_is_capped_with_a_marker()
    {
        var text = StreamReading.ReadTextCapped(new MemoryStream(Encoding.UTF8.GetBytes(new string('e', 100))), maxBytes: 10);
        await Assert.That(text).StartsWith("eeeeeeeeee");
        await Assert.That(text).Contains(StreamReading.TruncationMarker);
    }

    [Test]
    public async Task A_process_that_exits_before_attach_still_yields_its_outcome()
    {
        var runner = new TypstProcessRunner(_ => new ExitedBeforeAttachContainment());
        var outcome = await runner.RunAsync(ShellCommands.Spec(ShellCommands.Run("exit 0", "exit 0")), CancellationToken.None);
        var exited = outcome as ProcessOutcome.Exited;
        await Assert.That(exited).IsNotNull();
        await Assert.That(exited!.ExitCode).IsEqualTo(0);
    }

    private sealed class ExitedBeforeAttachContainment : IProcessContainment
    {
        public void Attach(Process process)
        {
            process.WaitForExit();
            throw new Win32Exception(5);
        }

        public void Terminate()
        {
        }

        public void Dispose()
        {
        }
    }
}

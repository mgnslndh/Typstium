using System.ComponentModel;
using System.Diagnostics;
using Typstium.CommandLine;
using Typstium.Utilities;

namespace Typstium.Processes;

internal sealed class TypstProcessRunner : ITypstProcessRunner
{
    private readonly Func<long?, IProcessContainment> _createContainment;

    public TypstProcessRunner()
        : this(ProcessContainment.Create)
    {
    }

    internal TypstProcessRunner(Func<long?, IProcessContainment> createContainment) => _createContainment = createContainment;

    public async Task<ProcessOutcome> RunAsync(ProcessSpec spec, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spec);
        cancellationToken.ThrowIfCancellationRequested();

        IProcessContainment containment;
        try
        {
            containment = _createContainment(spec.MemoryLimitBytes);
        }
        catch (Win32Exception exception)
        {
            throw new TypstProcessStartException(spec.Executable, exception);
        }

        using (containment)
        using (var process = new Process { StartInfo = CreateStartInfo(spec) })
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                process.Start();
            }
            catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or FileNotFoundException)
            {
                throw new TypstProcessStartException(spec.Executable, exception);
            }

            try
            {
                containment.Attach(process);
            }
            catch (Win32Exception exception)
            {
                Kill(process, containment);
                throw new TypstProcessStartException(spec.Executable, exception);
            }

            using var timeout = new CancellationTokenSource();
            if (spec.Timeout != Timeout.InfiniteTimeSpan)
            {
                timeout.CancelAfter(spec.Timeout);
            }

            using var outputLimit = new CancellationTokenSource();
            using var kill = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token, outputLimit.Token);
            using var registration = kill.Token.Register(() => Kill(process, containment));

            var stdout = StreamReading.ReadBoundedAsync(process.StandardOutput.BaseStream, spec.MaxStdoutBytes, outputLimit.Cancel);
            var stderr = StreamReading.ReadTextCappedAsync(process.StandardError.BaseStream, spec.MaxStderrBytes);
            var stdin = WriteStdinAsync(process, spec.Stdin);

            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            var (output, limitExceeded) = await stdout.ConfigureAwait(false);
            var errorText = await stderr.ConfigureAwait(false);
            await IgnorePipeErrorsAsync(stdin).ConfigureAwait(false);
            stopwatch.Stop();

            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("The Typst compile was cancelled.", cancellationToken);
            }

            if (limitExceeded)
            {
                return new ProcessOutcome.OutputLimitExceeded(spec.MaxStdoutBytes, stopwatch.Elapsed, errorText);
            }

            if (timeout.IsCancellationRequested)
            {
                return new ProcessOutcome.TimedOut(stopwatch.Elapsed, errorText);
            }

            return new ProcessOutcome.Exited(process.ExitCode, output, stopwatch.Elapsed, errorText);
        }
    }

    private static ProcessStartInfo CreateStartInfo(ProcessSpec spec)
    {
        var startInfo = new ProcessStartInfo(spec.Executable)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = spec.WorkingDirectory,
        };

#if NET8_0_OR_GREATER
        foreach (var argument in spec.Arguments)
        {
            startInfo.ArgumentList.Add(argument.Value);
        }
#else
        startInfo.Arguments = WindowsArgumentEscaper.Join(spec.Arguments.ToValues());
#endif

        spec.Environment.ApplyTo(startInfo);
        return startInfo;
    }

    private static async Task WriteStdinAsync(Process process, ReadOnlyMemory<byte> stdin)
    {
        var stream = process.StandardInput.BaseStream;
        try
        {
            if (!stdin.IsEmpty)
            {
                var segment = ByteMemory.AsSegment(stdin);
                await stream.WriteAsync(segment.Array!, segment.Offset, segment.Count).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            process.StandardInput.Close();
        }
    }

    private static async Task IgnorePipeErrorsAsync(Task stdin)
    {
        try
        {
            await stdin.ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            // The process exited before reading all of stdin (e.g. it was killed).
        }
    }

    /// <summary>
    /// Kills the process and anything it started. On netstandard2.0 Process.Kill(true) is not available,
    /// so the job object terminates grandchildren that may still hold our pipes open.
    /// </summary>
    private static void Kill(Process process, IProcessContainment containment)
    {
        try
        {
#if NET8_0_OR_GREATER
            process.Kill(entireProcessTree: true);
#else
            process.Kill();
#endif
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // Already exited.
        }

        containment.Terminate();
    }
}

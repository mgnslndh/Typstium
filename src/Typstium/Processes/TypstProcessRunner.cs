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

            SetPriority(process, containment, spec);

            try
            {
                containment.Attach(process);
            }
            catch (Win32Exception exception)
            {
                // A process that already exited cannot be assigned to a job (access denied). That is a valid run;
                // children spawned before Attach would be outside the job (Typst does not spawn children).
                if (!HasExited(process))
                {
                    Kill(process, containment, deferTreeWalk: false);
                    throw new TypstProcessStartException(spec.Executable, exception);
                }
            }

            using var timeout = new CancellationTokenSource();
            if (spec.Timeout != Timeout.InfiniteTimeSpan)
            {
                timeout.CancelAfter(spec.Timeout);
            }

            using var outputLimit = new CancellationTokenSource();
            using var kill = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token, outputLimit.Token);
            using var registration = kill.Token.Register(() => Kill(process, containment, deferTreeWalk: true));

            // Pipe I/O blocks, so it runs on dedicated threads: on pool threads, a full set of concurrent compiles
            // starves the pool and the timeout's kill callback fires late (see StreamReading).
            var stdout = DedicatedThread.Run(() => StreamReading.ReadBounded(process.StandardOutput.BaseStream, spec.MaxStdoutBytes, outputLimit.Cancel));
            var stderr = DedicatedThread.Run(() => StreamReading.ReadTextCapped(process.StandardError.BaseStream, spec.MaxStderrBytes));
            var stdin = DedicatedThread.Run(() => WriteStdin(process, spec.Stdin));

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

    private static void WriteStdin(Process process, ReadOnlyMemory<byte> stdin)
    {
        var stream = process.StandardInput.BaseStream;
        try
        {
            if (!stdin.IsEmpty)
            {
                var segment = ByteMemory.AsSegment(stdin);
                stream.Write(segment.Array!, segment.Offset, segment.Count);
                stream.Flush();
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
    /// Kills the process and anything it started. Runs on the timer or cancellation callback, which must not block:
    /// terminating the Windows job object is one system call and stops Typst at once. On .NET 8+ the tree walk
    /// (Kill(entireProcessTree: true)) runs on a dedicated thread, because it snapshots every process on the machine
    /// and takes seconds under load. It is the kill where there is no job (non-Windows), and the backstop for children
    /// outside the job (started before it was attached). It kills the parent itself, and must: on Unix a child whose
    /// parent died is re-parented and can no longer be found. netstandard2.0 has no tree walk and kills the process.
    /// On a failed start (<paramref name="deferTreeWalk"/> false) the tree walk runs right away instead: the caller
    /// disposes the process as soon as this returns, and a deferred walk would then find nothing to kill.
    /// </summary>
    private static void Kill(Process process, IProcessContainment containment, bool deferTreeWalk)
    {
        try
        {
            containment.Terminate();
        }
        catch (Exception)
        {
            // The kill below, and job kill-on-close, remain the backstops.
        }

#if NET8_0_OR_GREATER
        void KillTree()
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception)
            {
                // The process may be gone or disposed by now; nothing is left to kill then.
            }
        }

        if (deferTreeWalk)
        {
            _ = DedicatedThread.Run(KillTree);
        }
        else
        {
            KillTree();
        }
#else
        try
        {
            process.Kill();
        }
        catch (Exception)
        {
            // Never throw from the kill callback (it may run on a timer thread); the outcome is decided by the flags.
        }
#endif
    }

    /// <summary>
    /// Lowers the scheduling priority right after start; Typst runs a few milliseconds at normal priority first.
    /// Lowering never needs elevated rights, so a failure other than "already exited" is unexpected.
    /// </summary>
    private static void SetPriority(Process process, IProcessContainment containment, ProcessSpec spec)
    {
        var priorityClass = spec.Priority switch
        {
            TypstProcessPriority.Normal => (ProcessPriorityClass?)null,
            TypstProcessPriority.BelowNormal => ProcessPriorityClass.BelowNormal,
            TypstProcessPriority.Idle => ProcessPriorityClass.Idle,
            _ => throw new ArgumentOutOfRangeException(nameof(spec), spec.Priority, "Unknown process priority."),
        };

        if (priorityClass is not { } value)
        {
            return;
        }

        try
        {
            process.PriorityClass = value;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            if (!HasExited(process))
            {
                Kill(process, containment, deferTreeWalk: false);
                throw new TypstProcessStartException(spec.Executable, exception);
            }
        }
    }

    private static bool HasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            return false;
        }
    }
}

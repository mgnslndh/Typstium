namespace Typstium.Processes;

/// <summary>How one process run ended. Caller cancellation is not an outcome: it throws.</summary>
internal abstract record ProcessOutcome
{
    private ProcessOutcome(TimeSpan duration, string standardError)
    {
        Duration = duration;
        StandardError = standardError;
    }

    /// <summary>Wall-clock time from process start to exit or kill.</summary>
    public TimeSpan Duration { get; }

    /// <summary>UTF-8 decoded, capped stderr: the diagnostics.</summary>
    public string StandardError { get; }

    /// <summary>The process exited on its own.</summary>
    internal sealed record Exited : ProcessOutcome
    {
        public Exited(int exitCode, ReadOnlyMemory<byte> standardOutput, TimeSpan duration, string standardError)
            : base(duration, standardError)
        {
            ExitCode = exitCode;
            StandardOutput = standardOutput;
        }

        public int ExitCode { get; }

        /// <summary>The document for PDF and HTML output (written to "-").</summary>
        public ReadOnlyMemory<byte> StandardOutput { get; }
    }

    /// <summary>Killed by our timeout.</summary>
    internal sealed record TimedOut : ProcessOutcome
    {
        public TimedOut(TimeSpan duration, string standardError)
            : base(duration, standardError)
        {
        }
    }

    /// <summary>Killed because stdout passed the limit.</summary>
    internal sealed record OutputLimitExceeded : ProcessOutcome
    {
        public OutputLimitExceeded(long limitBytes, TimeSpan duration, string standardError)
            : base(duration, standardError)
        {
            LimitBytes = limitBytes;
        }

        public long LimitBytes { get; }
    }
}

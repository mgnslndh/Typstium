namespace Typstium;

/// <summary>Typst exited with an unexpected code, e.g. 101 for a Rust panic or 137 when killed for memory.</summary>
public sealed class TypstProcessCrashedException : TypstProcessException
{
    /// <summary>Creates the exception from the exit code and standard error.</summary>
    public TypstProcessCrashedException(int exitCode, string standardError)
        : base($"Typst exited unexpectedly with code {exitCode}.{Environment.NewLine}{standardError}", innerException: null)
    {
        ExitCode = exitCode;
        StandardError = standardError;
    }

    /// <summary>Typst's exit code.</summary>
    public int ExitCode { get; }

    /// <summary>Typst's standard error, capped in size.</summary>
    public string StandardError { get; }
}

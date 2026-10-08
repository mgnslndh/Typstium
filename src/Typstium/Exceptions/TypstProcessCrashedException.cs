namespace Typstium;

/// <summary>Typst exited with an unexpected code, e.g. 101 for a Rust panic or 137 when killed for memory.</summary>
public sealed class TypstProcessCrashedException : TypstProcessException
{
    public TypstProcessCrashedException(int exitCode, string standardError)
        : base($"Typst exited unexpectedly with code {exitCode}.{Environment.NewLine}{standardError}", innerException: null)
    {
        ExitCode = exitCode;
        StandardError = standardError;
    }

    public int ExitCode { get; }

    public string StandardError { get; }
}

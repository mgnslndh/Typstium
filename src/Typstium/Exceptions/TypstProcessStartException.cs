namespace Typstium;

/// <summary>The Typst process could not be started. Fix the configuration; retrying won't help.</summary>
public sealed class TypstProcessStartException : TypstProcessException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="executablePath">The executable that failed to start.</param>
    /// <param name="innerException">The operating system's error.</param>
    public TypstProcessStartException(string executablePath, Exception innerException)
        : base($"Could not start Typst at '{executablePath}': {innerException.Message} Check that the file exists, is executable and matches this machine's architecture.", innerException)
    {
        ExecutablePath = executablePath;
    }

    /// <summary>The executable that failed to start.</summary>
    public string ExecutablePath { get; }
}

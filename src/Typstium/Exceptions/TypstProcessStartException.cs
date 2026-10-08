namespace Typstium;

/// <summary>The Typst process could not be started. Fix the configuration; retrying won't help.</summary>
public sealed class TypstProcessStartException : TypstProcessException
{
    public TypstProcessStartException(string executablePath, Exception innerException)
        : base($"Could not start Typst at '{executablePath}': {innerException.Message} Check that the file exists, is executable and matches this machine's architecture.", innerException)
    {
        ExecutablePath = executablePath;
    }

    public string ExecutablePath { get; }
}

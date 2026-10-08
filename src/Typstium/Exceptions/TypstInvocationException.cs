namespace Typstium;

/// <summary>Typst rejected the command line (exit code 2). This is a Typstium bug or an ArgumentCustomization mistake.</summary>
public sealed class TypstInvocationException : TypstException
{
    public TypstInvocationException(string commandLine, int exitCode, string standardError)
        : base($"Typst rejected the command line (exit code {exitCode}). This is a bug in Typstium or a mistake in ArgumentCustomization/ExtraArguments.{Environment.NewLine}Command: {commandLine}{Environment.NewLine}{standardError}")
    {
        CommandLine = commandLine;
        ExitCode = exitCode;
        StandardError = standardError;
    }

    /// <summary>The command line with secrets redacted.</summary>
    public string CommandLine { get; }

    public int ExitCode { get; }

    public string StandardError { get; }
}

namespace Typstium;

/// <summary>Typst rejected the command line (exit code 2). This is a Typstium bug or an ArgumentCustomization mistake.</summary>
public sealed class TypstInvocationException : TypstException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="commandLine">The command line, with secrets redacted.</param>
    /// <param name="exitCode">Typst's exit code.</param>
    /// <param name="standardError">Typst's standard error.</param>
    public TypstInvocationException(string commandLine, int exitCode, string standardError)
        : base($"Typst rejected the command line (exit code {exitCode}). This is a bug in Typstium or a mistake in ArgumentCustomization/ExtraArguments.{Environment.NewLine}Command: {commandLine}{Environment.NewLine}{standardError}")
    {
        CommandLine = commandLine;
        ExitCode = exitCode;
        StandardError = standardError;
    }

    /// <summary>The command line with secrets redacted.</summary>
    public string CommandLine { get; }

    /// <summary>Typst's exit code, normally 2.</summary>
    public int ExitCode { get; }

    /// <summary>Typst's standard error, capped in size.</summary>
    public string StandardError { get; }
}

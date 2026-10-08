namespace Typstium;

/// <summary>How a compile ran: everything a host wants to log.</summary>
/// <param name="ExecutablePath">The path of the Typst executable that ran.</param>
/// <param name="ExecutableFoundBy">The locator that found the executable, e.g. "WinGet".</param>
/// <param name="TypstVersion">The version of the Typst executable that ran.</param>
/// <param name="CommandLine">The command line with inputs and secrets redacted.</param>
/// <param name="ProcessDuration">Time spent in the Typst process.</param>
/// <param name="TotalDuration">Time spent in the whole CompileAsync call.</param>
public sealed record TypstInvocation(
    string ExecutablePath,
    string ExecutableFoundBy,
    TypstVersion TypstVersion,
    string CommandLine,
    TimeSpan ProcessDuration,
    TimeSpan TotalDuration);

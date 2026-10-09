using Typstium.CommandLine;

namespace Typstium.Processes;

internal sealed record ProcessSpec(
    string Executable,
    TypstArguments Arguments,
    EnvironmentChanges Environment,
    string WorkingDirectory,
    ReadOnlyMemory<byte> Stdin,
    TimeSpan Timeout,
    long MaxStdoutBytes,
    long MaxStderrBytes,
    long? MemoryLimitBytes,
    TypstProcessPriority Priority = TypstProcessPriority.Normal)
{
    internal const long DefaultMaxStderrBytes = 1024 * 1024;
}

using System.Runtime.InteropServices;
using Typstium.CommandLine;
using Typstium.Processes;

namespace Typstium.UnitTests.Testing;

/// <summary>Cross-platform child processes for runner tests: cmd.exe on Windows, /bin/sh elsewhere.</summary>
internal static class ShellCommands
{
    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public static string Shell => IsWindows ? Path.Combine(Environment.SystemDirectory, "cmd.exe") : "/bin/sh";

    public static TypstArguments Run(string windowsCommand, string unixCommand) =>
        IsWindows ? TypstArguments.Of("/d", "/c", windowsCommand) : TypstArguments.Of("-c", unixCommand);

    public static TypstArguments Sleep30() => Run("ping -n 30 127.0.0.1 >nul", "sleep 30");

    public static ProcessSpec Spec(TypstArguments arguments, ReadOnlyMemory<byte> stdin = default, TimeSpan? timeout = null, long maxStdoutBytes = 1 << 20) =>
        new(Shell, arguments, EnvironmentChanges.None, Path.GetTempPath(), stdin, timeout ?? TimeSpan.FromSeconds(30), maxStdoutBytes, ProcessSpec.DefaultMaxStderrBytes, null);
}

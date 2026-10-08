using System.Runtime.InteropServices;

namespace Typstium.Executable;

internal sealed class SystemLocatorEnvironment : ILocatorEnvironment
{
    public static SystemLocatorEnvironment Instance { get; } = new();

    public bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public string BaseDirectory => AppContext.BaseDirectory;

    public string RuntimeIdentifier
    {
        get
        {
            var os = IsWindows ? "win" : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" : "linux";
            var architecture = RuntimeInformation.OSArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.Arm64 => "arm64",
                Architecture.Arm => "arm",
                Architecture.X86 => "x86",
                var other => other.ToString().ToLowerInvariant(),
            };
            return os + "-" + architecture;
        }
    }

    public string? HomeDirectory
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return string.IsNullOrEmpty(home) ? Environment.GetEnvironmentVariable("HOME") : home;
        }
    }

    public string? GetVariable(string name) => Environment.GetEnvironmentVariable(name);

    public bool FileExists(string path) => File.Exists(path);

    public IEnumerable<string> EnumerateDirectories(string path, string searchPattern)
    {
        try
        {
            return Directory.Exists(path) ? Directory.GetDirectories(path, searchPattern) : Array.Empty<string>();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

#if NET8_0_OR_GREATER
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
#endif
    public void EnsureExecutable(string path)
    {
#if NET8_0_OR_GREATER
        if (IsWindows)
        {
            return;
        }

        try
        {
            var mode = File.GetUnixFileMode(path);
            const UnixFileMode execute = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            if ((mode & UnixFileMode.UserExecute) == 0)
            {
                File.SetUnixFileMode(path, mode | execute);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Read-only deployment: starting the process will report the problem.
        }
#endif
    }
}

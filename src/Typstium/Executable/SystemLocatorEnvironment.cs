using System.Runtime.InteropServices;

namespace Typstium.Executable;

internal sealed class SystemLocatorEnvironment : ILocatorEnvironment
{
    public static SystemLocatorEnvironment Instance { get; } = new();

    public bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public string BaseDirectory => AppContext.BaseDirectory;

    public string RuntimeIdentifier =>
        RuntimeIdentifierFor(IsWindows, RuntimeInformation.IsOSPlatform(OSPlatform.OSX), RuntimeInformation.OSArchitecture);

    public string? HomeDirectory =>
        HomeDirectoryFrom(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Environment.GetEnvironmentVariable("HOME"));

    /// <summary>The NuGet runtime identifier, e.g. win-x64 or osx-arm64, which names the bundled runtime's folder.</summary>
    internal static string RuntimeIdentifierFor(bool isWindows, bool isMacOS, Architecture architecture)
    {
        var os = isWindows ? "win" : isMacOS ? "osx" : "linux";
        var name = architecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            Architecture.X86 => "x86",
            var other => other.ToString().ToLowerInvariant(),
        };
        return os + "-" + name;
    }

    /// <summary>The user profile folder, or $HOME when .NET can't tell (some containers and service accounts).</summary>
    internal static string? HomeDirectoryFrom(string? userProfile, string? homeVariable) =>
        string.IsNullOrEmpty(userProfile) ? homeVariable : userProfile;

    public string? GetVariable(string name) => Environment.GetEnvironmentVariable(name);

    public RuntimeManifestResult ReadRuntimeManifest(string directory)
    {
        var path = Path.Combine(directory, RuntimeManifest.FileName);
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return new RuntimeManifestResult.Missing(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new RuntimeManifestResult.Invalid(path, "unreadable: " + exception.Message);
        }

        return RuntimeManifest.Parse(path, json, IsWindows);
    }

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

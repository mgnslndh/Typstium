namespace Typstium.Executable;

/// <summary>The machine as locators see it; faked in unit tests.</summary>
internal interface ILocatorEnvironment
{
    bool IsWindows { get; }

    string BaseDirectory { get; }

    /// <summary>e.g. win-x64, linux-arm64, osx-arm64.</summary>
    string RuntimeIdentifier { get; }

    string? HomeDirectory { get; }

    string? GetVariable(string name);

    bool FileExists(string path);

    /// <returns>Full paths of matching child directories; empty when <paramref name="path"/> is missing.</returns>
    IEnumerable<string> EnumerateDirectories(string path, string searchPattern);

    /// <summary>Restores the execute bit NuGet extraction drops on Unix. No-op on Windows.</summary>
    void EnsureExecutable(string path);
}

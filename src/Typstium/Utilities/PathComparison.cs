namespace Typstium.Utilities;

/// <summary>
/// How to compare file-system paths. Decided by the path's shape, not the host, so Windows-shaped paths (from
/// Typst's diagnostics, or in tests on Linux) compare the way Windows compares them. For paths on the host
/// itself the answer matches the host: Windows file systems ignore case, Linux and macOS (by default) don't.
/// </summary>
internal static class PathComparison
{
    /// <summary>OrdinalIgnoreCase for a Windows-shaped path (drive letter or <c>\\</c> UNC), otherwise Ordinal.</summary>
    public static StringComparison For(string path) =>
        IsWindowsShaped(path) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static bool IsWindowsShaped(string path) =>
        (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':')
        || path.StartsWith(@"\\", StringComparison.Ordinal);
}

namespace Typstium.Utilities;

/// <summary>
/// netstandard2.0 has no Path.IsPathFullyQualified. A fully qualified path doesn't depend on the current
/// directory or the current drive: <c>C:\x</c>, <c>\server\share</c> on Windows, <c>/x</c> elsewhere.
/// </summary>
internal static class FullyQualifiedPath
{
    public static bool IsFullyQualified(string? path, bool isWindows)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        if (!isWindows)
        {
            return path![0] == '/';
        }

        static bool IsSeparator(char c) => c is '\\' or '/';

        if (path!.Length >= 2 && IsSeparator(path[0]) && IsSeparator(path[1]))
        {
            return true;
        }

        return path.Length >= 3 && char.IsLetter(path[0]) && path[1] == ':' && IsSeparator(path[2]);
    }
}

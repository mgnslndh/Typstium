using System.Runtime.InteropServices;
using Typstium.Interop.Windows;

namespace Typstium.Utilities;

/// <summary>
/// Produces the form of a directory path that Typst prints in diagnostics: 8.3 names expanded on
/// Windows, symlinks resolved on Unix (net8.0+). The directory must exist.
/// </summary>
internal static class PathCanonicalizer
{
    public static string CanonicalizeDirectory(string existingDirectory)
    {
        var full = Path.GetFullPath(existingDirectory);
        var root = Path.GetPathRoot(full)!;

        // A filesystem root keeps its trailing separator: trimmed, "/" would be empty and a drive root drive-relative.
        var trimmed = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (trimmed.Length <= root.Length)
        {
            return root;
        }

        full = trimmed;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Kernel32.GetLongPath(full) ?? full;
        }

#if NET8_0_OR_GREATER
        return ResolveSymbolicLinks(full);
#else
        return full;
#endif
    }

#if NET8_0_OR_GREATER
    private static string ResolveSymbolicLinks(string full)
    {
        var current = Path.GetPathRoot(full)!;
        foreach (var segment in full.Substring(current.Length).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(current, segment);
            var info = new DirectoryInfo(candidate);
            current = info.LinkTarget is not null ? info.ResolveLinkTarget(returnFinalTarget: true)!.FullName : candidate;
        }

        return current;
    }
#endif
}

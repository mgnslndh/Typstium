using System.Runtime.InteropServices;

namespace Typstium.Interop.Windows;

/// <summary>P/Invoke declarations for kernel32.dll. Windows only; callers check the platform.</summary>
internal static class Kernel32
{
    private const string LibraryName = "kernel32.dll";

    [DllImport(LibraryName, CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern int GetLongPathNameW(string shortPath, [Out] char[]? longPath, int bufferLength);

    /// <summary>Expands 8.3 short names (e.g. RUNNER~1). Returns null if the path does not exist.</summary>
    internal static string? GetLongPath(string path)
    {
        var length = GetLongPathNameW(path, null, 0);
        if (length == 0)
        {
            return null;
        }

        var buffer = new char[length];
        var written = GetLongPathNameW(path, buffer, buffer.Length);
        return written == 0 || written >= buffer.Length ? null : new string(buffer, 0, written);
    }
}

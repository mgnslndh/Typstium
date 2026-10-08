namespace Typstium;

/// <summary>
/// File names Windows can store as written, applied on every platform so a request or template that works on Linux
/// can't fail or alias on Windows. Windows strips trailing dots and spaces (<c>data.json.</c> is <c>data.json</c>),
/// reserves device names (<c>nul.json</c> is the NUL device) and rejects control characters.
/// </summary>
/// <remarks>
/// Applied where Typstium writes files (virtual files, template bundles) and to template names, never to
/// <see cref="TypstPath"/> itself: that also maps paths Typst prints back to the caller's files, which on Linux may
/// legitimately be named <c>aux.typ</c>.
/// </remarks>
internal static class PortableFileName
{
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM¹", "COM²", "COM³",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9", "LPT¹", "LPT²", "LPT³",
    };

    /// <summary>Why <paramref name="segment"/> isn't portable, or null when it is.</summary>
    public static string? Problem(string segment)
    {
        if (segment.Length == 0)
        {
            return "is empty";
        }

        var last = segment[segment.Length - 1];
        if (last is '.' or ' ')
        {
            return "ends with a dot or a space, which Windows removes";
        }

        foreach (var c in segment)
        {
            if (c < ' ')
            {
                return "contains a control character";
            }
        }

        var dot = segment.IndexOf('.');
        var baseName = (dot < 0 ? segment : segment.Substring(0, dot)).TrimEnd(' ');
        if (ReservedNames.Contains(baseName))
        {
            return "is a reserved device name on Windows";
        }

        return null;
    }

    /// <summary>The first non-portable segment of <paramref name="path"/> and why, or null when every segment is portable.</summary>
    public static string? Problem(TypstPath path)
    {
        foreach (var segment in path.Segments)
        {
            if (Problem(segment) is { } problem)
            {
                return $"'{path}': '{segment}' {problem}";
            }
        }

        return null;
    }
}

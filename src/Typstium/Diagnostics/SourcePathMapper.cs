using System.Text;
using System.Text.RegularExpressions;
using Typstium.Utilities;

namespace Typstium.Diagnostics;

/// <summary>Maps paths printed by Typst back to what the caller knows. Sandbox paths never escape.</summary>
internal sealed class SourcePathMapper
{
    private const string VerbatimPrefix = @"\\?\";
    private const string VerbatimUncPrefix = @"\\?\UNC\";
    private static readonly Regex PackagePath = new("^(?<spec>@[^/]+/[^/:]+:[^/]+)/(?<path>.+)\\z", RegexOptions.CultureInvariant);

    private readonly SourceMappingContext _context;
    private readonly string _root;
    private readonly StringComparison _comparison;

    public SourcePathMapper(SourceMappingContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        var root = ToOrdinaryPath(context.RootDirectory);
        _root = Normalize(root).TrimEnd('/');
        _comparison = PathComparison.For(root);
    }

    public TypstSourceFile? MapFile(string printedPath)
    {
        var path = ToOrdinaryPath(printedPath);
        if (path.StartsWith("@", StringComparison.Ordinal))
        {
            var package = PackagePath.Match(Normalize(path));
            return package.Success && TypstPath.TryParse(package.Groups["path"].Value, out var packageFile)
                ? new TypstSourceFile.Package(package.Groups["spec"].Value, packageFile)
                : null;
        }

        var normalized = Normalize(path);
        if (normalized == "<stdin>" || normalized.EndsWith("/<stdin>", StringComparison.Ordinal))
        {
            return TypstSourceFile.Markup.Instance;
        }

        // Typst prints paths under its working directory relative to it (Linux, macOS), and everything else
        // absolute (always on Windows, as \\?\-prefixed paths). The runner starts Typst with the project root
        // as its working directory, so a relative printed path is root-relative.
        string relativeText;
        if (IsAbsolute(normalized))
        {
            if (!normalized.StartsWith(_root + "/", _comparison))
            {
                return null;
            }

            relativeText = normalized.Substring(_root.Length + 1);
        }
        else
        {
            relativeText = normalized;
        }

        if (!TypstPath.TryParse(relativeText, out var relative))
        {
            return null;
        }

        if (!_context.IsSandbox)
        {
            var fullPath = IsAbsolute(normalized)
                ? path
                : Path.Combine(_context.RootDirectory, relative.Value.Replace('/', Path.DirectorySeparatorChar));
            return new TypstSourceFile.Project(relative, fullPath);
        }

        return _context.VirtualFiles.Contains(relative)
            ? new TypstSourceFile.Virtual(relative)
            : new TypstSourceFile.Project(relative, FullPath: null);
    }

    public TypstLocation? Map(RawLocation? location)
    {
        if (location is null || MapFile(location.Path) is not { } file)
        {
            return null;
        }

        var end = location.EndColumn is { } endColumn ? new TypstPosition(location.Line, endColumn) : (TypstPosition?)null;
        return new TypstLocation(file, new TypstPosition(location.Line, location.Column), end);
    }

    /// <summary>
    /// Replaces every printed sandbox root (with or without the verbatim prefix, with either separator)
    /// by <c>/</c>, so <c>\\?\C:\Temp\root\img.png</c> becomes <c>/img.png</c>. Returns the text unchanged
    /// when the sources are not sandboxed.
    /// </summary>
    public string Scrub(string text)
    {
        if (!_context.IsSandbox || string.IsNullOrEmpty(text) || _root.Length == 0)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        var index = 0;
        while (index < text.Length)
        {
            var end = RootEndAt(text, index);
            if (end < 0)
            {
                builder.Append(text[index]);
                index++;
                continue;
            }

            builder.Append('/');
            index = end;
            if (index < text.Length && IsSeparator(text[index]))
            {
                index++;
            }
        }

        return builder.ToString();
    }

    // The index just past the sandbox root starting at index (including an optional verbatim prefix), or -1.
    // A UNC root (//server/share/...) is printed as \\?\UNC\server\share\..., without its leading separators.
    private int RootEndAt(string text, int index)
    {
        if (_root.StartsWith("//", StringComparison.Ordinal) && HasVerbatimUncPrefixAt(text, index))
        {
            var afterUncPrefix = MatchRootAt(text, index + VerbatimUncPrefix.Length, rootStart: 2);
            if (afterUncPrefix >= 0)
            {
                return afterUncPrefix;
            }
        }

        if (HasVerbatimPrefixAt(text, index))
        {
            var afterPrefix = MatchRootAt(text, index + VerbatimPrefix.Length, rootStart: 0);
            if (afterPrefix >= 0)
            {
                return afterPrefix;
            }
        }

        return MatchRootAt(text, index, rootStart: 0);
    }

    // Matches _root (from rootStart) at start, treating both separators as equal. The root must not run into a longer name.
    private int MatchRootAt(string text, int start, int rootStart)
    {
        var index = start;
        for (var r = rootStart; r < _root.Length; r++, index++)
        {
            if (index >= text.Length || !SameChar(_root[r], text[index]))
            {
                return -1;
            }
        }

        return index < text.Length && IsNameChar(text[index]) ? -1 : index;
    }

    private bool SameChar(char root, char text)
    {
        if (root == '/')
        {
            return IsSeparator(text);
        }

        return _comparison == StringComparison.OrdinalIgnoreCase
            ? char.ToUpperInvariant(root) == char.ToUpperInvariant(text)
            : root == text;
    }

    private static bool HasVerbatimPrefixAt(string text, int index) =>
        index + VerbatimPrefix.Length <= text.Length
        && string.CompareOrdinal(text, index, VerbatimPrefix, 0, VerbatimPrefix.Length) == 0;

    private static bool IsSeparator(char c) => c == '/' || c == '\\';

    private static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.';

    // "UNC" is matched case-insensitively, as Windows does.
    private static bool HasVerbatimUncPrefixAt(string text, int index) =>
        HasVerbatimPrefixAt(text, index)
        && index + VerbatimUncPrefix.Length <= text.Length
        && string.Compare(text, index + VerbatimPrefix.Length, "UNC", 0, 3, StringComparison.OrdinalIgnoreCase) == 0
        && text[index + VerbatimUncPrefix.Length - 1] == '\\';

    /// <summary>
    /// Windows' verbatim forms back to ordinary ones: <c>\\?\UNC\server\share\x</c> to <c>\\server\share\x</c>,
    /// <c>\\?\C:\x</c> to <c>C:\x</c>. Anything else is returned unchanged.
    /// </summary>
    private static string ToOrdinaryPath(string path)
    {
        if (HasVerbatimUncPrefixAt(path, 0))
        {
            return @"\\" + path.Substring(VerbatimUncPrefix.Length);
        }

        return HasVerbatimPrefixAt(path, 0) ? path.Substring(VerbatimPrefix.Length) : path;
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static bool IsWindowsPath(string path) => path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':';

    // A normalized (forward-slash) path is absolute when it starts at a root: "/x", "//server/share", "C:/x".
    private static bool IsAbsolute(string normalized) =>
        normalized.StartsWith("/", StringComparison.Ordinal) || IsWindowsPath(normalized);
}

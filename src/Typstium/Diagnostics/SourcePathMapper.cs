using System.Text.RegularExpressions;

namespace Typstium.Diagnostics;

/// <summary>Maps paths printed by Typst back to what the caller knows. Sandbox paths never escape.</summary>
internal sealed class SourcePathMapper
{
    private const string VerbatimPrefix = @"\\?\";
    private static readonly Regex PackagePath = new("^(?<spec>@[^/]+/[^/:]+:[^/]+)/(?<path>.+)\\z", RegexOptions.CultureInvariant);

    private readonly SourceMappingContext _context;
    private readonly string _root;
    private readonly StringComparison _comparison;

    public SourcePathMapper(SourceMappingContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _root = Normalize(StripVerbatimPrefix(context.RootDirectory)).TrimEnd('/');
        _comparison = IsWindowsPath(_root) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    }

    public TypstSourceFile? MapFile(string printedPath)
    {
        var path = StripVerbatimPrefix(printedPath);
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

        if (!normalized.StartsWith(_root + "/", _comparison)
            || !TypstPath.TryParse(normalized.Substring(_root.Length + 1), out var relative))
        {
            return null;
        }

        if (!_context.IsSandbox)
        {
            return new TypstSourceFile.Project(relative, path);
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

    private static string StripVerbatimPrefix(string path) =>
        path.StartsWith(VerbatimPrefix, StringComparison.Ordinal) ? path.Substring(VerbatimPrefix.Length) : path;

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static bool IsWindowsPath(string path) => path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':';
}

using System.Diagnostics.CodeAnalysis;

namespace Typstium;

/// <summary>
/// A path relative to the Typst project root, normalized to forward slashes.
/// Cannot escape the root: absolute paths, drive letters, UNC paths and ".." segments are rejected.
/// </summary>
public sealed class TypstPath : IEquatable<TypstPath>
{
    private static readonly char[] InvalidCharacters = ['\0', ':', '*', '?', '"', '<', '>', '|'];

    private TypstPath(string value, IReadOnlyList<string> segments)
    {
        Value = value;
        Segments = segments;
    }

    /// <summary>The normalized path without a leading slash, e.g. <c>assets/logo.png</c>.</summary>
    public string Value { get; }

    /// <summary>The path in Typst's root-relative form, e.g. <c>/assets/logo.png</c>.</summary>
    public string RootRelative => "/" + Value;

    public IReadOnlyList<string> Segments { get; }

    public string FileName => Segments[Segments.Count - 1];

    public static TypstPath Parse(string value)
    {
        if (!TryParseCore(value, out var path, out var error))
        {
            throw new ArgumentException(error, nameof(value));
        }

        return path;
    }

    public static bool TryParse(string? value, [NotNullWhen(true)] out TypstPath? path)
    {
        if (TryParseCore(value, out var parsed, out _))
        {
            path = parsed;
            return true;
        }

        path = null;
        return false;
    }

    private static bool TryParseCore(string? value, [NotNullWhen(true)] out TypstPath? path, out string error)
    {
        path = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            error = "A Typst path must not be empty.";
            return false;
        }

        var text = value!.Replace('\\', '/');
        if (text.StartsWith("//", StringComparison.Ordinal))
        {
            error = $"'{value}' is a UNC path; Typst paths are relative to the project root.";
            return false;
        }

        if (text.Length >= 2 && char.IsLetter(text[0]) && text[1] == ':')
        {
            error = $"'{value}' contains a drive letter; Typst paths are relative to the project root.";
            return false;
        }

        if (text.StartsWith("/", StringComparison.Ordinal))
        {
            text = text.Substring(1);
        }

        var segments = new List<string>();
        foreach (var segment in text.Split('/'))
        {
            if (segment.Length == 0)
            {
                error = $"'{value}' contains an empty path segment.";
                return false;
            }

            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                error = $"'{value}' contains '..'; Typst paths cannot leave the project root.";
                return false;
            }

            if (segment.IndexOfAny(InvalidCharacters) >= 0)
            {
                error = $"'{value}' contains a character that isn't allowed in file names.";
                return false;
            }

            segments.Add(segment);
        }

        if (segments.Count == 0)
        {
            error = $"'{value}' doesn't name a file.";
            return false;
        }

        error = string.Empty;
        path = new TypstPath(string.Join("/", segments), segments.AsReadOnly());
        return true;
    }

    public bool Equals(TypstPath? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as TypstPath);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => RootRelative;

    public static bool operator ==(TypstPath? left, TypstPath? right) => Equals(left, right);

    public static bool operator !=(TypstPath? left, TypstPath? right) => !Equals(left, right);
}

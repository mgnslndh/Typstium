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

    /// <summary>The path's segments, without <c>.</c> segments; never empty.</summary>
    public IReadOnlyList<string> Segments { get; }

    /// <summary>The last segment, e.g. <c>logo.png</c>.</summary>
    public string FileName => Segments[Segments.Count - 1];

    /// <summary>Parses and normalizes a root-relative path. Backslashes become forward slashes and a leading slash is optional.</summary>
    /// <exception cref="ArgumentException">The path is empty, absolute (drive letter or UNC), contains <c>..</c>, an empty segment
    /// or a character that isn't allowed in file names.</exception>
    public static TypstPath Parse(string value)
    {
        if (!TryParseCore(value, out var path, out var error))
        {
            throw new ArgumentException(error, nameof(value));
        }

        return path;
    }

    /// <summary>Like <see cref="Parse"/>, but returns <c>false</c> instead of throwing.</summary>
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

    /// <summary>Two paths are equal when their normalized values are equal (ordinal, case-sensitive).</summary>
    public bool Equals(TypstPath? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc cref="Equals(TypstPath?)"/>
    public override bool Equals(object? obj) => Equals(obj as TypstPath);

    /// <inheritdoc/>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    /// <summary>Returns <see cref="RootRelative"/>, the form Typst shows.</summary>
    public override string ToString() => RootRelative;

    /// <inheritdoc cref="Equals(TypstPath?)"/>
    public static bool operator ==(TypstPath? left, TypstPath? right) => Equals(left, right);

    /// <summary>Whether two paths differ; see <see cref="Equals(TypstPath?)"/>.</summary>
    public static bool operator !=(TypstPath? left, TypstPath? right) => !Equals(left, right);
}

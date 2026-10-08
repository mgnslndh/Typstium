using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Typstium;

/// <summary>A template name: segments of <c>[A-Za-z0-9._-]</c> separated by <c>/</c>, without "..".</summary>
public sealed class TypstTemplateName : IEquatable<TypstTemplateName>
{
    private static readonly Regex Segment = new("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant);

    private TypstTemplateName(string value) => Value = value;

    public string Value { get; }

    public string LastSegment => Value.Substring(Value.LastIndexOf('/') + 1);

    public static TypstTemplateName Parse(string value) =>
        TryParse(value, out var name)
            ? name
            : throw new ArgumentException($"'{value}' is not a valid template name. Use segments of letters, digits, '.', '_' and '-' separated by '/'.", nameof(value));

    public static bool TryParse(string? value, [NotNullWhen(true)] out TypstTemplateName? name)
    {
        name = null;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        foreach (var segment in value!.Split('/'))
        {
            if (segment is "." or ".." || !Segment.IsMatch(segment))
            {
                return false;
            }
        }

        name = new TypstTemplateName(value);
        return true;
    }

    public bool Equals(TypstTemplateName? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as TypstTemplateName);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;
}

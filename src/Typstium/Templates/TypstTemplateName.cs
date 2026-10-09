using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Typstium;

/// <summary>
/// A template name: segments of <c>[A-Za-z0-9._-]</c> separated by <c>/</c>. No segment consists only of dots, ends
/// with a dot or is a Windows device name such as <c>con</c>: names become folder paths, which Windows would alias.
/// </summary>
public sealed class TypstTemplateName : IEquatable<TypstTemplateName>
{
    private static readonly Regex Segment = new("^[A-Za-z0-9._-]+\\z", RegexOptions.CultureInvariant);

    private TypstTemplateName(string value) => Value = value;

    /// <summary>The name as given, e.g. <c>invoices/standard</c>.</summary>
    public string Value { get; }

    /// <summary>The part after the last <c>/</c>, e.g. <c>standard</c>. Names the main file <c>standard.typ</c>.</summary>
    public string LastSegment => Value.Substring(Value.LastIndexOf('/') + 1);

    /// <summary>Parses a template name.</summary>
    /// <exception cref="ArgumentException"><paramref name="value"/> isn't a valid name.</exception>
    public static TypstTemplateName Parse(string value) =>
        TryParse(value, out var name)
            ? name
            : throw new ArgumentException($"'{value}' is not a valid template name. Use segments of letters, digits, '.', '_' and '-' separated by '/'.", nameof(value));

    /// <summary>Like <see cref="Parse"/>, but returns <c>false</c> instead of throwing.</summary>
    public static bool TryParse(string? value, [NotNullWhen(true)] out TypstTemplateName? name)
    {
        name = null;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        foreach (var segment in value!.Split('/'))
        {
            // All-dot segments ("..", "...") are rejected too: Windows normalizes them away.
            if (!Segment.IsMatch(segment) || segment.Trim('.').Length == 0 || PortableFileName.Problem(segment) is not null)
            {
                return false;
            }
        }

        name = new TypstTemplateName(value);
        return true;
    }

    /// <summary>Two names are equal when their values are equal (ordinal, case-sensitive).</summary>
    public bool Equals(TypstTemplateName? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc cref="Equals(TypstTemplateName?)"/>
    public override bool Equals(object? obj) => Equals(obj as TypstTemplateName);

    /// <inheritdoc/>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    /// <summary>Returns <see cref="Value"/>.</summary>
    public override string ToString() => Value;
}

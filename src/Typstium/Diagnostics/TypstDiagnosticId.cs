using System.Globalization;
using System.Text.RegularExpressions;

namespace Typstium;

/// <summary>A stable diagnostic code such as <c>TYPST1001</c>. Typst has no codes; Typstium assigns them.</summary>
public sealed class TypstDiagnosticId : IEquatable<TypstDiagnosticId>
{
    private static readonly Regex Format = new("^TYPST[0-9]{4}\\z", RegexOptions.CultureInvariant);

    private TypstDiagnosticId(string value) => Value = value;

    public string Value { get; }

    public int Number => int.Parse(Value.Substring(5), CultureInfo.InvariantCulture);

    public static TypstDiagnosticId Parse(string value)
    {
        if (value is null || !Format.IsMatch(value))
        {
            throw new ArgumentException($"'{value}' is not a diagnostic id. Expected TYPST followed by four digits.", nameof(value));
        }

        return new TypstDiagnosticId(value);
    }

    public bool Equals(TypstDiagnosticId? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as TypstDiagnosticId);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;
}

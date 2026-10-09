using System.Globalization;
using System.Text.RegularExpressions;

namespace Typstium;

/// <summary>A stable diagnostic code such as <c>TYPST1001</c>. Typst has no codes; Typstium assigns them.</summary>
public sealed class TypstDiagnosticId : IEquatable<TypstDiagnosticId>
{
    private static readonly Regex Format = new("^TYPST[0-9]{4}\\z", RegexOptions.CultureInvariant);

    private TypstDiagnosticId(string value) => Value = value;

    /// <summary>The code, e.g. <c>TYPST1001</c>.</summary>
    public string Value { get; }

    /// <summary>The four-digit number, e.g. 1001.</summary>
    public int Number => int.Parse(Value.Substring(5), CultureInfo.InvariantCulture);

    /// <summary>Parses a code: <c>TYPST</c> followed by four digits.</summary>
    /// <exception cref="ArgumentException"><paramref name="value"/> isn't a valid code.</exception>
    public static TypstDiagnosticId Parse(string value)
    {
        if (value is null || !Format.IsMatch(value))
        {
            throw new ArgumentException($"'{value}' is not a diagnostic id. Expected TYPST followed by four digits.", nameof(value));
        }

        return new TypstDiagnosticId(value);
    }

    /// <summary>Two codes are equal when their values are equal (ordinal).</summary>
    public bool Equals(TypstDiagnosticId? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc cref="Equals(TypstDiagnosticId?)"/>
    public override bool Equals(object? obj) => Equals(obj as TypstDiagnosticId);

    /// <inheritdoc/>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    /// <summary>Returns <see cref="Value"/>.</summary>
    public override string ToString() => Value;
}

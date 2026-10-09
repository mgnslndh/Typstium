using System.Diagnostics.CodeAnalysis;

namespace Typstium;

/// <summary>
/// The key of a Typst input, read in Typst as <c>sys.inputs.at("key")</c>. Any non-blank text without <c>=</c>,
/// which separates key and value on Typst's command line. Compared ordinally.
/// </summary>
public sealed class TypstInputKey : IEquatable<TypstInputKey>
{
    private TypstInputKey(string value) => Value = value;

    /// <summary>The key as given.</summary>
    public string Value { get; }

    /// <summary>Parses an input key.</summary>
    /// <exception cref="ArgumentException"><paramref name="value"/> is blank or contains <c>=</c>.</exception>
    public static TypstInputKey Parse(string value) => Parse(value, nameof(value));

    /// <summary>Parses a key, reporting a failure against the caller's parameter.</summary>
    internal static TypstInputKey Parse(string value, string parameterName) =>
        TryParseCore(value, out var key, out var error) ? key : throw new ArgumentException(error, parameterName);

    /// <summary>Like <see cref="Parse(string)"/>, but returns <c>false</c> instead of throwing.</summary>
    public static bool TryParse(string? value, [NotNullWhen(true)] out TypstInputKey? key) => TryParseCore(value, out key, out _);

    private static bool TryParseCore(string? value, [NotNullWhen(true)] out TypstInputKey? key, out string error)
    {
        key = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            error = "Input keys must not be empty.";
            return false;
        }

        if (value!.IndexOf('=') >= 0)
        {
            error = $"The input key '{value}' contains '=', which Typst uses to separate key and value.";
            return false;
        }

        error = string.Empty;
        key = new TypstInputKey(value);
        return true;
    }

    /// <summary>Two keys are equal when their values are equal (ordinal, case-sensitive).</summary>
    public bool Equals(TypstInputKey? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc cref="Equals(TypstInputKey?)"/>
    public override bool Equals(object? obj) => Equals(obj as TypstInputKey);

    /// <inheritdoc/>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    /// <summary>Returns <see cref="Value"/>.</summary>
    public override string ToString() => Value;

    /// <inheritdoc cref="Equals(TypstInputKey?)"/>
    public static bool operator ==(TypstInputKey? left, TypstInputKey? right) => Equals(left, right);

    /// <summary>Whether two keys differ; see <see cref="Equals(TypstInputKey?)"/>.</summary>
    public static bool operator !=(TypstInputKey? left, TypstInputKey? right) => !Equals(left, right);
}

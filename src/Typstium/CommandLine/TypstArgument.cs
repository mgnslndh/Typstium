namespace Typstium;

/// <summary>One command-line argument. Secret arguments are redacted by <c>TypstArguments.RenderSafe</c>.</summary>
public sealed class TypstArgument : IEquatable<TypstArgument>
{
    private TypstArgument(string value, bool isSecret)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
        IsSecret = isSecret;
    }

    /// <summary>The argument exactly as Typst receives it.</summary>
    public string Value { get; }

    /// <summary>Whether the value is redacted in logs, telemetry and <see cref="ToString"/>.</summary>
    public bool IsSecret { get; }

    /// <summary>An argument that may appear in logs.</summary>
    public static TypstArgument Plain(string value) => new(value, isSecret: false);

    /// <summary>An argument that is redacted in logs, such as a token passed as an input.</summary>
    public static TypstArgument Secret(string value) => new(value, isSecret: true);

    /// <summary>Two arguments are equal when their values (ordinal) and secrecy are equal.</summary>
    public bool Equals(TypstArgument? other) =>
        other is not null && IsSecret == other.IsSecret && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc cref="Equals(TypstArgument?)"/>
    public override bool Equals(object? obj) => Equals(obj as TypstArgument);

    /// <inheritdoc/>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value) ^ (IsSecret ? 1 : 0);

    /// <summary>Returns the value, or <c>[REDACTED]</c> for a secret.</summary>
    public override string ToString() => IsSecret ? TypstArguments.Redacted : Value;
}

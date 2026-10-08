namespace Typstium;

/// <summary>One command-line argument. Secret arguments are redacted by <c>TypstArguments.RenderSafe</c>.</summary>
public sealed class TypstArgument : IEquatable<TypstArgument>
{
    private TypstArgument(string value, bool isSecret)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
        IsSecret = isSecret;
    }

    public string Value { get; }

    public bool IsSecret { get; }

    public static TypstArgument Plain(string value) => new(value, isSecret: false);

    public static TypstArgument Secret(string value) => new(value, isSecret: true);

    public bool Equals(TypstArgument? other) =>
        other is not null && IsSecret == other.IsSecret && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as TypstArgument);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value) ^ (IsSecret ? 1 : 0);

    public override string ToString() => IsSecret ? TypstArguments.Redacted : Value;
}

namespace Typstium;

/// <summary>An opaque template version: a content hash or a provider ETag. Compared ordinally.</summary>
public sealed class TypstTemplateVersion : IEquatable<TypstTemplateVersion>
{
    public TypstTemplateVersion(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public bool Equals(TypstTemplateVersion? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as TypstTemplateVersion);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;
}

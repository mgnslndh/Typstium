namespace Typstium;

/// <summary>An opaque template version: a content hash or a provider ETag. Compared ordinally.</summary>
public sealed class TypstTemplateVersion : IEquatable<TypstTemplateVersion>
{
    /// <summary>Creates a version from an opaque, non-blank value.</summary>
    public TypstTemplateVersion(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>The opaque value, such as <c>sha256:</c> followed by a hex digest.</summary>
    public string Value { get; }

    /// <summary>Two versions are equal when their values are equal (ordinal).</summary>
    public bool Equals(TypstTemplateVersion? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc cref="Equals(TypstTemplateVersion?)"/>
    public override bool Equals(object? obj) => Equals(obj as TypstTemplateVersion);

    /// <inheritdoc/>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    /// <summary>Returns <see cref="Value"/>.</summary>
    public override string ToString() => Value;
}

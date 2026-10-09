namespace Typstium;

/// <summary>The document creation timestamp written into the output.</summary>
public abstract record TypstCreationTimestamp
{
    private TypstCreationTimestamp()
    {
    }

    /// <summary>Use the host's SOURCE_DATE_EPOCH when set (the CI convention), otherwise the current time.</summary>
    public static TypstCreationTimestamp Inherit { get; } = new InheritTimestamp();

    /// <summary>Always the current time, even if SOURCE_DATE_EPOCH is set.</summary>
    public static TypstCreationTimestamp Now { get; } = new NowTimestamp();

    /// <summary>A fixed timestamp, for byte-identical output.</summary>
    public static TypstCreationTimestamp Fixed(DateTimeOffset value) => new FixedTimestamp(value);

    /// <summary>See <see cref="Inherit"/>.</summary>
    public sealed record InheritTimestamp : TypstCreationTimestamp;

    /// <summary>See <see cref="Now"/>.</summary>
    public sealed record NowTimestamp : TypstCreationTimestamp;

    /// <summary>See <see cref="Fixed"/>.</summary>
    /// <param name="Value">The timestamp written into the document.</param>
    public sealed record FixedTimestamp(DateTimeOffset Value) : TypstCreationTimestamp;
}

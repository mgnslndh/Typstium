namespace Typstium;

/// <summary>Where Typst packages come from.</summary>
public sealed record TypstPackageOptions
{
    /// <summary>Typst's default package folders, with downloads blocked.</summary>
    public static TypstPackageOptions Default { get; } = new();

    /// <summary>Vendored packages laid out as namespace/name/version (--package-path). Null: Typst's default.</summary>
    public string? PackagePath { get; init; }

    /// <summary>The package cache (--package-cache-path). Null: Typst's default.</summary>
    public string? PackageCachePath { get; init; }

    /// <summary>Whether Typst may download missing packages. Blocked by default.</summary>
    public TypstNetworkAccess NetworkAccess { get; init; } = TypstNetworkAccess.Blocked;
}

namespace Typstium;

public sealed record TypstPackageOptions
{
    public static TypstPackageOptions Default { get; } = new();

    /// <summary>Vendored packages laid out as namespace/name/version (--package-path). Null: Typst's default.</summary>
    public string? PackagePath { get; init; }

    /// <summary>The package cache (--package-cache-path). Null: Typst's default.</summary>
    public string? PackageCachePath { get; init; }

    public TypstNetworkAccess NetworkAccess { get; init; } = TypstNetworkAccess.Blocked;
}

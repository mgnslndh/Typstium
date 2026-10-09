namespace Typstium;

/// <summary>The installed Typst is too old for the request, or lacks a feature it needs.</summary>
public sealed class TypstFeatureNotSupportedException : TypstException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="feature">The option or format that needs support, as the caller would name it.</param>
    /// <param name="installedVersion">The Typst version that was found.</param>
    /// <param name="requiredVersion">The first version that supports it, or null when this Typst build lacks it whatever its version (e.g. a build without HTML export).</param>
    public TypstFeatureNotSupportedException(string feature, TypstVersion installedVersion, TypstVersion? requiredVersion)
        : base(BuildMessage(feature, installedVersion, requiredVersion))
    {
        Feature = feature;
        InstalledVersion = installedVersion;
        RequiredVersion = requiredVersion;
    }

    /// <summary>The option or format that needs support.</summary>
    public string Feature { get; }

    /// <summary>The Typst version that was found.</summary>
    public TypstVersion InstalledVersion { get; }

    /// <summary>The first version that supports the feature, or null when this Typst build lacks the feature whatever its version.</summary>
    public TypstVersion? RequiredVersion { get; }

    private static string BuildMessage(string feature, TypstVersion installed, TypstVersion? required) =>
        required is { } version
            ? $"'{feature}' requires Typst {version} or newer, but Typst {installed} is installed."
            : $"'{feature}' is not supported by the installed Typst {installed}.";
}

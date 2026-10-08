namespace Typstium;

/// <summary>The installed Typst is too old for the request, or lacks a feature it needs.</summary>
public sealed class TypstFeatureNotSupportedException : TypstException
{
    public TypstFeatureNotSupportedException(string feature, TypstVersion installedVersion, TypstVersion? requiredVersion)
        : base(BuildMessage(feature, installedVersion, requiredVersion))
    {
        Feature = feature;
        InstalledVersion = installedVersion;
        RequiredVersion = requiredVersion;
    }

    public string Feature { get; }

    public TypstVersion InstalledVersion { get; }

    public TypstVersion? RequiredVersion { get; }

    private static string BuildMessage(string feature, TypstVersion installed, TypstVersion? required) =>
        required is { } version
            ? $"'{feature}' requires Typst {version} or newer, but Typst {installed} is installed."
            : $"'{feature}' is not supported by the installed Typst {installed}.";
}

namespace Typstium.Diagnostics;

/// <summary>Hints Typstium adds where its own defaults explain the problem.</summary>
internal static class DiagnosticHints
{
    internal const string SystemFontsDisabled =
        "system fonts are disabled by default; add a font folder to Fonts.FontPaths or set Fonts.IncludeSystemFonts.";

    internal const string NetworkBlocked =
        "network access is blocked; vendor the package into Packages.PackagePath or set Packages.NetworkAccess to Allowed.";

    public static IEnumerable<string> For(TypstDiagnosticId id, TypstCompileOptions options)
    {
        if (id.Equals(DiagnosticIds.UnknownFontFamily) && !options.Fonts.IncludeSystemFonts)
        {
            yield return SystemFontsDisabled;
        }

        if (id.Equals(DiagnosticIds.PackageDownloadFailed) && options.Packages.NetworkAccess == TypstNetworkAccess.Blocked)
        {
            yield return NetworkBlocked;
        }
    }
}

using Cake.Core.IO;
using Typstium;

namespace Cake.Typstium;

/// <summary>Fluent setters for <see cref="TypstCompileSettings"/>; each returns the same instance.</summary>
public static class TypstCompileSettingsExtensions
{
    /// <summary>Sets <see cref="TypstCompileSettings.ToolPath"/>.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="toolPath">The Typst executable.</param>
    /// <returns>The same settings.</returns>
    public static TypstCompileSettings WithToolPath(this TypstCompileSettings settings, FilePath toolPath) =>
        Set(settings, s => s.ToolPath = toolPath);

    /// <summary>Sets <see cref="TypstCompileSettings.Root"/>.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="root">The project root.</param>
    /// <returns>The same settings.</returns>
    public static TypstCompileSettings WithRoot(this TypstCompileSettings settings, DirectoryPath root) =>
        Set(settings, s => s.Root = root);

    /// <summary>Sets <see cref="TypstCompileSettings.Format"/>.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="format">The output format.</param>
    /// <returns>The same settings.</returns>
    public static TypstCompileSettings WithFormat(this TypstCompileSettings settings, TypstOutputFormat format) =>
        Set(settings, s => s.Format = format);

    /// <summary>Adds a standard to <see cref="TypstCompileSettings.PdfStandards"/>.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="standard">The PDF standard.</param>
    /// <returns>The same settings.</returns>
    public static TypstCompileSettings WithPdfStandard(this TypstCompileSettings settings, PdfStandard standard) =>
        Set(settings, s => s.PdfStandards.Add(standard));

    /// <summary>Sets <see cref="TypstCompileSettings.Ppi"/>.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="ppi">Pixels per inch for PNG output.</param>
    /// <returns>The same settings.</returns>
    public static TypstCompileSettings WithPpi(this TypstCompileSettings settings, int ppi) =>
        Set(settings, s => s.Ppi = ppi);

    /// <summary>Sets <see cref="TypstCompileSettings.Pages"/>.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="pages">The pages to export.</param>
    /// <returns>The same settings.</returns>
    public static TypstCompileSettings WithPages(this TypstCompileSettings settings, TypstPageSelection pages) =>
        Set(settings, s => s.Pages = pages);

    /// <summary>Adds or replaces an entry in <see cref="TypstCompileSettings.Inputs"/>.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="key">The key in <c>sys.inputs</c>.</param>
    /// <param name="value">The value.</param>
    /// <returns>The same settings.</returns>
    public static TypstCompileSettings WithInput(this TypstCompileSettings settings, string key, string value) =>
        Set(settings, s => s.Inputs[key] = value);

    /// <summary>Adds a folder to <see cref="TypstCompileSettings.FontPaths"/>.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="fontPath">A folder with fonts.</param>
    /// <returns>The same settings.</returns>
    public static TypstCompileSettings WithFontPath(this TypstCompileSettings settings, DirectoryPath fontPath) =>
        Set(settings, s => s.FontPaths.Add(fontPath));

    /// <summary>Sets <see cref="TypstCompileSettings.IncludeSystemFonts"/>.</summary>
    /// <param name="settings">The settings.</param>
    /// <returns>The same settings.</returns>
    public static TypstCompileSettings WithSystemFonts(this TypstCompileSettings settings) =>
        Set(settings, s => s.IncludeSystemFonts = true);

    /// <summary>Sets <see cref="TypstCompileSettings.PackagePath"/>.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="packagePath">A folder of vendored packages.</param>
    /// <returns>The same settings.</returns>
    public static TypstCompileSettings WithPackagePath(this TypstCompileSettings settings, DirectoryPath packagePath) =>
        Set(settings, s => s.PackagePath = packagePath);

    /// <summary>Sets <see cref="TypstCompileSettings.PackageCachePath"/>.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="packageCachePath">The package cache folder.</param>
    /// <returns>The same settings.</returns>
    public static TypstCompileSettings WithPackageCachePath(this TypstCompileSettings settings, DirectoryPath packageCachePath) =>
        Set(settings, s => s.PackageCachePath = packageCachePath);

    /// <summary>Sets <see cref="TypstCompileSettings.AllowNetwork"/>.</summary>
    /// <param name="settings">The settings.</param>
    /// <returns>The same settings.</returns>
    public static TypstCompileSettings WithNetwork(this TypstCompileSettings settings) =>
        Set(settings, s => s.AllowNetwork = true);

    /// <summary>Sets <see cref="TypstCompileSettings.CreationTimestamp"/>.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="timestamp">The creation timestamp.</param>
    /// <returns>The same settings.</returns>
    public static TypstCompileSettings WithCreationTimestamp(this TypstCompileSettings settings, DateTimeOffset timestamp) =>
        Set(settings, s => s.CreationTimestamp = timestamp);

    /// <summary>Sets <see cref="TypstCompileSettings.Timeout"/>.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="timeout">How long one compile may run.</param>
    /// <returns>The same settings.</returns>
    public static TypstCompileSettings WithTimeout(this TypstCompileSettings settings, TimeSpan timeout) =>
        Set(settings, s => s.Timeout = timeout);

    /// <summary>Sets <see cref="TypstCompileSettings.TreatWarningsAsErrors"/>.</summary>
    /// <param name="settings">The settings.</param>
    /// <returns>The same settings.</returns>
    public static TypstCompileSettings WithWarningsAsErrors(this TypstCompileSettings settings) =>
        Set(settings, s => s.TreatWarningsAsErrors = true);

    /// <summary>Sets <see cref="TypstCompileSettings.ExtraArguments"/>.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="arguments">Arguments appended after the generated ones.</param>
    /// <returns>The same settings.</returns>
    public static TypstCompileSettings WithExtraArguments(this TypstCompileSettings settings, TypstArguments arguments) =>
        Set(settings, s => s.ExtraArguments = arguments);

    /// <summary>Sets <see cref="TypstCompileSettings.ArgumentCustomization"/>.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="customization">Changes the generated command line.</param>
    /// <returns>The same settings.</returns>
    public static TypstCompileSettings WithArgumentCustomization(this TypstCompileSettings settings, Func<TypstArguments, TypstArguments> customization) =>
        Set(settings, s => s.ArgumentCustomization = customization);

    private static TypstCompileSettings Set(TypstCompileSettings settings, Action<TypstCompileSettings> change)
    {
        ArgumentNullException.ThrowIfNull(settings);
        change(settings);
        return settings;
    }
}

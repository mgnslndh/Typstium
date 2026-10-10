using Cake.Core.IO;
using Typstium;

namespace Cake.Typstium.Compilation;

/// <summary>Where the output goes and in which format, as <c>typst compile</c> decides them (spec cake.md §4.3).</summary>
internal static class TypstOutputPaths
{
    /// <summary>The input's path with a .pdf extension, as <c>typst compile in.typ</c> writes <c>in.pdf</c>.</summary>
    public static FilePath DefaultOutput(FilePath input) => input.ChangeExtension(".pdf");

    /// <summary>
    /// Page placeholders work in the file name only: the folder is created before the pages are saved, so a placeholder
    /// there would create a folder literally named <c>{p}</c>.
    /// </summary>
    public static void EnsurePlaceholdersInFileName(FilePath output)
    {
        var folder = output.GetDirectory().FullPath;
        if (folder.Contains("{p}", StringComparison.Ordinal) || folder.Contains("{0p}", StringComparison.Ordinal) || folder.Contains("{t}", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Page placeholders ({{p}}, {{0p}}, {{t}}) only work in the file name, not in a folder: '{output.FullPath}'.", nameof(output));
        }
    }

    public static TypstOutputFormat FormatOf(FilePath output, TypstOutputFormat? explicitFormat)
    {
        if (explicitFormat is { } format)
        {
            return format;
        }

        return output.GetExtension()?.ToLowerInvariant() switch
        {
            ".pdf" => TypstOutputFormat.Pdf,
            ".png" => TypstOutputFormat.Png,
            ".svg" => TypstOutputFormat.Svg,
            ".html" => TypstOutputFormat.Html,
            _ => throw new ArgumentException(
                $"Can't tell the output format from '{output.FullPath}'. Use .pdf, .png, .svg or .html, or set TypstCompileSettings.Format.",
                nameof(output)),
        };
    }
}

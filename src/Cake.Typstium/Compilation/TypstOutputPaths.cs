using Cake.Core.IO;
using Typstium;

namespace Cake.Typstium.Compilation;

/// <summary>Where the output goes and in which format, as <c>typst compile</c> decides them (spec cake.md §4.3).</summary>
internal static class TypstOutputPaths
{
    /// <summary>The input's path with a .pdf extension, as <c>typst compile in.typ</c> writes <c>in.pdf</c>.</summary>
    public static FilePath DefaultOutput(FilePath input) => input.ChangeExtension(".pdf");

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

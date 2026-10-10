using System.Collections.Immutable;
using Cake.Core;
using Cake.Typstium.Utilities;
using Typstium;

namespace Cake.Typstium.Compilation;

/// <summary>Turns the mutable Cake settings into the core's immutable request (spec cake.md §4.2).</summary>
internal static class TypstCompileRequestMapper
{
    public static TypstCompileRequest Map(string inputPath, TypstOutputFormat format, TypstCompileSettings settings, ICakeEnvironment environment)
    {
        var builder = settings.Root is null
            ? TypstCompileRequest.ForFile(inputPath)
            : TypstCompileRequest.ForFile(inputPath, CakePaths.Native(settings.Root, environment));
        foreach (var input in settings.Inputs)
        {
            builder.WithInput(input.Key, input.Value);
        }

        return builder
            .WithOutput(MapOutput(format, settings))
            .WithOptions(MapOptions(settings, environment))
            .Build();
    }

    private static TypstOutput MapOutput(TypstOutputFormat format, TypstCompileSettings settings)
    {
        // A setting that can't apply to the format is a mistake in the script; ignoring it would hide that.
        if (settings.Ppi is not null && format != TypstOutputFormat.Png)
        {
            throw new ArgumentException($"Ppi applies to PNG output only, but the output is {format}.", nameof(settings));
        }

        if (settings.PdfStandards.Count > 0 && format != TypstOutputFormat.Pdf)
        {
            throw new ArgumentException($"PdfStandards apply to PDF output only, but the output is {format}.", nameof(settings));
        }

        var pages = settings.Pages ?? TypstPageSelection.All;
        return format switch
        {
            TypstOutputFormat.Pdf when settings.PdfStandards.Count == 0 && settings.Pages is null => TypstOutput.Pdf(),
            TypstOutputFormat.Pdf => TypstOutput.Pdf(new TypstPdfOptions { Standards = settings.PdfStandards.ToImmutableArray(), Pages = pages }),
            TypstOutputFormat.Png => TypstOutput.Png(settings.Ppi ?? 144, pages),
            TypstOutputFormat.Svg => TypstOutput.Svg(pages),
            TypstOutputFormat.Html => TypstOutput.Html(),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown output format."),
        };
    }

    private static TypstCompileOptions MapOptions(TypstCompileSettings settings, ICakeEnvironment environment)
    {
        var defaults = TypstCompileOptions.Default;
        return defaults with
        {
            // Untouched defaults stay the very same instances: the core's option records compare their arrays by
            // reference, so a rebuilt empty font list would make default settings differ from the default options.
            Fonts = settings.FontPaths.Count == 0 && !settings.IncludeSystemFonts
                ? defaults.Fonts
                : defaults.Fonts with
                {
                    FontPaths = settings.FontPaths.Select(path => CakePaths.Native(path, environment)).ToImmutableArray(),
                    IncludeSystemFonts = settings.IncludeSystemFonts,
                },
            Packages = defaults.Packages with
            {
                PackagePath = settings.PackagePath is null ? null : CakePaths.Native(settings.PackagePath, environment),
                PackageCachePath = settings.PackageCachePath is null ? null : CakePaths.Native(settings.PackageCachePath, environment),
                NetworkAccess = settings.AllowNetwork ? TypstNetworkAccess.Allowed : TypstNetworkAccess.Blocked,
            },
            CreationTimestamp = settings.CreationTimestamp is { } timestamp
                ? TypstCreationTimestamp.Fixed(timestamp)
                : TypstCreationTimestamp.Inherit,
            Timeout = settings.Timeout ?? defaults.Timeout,
            ExtraArguments = settings.ExtraArguments,
            ArgumentCustomization = settings.ArgumentCustomization,
        };
    }
}

using System.Globalization;
using Typstium.Workspace;

namespace Typstium.CommandLine;

/// <summary>
/// Builds `typst compile` arguments for Typst 0.15 in a fixed order: global flags, subcommand, root,
/// diagnostics, fonts, packages, jobs, timestamp, format and output options, inputs, input, output,
/// extra arguments, then the customization hook.
/// </summary>
internal sealed class CompileArgumentsBuilderV015 : ICompileArgumentsBuilder
{
    public TypstArguments Build(CompileArgumentsInput input)
    {
        var request = input.Request;
        var options = request.Options;
        var layout = input.Layout;

        var arguments = TypstArguments.Empty
            .AddSwitch("--color", "never")
            .AddRange(options.ExtraGlobalArguments)
            .Add("compile")
            .AddSwitch("--root", layout.RootDirectory)
            .AddSwitch("--diagnostic-format", "human");

        arguments = AddFonts(arguments, options.Fonts);
        arguments = AddPackages(arguments, options.Packages);

        if (options.Jobs is { } jobs)
        {
            arguments = arguments.AddSwitch("--jobs", jobs.ToString(CultureInfo.InvariantCulture));
        }

        if (options.CreationTimestamp is TypstCreationTimestamp.FixedTimestamp timestamp)
        {
            arguments = arguments.AddSwitch("--creation-timestamp", timestamp.Value.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
        }

        arguments = arguments.AddSwitch("--format", request.Output.FileExtension);
        arguments = AddOutputOptions(arguments, request.Output);

        foreach (var pair in request.Inputs.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            arguments = arguments.AddSwitchSecret("--input", pair.Key + "=" + pair.Value);
        }

        arguments = arguments
            .Add(layout.MainInput)
            .Add(OutputTarget(request.Output, layout))
            .AddRange(options.ExtraArguments);

        if (options.ArgumentCustomization is { } customize)
        {
            arguments = customize(arguments) ?? throw new InvalidOperationException("ArgumentCustomization returned null.");
        }

        return arguments;
    }

    private static TypstArguments AddFonts(TypstArguments arguments, TypstFontOptions fonts)
    {
        if (fonts.FontPaths.Length > 0)
        {
            var paths = string.Join(Path.PathSeparator.ToString(), fonts.FontPaths.Select(Path.GetFullPath));
            arguments = arguments.AddSwitch("--font-path", paths);
        }

        if (!fonts.IncludeSystemFonts)
        {
            arguments = arguments.Add("--ignore-system-fonts");
        }

        if (!fonts.IncludeEmbeddedFonts)
        {
            arguments = arguments.Add("--ignore-embedded-fonts");
        }

        return arguments;
    }

    private static TypstArguments AddPackages(TypstArguments arguments, TypstPackageOptions packages)
    {
        if (packages.PackagePath is { } packagePath)
        {
            arguments = arguments.AddSwitch("--package-path", Path.GetFullPath(packagePath));
        }

        if (packages.PackageCachePath is { } cachePath)
        {
            arguments = arguments.AddSwitch("--package-cache-path", Path.GetFullPath(cachePath));
        }

        return arguments;
    }

    private static TypstArguments AddOutputOptions(TypstArguments arguments, TypstOutput output) => output switch
    {
        TypstOutput.PdfOutput pdf => AddPages(AddPdfOptions(arguments, pdf.Options), pdf.Options.Pages),
        TypstOutput.PngOutput png => AddPages(arguments.AddSwitch("--ppi", png.Ppi.ToString(CultureInfo.InvariantCulture)), png.Pages),
        TypstOutput.SvgOutput svg => AddPages(arguments, svg.Pages),
        TypstOutput.HtmlOutput => arguments.AddSwitch("--features", "html"),
        _ => throw new ArgumentException($"Unsupported output '{output.GetType().Name}'.", nameof(output)),
    };

    private static TypstArguments AddPdfOptions(TypstArguments arguments, TypstPdfOptions pdf)
    {
        if (pdf.Standards.Length > 0)
        {
            arguments = arguments.AddSwitch("--pdf-standard", string.Join(",", pdf.Standards.Select(standard => standard.CliValue)));
        }

        return pdf.Tagged ? arguments : arguments.Add("--no-pdf-tags");
    }

    private static TypstArguments AddPages(TypstArguments arguments, TypstPageSelection pages) =>
        pages.IsAll ? arguments : arguments.AddSwitch("--pages", pages.ToCliValue());

    private static string OutputTarget(TypstOutput output, WorkspaceLayout layout)
    {
        if (!output.IsPaged)
        {
            return "-";
        }

        var directory = layout.PagedOutputDirectory
            ?? throw new InvalidOperationException("Paged output needs an output folder in the workspace.");
        return Path.Combine(directory, "{0p}." + output.FileExtension);
    }
}

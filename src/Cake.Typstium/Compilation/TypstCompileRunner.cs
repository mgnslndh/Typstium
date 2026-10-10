using System.Globalization;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Typstium.Utilities;
using Typstium;

namespace Cake.Typstium.Compilation;

/// <summary>
/// One alias call (spec cake.md §4.3): validate before any I/O, map the settings, compile, log every diagnostic, fail the
/// task the Cake way, save the document. Core exceptions pass through unwrapped (D10).
/// </summary>
internal sealed class TypstCompileRunner(ICakeEnvironment environment, ICakeLog log, Func<FilePath?, ITypstCompiler> compilerFactory)
{
    public async Task<TypstCompileResult> RunAsync(FilePath input, FilePath? output, TypstCompileSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(settings);

        var inputPath = CakePaths.Native(input, environment);
        var outputFile = output ?? TypstOutputPaths.DefaultOutput(input);
        var format = TypstOutputPaths.FormatOf(outputFile, settings.Format);
        var outputPath = CakePaths.Native(outputFile, environment);
        var request = TypstCompileRequestMapper.Map(inputPath, format, settings, environment);

        var result = await compilerFactory(settings.ToolPath).CompileAsync(request, cancellationToken).ConfigureAwait(false);
        TypstDiagnosticLog.Write(log, result.Diagnostics);

        if (!result.IsSuccess)
        {
            throw new CakeException($"Typst failed to compile {inputPath}: {result.Status}, {Count(result.Errors, "error")}.");
        }

        if (settings.TreatWarningsAsErrors && result.Warnings.Any())
        {
            throw new CakeException($"Typst compiled {inputPath} with {Count(result.Warnings, "warning")}, and TreatWarningsAsErrors is set.");
        }

        var pages = await SaveAsync(result.Document!, outputPath, cancellationToken).ConfigureAwait(false);
        log.Information("{0}", pages == 1
            ? $"Typst compiled {inputPath} to {outputPath} in {Milliseconds(result)} ms."
            : $"Typst compiled {inputPath} to {outputPath} ({pages} pages) in {Milliseconds(result)} ms.");
        return result;
    }

    private static async Task<int> SaveAsync(TypstDocument document, string outputPath, CancellationToken cancellationToken)
    {
        if (document is TypstDocument.Paged paged
            && paged.Pages.Length > 1
            && !outputPath.Contains("{p}", StringComparison.Ordinal)
            && !outputPath.Contains("{0p}", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"The document has {paged.Pages.Length} pages, so the output path needs {{p}} or {{0p}}, e.g. page-{{0p}}.png: {outputPath}",
                "output");
        }

        if (System.IO.Path.GetDirectoryName(outputPath) is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
        }

        switch (document)
        {
            case TypstDocument.SingleFile single:
                await single.SaveAsync(outputPath, cancellationToken).ConfigureAwait(false);
                return 1;
            case TypstDocument.Paged pagedDocument:
                return (await pagedDocument.SaveAsync(outputPath, cancellationToken).ConfigureAwait(false)).Count;
            default:
                throw new InvalidOperationException($"Unknown document type {document.GetType().Name}.");
        }
    }

    private static string Count(IEnumerable<TypstDiagnostic> diagnostics, string noun)
    {
        var count = diagnostics.Count();
        return count.ToString(CultureInfo.InvariantCulture) + " " + noun + (count == 1 ? "" : "s");
    }

    private static string Milliseconds(TypstCompileResult result) =>
        ((long)result.Invocation.TotalDuration.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);
}

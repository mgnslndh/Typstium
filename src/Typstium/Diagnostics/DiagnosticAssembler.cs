using System.Collections.Immutable;

namespace Typstium.Diagnostics;

internal static class DiagnosticAssembler
{
    public static ImmutableArray<TypstDiagnostic> Assemble(
        string standardError,
        IDiagnosticParser parser,
        IDiagnosticClassifier classifier,
        SourcePathMapper mapper,
        TypstCompileOptions options)
    {
        var result = ImmutableArray.CreateBuilder<TypstDiagnostic>();
        foreach (var raw in parser.Parse(standardError))
        {
            var id = raw.IsRecognized
                ? classifier.Classify(raw.Severity, raw.Message)
                : raw.Severity == TypstDiagnosticSeverity.Warning ? DiagnosticIds.GenericWarning : DiagnosticIds.GenericError;

            var location = mapper.Map(raw.Location);
            var message = mapper.Scrub(raw.Message);
            if (raw.Location is { } printed && location is null)
            {
                message = $"{message} (at {FileName(printed.Path)}:{printed.Line}:{printed.Column})";
            }

            var trace = raw.Trace.Select(frame => new TypstTraceFrame(mapper.Scrub(frame.Message), mapper.Map(frame.Location))).ToImmutableArray();
            var hints = raw.Hints.Select(hint => mapper.Scrub(hint)).Concat(DiagnosticHints.For(id, options)).ToImmutableArray();
            result.Add(new TypstDiagnostic(id, raw.Severity, message, location, hints, trace));
        }

        return result.ToImmutable();
    }

    // An unmappable path may be a sandbox path; only its file name is safe to show.
    private static string FileName(string printedPath)
    {
        var normalized = printedPath.Replace('\\', '/');
        return normalized.Substring(normalized.LastIndexOf('/') + 1);
    }
}

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
            var message = raw.Location is { } printed && location is null
                ? $"{raw.Message} (at {FileName(printed.Path)}:{printed.Line}:{printed.Column})"
                : raw.Message;

            var trace = raw.Trace.Select(frame => new TypstTraceFrame(frame.Message, mapper.Map(frame.Location))).ToImmutableArray();
            var hints = raw.Hints.AddRange(DiagnosticHints.For(id, options));
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

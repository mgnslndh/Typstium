using System.Collections.Immutable;

namespace Typstium.Diagnostics;

/// <param name="Path">The path exactly as Typst printed it (may be a sandbox path; mapped later).</param>
/// <param name="Line">1-based.</param>
/// <param name="Column">1-based (already converted from Typst's 0-based column).</param>
/// <param name="EndColumn">1-based, inclusive; only for single-line spans.</param>
internal sealed record RawLocation(string Path, int Line, int Column, int? EndColumn);

/// <param name="Message">The frame text, for example "while calling <c>greet</c>".</param>
/// <param name="Location">Where the frame points, when it could be parsed.</param>
internal sealed record RawTraceFrame(string Message, RawLocation? Location);

/// <param name="Severity">Error or warning.</param>
/// <param name="Message">The headline message; for unrecognized text, the raw text.</param>
/// <param name="Location">The primary location, when it could be parsed.</param>
/// <param name="Hints">Hints, notes and help lines attached to the diagnostic.</param>
/// <param name="Trace">Trace frames printed under the diagnostic.</param>
/// <param name="RawText">The text this diagnostic was parsed from.</param>
/// <param name="IsRecognized">False when the text did not match the expected format; Message then holds the raw text.</param>
internal sealed record RawDiagnostic(
    TypstDiagnosticSeverity Severity,
    string Message,
    RawLocation? Location,
    ImmutableArray<string> Hints,
    ImmutableArray<RawTraceFrame> Trace,
    string RawText,
    bool IsRecognized);

namespace Typstium.Diagnostics;

/// <summary>
/// Maps Typst 0.15 messages to stable IDs by prefix. If Typst rewords a message, it falls back to the
/// generic ID: less specific, never lost.
/// </summary>
internal sealed class DiagnosticClassifierV015 : IDiagnosticClassifier
{
    private static readonly (Func<string, bool> Matches, TypstDiagnosticId Id)[] ErrorRules =
    [
        (m => m.StartsWith("unknown variable:", StringComparison.Ordinal), DiagnosticIds.UnknownVariable),
        (m => m.StartsWith("file not found", StringComparison.Ordinal), DiagnosticIds.FileNotFound),
        (m => m.StartsWith("path ", StringComparison.Ordinal) && m.IndexOf("would escape the project root", StringComparison.Ordinal) >= 0, DiagnosticIds.PathEscapesRoot),
        (m => m.StartsWith("package not found", StringComparison.Ordinal), DiagnosticIds.PackageNotFound),
        (m => m.StartsWith("failed to download package", StringComparison.Ordinal), DiagnosticIds.PackageDownloadFailed),
        (m => m.StartsWith("failed to decode image", StringComparison.Ordinal), DiagnosticIds.ImageDecodeFailed),
        (IsSyntaxError, DiagnosticIds.SyntaxError),
        (m => m == "maximum function call depth exceeded" || m == "loop seems to be infinite" || m == "condition is always true", DiagnosticIds.EvaluationLimit),
    ];

    public TypstDiagnosticId Classify(TypstDiagnosticSeverity severity, string message)
    {
        if (severity == TypstDiagnosticSeverity.Warning)
        {
            return message.StartsWith("unknown font family:", StringComparison.Ordinal)
                ? DiagnosticIds.UnknownFontFamily
                : DiagnosticIds.GenericWarning;
        }

        foreach (var (matches, id) in ErrorRules)
        {
            if (matches(message))
            {
                return id;
            }
        }

        return DiagnosticIds.GenericError;
    }

    // "expected X, found Y" is a type error, not a syntax error.
    private static bool IsSyntaxError(string message) =>
        message.StartsWith("unclosed delimiter", StringComparison.Ordinal)
        || message.StartsWith("unexpected ", StringComparison.Ordinal)
        || (message.StartsWith("expected ", StringComparison.Ordinal) && message.IndexOf(", found ", StringComparison.Ordinal) < 0);
}

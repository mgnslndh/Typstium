namespace Typstium.Diagnostics;

/// <summary>Stable diagnostic IDs. Never reuse a number.</summary>
internal static class DiagnosticIds
{
    public static TypstDiagnosticId GenericError { get; } = TypstDiagnosticId.Parse("TYPST0001");
    public static TypstDiagnosticId GenericWarning { get; } = TypstDiagnosticId.Parse("TYPST0002");
    public static TypstDiagnosticId UnknownVariable { get; } = TypstDiagnosticId.Parse("TYPST1001");
    public static TypstDiagnosticId FileNotFound { get; } = TypstDiagnosticId.Parse("TYPST1002");
    public static TypstDiagnosticId PathEscapesRoot { get; } = TypstDiagnosticId.Parse("TYPST1003");
    public static TypstDiagnosticId PackageNotFound { get; } = TypstDiagnosticId.Parse("TYPST1004");
    public static TypstDiagnosticId PackageDownloadFailed { get; } = TypstDiagnosticId.Parse("TYPST1005");
    public static TypstDiagnosticId ImageDecodeFailed { get; } = TypstDiagnosticId.Parse("TYPST1006");
    public static TypstDiagnosticId SyntaxError { get; } = TypstDiagnosticId.Parse("TYPST1007");
    public static TypstDiagnosticId EvaluationLimit { get; } = TypstDiagnosticId.Parse("TYPST1008");
    public static TypstDiagnosticId UnknownFontFamily { get; } = TypstDiagnosticId.Parse("TYPST2001");
    public static TypstDiagnosticId OutputLimitExceeded { get; } = TypstDiagnosticId.Parse("TYPST9001");
    public static TypstDiagnosticId TimedOut { get; } = TypstDiagnosticId.Parse("TYPST9002");
}

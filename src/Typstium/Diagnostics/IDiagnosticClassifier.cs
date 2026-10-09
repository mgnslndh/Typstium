namespace Typstium.Diagnostics;

internal interface IDiagnosticClassifier
{
    TypstDiagnosticId Classify(TypstDiagnosticSeverity severity, string message);
}

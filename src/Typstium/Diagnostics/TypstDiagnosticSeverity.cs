namespace Typstium;

/// <summary>How serious a diagnostic is.</summary>
public enum TypstDiagnosticSeverity
{
    /// <summary>The compile failed because of it.</summary>
    Error,

    /// <summary>The compile can still succeed.</summary>
    Warning,
}

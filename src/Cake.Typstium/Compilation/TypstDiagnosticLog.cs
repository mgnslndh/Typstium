using Cake.Core.Diagnostics;
using Typstium;

namespace Cake.Typstium.Compilation;

/// <summary>
/// Writes Typst's diagnostics to Cake's log in the canonical <c>path(line,col): error TYPST1001: message</c> form, errors
/// and warnings at their level, hints and call traces underneath at Verbose (spec cake.md §4.3). The text always goes in
/// as a format argument: Cake formats the message, and Typst's messages contain braces.
/// </summary>
internal static class TypstDiagnosticLog
{
    public static void Write(ICakeLog log, IEnumerable<TypstDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Severity == TypstDiagnosticSeverity.Error)
            {
                log.Error("{0}", diagnostic.ToString());
            }
            else
            {
                log.Warning("{0}", diagnostic.ToString());
            }

            foreach (var hint in diagnostic.Hints)
            {
                log.Verbose("    hint: {0}", hint);
            }

            foreach (var frame in diagnostic.Trace)
            {
                log.Verbose("    {0}", Describe(frame));
            }
        }
    }

    private static string Describe(TypstTraceFrame frame) => frame.Location is { } location
        ? $"{frame.Message} at {location.File.DisplayPath}({location.Start.Line},{location.Start.Column})"
        : frame.Message;
}

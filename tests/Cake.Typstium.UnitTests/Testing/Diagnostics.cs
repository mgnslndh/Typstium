using System.Collections.Immutable;
using Typstium;

namespace Cake.Typstium.UnitTests.Testing;

internal static class Diagnostics
{
    public static TypstDiagnostic Error(string message, string file = "doc.typ", int line = 1, int column = 1) =>
        Create("TYPST0001", TypstDiagnosticSeverity.Error, message, file, line, column);

    public static TypstDiagnostic Warning(string message, string file = "doc.typ", int line = 1, int column = 1) =>
        Create("TYPST0002", TypstDiagnosticSeverity.Warning, message, file, line, column);

    private static TypstDiagnostic Create(string id, TypstDiagnosticSeverity severity, string message, string file, int line, int column) =>
        new(TypstDiagnosticId.Parse(id), severity, message,
            new TypstLocation(new TypstSourceFile.Project(TypstPath.Parse(file), FullPath: null), new TypstPosition(line, column), End: null),
            ImmutableArray<string>.Empty, ImmutableArray<TypstTraceFrame>.Empty);
}

using System.Collections.Immutable;

namespace Typstium.Diagnostics;

/// <summary>Parses Typst's stderr. Never throws; unrecognized text becomes unrecognized diagnostics.</summary>
internal interface IDiagnosticParser
{
    ImmutableArray<RawDiagnostic> Parse(string standardError);
}

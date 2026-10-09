using Typstium.CommandLine;
using Typstium.Diagnostics;

namespace Typstium.Capabilities;

/// <summary>The version-specific components for one Typst executable, chosen once (spec 5.7).</summary>
internal sealed record TypstCliProfile(
    TypstCapabilities Capabilities,
    ICompileArgumentsBuilder ArgumentsBuilder,
    IDiagnosticParser DiagnosticParser,
    IDiagnosticClassifier DiagnosticClassifier);

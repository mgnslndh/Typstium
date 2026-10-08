using Typstium.Diagnostics;

namespace Typstium.Workspace;

/// <param name="RootDirectory">Canonical --root.</param>
/// <param name="MainInput">Absolute path of the main file, or "-" when the markup comes over stdin.</param>
/// <param name="Stdin">Bytes for stdin (markup sources), otherwise empty.</param>
/// <param name="PagedOutputDirectory">Folder for {0p} page files (PNG/SVG), otherwise null.</param>
/// <param name="Mapping">How diagnostics paths map back to the caller's paths.</param>
internal sealed record WorkspaceLayout(
    string RootDirectory,
    string MainInput,
    ReadOnlyMemory<byte> Stdin,
    string? PagedOutputDirectory,
    SourceMappingContext Mapping)
{
    internal const string StdinInput = "-";
}

using System.Collections.Immutable;

namespace Typstium.Diagnostics;

/// <param name="RootDirectory">The canonical root passed to Typst as --root.</param>
/// <param name="IsSandbox">True for template and markup sources; full paths are then hidden.</param>
/// <param name="VirtualFiles">Paths written from TypstVirtualFiles (sandbox only).</param>
internal sealed record SourceMappingContext(string RootDirectory, bool IsSandbox, ImmutableHashSet<TypstPath> VirtualFiles);

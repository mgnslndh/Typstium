namespace Typstium;

/// <param name="File">The file the location is in.</param>
/// <param name="Start">The start position.</param>
/// <param name="End">Inclusive end position, when Typst reports a single-line span.</param>
public sealed record TypstLocation(TypstSourceFile File, TypstPosition Start, TypstPosition? End);

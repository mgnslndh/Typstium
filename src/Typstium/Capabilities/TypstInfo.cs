using System.Collections.Immutable;

namespace Typstium.Capabilities;

/// <param name="Version">The Typst version reported by <c>typst info</c>.</param>
/// <param name="Features">Feature names Typst knows (enabled or not), e.g. "html".</param>
internal sealed record TypstInfo(TypstVersion Version, ImmutableHashSet<string> Features);

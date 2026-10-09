using System.Collections.Immutable;
using Typstium.Capabilities;

namespace Typstium.UnitTests.Testing;

internal static class TestCapabilities
{
    public static TypstCapabilities V015 { get; } =
        new(new TypstVersion(0, 15, 1), SupportsHtml: true, PdfStandard.KnownInTypst015.ToImmutableHashSet());

    public static TypstCapabilities V015WithoutHtml { get; } = V015 with { SupportsHtml = false };
}

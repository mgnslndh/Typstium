using System.Collections.Immutable;

namespace Typstium.Capabilities;

/// <summary>
/// Named facts about an installed Typst. Pipeline components ask these questions; only
/// TypstCliProfileFactory compares version numbers (spec 5.7).
/// </summary>
/// <param name="Version">For TypstInvocation and telemetry only, never for decisions outside the factory.</param>
/// <param name="SupportsHtml">Whether the installed Typst can produce HTML output.</param>
/// <param name="PdfStandards">The PDF standards the installed Typst can enforce.</param>
internal sealed record TypstCapabilities(TypstVersion Version, bool SupportsHtml, ImmutableHashSet<PdfStandard> PdfStandards);

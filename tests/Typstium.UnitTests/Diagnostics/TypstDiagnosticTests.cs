using System.Collections.Immutable;
using TUnit.Assertions.Enums;

namespace Typstium.UnitTests.Diagnostics;

public class TypstDiagnosticTests
{
    private static readonly TypstDiagnosticId UnknownVariable = TypstDiagnosticId.Parse("TYPST1001");

    [Test]
    public async Task ToString_uses_the_msbuild_canonical_shape_with_the_full_path_when_on_disk()
    {
        var file = new TypstSourceFile.Project(TypstPath.Parse("docs/spec.typ"), "C:\\repo\\docs\\spec.typ");
        var diagnostic = new TypstDiagnostic(UnknownVariable, TypstDiagnosticSeverity.Error, "unknown variable: x",
            new TypstLocation(file, new TypstPosition(3, 2), null), ImmutableArray<string>.Empty, ImmutableArray<TypstTraceFrame>.Empty);

        await Assert.That(diagnostic.ToString()).IsEqualTo("C:\\repo\\docs\\spec.typ(3,2): error TYPST1001: unknown variable: x");
    }

    [Test]
    public async Task ToString_uses_display_paths_for_sandboxed_files_and_omits_missing_locations()
    {
        var located = new TypstDiagnostic(UnknownVariable, TypstDiagnosticSeverity.Warning, "w",
            new TypstLocation(new TypstSourceFile.Virtual(TypstPath.Parse("data.json")), new TypstPosition(1, 1), null),
            default, default);
        var unlocated = new TypstDiagnostic(UnknownVariable, TypstDiagnosticSeverity.Error, "e", null, default, default);

        await Assert.That(located.ToString()).IsEqualTo("/data.json(1,1): warning TYPST1001: w");
        await Assert.That(unlocated.ToString()).IsEqualTo("error TYPST1001: e");
        await Assert.That(unlocated.Hints.IsDefault).IsFalse();
        await Assert.That(unlocated.Trace.IsDefault).IsFalse();
    }

    [Test]
    public async Task Display_paths_per_source_kind()
    {
        var package = new TypstSourceFile.Package("@preview/cmarker:0.1.10", TypstPath.Parse("lib.typ"));
        await Assert.That(package.DisplayPath).IsEqualTo("@preview/cmarker:0.1.10/lib.typ");
        await Assert.That(TypstSourceFile.Markup.Instance.DisplayPath).IsEqualTo("<markup>");
    }

    [Test]
    public async Task WithHints_appends()
    {
        var diagnostic = new TypstDiagnostic(UnknownVariable, TypstDiagnosticSeverity.Error, "e", null, ImmutableArray.Create("a"), default);
        var extended = diagnostic.WithHints(new[] { "b" });
        await Assert.That(extended.Hints).IsEquivalentTo(new[] { "a", "b" }, CollectionOrdering.Matching);
    }
}

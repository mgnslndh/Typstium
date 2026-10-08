using System.Collections.Immutable;
using Typstium.Diagnostics;
using Typstium.Testing.Fixtures;

namespace Typstium.UnitTests.Diagnostics;

public class DiagnosticAssemblerTests
{
    private const string Root = @"\\?\C:\Temp\typstium\ws-1\root";
    private static readonly TypstVersion V015 = new(0, 15, 0);

    private static ImmutableArray<TypstDiagnostic> Assemble(string fixtureName, TypstCompileOptions options)
    {
        var stderr = DiagnosticFixtures.Get(V015, fixtureName).StandardErrorFor(Root);
        var mapper = new SourcePathMapper(new SourceMappingContext(@"C:\Temp\typstium\ws-1\root", IsSandbox: true, ImmutableHashSet<TypstPath>.Empty));
        return DiagnosticAssembler.Assemble(stderr, new HumanDiagnosticParserV015(), new DiagnosticClassifierV015(), mapper, options);
    }

    [Test]
    [MethodDataSource(typeof(DiagnosticFixtures), nameof(DiagnosticFixtures.All))]
    public async Task No_sandbox_path_leaks_into_public_diagnostics(DiagnosticFixture fixture)
    {
        var stderr = fixture.StandardErrorFor(Root);
        var mapper = new SourcePathMapper(new SourceMappingContext(@"C:\Temp\typstium\ws-1\root", IsSandbox: true, ImmutableHashSet<TypstPath>.Empty));
        var diagnostics = DiagnosticAssembler.Assemble(stderr, new HumanDiagnosticParserV015(), new DiagnosticClassifierV015(), mapper, TypstCompileOptions.Default);

        foreach (var diagnostic in diagnostics)
        {
            await Assert.That(diagnostic.ToString()).DoesNotContain("ws-1");
            await Assert.That(diagnostic.Location).IsNotNull();
        }
    }

    [Test]
    public async Task Unknown_font_family_gets_its_id_and_the_system_fonts_hint()
    {
        var diagnostic = Assemble("05-warning-font", TypstCompileOptions.Default).Single();
        await Assert.That(diagnostic.Id.Value).IsEqualTo("TYPST2001");
        await Assert.That(diagnostic.Hints).Contains(DiagnosticHints.SystemFontsDisabled);
        await Assert.That(diagnostic.Location!.File).IsEqualTo((TypstSourceFile)new TypstSourceFile.Project(TypstPath.Parse("05-warning-font.typ"), null));
    }

    [Test]
    public async Task No_system_fonts_hint_when_system_fonts_are_enabled()
    {
        var options = TypstCompileOptions.Default with { Fonts = TypstFontOptions.Default with { IncludeSystemFonts = true } };
        var diagnostic = Assemble("05-warning-font", options).Single();
        await Assert.That(diagnostic.Hints.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Trace_frames_are_mapped()
    {
        var diagnostic = Assemble("06-error-in-import", TypstCompileOptions.Default).Single();
        var frame = diagnostic.Trace.Single();
        await Assert.That(frame.Location!.File).IsEqualTo((TypstSourceFile)new TypstSourceFile.Project(TypstPath.Parse("06-error-in-import.typ"), null));
    }

    [Test]
    public async Task Unrecognized_text_gets_the_generic_error_id()
    {
        var mapper = new SourcePathMapper(new SourceMappingContext("/tmp/r", true, ImmutableHashSet<TypstPath>.Empty));
        var diagnostic = DiagnosticAssembler.Assemble("thread 'main' panicked", new HumanDiagnosticParserV015(), new DiagnosticClassifierV015(), mapper, TypstCompileOptions.Default).Single();
        await Assert.That(diagnostic.Id.Value).IsEqualTo("TYPST0001");
        await Assert.That(diagnostic.Message).Contains("panicked");
    }

    [Test]
    public async Task Unmappable_paths_are_reduced_to_the_file_name()
    {
        var mapper = new SourcePathMapper(new SourceMappingContext("/tmp/other", true, ImmutableHashSet<TypstPath>.Empty));
        var stderr = "error: boom\n  ┌─ /tmp/typstium/ws-9/root/a.typ:1:0\n";
        var diagnostic = DiagnosticAssembler.Assemble(stderr, new HumanDiagnosticParserV015(), new DiagnosticClassifierV015(), mapper, TypstCompileOptions.Default).Single();
        await Assert.That(diagnostic.Location).IsNull();
        await Assert.That(diagnostic.Message).IsEqualTo("boom (at a.typ:1:1)");
    }
}

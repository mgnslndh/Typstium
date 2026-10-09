using System.Collections.Immutable;
using Typstium.Testing;
using Typstium.Testing.Assertions;
using Typstium.Testing.Fixtures;
using Typstium.Testing.Typst;

namespace Typstium.IntegrationTests;

public class SampleTemplatesTests
{
    private static readonly DirectoryTemplateProvider Templates = new(Path.Combine(RepositoryPaths.Samples, "templates"));

    private static TypstCompileOptions SampleOptions => TypstCompileOptions.Default with
    {
        Fonts = TypstFontOptions.Default with { FontPaths = ImmutableArray.Create(Path.Combine(RepositoryPaths.Samples, "fonts")) },
    };

    [Test]
    public async Task License_certificate_compiles_to_pdf_and_three_png_pages()
    {
        using var temp = new TemporaryDirectory("it-samples");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path });

        var pdf = await compiler.CompileTemplateAsync(Templates, "license", TypstOutput.Pdf(PdfStandard.A2b), SampleOptions);
        var png = await compiler.CompileTemplateAsync(Templates, "license", TypstOutput.Png(72), SampleOptions);

        await DocumentAssertions.PdfBytesAsync(pdf);
        await Assert.That(pdf.Warnings.Count()).IsEqualTo(0);
        await Assert.That(await DocumentAssertions.PageCountAsync(png)).IsEqualTo(3);
        await Assert.That(pdf.Invocation.TypstVersion >= new TypstVersion(0, 15, 0)).IsTrue();
    }

    [Test]
    public async Task Release_notes_render_markdown_through_cmarker()
    {
        using var temp = new TemporaryDirectory("it-samples");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path });
        var cache = Path.Combine(Path.GetTempPath(), "typstium-tests", "package-cache");
        var options = SampleOptions with
        {
            Packages = TypstPackageOptions.Default with { NetworkAccess = TypstNetworkAccess.Allowed, PackageCachePath = cache },
        };

        var result = await compiler.CompileTemplateAsync(Templates, "release-notes", TypstOutput.Png(36), options);

        await Assert.That(await DocumentAssertions.PageCountAsync(result)).IsGreaterThan(3);
    }
}

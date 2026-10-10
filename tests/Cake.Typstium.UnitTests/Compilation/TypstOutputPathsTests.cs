using Cake.Core.IO;
using Cake.Typstium.Compilation;
using Typstium;

namespace Cake.Typstium.UnitTests.Compilation;

public class TypstOutputPathsTests
{
    [Test]
    [Arguments("docs/spec.typ", "docs/spec.pdf")]
    [Arguments("docs/spec", "docs/spec.pdf")]
    [Arguments("docs/v1.2/spec.typ", "docs/v1.2/spec.pdf")]
    public async Task The_default_output_is_a_pdf_beside_the_input(string input, string expected)
    {
        await Assert.That(TypstOutputPaths.DefaultOutput(new FilePath(input)).FullPath).IsEqualTo(expected);
    }

    [Test]
    [Arguments("out/spec.pdf", TypstOutputFormat.Pdf)]
    [Arguments("out/spec.PDF", TypstOutputFormat.Pdf)]
    [Arguments("out/page-{0p}.png", TypstOutputFormat.Png)]
    [Arguments("out/page-{p}.svg", TypstOutputFormat.Svg)]
    [Arguments("out/index.html", TypstOutputFormat.Html)]
    public async Task Infers_the_format_from_the_extension(string output, TypstOutputFormat expected)
    {
        await Assert.That(TypstOutputPaths.FormatOf(new FilePath(output), explicitFormat: null)).IsEqualTo(expected);
    }

    [Test]
    public async Task An_explicit_format_wins_over_the_extension()
    {
        await Assert.That(TypstOutputPaths.FormatOf(new FilePath("out/spec.bin"), TypstOutputFormat.Pdf)).IsEqualTo(TypstOutputFormat.Pdf);
    }

    [Test]
    [Arguments("out/spec.docx")]
    [Arguments("out/spec")]
    public async Task An_unknown_extension_without_a_format_names_the_setting(string output)
    {
        var exception = Assert.Throws<ArgumentException>(() => TypstOutputPaths.FormatOf(new FilePath(output), explicitFormat: null));

        await Assert.That(exception!.ParamName).IsEqualTo("output");
        await Assert.That(exception.Message).Contains("TypstCompileSettings.Format");
    }
}

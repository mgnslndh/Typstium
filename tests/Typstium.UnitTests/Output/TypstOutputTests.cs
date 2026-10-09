using TUnit.Assertions.Enums;

namespace Typstium.UnitTests.Output;

public class TypstOutputTests
{
    [Test]
    public async Task Pdf_with_standards()
    {
        var output = (TypstOutput.PdfOutput)TypstOutput.Pdf(PdfStandard.A2b, PdfStandard.UA1);
        await Assert.That(output.Format).IsEqualTo(TypstOutputFormat.Pdf);
        await Assert.That(output.Options.Standards.Select(s => s.CliValue)).IsEquivalentTo(new[] { "a-2b", "ua-1" }, CollectionOrdering.Matching);
        await Assert.That(output.Options.Tagged).IsTrue();
        await Assert.That(output.IsPaged).IsFalse();
    }

    [Test]
    public async Task Png_and_svg_are_paged()
    {
        var png = (TypstOutput.PngOutput)TypstOutput.Png(300, TypstPageSelection.Of(TypstPageRange.Single(1), TypstPageRange.From(3)));
        await Assert.That(png.Ppi).IsEqualTo(300);
        await Assert.That(png.Pages.ToCliValue()).IsEqualTo("1,3-");
        await Assert.That(png.IsPaged).IsTrue();
        await Assert.That(TypstOutput.Svg().IsPaged).IsTrue();
        await Assert.That(TypstOutput.Html().FileExtension).IsEqualTo("html");
    }

    [Test]
    public async Task Page_ranges_render_like_the_cli()
    {
        await Assert.That(TypstPageRange.Single(2).ToCliValue()).IsEqualTo("2");
        await Assert.That(TypstPageRange.Between(3, 6).ToCliValue()).IsEqualTo("3-6");
        await Assert.That(TypstPageRange.From(8).ToCliValue()).IsEqualTo("8-");
        await Assert.That(TypstPageSelection.All.IsAll).IsTrue();
    }

    [Test]
    public async Task Invalid_values_are_rejected()
    {
        await Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => TypstOutput.Png(0))).IsNotNull();
        await Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => TypstPageRange.Between(5, 2))).IsNotNull();
        await Assert.That(Assert.Throws<ArgumentException>(() => TypstPageSelection.Of())).IsNotNull();
        await Assert.That(Assert.Throws<ArgumentException>(() => PdfStandard.FromCliValue("a-2b,ua-1"))).IsNotNull();
        await Assert.That(Assert.Throws<ArgumentException>(() => PdfStandard.FromCliValue("a-2b\n"))).IsNotNull();
    }

    [Test]
    public async Task Pdf_standards_compare_by_cli_value()
    {
        await Assert.That(PdfStandard.FromCliValue("a-2b")).IsEqualTo(PdfStandard.A2b);
        await Assert.That(PdfStandard.KnownInTypst015.Count).IsEqualTo(17);
    }
}

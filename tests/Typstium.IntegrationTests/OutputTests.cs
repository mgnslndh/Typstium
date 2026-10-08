using Typstium.Testing.Assertions;
using Typstium.Testing.Fixtures;
using Typstium.Testing.Typst;
using TUnit.Assertions.Enums;

namespace Typstium.IntegrationTests;

public class OutputTests
{
    [Test]
    public async Task Fixed_creation_timestamp_gives_byte_identical_pdfs()
    {
        using var temp = new TemporaryDirectory("it-output");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path });
        var options = TypstCompileOptions.Default with { CreationTimestamp = TypstCreationTimestamp.Fixed(DateTimeOffset.FromUnixTimeSeconds(1700000000)) };

        var first = await DocumentAssertions.PdfBytesAsync(await compiler.CompileMarkupAsync("= Same\n#lorem(30)", TypstOutput.Pdf(), options));
        var second = await DocumentAssertions.PdfBytesAsync(await compiler.CompileMarkupAsync("= Same\n#lorem(30)", TypstOutput.Pdf(), options));

        await Assert.That(second).IsEquivalentTo(first, CollectionOrdering.Matching);
    }

    [Test]
    public async Task Multi_page_png_and_svg_come_back_in_page_order()
    {
        using var temp = new TemporaryDirectory("it-output");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path });
        const string ThreePages = "= One\n#pagebreak()\n= Two\n#lorem(40)\n#pagebreak()\n= Three\n#lorem(120)";

        var png = await compiler.CompileMarkupAsync(ThreePages, TypstOutput.Png(36));
        var svg = await compiler.CompileMarkupAsync(ThreePages, TypstOutput.Svg(TypstPageSelection.Of(TypstPageRange.From(2))));

        var allPng = ((TypstDocument.Paged)png.Document!).Pages;
        await Assert.That(await DocumentAssertions.PageCountAsync(png)).IsEqualTo(3);
        for (var n = 1; n <= 3; n++)
        {
            var single = await compiler.CompileMarkupAsync(ThreePages, TypstOutput.Png(36, TypstPageSelection.Of(TypstPageRange.Single(n))));
            await Assert.That(allPng[n - 1].ToArray()).IsEquivalentTo(await OnlyPageAsync(single), CollectionOrdering.Matching);
        }

        var tailSvg = ((TypstDocument.Paged)svg.Document!).Pages;
        await Assert.That(await DocumentAssertions.PageCountAsync(svg)).IsEqualTo(2);
        for (var n = 2; n <= 3; n++)
        {
            var single = await compiler.CompileMarkupAsync(ThreePages, TypstOutput.Svg(TypstPageSelection.Of(TypstPageRange.Single(n))));
            await Assert.That(tailSvg[n - 2].ToArray()).IsEquivalentTo(await OnlyPageAsync(single), CollectionOrdering.Matching);
        }
    }

    private static async Task<byte[]> OnlyPageAsync(TypstCompileResult result)
    {
        await Assert.That(await DocumentAssertions.PageCountAsync(result)).IsEqualTo(1);
        return ((TypstDocument.Paged)result.Document!).Pages[0].ToArray();
    }

    [Test]
    public async Task Json_virtual_files_reach_the_template()
    {
        using var temp = new TemporaryDirectory("it-output");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path });

        var result = await compiler.CompileAsync(
            TypstCompileRequest.ForMarkup("#let d = json(\"/data.json\")\n#assert(d.name == \"Åsa\")\nHello #d.name")
                .WithJson("data.json", new { Name = "Åsa" }));

        await DocumentAssertions.PdfBytesAsync(result);
    }
}

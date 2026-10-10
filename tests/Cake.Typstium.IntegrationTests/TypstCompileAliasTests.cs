using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Typstium;
using Typstium.Testing;
using Typstium.Testing.Fixtures;
using Typstium.Testing.Typst;

namespace Cake.Typstium.IntegrationTests;

public class TypstCompileAliasTests
{
    private static readonly DirectoryPath Fonts = new(System.IO.Path.Combine(RepositoryPaths.Samples, "fonts"));

    private static async Task<(TemporaryDirectory Work, TestCakeContext Context, TypstCompileSettings Settings)> ArrangeAsync(string document)
    {
        var typst = await TypstAvailability.RequireExecutablePathAsync();
        var work = new TemporaryDirectory("cake-it");
        File.Copy(System.IO.Path.Combine(AppContext.BaseDirectory, "Documents", document), work.Combine(document));
        var settings = new TypstCompileSettings().WithToolPath(new FilePath(typst)).WithFontPath(Fonts);
        return (work, TestCakeContext.In(work.Path), settings);
    }

    [Test]
    public async Task Compiles_a_pdf_with_a_bundled_font_and_no_warnings()
    {
        var (work, context, settings) = await ArrangeAsync("two-pages.typ");
        using var _ = work;

        var result = context.TypstCompile(new FilePath("two-pages.typ"), new FilePath("out/two-pages.pdf"), settings);

        await Assert.That(result.Warnings.Any()).IsFalse();
        await Assert.That(File.ReadAllBytes(work.Combine("out", "two-pages.pdf"))[..4]).IsEquivalentTo("%PDF"u8.ToArray());
        await Assert.That(result.Invocation.ExecutableFoundBy).IsEqualTo("ExplicitPath");
    }

    [Test]
    public async Task Compiles_every_page_to_png_through_the_pattern()
    {
        var (work, context, settings) = await ArrangeAsync("two-pages.typ");
        using var _ = work;

        await context.TypstCompileAsync(new FilePath("two-pages.typ"), new FilePath("pages/page-{0p}.png"), settings);

        await Assert.That(File.Exists(work.Combine("pages", "page-1.png"))).IsTrue();
        await Assert.That(File.Exists(work.Combine("pages", "page-2.png"))).IsTrue();
    }

    [Test]
    public async Task Compiles_svg_pages()
    {
        var (work, context, settings) = await ArrangeAsync("two-pages.typ");
        using var _ = work;

        context.TypstCompile(new FilePath("two-pages.typ"), new FilePath("svg/{p}.svg"), settings);

        await Assert.That(File.ReadAllText(work.Combine("svg", "2.svg"))).Contains("<svg");
    }

    [Test]
    public async Task Inputs_reach_sys_inputs()
    {
        var (work, context, settings) = await ArrangeAsync("needs-input.typ");
        using var _ = work;

        var result = context.TypstCompile(new FilePath("needs-input.typ"), new FilePath("needs-input.pdf"), settings.WithInput("version", "1.2.3"));

        await Assert.That(result.IsSuccess).IsTrue();
    }

    [Test]
    public async Task A_syntax_error_fails_with_the_canonical_line_in_the_log()
    {
        var (work, context, settings) = await ArrangeAsync("broken.typ");
        using var _ = work;

        var exception = Assert.Throws<CakeException>(() => context.TypstCompile(new FilePath("broken.typ"), new FilePath("broken.pdf"), settings));

        await Assert.That(exception!.Message).Contains("Failed");
        var errors = context.FakeLog.Entries.Where(entry => entry.Level == LogLevel.Error).Select(entry => entry.Message).ToArray();
        await Assert.That(errors.Any(message => message.Contains("broken.typ(") && message.Contains("error TYPST"))).IsTrue();
        await Assert.That(File.Exists(work.Combine("broken.pdf"))).IsFalse();
    }

    [Test]
    public async Task A_fixed_creation_timestamp_gives_byte_identical_pdfs()
    {
        var (work, context, settings) = await ArrangeAsync("two-pages.typ");
        using var _ = work;
        settings.WithCreationTimestamp(new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));

        context.TypstCompile(new FilePath("two-pages.typ"), new FilePath("a.pdf"), settings);
        context.TypstCompile(new FilePath("two-pages.typ"), new FilePath("b.pdf"), settings);

        await Assert.That(File.ReadAllBytes(work.Combine("a.pdf"))).IsEquivalentTo(File.ReadAllBytes(work.Combine("b.pdf")));
    }
}

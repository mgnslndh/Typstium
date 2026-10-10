using Cake.Core.IO;
using Cake.Typstium.Compilation;
using Cake.Typstium.UnitTests.Testing;
using Typstium;
using Typstium.Testing.Fixtures;

namespace Cake.Typstium.UnitTests.Compilation;

public class TypstCompileRequestMapperTests
{
    private static TypstCompileRequest Map(TemporaryDirectory work, TypstOutputFormat format, TypstCompileSettings settings) =>
        TypstCompileRequestMapper.Map(work.Combine("doc.typ"), format, settings, CakeFakes.Environment(work.Path));

    [Test]
    public async Task Default_settings_give_the_cores_default_options_and_a_plain_pdf()
    {
        using var work = new TemporaryDirectory("cake-map");
        var request = Map(work, TypstOutputFormat.Pdf, new TypstCompileSettings());

        await Assert.That(request.Options).IsEqualTo(TypstCompileOptions.Default);
        await Assert.That(request.Output).IsEqualTo(TypstOutput.Pdf());
        await Assert.That(request.Inputs.Count).IsEqualTo(0);
    }

    [Test]
    public async Task The_source_is_the_input_file_in_its_own_folder_by_default()
    {
        using var work = new TemporaryDirectory("cake-map");
        var request = Map(work, TypstOutputFormat.Pdf, new TypstCompileSettings());

        await Assert.That(request.Source).IsEqualTo(TypstCompileRequest.ForFile(work.Combine("doc.typ")).Build().Source);
    }

    [Test]
    public async Task A_relative_root_resolves_against_cakes_working_directory()
    {
        using var work = new TemporaryDirectory("cake-map");
        var request = Map(work, TypstOutputFormat.Pdf, new TypstCompileSettings { Root = new DirectoryPath("docs") });

        await Assert.That(request.Source).IsEqualTo(TypstCompileRequest.ForFile(work.Combine("doc.typ"), work.Combine("docs")).Build().Source);
    }

    [Test]
    public async Task Fonts_packages_and_limits_map_to_the_options_with_paths_against_the_working_directory()
    {
        using var work = new TemporaryDirectory("cake-map");
        var timestamp = new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.Zero);
        Func<TypstArguments, TypstArguments> customize = arguments => arguments;
        var settings = new TypstCompileSettings()
            .WithFontPath(new DirectoryPath("fonts"))
            .WithSystemFonts()
            .WithPackagePath(new DirectoryPath("vendor/packages"))
            .WithPackageCachePath(new DirectoryPath("cache"))
            .WithNetwork()
            .WithCreationTimestamp(timestamp)
            .WithTimeout(TimeSpan.FromMinutes(5))
            .WithExtraArguments(TypstArguments.Empty.Add("--timings"))
            .WithArgumentCustomization(customize);

        var options = Map(work, TypstOutputFormat.Pdf, settings).Options;

        await Assert.That(options.Fonts.FontPaths.Single()).IsEqualTo(work.Combine("fonts"));
        await Assert.That(options.Fonts.IncludeSystemFonts).IsTrue();
        await Assert.That(options.Fonts.IncludeEmbeddedFonts).IsTrue();
        await Assert.That(options.Packages.PackagePath).IsEqualTo(work.Combine("vendor", "packages"));
        await Assert.That(options.Packages.PackageCachePath).IsEqualTo(work.Combine("cache"));
        await Assert.That(options.Packages.NetworkAccess).IsEqualTo(TypstNetworkAccess.Allowed);
        await Assert.That(options.CreationTimestamp).IsEqualTo(TypstCreationTimestamp.Fixed(timestamp));
        await Assert.That(options.Timeout).IsEqualTo(TimeSpan.FromMinutes(5));
        await Assert.That(options.ExtraArguments).IsEqualTo(settings.ExtraArguments);
        await Assert.That(options.ArgumentCustomization).IsSameReferenceAs(customize);
        await Assert.That(options.MaxOutputBytes).IsEqualTo(TypstCompileOptions.Default.MaxOutputBytes);
    }

    [Test]
    public async Task Inputs_become_request_inputs()
    {
        using var work = new TemporaryDirectory("cake-map");
        var request = Map(work, TypstOutputFormat.Pdf, new TypstCompileSettings().WithInput("version", "1.2.3"));

        await Assert.That(request.Inputs["version"]).IsEqualTo("1.2.3");
    }

    [Test]
    public async Task Each_format_maps_its_own_settings()
    {
        using var work = new TemporaryDirectory("cake-map");
        var pages = TypstPageSelection.Of(TypstPageRange.Between(2, 3));

        var pdf = (TypstOutput.PdfOutput)Map(work, TypstOutputFormat.Pdf, new TypstCompileSettings().WithPdfStandard(PdfStandard.A2b).WithPages(pages)).Output;
        var png = Map(work, TypstOutputFormat.Png, new TypstCompileSettings().WithPpi(300).WithPages(pages)).Output;
        var svg = Map(work, TypstOutputFormat.Svg, new TypstCompileSettings().WithPages(pages)).Output;
        var html = Map(work, TypstOutputFormat.Html, new TypstCompileSettings()).Output;

        await Assert.That(pdf.Options.Standards.Single()).IsEqualTo(PdfStandard.A2b);
        await Assert.That(pdf.Options.Pages).IsSameReferenceAs(pages);
        await Assert.That(pdf.Options.Tagged).IsTrue();
        await Assert.That(png).IsEqualTo(TypstOutput.Png(300, pages));
        await Assert.That(svg).IsEqualTo(TypstOutput.Svg(pages));
        await Assert.That(html).IsEqualTo(TypstOutput.Html());
    }

    [Test]
    public async Task Png_defaults_to_144_ppi_and_every_page()
    {
        using var work = new TemporaryDirectory("cake-map");
        var png = Map(work, TypstOutputFormat.Png, new TypstCompileSettings()).Output;

        await Assert.That(png).IsEqualTo(TypstOutput.Png(144, TypstPageSelection.All));
    }

    [Test]
    [Arguments(TypstOutputFormat.Pdf)]
    [Arguments(TypstOutputFormat.Svg)]
    public async Task Ppi_for_anything_but_png_is_refused(TypstOutputFormat format)
    {
        using var work = new TemporaryDirectory("cake-map");
        var exception = Assert.Throws<ArgumentException>(() => Map(work, format, new TypstCompileSettings().WithPpi(300)));

        await Assert.That(exception!.Message).Contains("Ppi");
        await Assert.That(exception.ParamName).IsEqualTo("settings");
    }

    [Test]
    public async Task Pages_for_html_are_refused()
    {
        using var work = new TemporaryDirectory("cake-map");
        var exception = Assert.Throws<ArgumentException>(() =>
            Map(work, TypstOutputFormat.Html, new TypstCompileSettings().WithPages(TypstPageSelection.Of(TypstPageRange.Single(1)))));

        await Assert.That(exception!.Message).Contains("Pages");
        await Assert.That(exception.ParamName).IsEqualTo("settings");
    }

    [Test]
    [Arguments(TypstOutputFormat.Png)]
    [Arguments(TypstOutputFormat.Html)]
    public async Task Pdf_standards_for_anything_but_pdf_are_refused(TypstOutputFormat format)
    {
        using var work = new TemporaryDirectory("cake-map");
        var exception = Assert.Throws<ArgumentException>(() => Map(work, format, new TypstCompileSettings().WithPdfStandard(PdfStandard.A2b)));

        await Assert.That(exception!.Message).Contains("PdfStandards");
    }
}

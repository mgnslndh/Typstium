using Cake.Core.IO;
using Typstium;

namespace Cake.Typstium.UnitTests;

public class TypstCompileSettingsTests
{
    [Test]
    public async Task Starts_empty_with_safe_defaults()
    {
        var settings = new TypstCompileSettings();

        await Assert.That(settings.ToolPath).IsNull();
        await Assert.That(settings.Root).IsNull();
        await Assert.That(settings.Format).IsNull();
        await Assert.That(settings.PdfStandards.Count).IsEqualTo(0);
        await Assert.That(settings.Inputs.Count).IsEqualTo(0);
        await Assert.That(settings.FontPaths.Count).IsEqualTo(0);
        await Assert.That(settings.IncludeSystemFonts).IsFalse();
        await Assert.That(settings.AllowNetwork).IsFalse();
        await Assert.That(settings.TreatWarningsAsErrors).IsFalse();
        await Assert.That(settings.ExtraArguments).IsEqualTo(TypstArguments.Empty);
        await Assert.That(settings.ArgumentCustomization).IsNull();
    }

    [Test]
    public async Task Fluent_methods_set_each_setting_and_return_the_same_instance()
    {
        var settings = new TypstCompileSettings();
        Func<TypstArguments, TypstArguments> customize = arguments => arguments;
        var timestamp = new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.Zero);
        var pages = TypstPageSelection.Of(TypstPageRange.Single(1));

        var returned = settings
            .WithToolPath(new FilePath("bin/typst"))
            .WithRoot(new DirectoryPath("docs"))
            .WithFormat(TypstOutputFormat.Png)
            .WithPdfStandard(PdfStandard.A2b)
            .WithPpi(300)
            .WithPages(pages)
            .WithInput("version", "1.2.3")
            .WithFontPath(new DirectoryPath("fonts"))
            .WithSystemFonts()
            .WithPackagePath(new DirectoryPath("packages"))
            .WithPackageCachePath(new DirectoryPath("cache"))
            .WithNetwork()
            .WithCreationTimestamp(timestamp)
            .WithTimeout(TimeSpan.FromMinutes(2))
            .WithWarningsAsErrors()
            .WithExtraArguments(TypstArguments.Empty.Add("--timings"))
            .WithArgumentCustomization(customize);

        await Assert.That(returned).IsSameReferenceAs(settings);
        await Assert.That(settings.ToolPath!.FullPath).IsEqualTo("bin/typst");
        await Assert.That(settings.Root!.FullPath).IsEqualTo("docs");
        await Assert.That(settings.Format).IsEqualTo(TypstOutputFormat.Png);
        await Assert.That(settings.PdfStandards.Single()).IsEqualTo(PdfStandard.A2b);
        await Assert.That(settings.Ppi).IsEqualTo(300);
        await Assert.That(settings.Pages).IsSameReferenceAs(pages);
        await Assert.That(settings.Inputs["version"]).IsEqualTo("1.2.3");
        await Assert.That(settings.FontPaths.Single().FullPath).IsEqualTo("fonts");
        await Assert.That(settings.IncludeSystemFonts).IsTrue();
        await Assert.That(settings.PackagePath!.FullPath).IsEqualTo("packages");
        await Assert.That(settings.PackageCachePath!.FullPath).IsEqualTo("cache");
        await Assert.That(settings.AllowNetwork).IsTrue();
        await Assert.That(settings.CreationTimestamp).IsEqualTo(timestamp);
        await Assert.That(settings.Timeout).IsEqualTo(TimeSpan.FromMinutes(2));
        await Assert.That(settings.TreatWarningsAsErrors).IsTrue();
        await Assert.That(settings.ExtraArguments.Count).IsEqualTo(1);
        await Assert.That(settings.ArgumentCustomization).IsSameReferenceAs(customize);
    }

    [Test]
    public async Task Null_extra_arguments_are_refused_where_they_are_set()
    {
        var settings = new TypstCompileSettings();

        var direct = Assert.Throws<ArgumentNullException>(() => settings.ExtraArguments = null!);
        var fluent = Assert.Throws<ArgumentNullException>(() => settings.WithExtraArguments(null!));

        await Assert.That(direct!.ParamName).IsEqualTo("ExtraArguments");
        await Assert.That(fluent!.ParamName).IsEqualTo("ExtraArguments");
        await Assert.That(settings.ExtraArguments).IsEqualTo(TypstArguments.Empty);
    }

    [Test]
    public async Task WithInput_replaces_an_existing_key()
    {
        var settings = new TypstCompileSettings().WithInput("version", "1").WithInput("version", "2");

        await Assert.That(settings.Inputs.Count).IsEqualTo(1);
        await Assert.That(settings.Inputs["version"]).IsEqualTo("2");
    }

    [Test]
    public async Task Adding_methods_accumulate()
    {
        var settings = new TypstCompileSettings()
            .WithFontPath(new DirectoryPath("a")).WithFontPath(new DirectoryPath("b"))
            .WithPdfStandard(PdfStandard.A2b).WithPdfStandard(PdfStandard.UA1);

        await Assert.That(settings.FontPaths.Count).IsEqualTo(2);
        await Assert.That(settings.PdfStandards.Count).IsEqualTo(2);
    }
}

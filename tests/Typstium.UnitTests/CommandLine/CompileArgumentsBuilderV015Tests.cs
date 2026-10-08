using System.Collections.Immutable;
using TUnit.Assertions.Enums;
using Typstium.CommandLine;
using Typstium.Diagnostics;
using Typstium.UnitTests.Testing;
using Typstium.Workspace;

namespace Typstium.UnitTests.CommandLine;

public class CompileArgumentsBuilderV015Tests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "typstium", "ws-1", "root");
    private static readonly string Out = Path.Combine(Path.GetTempPath(), "typstium", "ws-1", "out");

    private static WorkspaceLayout Layout(string mainInput = "-", string? pagedOutput = null) =>
        new(Root, mainInput, ReadOnlyMemory<byte>.Empty, pagedOutput, new SourceMappingContext(Root, true, ImmutableHashSet<TypstPath>.Empty));

    private static IReadOnlyList<string> Build(TypstCompileRequest request, WorkspaceLayout? layout = null) =>
        new CompileArgumentsBuilderV015().Build(new CompileArgumentsInput(request, layout ?? Layout(), TestCapabilities.V015)).ToValues();

    [Test]
    public async Task Default_markup_pdf_compile()
    {
        var arguments = Build(new TypstCompileRequest(TypstSource.Markup("= Hi")));
        await Assert.That(arguments).IsEquivalentTo(new[]
        {
            "--color", "never", "compile", "--root", Root, "--diagnostic-format", "human",
            "--ignore-system-fonts", "--format", "pdf", "-", "-",
        }, CollectionOrdering.Matching);
    }

    [Test]
    public async Task Fonts_packages_jobs_timestamp_and_inputs()
    {
        var options = TypstCompileOptions.Default with
        {
            Fonts = TypstFontOptions.Default with { FontPaths = ImmutableArray.Create("fonts-a", "fonts-b"), IncludeEmbeddedFonts = false },
            Packages = TypstPackageOptions.Default with { PackagePath = "vendor", PackageCachePath = "cache" },
            Jobs = 2,
            CreationTimestamp = TypstCreationTimestamp.Fixed(DateTimeOffset.FromUnixTimeSeconds(1700000000)),
        };
        var request = new TypstCompileRequest(TypstSource.Markup("x"))
        {
            Options = options,
            Inputs = TypstInputs.Of(Input("z", "last"), Input("a", "first")),
        };

        var arguments = Build(request, Layout(mainInput: Path.Combine(Root, "main.typ")));

        var expectedFontPath = Path.GetFullPath("fonts-a") + Path.PathSeparator + Path.GetFullPath("fonts-b");
        await Assert.That(arguments).IsEquivalentTo(new[]
        {
            "--color", "never", "compile", "--root", Root, "--diagnostic-format", "human",
            "--font-path", expectedFontPath, "--ignore-system-fonts", "--ignore-embedded-fonts",
            "--package-path", Path.GetFullPath("vendor"), "--package-cache-path", Path.GetFullPath("cache"),
            "--jobs", "2", "--creation-timestamp", "1700000000",
            "--format", "pdf",
            "--input", "a=first", "--input", "z=last",
            Path.Combine(Root, "main.typ"), "-",
        }, CollectionOrdering.Matching);
    }

    [Test]
    public async Task Inputs_are_secret_in_the_safe_rendering()
    {
        var request = new TypstCompileRequest(TypstSource.Markup("x")) { Inputs = TypstInputs.Of(Input("license", "{\"id\":1}")) };
        var arguments = new CompileArgumentsBuilderV015().Build(new CompileArgumentsInput(request, Layout(), TestCapabilities.V015));
        await Assert.That(arguments.RenderSafe()).DoesNotContain("\"id\"");
        await Assert.That(arguments.RenderSafe()).Contains("--input [REDACTED]");
    }

    [Test]
    public async Task Png_writes_zero_padded_pages_to_the_output_folder()
    {
        var request = new TypstCompileRequest(TypstSource.Markup("x"))
        {
            Output = TypstOutput.Png(300, TypstPageSelection.Of(TypstPageRange.Between(2, 3))),
        };
        var arguments = Build(request, Layout(pagedOutput: Out));

        await Assert.That(arguments.Skip(8)).IsEquivalentTo(new[]
        {
            "--format", "png", "--ppi", "300", "--pages", "2-3", "-", Path.Combine(Out, "{0p}.png"),
        }, CollectionOrdering.Matching);
    }

    [Test]
    public async Task Pdf_standards_tags_and_html_feature()
    {
        var pdf = new TypstCompileRequest(TypstSource.Markup("x"))
        {
            Output = TypstOutput.Pdf(new TypstPdfOptions { Standards = ImmutableArray.Create(PdfStandard.A2b, PdfStandard.UA1), Tagged = false }),
        };
        var html = new TypstCompileRequest(TypstSource.Markup("x")) { Output = TypstOutput.Html() };

        await Assert.That(Build(pdf).Skip(8)).IsEquivalentTo(new[] { "--format", "pdf", "--pdf-standard", "a-2b,ua-1", "--no-pdf-tags", "-", "-" }, CollectionOrdering.Matching);
        await Assert.That(Build(html).Skip(8)).IsEquivalentTo(new[] { "--format", "html", "--features", "html", "-", "-" }, CollectionOrdering.Matching);
    }

    [Test]
    public async Task Extra_arguments_and_customization_come_last()
    {
        var options = TypstCompileOptions.Default with
        {
            ExtraGlobalArguments = TypstArguments.Of("--cert", "ca.pem"),
            ExtraArguments = TypstArguments.Of("--timings", "t.json"),
            ArgumentCustomization = args => args.Add("--custom"),
        };
        var arguments = Build(new TypstCompileRequest(TypstSource.Markup("x")) { Options = options });

        await Assert.That(arguments.Take(5)).IsEquivalentTo(new[] { "--color", "never", "--cert", "ca.pem", "compile" }, CollectionOrdering.Matching);
        await Assert.That(arguments.Skip(arguments.Count - 3)).IsEquivalentTo(new[] { "--timings", "t.json", "--custom" }, CollectionOrdering.Matching);
    }

    [Test]
    public async Task Customization_returning_null_is_an_error()
    {
        var options = TypstCompileOptions.Default with { ArgumentCustomization = _ => null! };
        var exception = Assert.Throws<InvalidOperationException>(() => Build(new TypstCompileRequest(TypstSource.Markup("x")) { Options = options }));
        await Assert.That(exception).IsNotNull();
    }

    private static TypstInput Input(string key, string value) => TypstInput.FromText(TypstInputKey.Parse(key), value);
}

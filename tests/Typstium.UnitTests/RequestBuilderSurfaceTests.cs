using System.Collections.Immutable;
using System.Text.Json.Serialization;
using TUnit.Assertions.Enums;

namespace Typstium.UnitTests;

/// <summary>
/// The fluent surface both request builders share (D10), run against each: they forward to one core, but each has its
/// own forwarding code, which these tests keep from drifting apart.
/// </summary>
public partial class RequestBuilderSurfaceTests
{
    private sealed record SurfaceModel(string Name);

    [JsonSerializable(typeof(SurfaceModel))]
    private sealed partial class SurfaceModelContext : JsonSerializerContext;

    /// <summary>Drives either builder; every call also checks that the builder returned itself.</summary>
    private sealed class Builder
    {
        private readonly TypstCompileRequestBuilder? _sandbox;
        private readonly TypstFileCompileRequestBuilder? _file;

        private Builder(TypstCompileRequestBuilder? sandbox, TypstFileCompileRequestBuilder? file)
        {
            _sandbox = sandbox;
            _file = file;
        }

        public static Builder For(string kind) => kind == "file"
            ? new Builder(null, TypstCompileRequest.ForFile("docs/spec.typ"))
            : new Builder(TypstCompileRequest.ForMarkup("= Hi"), null);

        public Builder WithInput(string key, string value) => Fluent(b => b.WithInput(key, value), f => f.WithInput(key, value));

        public Builder WithInput(TypstInput input) => Fluent(b => b.WithInput(input), f => f.WithInput(input));

        public Builder WithJsonInput(string key, SurfaceModel value) => Fluent(b => b.WithJsonInput(key, value), f => f.WithJsonInput(key, value));

        public Builder WithJsonInput(string key, SurfaceModel value, SurfaceModelContext context) =>
            Fluent(b => b.WithJsonInput(key, value, context.SurfaceModel), f => f.WithJsonInput(key, value, context.SurfaceModel));

        public Builder WithOutput(TypstOutput output) => Fluent(b => b.WithOutput(output), f => f.WithOutput(output));

        public Builder ToPdf(params PdfStandard[] standards) => Fluent(b => b.ToPdf(standards), f => f.ToPdf(standards));

        public Builder ToPng() => Fluent(b => b.ToPng(), f => f.ToPng());

        public Builder ToPng(int ppi) => Fluent(b => b.ToPng(ppi), f => f.ToPng(ppi));

        public Builder ToSvg() => Fluent(b => b.ToSvg(), f => f.ToSvg());

        public Builder WithOptions(TypstCompileOptions options) => Fluent(b => b.WithOptions(options), f => f.WithOptions(options));

        public Builder ConfigureOptions(Func<TypstCompileOptions, TypstCompileOptions> configure) =>
            Fluent(b => b.ConfigureOptions(configure), f => f.ConfigureOptions(configure));

        public TypstCompileRequest Build() => _sandbox?.Build() ?? _file!.Build();

        private Builder Fluent(Func<TypstCompileRequestBuilder, object> sandbox, Func<TypstFileCompileRequestBuilder, object> file)
        {
            object? builder = _sandbox ?? (object?)_file;
            var returned = _sandbox is not null ? sandbox(_sandbox) : file(_file!);
            if (!ReferenceEquals(returned, builder))
            {
                throw new InvalidOperationException("A fluent builder method returned a different object than the builder.");
            }

            return this;
        }
    }

    [Test]
    [Arguments("markup")]
    [Arguments("file")]
    public async Task An_untouched_builder_compiles_to_pdf_with_default_options_and_no_inputs(string kind)
    {
        var request = Builder.For(kind).Build();

        await Assert.That(request.Output).IsEqualTo(TypstOutput.Pdf());
        await Assert.That(request.Options).IsEqualTo(TypstCompileOptions.Default);
        await Assert.That(request.Inputs.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments("markup")]
    [Arguments("file")]
    public async Task Inputs_from_text_typed_and_json_overloads_all_land_in_the_request(string kind)
    {
        var request = Builder.For(kind)
            .WithInput("edition", "Community")
            .WithInput(TypstInput.FromText(TypstInputKey.Parse("region"), "EU"))
            .WithJsonInput("reflected", new SurfaceModel("a"))
            .WithJsonInput("generated", new SurfaceModel("b"), SurfaceModelContext.Default)
            .WithInput("edition", "Enterprise")
            .Build();

        await Assert.That(request.Inputs.Select(input => $"{input.Key}={input.Value}")).IsEquivalentTo(new[]
        {
            "edition=Enterprise",
            "generated={\"Name\":\"b\"}",
            "reflected={\"name\":\"a\"}",
            "region=EU",
        }, CollectionOrdering.Matching);
    }

    [Test]
    [Arguments("markup")]
    [Arguments("file")]
    public async Task Output_shortcuts_pick_the_format_and_its_options(string kind)
    {
        var plainPdf = Builder.For(kind).ToPdf().Build().Output;
        var archivalPdf = (TypstOutput.PdfOutput)Builder.For(kind).ToPdf(PdfStandard.A2b, PdfStandard.UA1).Build().Output;
        var defaultPng = (TypstOutput.PngOutput)Builder.For(kind).ToPng().Build().Output;
        var sharpPng = (TypstOutput.PngOutput)Builder.For(kind).ToPng(300).Build().Output;
        var svg = Builder.For(kind).ToSvg().Build().Output;
        var html = Builder.For(kind).WithOutput(TypstOutput.Html()).Build().Output;

        await Assert.That(plainPdf).IsEqualTo(TypstOutput.Pdf());
        await Assert.That(archivalPdf.Options.Standards).IsEquivalentTo(new[] { PdfStandard.A2b, PdfStandard.UA1 }, CollectionOrdering.Matching);
        await Assert.That(defaultPng.Ppi).IsEqualTo(144);
        await Assert.That(sharpPng.Ppi).IsEqualTo(300);
        await Assert.That(svg.Format).IsEqualTo(TypstOutputFormat.Svg);
        await Assert.That(html.Format).IsEqualTo(TypstOutputFormat.Html);
    }

    [Test]
    [Arguments("markup")]
    [Arguments("file")]
    public async Task Options_are_replaced_then_configured_from_the_current_value(string kind)
    {
        var request = Builder.For(kind)
            .WithOptions(TypstCompileOptions.Untrusted)
            .ConfigureOptions(options => options with { Jobs = 2 })
            .Build();

        await Assert.That(request.Options).IsEqualTo(TypstCompileOptions.Untrusted with { Jobs = 2 });
    }

    [Test]
    [Arguments("markup")]
    [Arguments("file")]
    public async Task Null_outputs_options_and_inputs_are_rejected(string kind)
    {
        await Assert.That(Assert.Throws<ArgumentNullException>(() => Builder.For(kind).WithOutput(null!))!.ParamName).IsEqualTo("output");
        await Assert.That(Assert.Throws<ArgumentNullException>(() => Builder.For(kind).WithOptions(null!))!.ParamName).IsEqualTo("options");
        await Assert.That(Assert.Throws<ArgumentNullException>(() => Builder.For(kind).WithInput(null!))!.ParamName).IsEqualTo("input");
    }
}

using System.Text;
using Typstium.Testing.Fakes;
using TUnit.Assertions.Enums;

namespace Typstium.UnitTests;

public class TypstCompileRequestBuilderTests
{
    private sealed record Model(string Name);

    [Test]
    public async Task Template_builder_collects_files_inputs_output_and_options()
    {
        var provider = new InMemoryTemplateProvider();
        var request = TypstCompileRequest.ForTemplate(provider, "license")
            .WithJson("data.json", new Model("Asa"))
            .WithFile("logo.png", new byte[] { 1, 2 })
            .WithText("notes.md", "# Notes")
            .WithInput("edition", "Enterprise")
            .ToPdf(PdfStandard.A2b)
            .WithOptions(TypstCompileOptions.Untrusted)
            .Build();

        var source = (TypstSource.TemplateSource)request.Source;
        await Assert.That(source.Files.Select(f => f.Path.Value)).IsEquivalentTo(new[] { "data.json", "logo.png", "notes.md" }, CollectionOrdering.Matching);
        await Assert.That(Encoding.UTF8.GetString(source.Files[0].Content.ToArray())).IsEqualTo("{\"name\":\"Asa\"}");
        await Assert.That(request.Inputs["edition"]).IsEqualTo("Enterprise");
        await Assert.That(((TypstOutput.PdfOutput)request.Output).Options.Standards[0]).IsEqualTo(PdfStandard.A2b);
        await Assert.That(request.Options).IsEqualTo(TypstCompileOptions.Untrusted);
    }

    [Test]
    public async Task File_builder_passes_json_through_inputs()
    {
        var request = TypstCompileRequest.ForFile("docs/spec.typ")
            .WithJsonInput("data", new Model("x"))
            .ToPng(300)
            .ConfigureOptions(o => o with { Jobs = 2 })
            .Build();

        await Assert.That(request.Source is TypstSource.FileSource).IsTrue();
        await Assert.That(request.Inputs["data"]).IsEqualTo("{\"name\":\"x\"}");
        await Assert.That(((TypstOutput.PngOutput)request.Output).Ppi).IsEqualTo(300);
        await Assert.That(request.Options.Jobs).IsEqualTo(2);
    }

    [Test]
    public async Task Builder_rejects_case_colliding_files_at_build_time()
    {
        var builder = TypstCompileRequest.ForMarkup("= Hi").WithText("Data.json", "{}").WithText("data.json", "{}");
        var exception = Assert.Throws<ArgumentException>(() => builder.Build());
        await Assert.That(exception!.Message).Contains("differ only by case");
    }

    [Test]
    public async Task Invalid_input_keys_fail_immediately()
    {
        var exception = Assert.Throws<ArgumentException>(() => TypstCompileRequest.ForMarkup("x").WithInput("a=b", "c"));
        await Assert.That(exception!.ParamName).IsEqualTo("key");
    }

    [Test]
    public async Task Setting_an_input_again_replaces_it()
    {
        var request = TypstCompileRequest.ForMarkup("x")
            .WithInput("edition", "Community")
            .WithInput(TypstInput.FromText(TypstInputKey.Parse("edition"), "Enterprise"))
            .Build();

        await Assert.That(request.Inputs.Count).IsEqualTo(1);
        await Assert.That(request.Inputs["edition"]).IsEqualTo("Enterprise");
    }
}

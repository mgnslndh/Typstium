namespace Typstium.UnitTests.Templates;

public class EmbeddedResourceTemplateProviderTests
{
    private static readonly EmbeddedResourceTemplateProvider Provider =
        new(typeof(EmbeddedResourceTemplateProviderTests).Assembly, "TestTemplates");

    [Test]
    public async Task Loads_templates_from_logical_names()
    {
        var template = await Provider.GetTemplateAsync(TypstTemplateName.Parse("greeting"), CancellationToken.None);

        await Assert.That(template).IsNotNull();
        await Assert.That(template!.MainFile).IsEqualTo(TypstPath.Parse("greeting.typ"));
        await Assert.That(template.Files.Select(file => file.Path.Value)).IsEquivalentTo(new[] { "assets/logo.svg", "greeting.typ" });

        var logo = template.Files.Single(file => file.Path.Value == "assets/logo.svg");
        using var stream = await logo.OpenReadAsync(CancellationToken.None);
        using var reader = new StreamReader(stream);
        await Assert.That(await reader.ReadToEndAsync()).Contains("<svg");
    }

    [Test]
    public async Task Unknown_templates_return_null()
    {
        var template = await Provider.GetTemplateAsync(TypstTemplateName.Parse("nope"), CancellationToken.None);
        await Assert.That(template).IsNull();
    }
}

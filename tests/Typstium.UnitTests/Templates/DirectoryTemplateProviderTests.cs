using Typstium.Testing;
using Typstium.Testing.Fixtures;

namespace Typstium.UnitTests.Templates;

public class DirectoryTemplateProviderTests
{
    private static readonly DirectoryTemplateProvider Samples = new(Path.Combine(RepositoryPaths.Samples, "templates"));

    [Test]
    public async Task Loads_the_license_sample_as_a_bundle()
    {
        var template = await Samples.GetTemplateAsync(TypstTemplateName.Parse("license"), CancellationToken.None);

        await Assert.That(template).IsNotNull();
        await Assert.That(template!.MainFile).IsEqualTo(TypstPath.Parse("license.typ"));
        await Assert.That(template.Files.Select(file => file.Path.Value)).Contains("logo.svg");
        await Assert.That(template.Version.Value).StartsWith("sha256:");
    }

    [Test]
    public async Task Version_is_stable_and_changes_with_content()
    {
        using var temp = new TemporaryDirectory("templates");
        var file = temp.CreateFile(Path.Combine("invoice", "main.typ"), "= Invoice");
        var provider = new DirectoryTemplateProvider(temp.Path);
        var name = TypstTemplateName.Parse("invoice");

        var first = await provider.GetTemplateAsync(name, CancellationToken.None);
        var second = await provider.GetTemplateAsync(name, CancellationToken.None);
        File.WriteAllText(file, "= Invoice v2");
        var third = await provider.GetTemplateAsync(name, CancellationToken.None);

        await Assert.That(first!.MainFile).IsEqualTo(TypstPath.Parse("main.typ"));
        await Assert.That(second!.Version).IsEqualTo(first.Version);
        await Assert.That(third!.Version.Equals(first.Version)).IsFalse();
    }

    [Test]
    public async Task Unknown_templates_return_null_and_bundles_without_a_main_file_are_invalid()
    {
        using var temp = new TemporaryDirectory("templates");
        temp.CreateFile(Path.Combine("broken", "other.typ"), "= Other");
        var provider = new DirectoryTemplateProvider(temp.Path);

        var missing = await provider.GetTemplateAsync(TypstTemplateName.Parse("nope"), CancellationToken.None);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetTemplateAsync(TypstTemplateName.Parse("broken"), CancellationToken.None).AsTask());

        await Assert.That(missing).IsNull();
        await Assert.That(exception!.Message).Contains("broken.typ");
        await Assert.That(exception.Message).Contains("main.typ");
    }
}

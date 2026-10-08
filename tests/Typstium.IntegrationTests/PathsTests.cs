using Typstium.Testing.Fakes;
using Typstium.Testing.Fixtures;
using Typstium.Testing.Typst;

namespace Typstium.IntegrationTests;

public class PathsTests
{
    [Test]
    public async Task Temp_root_with_spaces_and_non_ascii_characters_works_and_maps_diagnostics()
    {
        using var temp = new TemporaryDirectory("it-paths");
        var tempRoot = Path.Combine(temp.Path, "Åsa Ström", "typstium");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = tempRoot });
        var templates = new InMemoryTemplateProvider()
            .Add("ok", "ok.typ", ("ok.typ", "= Fine"))
            .Add("broken", "broken.typ", ("broken.typ", "#nope"));

        var ok = await compiler.CompileTemplateAsync(templates, "ok");
        var broken = await compiler.CompileTemplateAsync(templates, "broken");

        await Assert.That(ok.IsSuccess).IsTrue();
        var error = broken.Errors.Single();
        await Assert.That(error.Location!.File).IsEqualTo((TypstSourceFile)new TypstSourceFile.Project(TypstPath.Parse("broken.typ"), null));
        await Assert.That(error.ToString()).DoesNotContain("Åsa Ström");
    }
}

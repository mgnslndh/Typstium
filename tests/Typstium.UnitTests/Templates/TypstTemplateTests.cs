using System.Text;
using Typstium.Testing.Fixtures;

namespace Typstium.UnitTests.Templates;

public class TypstTemplateTests
{
    private static readonly TypstTemplateName Name = TypstTemplateName.Parse("license");
    private static readonly TypstTemplateVersion Version = new("v1");

    [Test]
    public async Task A_valid_bundle_keeps_its_files()
    {
        var main = TypstTemplateFile.FromBytes(TypstPath.Parse("license.typ"), Encoding.UTF8.GetBytes("= Hi"));
        var logo = TypstTemplateFile.FromBytes(TypstPath.Parse("logo.svg"), new byte[] { 1, 2 });

        var template = new TypstTemplate(Name, TypstPath.Parse("license.typ"), Version, new[] { main, logo });

        await Assert.That(template.Files.Count).IsEqualTo(2);
        await Assert.That(template.MainFile).IsEqualTo(TypstPath.Parse("/license.typ"));
    }

    [Test]
    public async Task The_main_file_must_be_part_of_the_bundle()
    {
        var logo = TypstTemplateFile.FromBytes(TypstPath.Parse("logo.svg"), new byte[] { 1 });
        var exception = Assert.Throws<ArgumentException>(() => new TypstTemplate(Name, TypstPath.Parse("license.typ"), Version, new[] { logo }));
        await Assert.That(exception!.ParamName).IsEqualTo("mainFile");
    }

    [Test]
    public async Task Paths_differing_only_by_case_are_rejected()
    {
        var a = TypstTemplateFile.FromBytes(TypstPath.Parse("Logo.svg"), new byte[] { 1 });
        var b = TypstTemplateFile.FromBytes(TypstPath.Parse("logo.svg"), new byte[] { 2 });
        var main = TypstTemplateFile.FromBytes(TypstPath.Parse("license.typ"), new byte[] { 3 });
        var exception = Assert.Throws<ArgumentException>(() => new TypstTemplate(Name, TypstPath.Parse("license.typ"), Version, new[] { main, a, b }));
        await Assert.That(exception!.Message).Contains("differ only by case");
    }

    [Test]
    public async Task Files_open_their_content()
    {
        using var temp = new TemporaryDirectory("template-file");
        var path = temp.CreateFile("a.typ", "from disk");

        var fromFile = TypstTemplateFile.FromFile(TypstPath.Parse("a.typ"), path);
        var fromBytes = TypstTemplateFile.FromBytes(TypstPath.Parse("b.typ"), Encoding.UTF8.GetBytes("from memory"));

        await Assert.That(await ReadAsync(fromFile)).IsEqualTo("from disk");
        await Assert.That(await ReadAsync(fromBytes)).IsEqualTo("from memory");
    }

    private static async Task<string> ReadAsync(TypstTemplateFile file)
    {
        using var stream = await file.OpenReadAsync(CancellationToken.None);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}

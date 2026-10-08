using System.Collections.Immutable;
using Typstium.Testing.Fakes;

namespace Typstium.UnitTests.Sources;

public class TypstSourceTests
{
    private static TypstVirtualFile Text(string path) => TypstVirtualFile.FromText(TypstPath.Parse(path), "x");

    [Test]
    public async Task File_sources_hold_a_path_and_optional_root()
    {
        var source = TypstSource.File("docs/spec.typ", "docs");
        var file = source as TypstSource.FileSource;
        await Assert.That(file).IsNotNull();
        await Assert.That(file!.Root).IsEqualTo("docs");
    }

    [Test]
    public async Task Template_and_markup_sources_normalize_default_file_arrays()
    {
        var template = (TypstSource.TemplateSource)TypstSource.Template(new InMemoryTemplateProvider(), TypstTemplateName.Parse("a"), default);
        var markup = (TypstSource.MarkupSource)TypstSource.Markup("= Hi");
        await Assert.That(template.Files.IsDefault).IsFalse();
        await Assert.That(markup.Files.Length).IsEqualTo(0);
        await Assert.That(markup.Text).IsEqualTo("= Hi");
    }

    [Test]
    public async Task Virtual_files_differing_only_by_case_are_rejected()
    {
        var files = ImmutableArray.Create(Text("Data.json"), Text("data.json"));
        var exception = Assert.Throws<ArgumentException>(() => TypstSource.Markup("= Hi", files));
        await Assert.That(exception!.Message).Contains("differ only by case");
        await Assert.That(exception.ParamName).IsEqualTo("files");
    }

    [Test]
    public async Task Exact_duplicate_virtual_files_are_rejected()
    {
        var files = ImmutableArray.Create(Text("data.json"), Text("/data.json"));
        var exception = Assert.Throws<ArgumentException>(() => TypstSource.Markup("= Hi", files));
        await Assert.That(exception!.Message).Contains("more than once");
    }

    [Test]
    public async Task File_source_requires_a_path()
    {
        var exception = Assert.Throws<ArgumentException>(() => TypstSource.File(" "));
        await Assert.That(exception).IsNotNull();
    }
}

using System.Text;
using System.Text.RegularExpressions;
using Typstium.Templates;

namespace Typstium.UnitTests.Templates;

public class TemplateHasherTests
{
    private static TypstTemplateFile File(string path, string content) =>
        TypstTemplateFile.FromBytes(TypstPath.Parse(path), Encoding.UTF8.GetBytes(content));

    private static Task<TypstTemplateVersion> Version(params TypstTemplateFile[] files) =>
        TemplateHasher.ComputeAsync(files, CancellationToken.None);

    // The review's case: with path-NUL-content-NUL framing, content holding "\0b.bin\0" forged a second file.
    [Test]
    public async Task Content_containing_nul_bytes_cannot_forge_file_boundaries()
    {
        var oneFile = await Version(File("main.typ", "= Hi"), File("a.bin", "X\0b.bin\0Y"));
        var twoFiles = await Version(File("main.typ", "= Hi"), File("a.bin", "X"), File("b.bin", "Y"));

        await Assert.That(oneFile).IsNotEqualTo(twoFiles);
    }

    [Test]
    public async Task Any_change_to_names_contents_or_boundaries_changes_the_version()
    {
        var versions = new[]
        {
            await Version(File("a.typ", "ab"), File("b.typ", "cd")),
            await Version(File("a.typ", "a"), File("b.typ", "bcd")),
            await Version(File("a.typ", "cd"), File("b.typ", "ab")),
            await Version(File("a.typ", "ab"), File("c.typ", "cd")),
            await Version(File("a.typ", "ab"), File("b.typ", "cd"), File("e.typ", "")),
            await Version(File("a.typ", "abcd")),
            await Version(File("a", "b.typ"), File("a.typ", "")),
        };

        await Assert.That(versions.Distinct().Count()).IsEqualTo(versions.Length);
    }

    [Test]
    public async Task The_same_bundle_gets_the_same_version_in_any_listing_order()
    {
        var forward = await Version(File("a.typ", "1"), File("b/c.typ", "2"), File("d.typ", "3"));
        var backward = await Version(File("d.typ", "3"), File("b/c.typ", "2"), File("a.typ", "1"));

        await Assert.That(backward).IsEqualTo(forward);
        await Assert.That(Regex.IsMatch(forward.Value, "^sha256:[0-9a-f]{64}\\z")).IsTrue();
    }
}

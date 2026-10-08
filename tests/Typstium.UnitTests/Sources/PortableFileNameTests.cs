using System.Collections.Immutable;
using System.Text;

namespace Typstium.UnitTests.Sources;

public class PortableFileNameTests
{
    [Test]
    [Arguments("data.json.", "ends with a dot or a space")]
    [Arguments("data.json ", "ends with a dot or a space")]
    [Arguments("nul", "reserved device name")]
    [Arguments("NUL.json", "reserved device name")]
    [Arguments("con.tar.gz", "reserved device name")]
    [Arguments("Aux", "reserved device name")]
    [Arguments("com1.txt", "reserved device name")]
    [Arguments("LPT9", "reserved device name")]
    [Arguments("com¹", "reserved device name")]
    [Arguments("nul .txt", "reserved device name")]
    [Arguments("tab\there", "control character")]
    public async Task Names_Windows_would_alias_or_reject_are_not_portable(string segment, string reason)
    {
        await Assert.That(PortableFileName.Problem(segment)).Contains(reason);
    }

    [Test]
    [Arguments("data.json")]
    [Arguments("console.typ")]
    [Arguments("nullable.json")]
    [Arguments("com10.txt")]
    [Arguments("lpt")]
    [Arguments(".hidden")]
    [Arguments("a..b")]
    [Arguments(" leading space.typ")]
    [Arguments("DATAFI~1.JSO")]
    public async Task Ordinary_names_are_portable(string segment)
    {
        await Assert.That(PortableFileName.Problem(segment)).IsNull();
    }

    [Test]
    [Arguments("data.json.")]
    [Arguments("assets/nul.png")]
    [Arguments("con/logo.svg")]
    public async Task Virtual_files_with_non_portable_names_are_rejected(string path)
    {
        var file = TypstVirtualFile.FromText(TypstPath.Parse(path), "x");

        var exception = Assert.Throws<ArgumentException>(() => TypstSource.Markup("= Hi", ImmutableArray.Create(file)));

        await Assert.That(exception!.Message).Contains(path);
    }

    [Test]
    public async Task Template_bundles_with_non_portable_names_are_rejected()
    {
        var files = new[]
        {
            TypstTemplateFile.FromBytes(TypstPath.Parse("main.typ"), Encoding.UTF8.GetBytes("= Hi")),
            TypstTemplateFile.FromBytes(TypstPath.Parse("aux.typ"), Encoding.UTF8.GetBytes("x")),
        };

        var exception = Assert.Throws<ArgumentException>(() =>
            new TypstTemplate(TypstTemplateName.Parse("t"), TypstPath.Parse("main.typ"), new TypstTemplateVersion("v1"), files));

        await Assert.That(exception!.Message).Contains("reserved device name");
    }

    [Test]
    [Arguments("invoices.")]
    [Arguments("con")]
    [Arguments("reports/nul")]
    [Arguments("lpt1.typ")]
    public async Task Template_names_that_Windows_would_alias_are_rejected(string name)
    {
        await Assert.That(TypstTemplateName.TryParse(name, out _)).IsFalse();
    }

    // TypstPath also maps paths Typst prints back to the caller's files, which on Linux may have these names.
    [Test]
    [Arguments("aux.typ")]
    [Arguments("notes.")]
    public async Task TypstPath_itself_still_accepts_names_only_Windows_rejects(string path)
    {
        await Assert.That(TypstPath.TryParse(path, out _)).IsTrue();
    }
}

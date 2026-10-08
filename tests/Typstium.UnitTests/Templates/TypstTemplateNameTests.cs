namespace Typstium.UnitTests.Templates;

public class TypstTemplateNameTests
{
    [Test]
    [Arguments("license", "license", "license")]
    [Arguments("invoices/standard", "invoices/standard", "standard")]
    [Arguments("release-notes_v2.1", "release-notes_v2.1", "release-notes_v2.1")]
    public async Task Parse_accepts_valid_names(string input, string value, string lastSegment)
    {
        var name = TypstTemplateName.Parse(input);
        await Assert.That(name.Value).IsEqualTo(value);
        await Assert.That(name.LastSegment).IsEqualTo(lastSegment);
    }

    [Test]
    [Arguments("")]
    [Arguments("../license")]
    [Arguments("a/../b")]
    [Arguments("/license")]
    [Arguments("license/")]
    [Arguments("lic ense")]
    [Arguments("lic\\ense")]
    [Arguments("a//b")]
    [Arguments("license\n")]
    public async Task Parse_rejects_invalid_names(string input)
    {
        var parsed = TypstTemplateName.TryParse(input, out _);
        await Assert.That(parsed).IsFalse();
    }
}

namespace Typstium.UnitTests.Diagnostics;

public class TypstDiagnosticIdTests
{
    [Test]
    public async Task Parse_accepts_the_typst_prefix_and_four_digits()
    {
        var id = TypstDiagnosticId.Parse("TYPST1001");
        await Assert.That(id.Value).IsEqualTo("TYPST1001");
        await Assert.That(id.Number).IsEqualTo(1001);
        await Assert.That(id).IsEqualTo(TypstDiagnosticId.Parse("TYPST1001"));
    }

    [Test]
    [Arguments("TYPST101")]
    [Arguments("typst1001")]
    [Arguments("CS1001")]
    [Arguments("TYPST10011")]
    [Arguments("TYPST1001\n")]
    public async Task Parse_rejects_other_formats(string input)
    {
        var exception = Assert.Throws<ArgumentException>(() => TypstDiagnosticId.Parse(input));
        await Assert.That(exception).IsNotNull();
    }
}

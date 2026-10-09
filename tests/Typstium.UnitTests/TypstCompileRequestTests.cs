namespace Typstium.UnitTests;

public class TypstCompileRequestTests
{
    [Test]
    public async Task Defaults_to_pdf_with_default_options()
    {
        var request = new TypstCompileRequest(TypstSource.Markup("= Hi"));
        await Assert.That(request.Output.Format).IsEqualTo(TypstOutputFormat.Pdf);
        await Assert.That(request.Options).IsEqualTo(TypstCompileOptions.Default);
        await Assert.That(request.Inputs.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Inputs_cannot_be_null()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new TypstCompileRequest(TypstSource.Markup("x")) { Inputs = null! });
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task Source_cannot_be_null()
    {
        await Assert.That(Assert.Throws<ArgumentNullException>(() => new TypstCompileRequest(null!))).IsNotNull();
    }
}

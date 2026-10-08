using System.Collections.Immutable;

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
    [Arguments("")]
    [Arguments(" ")]
    [Arguments("a=b")]
    public async Task Input_keys_must_be_non_empty_and_free_of_equals_signs(string key)
    {
        var inputs = ImmutableDictionary<string, string>.Empty.Add(key, "value");
        var exception = Assert.Throws<ArgumentException>(() => new TypstCompileRequest(TypstSource.Markup("x")) { Inputs = inputs });
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task Source_cannot_be_null()
    {
        await Assert.That(Assert.Throws<ArgumentNullException>(() => new TypstCompileRequest(null!))).IsNotNull();
    }
}

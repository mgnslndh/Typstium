using Typstium.Testing.Fakes;

namespace Typstium.UnitTests;

public class TypstCompilerExtensionsTests
{
    [Test]
    public async Task Each_extension_builds_the_matching_request()
    {
        var compiler = new RecordingTypstCompiler();
        var provider = new InMemoryTemplateProvider();

        await compiler.CompileFileAsync("docs/spec.typ");
        await compiler.CompileMarkupAsync("= Hi", TypstOutput.Svg());
        await compiler.CompileTemplateAsync(provider, "license", TypstOutput.Pdf(), TypstCompileOptions.Untrusted);
        await compiler.CompileAsync(TypstCompileRequest.ForMarkup("= Builder"));

        var requests = compiler.Requests;
        await Assert.That(requests.Count).IsEqualTo(4);
        await Assert.That(requests[0].Source is TypstSource.FileSource).IsTrue();
        await Assert.That(requests[1].Output.Format).IsEqualTo(TypstOutputFormat.Svg);
        await Assert.That(requests[2].Options).IsEqualTo(TypstCompileOptions.Untrusted);
        await Assert.That(((TypstSource.MarkupSource)requests[3].Source).Text).IsEqualTo("= Builder");
    }
}

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

    [Test]
    public async Task Overloads_without_a_token_pass_none_and_overloads_with_a_token_forward_it()
    {
        var compiler = new TokenRecordingCompiler();
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;

        await compiler.CompileMarkupAsync("= Hi");
        await compiler.CompileMarkupAsync("= Hi", token);
        await compiler.CompileFileAsync("docs/spec.typ", TypstOutput.Png(), TypstCompileOptions.Default, token);
        await compiler.CompileAsync(TypstCompileRequest.ForFile("docs/spec.typ"), token);

        await Assert.That(compiler.Tokens[0]).IsEqualTo(CancellationToken.None);
        await Assert.That(compiler.Tokens.Skip(1).All(recorded => recorded == token)).IsTrue();
    }

    private sealed class TokenRecordingCompiler : ITypstCompiler
    {
        private readonly RecordingTypstCompiler _inner = new();

        public List<CancellationToken> Tokens { get; } = new();

        public Task<TypstCompileResult> CompileAsync(TypstCompileRequest request, CancellationToken cancellationToken = default)
        {
            Tokens.Add(cancellationToken);
            return _inner.CompileAsync(request, cancellationToken);
        }
    }
}

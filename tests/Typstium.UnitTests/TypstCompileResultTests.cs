using System.Collections.Immutable;
using Typstium.Testing.Fakes;

namespace Typstium.UnitTests;

public class TypstCompileResultTests
{
    private static readonly TypstDiagnostic Error = new(
        TypstDiagnosticId.Parse("TYPST0001"), TypstDiagnosticSeverity.Error, "boom", null, default, default);

    [Test]
    public async Task Succeeded_results_carry_a_document()
    {
        var document = TypstDocument.CreateSingleFile(TypstOutputFormat.Pdf, new byte[] { 1 });
        var result = TypstCompileResult.CreateSucceeded(document, ImmutableArray<TypstDiagnostic>.Empty, FakeInvocation.Create());
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Document).IsNotNull();
        await Assert.That(result.EnsureSuccess()).IsEqualTo(result);
    }

    [Test]
    public async Task Unsuccessful_results_have_no_document_and_throw_from_EnsureSuccess()
    {
        var result = TypstCompileResult.CreateUnsuccessful(TypstCompileStatus.Failed, ImmutableArray.Create(Error), FakeInvocation.Create());
        await Assert.That(result.Document).IsNull();
        await Assert.That(result.Errors.Count()).IsEqualTo(1);
        var exception = Assert.Throws<TypstCompileException>(() => result.EnsureSuccess());
        await Assert.That(exception!.Result).IsEqualTo(result);
        await Assert.That(exception.Message).Contains("boom");
    }

    [Test]
    public async Task CreateUnsuccessful_rejects_the_succeeded_status()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            TypstCompileResult.CreateUnsuccessful(TypstCompileStatus.Succeeded, ImmutableArray<TypstDiagnostic>.Empty, FakeInvocation.Create()));
        await Assert.That(exception).IsNotNull();
    }
}

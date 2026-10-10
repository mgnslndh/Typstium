using System.Collections.Immutable;
using Typstium;
using Typstium.Testing.Fakes;

namespace Cake.Typstium.UnitTests.Testing;

/// <summary>Returns the same result for every request and remembers the last request.</summary>
internal sealed class FixedResultCompiler(TypstCompileResult result) : ITypstCompiler
{
    public TypstCompileRequest? LastRequest { get; private set; }

    public Task<TypstCompileResult> CompileAsync(TypstCompileRequest request, CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        return Task.FromResult(result);
    }

    public static FixedResultCompiler Pdf(params TypstDiagnostic[] diagnostics) =>
        new(TypstCompileResult.CreateSucceeded(TypstDocument.CreateSingleFile(TypstOutputFormat.Pdf, "%PDF-1.7"u8.ToArray()),
            [.. diagnostics], FakeInvocation.Create()));

    public static FixedResultCompiler Pages(TypstOutputFormat format, int count) =>
        new(TypstCompileResult.CreateSucceeded(
            TypstDocument.CreatePaged(format, Enumerable.Range(1, count).Select(page => (ReadOnlyMemory<byte>)new[] { (byte)page }).ToImmutableArray()),
            ImmutableArray<TypstDiagnostic>.Empty, FakeInvocation.Create()));

    public static FixedResultCompiler Unsuccessful(TypstCompileStatus status, params TypstDiagnostic[] diagnostics) =>
        new(TypstCompileResult.CreateUnsuccessful(status, [.. diagnostics], FakeInvocation.Create()));
}

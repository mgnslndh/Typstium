using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Typstium.Testing.Fakes;

/// <summary>Records every request and returns a successful one-byte PDF.</summary>
public sealed class RecordingTypstCompiler : ITypstCompiler
{
    private readonly ConcurrentQueue<TypstCompileRequest> _requests = new();

    public IReadOnlyList<TypstCompileRequest> Requests => _requests.ToArray();

    public Task<TypstCompileResult> CompileAsync(TypstCompileRequest request, CancellationToken cancellationToken = default)
    {
        _requests.Enqueue(request);
        var document = TypstDocument.CreateSingleFile(TypstOutputFormat.Pdf, new byte[] { 37 });
        return Task.FromResult(TypstCompileResult.CreateSucceeded(document, ImmutableArray<TypstDiagnostic>.Empty, FakeInvocation.Create()));
    }
}

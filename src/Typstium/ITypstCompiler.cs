namespace Typstium;

/// <summary>Compiles Typst documents. The only method a backend implements; everything else builds on it.</summary>
public interface ITypstCompiler
{
    /// <summary>Compiles a request.</summary>
    /// <param name="request">What to compile, to which output and with which options.</param>
    /// <param name="cancellationToken">Cancels the compile and stops Typst.</param>
    /// <returns>The result. Template errors, timeouts and the output limit are reported here, not thrown.</returns>
    /// <exception cref="TypstException">Typst couldn't be found, started or understood; see the derived exceptions.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled; Typst is stopped.</exception>
    Task<TypstCompileResult> CompileAsync(TypstCompileRequest request, CancellationToken cancellationToken = default);
}

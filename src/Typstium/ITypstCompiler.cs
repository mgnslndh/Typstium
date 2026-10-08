namespace Typstium;

/// <summary>Compiles Typst documents. The only method a backend implements; everything else builds on it.</summary>
public interface ITypstCompiler
{
    Task<TypstCompileResult> CompileAsync(TypstCompileRequest request, CancellationToken cancellationToken = default);
}

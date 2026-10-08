namespace Typstium;

/// <summary>
/// Discoverable one-liners. Each builds a <see cref="TypstCompileRequest"/> and calls
/// <see cref="ITypstCompiler.CompileAsync"/>, so every backend supports them.
/// </summary>
public static class TypstCompilerExtensions
{
    public static Task<TypstCompileResult> CompileAsync(this ITypstCompiler compiler, TypstCompileRequestBuilder builder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(builder);
        return compiler.CompileAsync(builder.Build(), cancellationToken);
    }

    public static Task<TypstCompileResult> CompileAsync(this ITypstCompiler compiler, TypstFileCompileRequestBuilder builder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(builder);
        return compiler.CompileAsync(builder.Build(), cancellationToken);
    }

    public static Task<TypstCompileResult> CompileAsync(this ITypstCompiler compiler, TypstSource source, CancellationToken cancellationToken = default) =>
        Compile(compiler, new TypstCompileRequest(source), cancellationToken);

    public static Task<TypstCompileResult> CompileAsync(this ITypstCompiler compiler, TypstSource source, TypstOutput output, CancellationToken cancellationToken = default) =>
        Compile(compiler, new TypstCompileRequest(source) { Output = output }, cancellationToken);

    public static Task<TypstCompileResult> CompileAsync(this ITypstCompiler compiler, TypstSource source, TypstOutput output, TypstCompileOptions options, CancellationToken cancellationToken = default) =>
        Compile(compiler, new TypstCompileRequest(source) { Output = output, Options = options }, cancellationToken);

    public static Task<TypstCompileResult> CompileFileAsync(this ITypstCompiler compiler, string path, CancellationToken cancellationToken = default) =>
        compiler.CompileAsync(TypstSource.File(path), cancellationToken);

    public static Task<TypstCompileResult> CompileFileAsync(this ITypstCompiler compiler, string path, TypstOutput output, CancellationToken cancellationToken = default) =>
        compiler.CompileAsync(TypstSource.File(path), output, cancellationToken);

    public static Task<TypstCompileResult> CompileFileAsync(this ITypstCompiler compiler, string path, TypstOutput output, TypstCompileOptions options, CancellationToken cancellationToken = default) =>
        compiler.CompileAsync(TypstSource.File(path), output, options, cancellationToken);

    public static Task<TypstCompileResult> CompileMarkupAsync(this ITypstCompiler compiler, string markup, CancellationToken cancellationToken = default) =>
        compiler.CompileAsync(TypstSource.Markup(markup), cancellationToken);

    public static Task<TypstCompileResult> CompileMarkupAsync(this ITypstCompiler compiler, string markup, TypstOutput output, CancellationToken cancellationToken = default) =>
        compiler.CompileAsync(TypstSource.Markup(markup), output, cancellationToken);

    public static Task<TypstCompileResult> CompileMarkupAsync(this ITypstCompiler compiler, string markup, TypstOutput output, TypstCompileOptions options, CancellationToken cancellationToken = default) =>
        compiler.CompileAsync(TypstSource.Markup(markup), output, options, cancellationToken);

    public static Task<TypstCompileResult> CompileTemplateAsync(this ITypstCompiler compiler, ITemplateProvider provider, string templateName, CancellationToken cancellationToken = default) =>
        compiler.CompileAsync(TypstSource.Template(provider, TypstTemplateName.Parse(templateName)), cancellationToken);

    public static Task<TypstCompileResult> CompileTemplateAsync(this ITypstCompiler compiler, ITemplateProvider provider, string templateName, TypstOutput output, CancellationToken cancellationToken = default) =>
        compiler.CompileAsync(TypstSource.Template(provider, TypstTemplateName.Parse(templateName)), output, cancellationToken);

    public static Task<TypstCompileResult> CompileTemplateAsync(this ITypstCompiler compiler, ITemplateProvider provider, string templateName, TypstOutput output, TypstCompileOptions options, CancellationToken cancellationToken = default) =>
        compiler.CompileAsync(TypstSource.Template(provider, TypstTemplateName.Parse(templateName)), output, options, cancellationToken);

    private static Task<TypstCompileResult> Compile(ITypstCompiler compiler, TypstCompileRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        return compiler.CompileAsync(request, cancellationToken);
    }
}

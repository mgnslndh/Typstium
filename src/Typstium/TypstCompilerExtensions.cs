namespace Typstium;

/// <summary>
/// Discoverable one-liners. Each builds a <see cref="TypstCompileRequest"/> and calls
/// <see cref="ITypstCompiler.CompileAsync"/>, so every backend supports them.
/// </summary>
/// <remarks>
/// Every shape comes as a pair: one overload without a cancellation token and one with a required token.
/// Optional parameters on overload families are avoided on purpose (RS0026/RS0027): their default values
/// are compiled into callers, which makes adding overloads later a compatibility hazard.
/// </remarks>
public static class TypstCompilerExtensions
{
    public static Task<TypstCompileResult> CompileAsync(this ITypstCompiler compiler, TypstCompileRequestBuilder builder) =>
        compiler.CompileAsync(builder, CancellationToken.None);

    public static Task<TypstCompileResult> CompileAsync(this ITypstCompiler compiler, TypstCompileRequestBuilder builder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(builder);
        return compiler.CompileAsync(builder.Build(), cancellationToken);
    }

    public static Task<TypstCompileResult> CompileAsync(this ITypstCompiler compiler, TypstFileCompileRequestBuilder builder) =>
        compiler.CompileAsync(builder, CancellationToken.None);

    public static Task<TypstCompileResult> CompileAsync(this ITypstCompiler compiler, TypstFileCompileRequestBuilder builder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(builder);
        return compiler.CompileAsync(builder.Build(), cancellationToken);
    }

    public static Task<TypstCompileResult> CompileAsync(this ITypstCompiler compiler, TypstSource source) =>
        compiler.CompileAsync(source, CancellationToken.None);

    public static Task<TypstCompileResult> CompileAsync(this ITypstCompiler compiler, TypstSource source, CancellationToken cancellationToken) =>
        Compile(compiler, new TypstCompileRequest(source), cancellationToken);

    public static Task<TypstCompileResult> CompileAsync(this ITypstCompiler compiler, TypstSource source, TypstOutput output) =>
        compiler.CompileAsync(source, output, CancellationToken.None);

    public static Task<TypstCompileResult> CompileAsync(this ITypstCompiler compiler, TypstSource source, TypstOutput output, CancellationToken cancellationToken) =>
        Compile(compiler, new TypstCompileRequest(source) { Output = output }, cancellationToken);

    public static Task<TypstCompileResult> CompileAsync(this ITypstCompiler compiler, TypstSource source, TypstOutput output, TypstCompileOptions options) =>
        compiler.CompileAsync(source, output, options, CancellationToken.None);

    public static Task<TypstCompileResult> CompileAsync(this ITypstCompiler compiler, TypstSource source, TypstOutput output, TypstCompileOptions options, CancellationToken cancellationToken) =>
        Compile(compiler, new TypstCompileRequest(source) { Output = output, Options = options }, cancellationToken);

    public static Task<TypstCompileResult> CompileFileAsync(this ITypstCompiler compiler, string path) =>
        compiler.CompileFileAsync(path, CancellationToken.None);

    public static Task<TypstCompileResult> CompileFileAsync(this ITypstCompiler compiler, string path, CancellationToken cancellationToken) =>
        compiler.CompileAsync(TypstSource.File(path), cancellationToken);

    public static Task<TypstCompileResult> CompileFileAsync(this ITypstCompiler compiler, string path, TypstOutput output) =>
        compiler.CompileFileAsync(path, output, CancellationToken.None);

    public static Task<TypstCompileResult> CompileFileAsync(this ITypstCompiler compiler, string path, TypstOutput output, CancellationToken cancellationToken) =>
        compiler.CompileAsync(TypstSource.File(path), output, cancellationToken);

    public static Task<TypstCompileResult> CompileFileAsync(this ITypstCompiler compiler, string path, TypstOutput output, TypstCompileOptions options) =>
        compiler.CompileFileAsync(path, output, options, CancellationToken.None);

    public static Task<TypstCompileResult> CompileFileAsync(this ITypstCompiler compiler, string path, TypstOutput output, TypstCompileOptions options, CancellationToken cancellationToken) =>
        compiler.CompileAsync(TypstSource.File(path), output, options, cancellationToken);

    public static Task<TypstCompileResult> CompileMarkupAsync(this ITypstCompiler compiler, string markup) =>
        compiler.CompileMarkupAsync(markup, CancellationToken.None);

    public static Task<TypstCompileResult> CompileMarkupAsync(this ITypstCompiler compiler, string markup, CancellationToken cancellationToken) =>
        compiler.CompileAsync(TypstSource.Markup(markup), cancellationToken);

    public static Task<TypstCompileResult> CompileMarkupAsync(this ITypstCompiler compiler, string markup, TypstOutput output) =>
        compiler.CompileMarkupAsync(markup, output, CancellationToken.None);

    public static Task<TypstCompileResult> CompileMarkupAsync(this ITypstCompiler compiler, string markup, TypstOutput output, CancellationToken cancellationToken) =>
        compiler.CompileAsync(TypstSource.Markup(markup), output, cancellationToken);

    public static Task<TypstCompileResult> CompileMarkupAsync(this ITypstCompiler compiler, string markup, TypstOutput output, TypstCompileOptions options) =>
        compiler.CompileMarkupAsync(markup, output, options, CancellationToken.None);

    public static Task<TypstCompileResult> CompileMarkupAsync(this ITypstCompiler compiler, string markup, TypstOutput output, TypstCompileOptions options, CancellationToken cancellationToken) =>
        compiler.CompileAsync(TypstSource.Markup(markup), output, options, cancellationToken);

    public static Task<TypstCompileResult> CompileTemplateAsync(this ITypstCompiler compiler, ITemplateProvider provider, string templateName) =>
        compiler.CompileTemplateAsync(provider, templateName, CancellationToken.None);

    public static Task<TypstCompileResult> CompileTemplateAsync(this ITypstCompiler compiler, ITemplateProvider provider, string templateName, CancellationToken cancellationToken) =>
        compiler.CompileAsync(TypstSource.Template(provider, TypstTemplateName.Parse(templateName)), cancellationToken);

    public static Task<TypstCompileResult> CompileTemplateAsync(this ITypstCompiler compiler, ITemplateProvider provider, string templateName, TypstOutput output) =>
        compiler.CompileTemplateAsync(provider, templateName, output, CancellationToken.None);

    public static Task<TypstCompileResult> CompileTemplateAsync(this ITypstCompiler compiler, ITemplateProvider provider, string templateName, TypstOutput output, CancellationToken cancellationToken) =>
        compiler.CompileAsync(TypstSource.Template(provider, TypstTemplateName.Parse(templateName)), output, cancellationToken);

    public static Task<TypstCompileResult> CompileTemplateAsync(this ITypstCompiler compiler, ITemplateProvider provider, string templateName, TypstOutput output, TypstCompileOptions options) =>
        compiler.CompileTemplateAsync(provider, templateName, output, options, CancellationToken.None);

    public static Task<TypstCompileResult> CompileTemplateAsync(this ITypstCompiler compiler, ITemplateProvider provider, string templateName, TypstOutput output, TypstCompileOptions options, CancellationToken cancellationToken) =>
        compiler.CompileAsync(TypstSource.Template(provider, TypstTemplateName.Parse(templateName)), output, options, cancellationToken);

    private static Task<TypstCompileResult> Compile(ITypstCompiler compiler, TypstCompileRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        return compiler.CompileAsync(request, cancellationToken);
    }
}

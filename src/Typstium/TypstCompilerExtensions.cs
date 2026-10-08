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
    /// <inheritdoc cref="CompileAsync(ITypstCompiler, TypstCompileRequestBuilder, CancellationToken)"/>
    public static Task<TypstCompileResult> CompileAsync(this ITypstCompiler compiler, TypstCompileRequestBuilder builder) =>
        compiler.CompileAsync(builder, CancellationToken.None);

    /// <summary>Builds the request and compiles it.</summary>
    /// <param name="compiler">The compiler.</param>
    /// <param name="builder">The request to build.</param>
    /// <param name="cancellationToken">Cancels the compile and stops Typst.</param>
    /// <returns>The result. Template errors, timeouts and the output limit are reported here, not thrown.</returns>
    /// <exception cref="TypstException">Typst couldn't be found, started or understood; see the derived exceptions.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled; Typst is stopped.</exception>
    public static Task<TypstCompileResult> CompileAsync(this ITypstCompiler compiler, TypstCompileRequestBuilder builder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(builder);
        return compiler.CompileAsync(builder.Build(), cancellationToken);
    }

    /// <inheritdoc cref="CompileAsync(ITypstCompiler, TypstFileCompileRequestBuilder, CancellationToken)"/>
    public static Task<TypstCompileResult> CompileAsync(this ITypstCompiler compiler, TypstFileCompileRequestBuilder builder) =>
        compiler.CompileAsync(builder, CancellationToken.None);

    /// <summary>Builds the request and compiles it.</summary>
    /// <param name="compiler">The compiler.</param>
    /// <param name="builder">The request to build.</param>
    /// <param name="cancellationToken">Cancels the compile and stops Typst.</param>
    /// <returns>The result. Template errors, timeouts and the output limit are reported here, not thrown.</returns>
    /// <exception cref="TypstException">Typst couldn't be found, started or understood; see the derived exceptions.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled; Typst is stopped.</exception>
    public static Task<TypstCompileResult> CompileAsync(this ITypstCompiler compiler, TypstFileCompileRequestBuilder builder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(builder);
        return compiler.CompileAsync(builder.Build(), cancellationToken);
    }

    /// <inheritdoc cref="CompileAsync(ITypstCompiler, TypstSource, CancellationToken)"/>
    public static Task<TypstCompileResult> CompileAsync(this ITypstCompiler compiler, TypstSource source) =>
        compiler.CompileAsync(source, CancellationToken.None);

    /// <summary>Compiles a source to PDF with default options.</summary>
    /// <param name="compiler">The compiler.</param>
    /// <param name="source">What to compile.</param>
    /// <param name="cancellationToken">Cancels the compile and stops Typst.</param>
    /// <returns>The result. Template errors, timeouts and the output limit are reported here, not thrown.</returns>
    /// <exception cref="TypstException">Typst couldn't be found, started or understood; see the derived exceptions.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled; Typst is stopped.</exception>
    public static Task<TypstCompileResult> CompileAsync(this ITypstCompiler compiler, TypstSource source, CancellationToken cancellationToken) =>
        Compile(compiler, new TypstCompileRequest(source), cancellationToken);

    /// <inheritdoc cref="CompileAsync(ITypstCompiler, TypstSource, TypstOutput, CancellationToken)"/>
    public static Task<TypstCompileResult> CompileAsync(this ITypstCompiler compiler, TypstSource source, TypstOutput output) =>
        compiler.CompileAsync(source, output, CancellationToken.None);

    /// <summary>Compiles a source with default options.</summary>
    /// <param name="compiler">The compiler.</param>
    /// <param name="source">What to compile.</param>
    /// <param name="output">The output format and its options.</param>
    /// <param name="cancellationToken">Cancels the compile and stops Typst.</param>
    /// <returns>The result. Template errors, timeouts and the output limit are reported here, not thrown.</returns>
    /// <exception cref="TypstException">Typst couldn't be found, started or understood; see the derived exceptions.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled; Typst is stopped.</exception>
    public static Task<TypstCompileResult> CompileAsync(this ITypstCompiler compiler, TypstSource source, TypstOutput output, CancellationToken cancellationToken) =>
        Compile(compiler, new TypstCompileRequest(source) { Output = output }, cancellationToken);

    /// <inheritdoc cref="CompileAsync(ITypstCompiler, TypstSource, TypstOutput, TypstCompileOptions, CancellationToken)"/>
    public static Task<TypstCompileResult> CompileAsync(this ITypstCompiler compiler, TypstSource source, TypstOutput output, TypstCompileOptions options) =>
        compiler.CompileAsync(source, output, options, CancellationToken.None);

    /// <summary>Compiles a source.</summary>
    /// <param name="compiler">The compiler.</param>
    /// <param name="source">What to compile.</param>
    /// <param name="output">The output format and its options.</param>
    /// <param name="options">How to compile and under which limits.</param>
    /// <param name="cancellationToken">Cancels the compile and stops Typst.</param>
    /// <returns>The result. Template errors, timeouts and the output limit are reported here, not thrown.</returns>
    /// <exception cref="TypstException">Typst couldn't be found, started or understood; see the derived exceptions.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled; Typst is stopped.</exception>
    public static Task<TypstCompileResult> CompileAsync(this ITypstCompiler compiler, TypstSource source, TypstOutput output, TypstCompileOptions options, CancellationToken cancellationToken) =>
        Compile(compiler, new TypstCompileRequest(source) { Output = output, Options = options }, cancellationToken);

    /// <inheritdoc cref="CompileFileAsync(ITypstCompiler, string, CancellationToken)"/>
    public static Task<TypstCompileResult> CompileFileAsync(this ITypstCompiler compiler, string path) =>
        compiler.CompileFileAsync(path, CancellationToken.None);

    /// <summary>Compiles a .typ file in place, with the file's folder as the project root, to PDF with default options.</summary>
    /// <param name="compiler">The compiler.</param>
    /// <param name="path">The .typ file; resolved against the current directory.</param>
    /// <param name="cancellationToken">Cancels the compile and stops Typst.</param>
    /// <returns>The result. Template errors, timeouts and the output limit are reported here, not thrown.</returns>
    /// <exception cref="TypstException">Typst couldn't be found, started or understood; see the derived exceptions.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled; Typst is stopped.</exception>
    public static Task<TypstCompileResult> CompileFileAsync(this ITypstCompiler compiler, string path, CancellationToken cancellationToken) =>
        compiler.CompileAsync(TypstSource.File(path), cancellationToken);

    /// <inheritdoc cref="CompileFileAsync(ITypstCompiler, string, TypstOutput, CancellationToken)"/>
    public static Task<TypstCompileResult> CompileFileAsync(this ITypstCompiler compiler, string path, TypstOutput output) =>
        compiler.CompileFileAsync(path, output, CancellationToken.None);

    /// <summary>Compiles a .typ file in place, with the file's folder as the project root, with default options.</summary>
    /// <param name="compiler">The compiler.</param>
    /// <param name="path">The .typ file; resolved against the current directory.</param>
    /// <param name="output">The output format and its options.</param>
    /// <param name="cancellationToken">Cancels the compile and stops Typst.</param>
    /// <returns>The result. Template errors, timeouts and the output limit are reported here, not thrown.</returns>
    /// <exception cref="TypstException">Typst couldn't be found, started or understood; see the derived exceptions.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled; Typst is stopped.</exception>
    public static Task<TypstCompileResult> CompileFileAsync(this ITypstCompiler compiler, string path, TypstOutput output, CancellationToken cancellationToken) =>
        compiler.CompileAsync(TypstSource.File(path), output, cancellationToken);

    /// <inheritdoc cref="CompileFileAsync(ITypstCompiler, string, TypstOutput, TypstCompileOptions, CancellationToken)"/>
    public static Task<TypstCompileResult> CompileFileAsync(this ITypstCompiler compiler, string path, TypstOutput output, TypstCompileOptions options) =>
        compiler.CompileFileAsync(path, output, options, CancellationToken.None);

    /// <summary>Compiles a .typ file in place, with the file's folder as the project root,.</summary>
    /// <param name="compiler">The compiler.</param>
    /// <param name="path">The .typ file; resolved against the current directory.</param>
    /// <param name="output">The output format and its options.</param>
    /// <param name="options">How to compile and under which limits.</param>
    /// <param name="cancellationToken">Cancels the compile and stops Typst.</param>
    /// <returns>The result. Template errors, timeouts and the output limit are reported here, not thrown.</returns>
    /// <exception cref="TypstException">Typst couldn't be found, started or understood; see the derived exceptions.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled; Typst is stopped.</exception>
    public static Task<TypstCompileResult> CompileFileAsync(this ITypstCompiler compiler, string path, TypstOutput output, TypstCompileOptions options, CancellationToken cancellationToken) =>
        compiler.CompileAsync(TypstSource.File(path), output, options, cancellationToken);

    /// <inheritdoc cref="CompileMarkupAsync(ITypstCompiler, string, CancellationToken)"/>
    public static Task<TypstCompileResult> CompileMarkupAsync(this ITypstCompiler compiler, string markup) =>
        compiler.CompileMarkupAsync(markup, CancellationToken.None);

    /// <summary>Compiles markup in a fresh, empty sandbox to PDF with default options.</summary>
    /// <param name="compiler">The compiler.</param>
    /// <param name="markup">The Typst markup.</param>
    /// <param name="cancellationToken">Cancels the compile and stops Typst.</param>
    /// <returns>The result. Template errors, timeouts and the output limit are reported here, not thrown.</returns>
    /// <exception cref="TypstException">Typst couldn't be found, started or understood; see the derived exceptions.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled; Typst is stopped.</exception>
    public static Task<TypstCompileResult> CompileMarkupAsync(this ITypstCompiler compiler, string markup, CancellationToken cancellationToken) =>
        compiler.CompileAsync(TypstSource.Markup(markup), cancellationToken);

    /// <inheritdoc cref="CompileMarkupAsync(ITypstCompiler, string, TypstOutput, CancellationToken)"/>
    public static Task<TypstCompileResult> CompileMarkupAsync(this ITypstCompiler compiler, string markup, TypstOutput output) =>
        compiler.CompileMarkupAsync(markup, output, CancellationToken.None);

    /// <summary>Compiles markup in a fresh, empty sandbox with default options.</summary>
    /// <param name="compiler">The compiler.</param>
    /// <param name="markup">The Typst markup.</param>
    /// <param name="output">The output format and its options.</param>
    /// <param name="cancellationToken">Cancels the compile and stops Typst.</param>
    /// <returns>The result. Template errors, timeouts and the output limit are reported here, not thrown.</returns>
    /// <exception cref="TypstException">Typst couldn't be found, started or understood; see the derived exceptions.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled; Typst is stopped.</exception>
    public static Task<TypstCompileResult> CompileMarkupAsync(this ITypstCompiler compiler, string markup, TypstOutput output, CancellationToken cancellationToken) =>
        compiler.CompileAsync(TypstSource.Markup(markup), output, cancellationToken);

    /// <inheritdoc cref="CompileMarkupAsync(ITypstCompiler, string, TypstOutput, TypstCompileOptions, CancellationToken)"/>
    public static Task<TypstCompileResult> CompileMarkupAsync(this ITypstCompiler compiler, string markup, TypstOutput output, TypstCompileOptions options) =>
        compiler.CompileMarkupAsync(markup, output, options, CancellationToken.None);

    /// <summary>Compiles markup in a fresh, empty sandbox.</summary>
    /// <param name="compiler">The compiler.</param>
    /// <param name="markup">The Typst markup.</param>
    /// <param name="output">The output format and its options.</param>
    /// <param name="options">How to compile and under which limits.</param>
    /// <param name="cancellationToken">Cancels the compile and stops Typst.</param>
    /// <returns>The result. Template errors, timeouts and the output limit are reported here, not thrown.</returns>
    /// <exception cref="TypstException">Typst couldn't be found, started or understood; see the derived exceptions.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled; Typst is stopped.</exception>
    public static Task<TypstCompileResult> CompileMarkupAsync(this ITypstCompiler compiler, string markup, TypstOutput output, TypstCompileOptions options, CancellationToken cancellationToken) =>
        compiler.CompileAsync(TypstSource.Markup(markup), output, options, cancellationToken);

    /// <inheritdoc cref="CompileTemplateAsync(ITypstCompiler, ITemplateProvider, string, CancellationToken)"/>
    public static Task<TypstCompileResult> CompileTemplateAsync(this ITypstCompiler compiler, ITemplateProvider provider, string templateName) =>
        compiler.CompileTemplateAsync(provider, templateName, CancellationToken.None);

    /// <summary>Compiles a template in a fresh sandbox to PDF with default options.</summary>
    /// <param name="compiler">The compiler.</param>
    /// <param name="provider">Supplies the template.</param>
    /// <param name="templateName">The template's name; see <see cref="TypstTemplateName"/>.</param>
    /// <param name="cancellationToken">Cancels the compile and stops Typst.</param>
    /// <returns>The result. Template errors, timeouts and the output limit are reported here, not thrown.</returns>
    /// <exception cref="TypstException">Typst couldn't be found, started or understood; see the derived exceptions.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled; Typst is stopped.</exception>
    public static Task<TypstCompileResult> CompileTemplateAsync(this ITypstCompiler compiler, ITemplateProvider provider, string templateName, CancellationToken cancellationToken) =>
        compiler.CompileAsync(TypstSource.Template(provider, TypstTemplateName.Parse(templateName)), cancellationToken);

    /// <inheritdoc cref="CompileTemplateAsync(ITypstCompiler, ITemplateProvider, string, TypstOutput, CancellationToken)"/>
    public static Task<TypstCompileResult> CompileTemplateAsync(this ITypstCompiler compiler, ITemplateProvider provider, string templateName, TypstOutput output) =>
        compiler.CompileTemplateAsync(provider, templateName, output, CancellationToken.None);

    /// <summary>Compiles a template in a fresh sandbox with default options.</summary>
    /// <param name="compiler">The compiler.</param>
    /// <param name="provider">Supplies the template.</param>
    /// <param name="templateName">The template's name; see <see cref="TypstTemplateName"/>.</param>
    /// <param name="output">The output format and its options.</param>
    /// <param name="cancellationToken">Cancels the compile and stops Typst.</param>
    /// <returns>The result. Template errors, timeouts and the output limit are reported here, not thrown.</returns>
    /// <exception cref="TypstException">Typst couldn't be found, started or understood; see the derived exceptions.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled; Typst is stopped.</exception>
    public static Task<TypstCompileResult> CompileTemplateAsync(this ITypstCompiler compiler, ITemplateProvider provider, string templateName, TypstOutput output, CancellationToken cancellationToken) =>
        compiler.CompileAsync(TypstSource.Template(provider, TypstTemplateName.Parse(templateName)), output, cancellationToken);

    /// <inheritdoc cref="CompileTemplateAsync(ITypstCompiler, ITemplateProvider, string, TypstOutput, TypstCompileOptions, CancellationToken)"/>
    public static Task<TypstCompileResult> CompileTemplateAsync(this ITypstCompiler compiler, ITemplateProvider provider, string templateName, TypstOutput output, TypstCompileOptions options) =>
        compiler.CompileTemplateAsync(provider, templateName, output, options, CancellationToken.None);

    /// <summary>Compiles a template in a fresh sandbox.</summary>
    /// <param name="compiler">The compiler.</param>
    /// <param name="provider">Supplies the template.</param>
    /// <param name="templateName">The template's name; see <see cref="TypstTemplateName"/>.</param>
    /// <param name="output">The output format and its options.</param>
    /// <param name="options">How to compile and under which limits.</param>
    /// <param name="cancellationToken">Cancels the compile and stops Typst.</param>
    /// <returns>The result. Template errors, timeouts and the output limit are reported here, not thrown.</returns>
    /// <exception cref="TypstException">Typst couldn't be found, started or understood; see the derived exceptions.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled; Typst is stopped.</exception>
    public static Task<TypstCompileResult> CompileTemplateAsync(this ITypstCompiler compiler, ITemplateProvider provider, string templateName, TypstOutput output, TypstCompileOptions options, CancellationToken cancellationToken) =>
        compiler.CompileAsync(TypstSource.Template(provider, TypstTemplateName.Parse(templateName)), output, options, cancellationToken);

    private static Task<TypstCompileResult> Compile(ITypstCompiler compiler, TypstCompileRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        return compiler.CompileAsync(request, cancellationToken);
    }
}

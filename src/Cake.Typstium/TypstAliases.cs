using Cake.Core;
using Cake.Core.Annotations;
using Cake.Core.IO;
using Cake.Typstium.Compilation;
using Typstium;

namespace Cake.Typstium;

/// <summary>
/// Compiles Typst documents. Typst is found through, in order: <see cref="TypstCompileSettings.ToolPath"/>, TYPST_PATH,
/// a <c>#tool nuget:?package=Typstium.Runtime.&lt;rid&gt;</c> in Cake's tools folder, a runtime package referenced by the
/// build project, WinGet, well-known install folders, and PATH.
/// </summary>
[CakeAliasCategory("Typst")]
[CakeNamespaceImport("Typstium")]
public static class TypstAliases
{
    /// <summary>Compiles <paramref name="input"/> to a PDF beside it, as <c>typst compile input.typ</c> does.</summary>
    /// <param name="context">The Cake context.</param>
    /// <param name="input">The .typ file.</param>
    /// <returns>The result; a failed compile throws <see cref="CakeException"/> instead.</returns>
    [CakeMethodAlias]
    public static TypstCompileResult TypstCompile(this ICakeContext context, FilePath input) =>
        TypstCompileAsync(context, input).GetAwaiter().GetResult();

    /// <summary>Compiles <paramref name="input"/> to <paramref name="output"/>; the extension picks the format.</summary>
    /// <param name="context">The Cake context.</param>
    /// <param name="input">The .typ file.</param>
    /// <param name="output">The output file, or a page pattern with {p} or {0p} for PNG and SVG.</param>
    /// <returns>The result; a failed compile throws <see cref="CakeException"/> instead.</returns>
    [CakeMethodAlias]
    public static TypstCompileResult TypstCompile(this ICakeContext context, FilePath input, FilePath output) =>
        TypstCompileAsync(context, input, output).GetAwaiter().GetResult();

    /// <summary>Compiles <paramref name="input"/> to <paramref name="output"/> with <paramref name="settings"/>.</summary>
    /// <param name="context">The Cake context.</param>
    /// <param name="input">The .typ file.</param>
    /// <param name="output">The output file, or a page pattern with {p} or {0p} for PNG and SVG.</param>
    /// <param name="settings">Fonts, inputs, format and limits.</param>
    /// <returns>The result; a failed compile throws <see cref="CakeException"/> instead.</returns>
    [CakeMethodAlias]
    public static TypstCompileResult TypstCompile(this ICakeContext context, FilePath input, FilePath output, TypstCompileSettings settings) =>
        TypstCompileAsync(context, input, output, settings).GetAwaiter().GetResult();

    /// <summary>Compiles <paramref name="input"/> to a PDF beside it.</summary>
    /// <param name="context">The Cake context.</param>
    /// <param name="input">The .typ file.</param>
    /// <returns>The result; a failed compile throws <see cref="CakeException"/> instead.</returns>
    [CakeMethodAlias]
    public static Task<TypstCompileResult> TypstCompileAsync(this ICakeContext context, FilePath input) =>
        Run(context, input, output: null, new TypstCompileSettings(), CancellationToken.None);

    /// <summary>Compiles <paramref name="input"/> to a PDF beside it.</summary>
    /// <param name="context">The Cake context.</param>
    /// <param name="input">The .typ file.</param>
    /// <param name="cancellationToken">Stops the compile and kills Typst.</param>
    /// <returns>The result; a failed compile throws <see cref="CakeException"/> instead.</returns>
    [CakeMethodAlias]
    public static Task<TypstCompileResult> TypstCompileAsync(this ICakeContext context, FilePath input, CancellationToken cancellationToken) =>
        Run(context, input, output: null, new TypstCompileSettings(), cancellationToken);

    /// <summary>Compiles <paramref name="input"/> to <paramref name="output"/>; the extension picks the format.</summary>
    /// <param name="context">The Cake context.</param>
    /// <param name="input">The .typ file.</param>
    /// <param name="output">The output file, or a page pattern with {p} or {0p} for PNG and SVG.</param>
    /// <returns>The result; a failed compile throws <see cref="CakeException"/> instead.</returns>
    [CakeMethodAlias]
    public static Task<TypstCompileResult> TypstCompileAsync(this ICakeContext context, FilePath input, FilePath output) =>
        Run(context, input, output ?? throw new ArgumentNullException(nameof(output)), new TypstCompileSettings(), CancellationToken.None);

    /// <summary>Compiles <paramref name="input"/> to <paramref name="output"/>; the extension picks the format.</summary>
    /// <param name="context">The Cake context.</param>
    /// <param name="input">The .typ file.</param>
    /// <param name="output">The output file, or a page pattern with {p} or {0p} for PNG and SVG.</param>
    /// <param name="cancellationToken">Stops the compile and kills Typst.</param>
    /// <returns>The result; a failed compile throws <see cref="CakeException"/> instead.</returns>
    [CakeMethodAlias]
    public static Task<TypstCompileResult> TypstCompileAsync(this ICakeContext context, FilePath input, FilePath output, CancellationToken cancellationToken) =>
        Run(context, input, output ?? throw new ArgumentNullException(nameof(output)), new TypstCompileSettings(), cancellationToken);

    /// <summary>Compiles <paramref name="input"/> to <paramref name="output"/> with <paramref name="settings"/>.</summary>
    /// <param name="context">The Cake context.</param>
    /// <param name="input">The .typ file.</param>
    /// <param name="output">The output file, or a page pattern with {p} or {0p} for PNG and SVG.</param>
    /// <param name="settings">Fonts, inputs, format and limits.</param>
    /// <returns>The result; a failed compile throws <see cref="CakeException"/> instead.</returns>
    [CakeMethodAlias]
    public static Task<TypstCompileResult> TypstCompileAsync(this ICakeContext context, FilePath input, FilePath output, TypstCompileSettings settings) =>
        Run(context, input, output ?? throw new ArgumentNullException(nameof(output)), settings, CancellationToken.None);

    /// <summary>Compiles <paramref name="input"/> to <paramref name="output"/> with <paramref name="settings"/>.</summary>
    /// <param name="context">The Cake context.</param>
    /// <param name="input">The .typ file.</param>
    /// <param name="output">The output file, or a page pattern with {p} or {0p} for PNG and SVG.</param>
    /// <param name="settings">Fonts, inputs, format and limits.</param>
    /// <param name="cancellationToken">Stops the compile and kills Typst.</param>
    /// <returns>The result; a failed compile throws <see cref="CakeException"/> instead.</returns>
    [CakeMethodAlias]
    public static Task<TypstCompileResult> TypstCompileAsync(this ICakeContext context, FilePath input, FilePath output, TypstCompileSettings settings, CancellationToken cancellationToken) =>
        Run(context, input, output ?? throw new ArgumentNullException(nameof(output)), settings, cancellationToken);

    /// <summary>
    /// A compiler over the same search for Typst, for what the TypstCompile aliases don't cover: markup and template
    /// sources, virtual files, pages in memory, and batches that share one compiler's concurrency limit.
    /// </summary>
    /// <param name="context">The Cake context.</param>
    /// <returns>A new compiler; keep it for a batch.</returns>
    [CakeMethodAlias]
    public static ITypstCompiler TypstCompiler(this ICakeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return TypstCakeCompilers.Create(context, toolPath: null);
    }

    /// <summary>A compiler that tries <paramref name="toolPath"/> first.</summary>
    /// <param name="context">The Cake context.</param>
    /// <param name="toolPath">The Typst executable; relative paths resolve against Cake's working directory.</param>
    /// <returns>A new compiler; keep it for a batch.</returns>
    [CakeMethodAlias]
    public static ITypstCompiler TypstCompiler(this ICakeContext context, FilePath toolPath)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(toolPath);
        return TypstCakeCompilers.Create(context, toolPath);
    }

    private static Task<TypstCompileResult> Run(ICakeContext context, FilePath input, FilePath? output, TypstCompileSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(settings);
        var runner = new TypstCompileRunner(context.Environment, context.Log, toolPath => TypstCakeCompilers.Create(context, toolPath));
        return runner.RunAsync(input, output, settings, cancellationToken);
    }
}

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Typstium.Utilities;

namespace Typstium;

/// <summary>Builds requests for template and markup sources, which compile in a sandbox and accept virtual files.</summary>
public sealed class TypstCompileRequestBuilder
{
    private readonly Func<ImmutableArray<TypstVirtualFile>, TypstSource> _createSource;
    private readonly ImmutableArray<TypstVirtualFile>.Builder _files = ImmutableArray.CreateBuilder<TypstVirtualFile>();
    private readonly RequestBuilderCore _core = new();

    internal TypstCompileRequestBuilder(Func<ImmutableArray<TypstVirtualFile>, TypstSource> createSource) => _createSource = createSource;

    /// <summary>Adds a virtual file with the given bytes. The memory is not copied.</summary>
    /// <param name="path">Where the file is written, relative to the sandbox root; see <see cref="TypstPath"/>.</param>
    /// <param name="content">The file content.</param>
    /// <returns>This builder.</returns>
    public TypstCompileRequestBuilder WithFile(string path, ReadOnlyMemory<byte> content) =>
        WithFile(TypstVirtualFile.FromBytes(TypstPath.Parse(path), content));

    /// <summary>Adds a virtual file.</summary>
    /// <returns>This builder.</returns>
    public TypstCompileRequestBuilder WithFile(TypstVirtualFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        _files.Add(file);
        return this;
    }

    /// <summary>Adds a virtual text file, encoded as UTF-8 without a byte order mark.</summary>
    /// <returns>This builder.</returns>
    public TypstCompileRequestBuilder WithText(string path, string text) =>
        WithFile(TypstVirtualFile.FromText(TypstPath.Parse(path), text));

    /// <summary>Adds a virtual JSON file serialized with Typstium's defaults (camelCase property names), for Typst's
    /// <c>json()</c> function. Prefer this to <see cref="WithJsonInput{T}(string, T)"/> for large data.</summary>
    /// <remarks>Uses reflection; for trimmed or AOT apps use the <see cref="JsonTypeInfo{T}"/> overload.</remarks>
    /// <returns>This builder.</returns>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(JsonDefaults.UnreferencedCodeMessage)]
    [RequiresDynamicCode(JsonDefaults.DynamicCodeMessage)]
#endif
    public TypstCompileRequestBuilder WithJson<T>(string path, T value) =>
        WithFile(TypstVirtualFile.FromJson(TypstPath.Parse(path), value));

    /// <summary>Adds a virtual JSON file serialized with the given options.</summary>
    /// <remarks>Uses reflection; for trimmed or AOT apps use the <see cref="JsonTypeInfo{T}"/> overload.</remarks>
    /// <returns>This builder.</returns>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(JsonDefaults.UnreferencedCodeMessage)]
    [RequiresDynamicCode(JsonDefaults.DynamicCodeMessage)]
#endif
    public TypstCompileRequestBuilder WithJson<T>(string path, T value, JsonSerializerOptions options) =>
        WithFile(TypstVirtualFile.FromJson(TypstPath.Parse(path), value, options));

    /// <summary>Adds a virtual JSON file serialized with source-generated metadata; safe for trimmed and AOT apps.</summary>
    /// <returns>This builder.</returns>
    public TypstCompileRequestBuilder WithJson<T>(string path, T value, JsonTypeInfo<T> typeInfo) =>
        WithFile(TypstVirtualFile.FromJson(TypstPath.Parse(path), value, typeInfo));

    /// <summary>Sets an input. An input with the same key is replaced.</summary>
    /// <returns>This builder.</returns>
    public TypstCompileRequestBuilder WithInput(TypstInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        _core.SetInput(input);
        return this;
    }

    /// <summary>Sets an input the document reads as <c>sys.inputs.at(key)</c>. Setting a key again replaces its value.</summary>
    /// <param name="key">The key; must not be blank or contain <c>=</c>.</param>
    /// <param name="value">The value; passed on the command line, so keep it small.</param>
    /// <returns>This builder.</returns>
    public TypstCompileRequestBuilder WithInput(string key, string value)
    {
        _core.SetInput(key, value);
        return this;
    }

    /// <summary>Sets an input to <paramref name="value"/> serialized as JSON with Typstium's defaults (camelCase property names).
    /// The document reads it with <c>json(bytes(sys.inputs.at(key)))</c>.</summary>
    /// <remarks>Uses reflection; for trimmed or AOT apps use the <see cref="JsonTypeInfo{T}"/> overload.</remarks>
    /// <returns>This builder.</returns>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(JsonDefaults.UnreferencedCodeMessage)]
    [RequiresDynamicCode(JsonDefaults.DynamicCodeMessage)]
#endif
    public TypstCompileRequestBuilder WithJsonInput<T>(string key, T value) => WithJsonInput(key, value, JsonDefaults.Options);

    /// <summary>Sets an input to <paramref name="value"/> serialized as JSON with the given options.</summary>
    /// <remarks>Uses reflection; for trimmed or AOT apps use the <see cref="JsonTypeInfo{T}"/> overload.</remarks>
    /// <returns>This builder.</returns>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(JsonDefaults.UnreferencedCodeMessage)]
    [RequiresDynamicCode(JsonDefaults.DynamicCodeMessage)]
#endif
    public TypstCompileRequestBuilder WithJsonInput<T>(string key, T value, JsonSerializerOptions options)
    {
        _core.SetJsonInput(key, value, options);
        return this;
    }

    /// <summary>Sets an input to <paramref name="value"/> serialized as JSON with source-generated metadata; safe for trimmed and AOT apps.</summary>
    /// <returns>This builder.</returns>
    public TypstCompileRequestBuilder WithJsonInput<T>(string key, T value, JsonTypeInfo<T> typeInfo)
    {
        _core.SetJsonInput(key, value, typeInfo);
        return this;
    }

    /// <summary>Sets the output format and its options. PDF when not set.</summary>
    /// <returns>This builder.</returns>
    public TypstCompileRequestBuilder WithOutput(TypstOutput output)
    {
        _core.Output = output ?? throw new ArgumentNullException(nameof(output));
        return this;
    }

    /// <summary>Compiles to PDF, enforcing the given standards, if any.</summary>
    /// <returns>This builder.</returns>
    public TypstCompileRequestBuilder ToPdf(params PdfStandard[] standards) =>
        WithOutput(standards is { Length: > 0 } ? TypstOutput.Pdf(standards) : TypstOutput.Pdf());

    /// <summary>Compiles to one PNG per page.</summary>
    /// <param name="ppi">Pixels per inch; must be positive.</param>
    /// <returns>This builder.</returns>
    public TypstCompileRequestBuilder ToPng(int ppi = 144) => WithOutput(TypstOutput.Png(ppi));

    /// <summary>Compiles to one SVG per page.</summary>
    /// <returns>This builder.</returns>
    public TypstCompileRequestBuilder ToSvg() => WithOutput(TypstOutput.Svg());

    /// <summary>Replaces the options. <see cref="TypstCompileOptions.Default"/> when not set.</summary>
    /// <returns>This builder.</returns>
    public TypstCompileRequestBuilder WithOptions(TypstCompileOptions options)
    {
        _core.Options = options ?? throw new ArgumentNullException(nameof(options));
        return this;
    }

    /// <summary>Replaces the options with <paramref name="configure"/>'s result, e.g. <c>o =&gt; o with { Timeout = ... }</c>.</summary>
    /// <returns>This builder.</returns>
    public TypstCompileRequestBuilder ConfigureOptions(Func<TypstCompileOptions, TypstCompileOptions> configure)
    {
        _core.Configure(configure);
        return this;
    }

    /// <summary>Creates the request. The builder can be reused; later changes don't affect requests already built.</summary>
    /// <exception cref="ArgumentException">Two virtual files have paths that are equal or differ only by case.</exception>
    public TypstCompileRequest Build() => _core.Build(_createSource(_files.ToImmutable()));
}

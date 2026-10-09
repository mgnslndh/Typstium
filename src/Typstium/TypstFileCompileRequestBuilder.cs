using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Typstium.Utilities;

namespace Typstium;

/// <summary>
/// Builds requests for file sources, which compile in place. There are no file methods: a file source
/// cannot carry virtual files. Pass data through inputs instead.
/// </summary>
public sealed class TypstFileCompileRequestBuilder
{
    private readonly TypstSource _source;
    private readonly RequestBuilderCore _core = new();

    internal TypstFileCompileRequestBuilder(TypstSource source) => _source = source;

    /// <summary>Sets an input. An input with the same key is replaced.</summary>
    /// <returns>This builder.</returns>
    public TypstFileCompileRequestBuilder WithInput(TypstInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        _core.SetInput(input);
        return this;
    }

    /// <summary>Sets an input the document reads as <c>sys.inputs.at(key)</c>. Setting a key again replaces its value.</summary>
    /// <param name="key">The key; must not be blank or contain <c>=</c>.</param>
    /// <param name="value">The value; passed on the command line, so keep it small.</param>
    /// <returns>This builder.</returns>
    public TypstFileCompileRequestBuilder WithInput(string key, string value)
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
    public TypstFileCompileRequestBuilder WithJsonInput<T>(string key, T value) => WithJsonInput(key, value, JsonDefaults.Options);

    /// <summary>Sets an input to <paramref name="value"/> serialized as JSON with the given options.</summary>
    /// <remarks>Uses reflection; for trimmed or AOT apps use the <see cref="JsonTypeInfo{T}"/> overload.</remarks>
    /// <returns>This builder.</returns>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(JsonDefaults.UnreferencedCodeMessage)]
    [RequiresDynamicCode(JsonDefaults.DynamicCodeMessage)]
#endif
    public TypstFileCompileRequestBuilder WithJsonInput<T>(string key, T value, JsonSerializerOptions options)
    {
        _core.SetJsonInput(key, value, options);
        return this;
    }

    /// <summary>Sets an input to <paramref name="value"/> serialized as JSON with source-generated metadata; safe for trimmed and AOT apps.</summary>
    /// <returns>This builder.</returns>
    public TypstFileCompileRequestBuilder WithJsonInput<T>(string key, T value, JsonTypeInfo<T> typeInfo)
    {
        _core.SetJsonInput(key, value, typeInfo);
        return this;
    }

    /// <summary>Sets the output format and its options. PDF when not set.</summary>
    /// <returns>This builder.</returns>
    public TypstFileCompileRequestBuilder WithOutput(TypstOutput output)
    {
        _core.Output = output ?? throw new ArgumentNullException(nameof(output));
        return this;
    }

    /// <summary>Compiles to PDF, enforcing the given standards, if any.</summary>
    /// <returns>This builder.</returns>
    public TypstFileCompileRequestBuilder ToPdf(params PdfStandard[] standards) =>
        WithOutput(standards is { Length: > 0 } ? TypstOutput.Pdf(standards) : TypstOutput.Pdf());

    /// <summary>Compiles to one PNG per page.</summary>
    /// <param name="ppi">Pixels per inch; must be positive.</param>
    /// <returns>This builder.</returns>
    public TypstFileCompileRequestBuilder ToPng(int ppi = 144) => WithOutput(TypstOutput.Png(ppi));

    /// <summary>Compiles to one SVG per page.</summary>
    /// <returns>This builder.</returns>
    public TypstFileCompileRequestBuilder ToSvg() => WithOutput(TypstOutput.Svg());

    /// <summary>Replaces the options. <see cref="TypstCompileOptions.Default"/> when not set.</summary>
    /// <returns>This builder.</returns>
    public TypstFileCompileRequestBuilder WithOptions(TypstCompileOptions options)
    {
        _core.Options = options ?? throw new ArgumentNullException(nameof(options));
        return this;
    }

    /// <summary>Replaces the options with <paramref name="configure"/>'s result, e.g. <c>o =&gt; o with { Timeout = ... }</c>.</summary>
    /// <returns>This builder.</returns>
    public TypstFileCompileRequestBuilder ConfigureOptions(Func<TypstCompileOptions, TypstCompileOptions> configure)
    {
        _core.Configure(configure);
        return this;
    }

    /// <summary>Creates the request. The builder can be reused; later changes don't affect requests already built.</summary>
    public TypstCompileRequest Build() => _core.Build(_source);
}

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

    public TypstFileCompileRequestBuilder WithInput(string key, string value)
    {
        _core.AddInput(key, value);
        return this;
    }

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(JsonDefaults.UnreferencedCodeMessage)]
    [RequiresDynamicCode(JsonDefaults.DynamicCodeMessage)]
#endif
    public TypstFileCompileRequestBuilder WithJsonInput<T>(string key, T value) => WithJsonInput(key, value, JsonDefaults.Options);

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(JsonDefaults.UnreferencedCodeMessage)]
    [RequiresDynamicCode(JsonDefaults.DynamicCodeMessage)]
#endif
    public TypstFileCompileRequestBuilder WithJsonInput<T>(string key, T value, JsonSerializerOptions options)
    {
        _core.AddJsonInput(key, value, options);
        return this;
    }

    public TypstFileCompileRequestBuilder WithJsonInput<T>(string key, T value, JsonTypeInfo<T> typeInfo)
    {
        _core.AddJsonInput(key, value, typeInfo);
        return this;
    }

    public TypstFileCompileRequestBuilder WithOutput(TypstOutput output)
    {
        _core.Output = output ?? throw new ArgumentNullException(nameof(output));
        return this;
    }

    public TypstFileCompileRequestBuilder ToPdf(params PdfStandard[] standards) =>
        WithOutput(standards is { Length: > 0 } ? TypstOutput.Pdf(standards) : TypstOutput.Pdf());

    public TypstFileCompileRequestBuilder ToPng(int ppi = 144) => WithOutput(TypstOutput.Png(ppi));

    public TypstFileCompileRequestBuilder ToSvg() => WithOutput(TypstOutput.Svg());

    public TypstFileCompileRequestBuilder WithOptions(TypstCompileOptions options)
    {
        _core.Options = options ?? throw new ArgumentNullException(nameof(options));
        return this;
    }

    public TypstFileCompileRequestBuilder ConfigureOptions(Func<TypstCompileOptions, TypstCompileOptions> configure)
    {
        _core.Configure(configure);
        return this;
    }

    public TypstCompileRequest Build() => _core.Build(_source);
}

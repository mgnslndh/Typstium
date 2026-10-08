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

    public TypstCompileRequestBuilder WithFile(string path, ReadOnlyMemory<byte> content) =>
        WithFile(TypstVirtualFile.FromBytes(TypstPath.Parse(path), content));

    public TypstCompileRequestBuilder WithFile(TypstVirtualFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        _files.Add(file);
        return this;
    }

    public TypstCompileRequestBuilder WithText(string path, string text) =>
        WithFile(TypstVirtualFile.FromText(TypstPath.Parse(path), text));

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(JsonDefaults.UnreferencedCodeMessage)]
    [RequiresDynamicCode(JsonDefaults.DynamicCodeMessage)]
#endif
    public TypstCompileRequestBuilder WithJson<T>(string path, T value) =>
        WithFile(TypstVirtualFile.FromJson(TypstPath.Parse(path), value));

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(JsonDefaults.UnreferencedCodeMessage)]
    [RequiresDynamicCode(JsonDefaults.DynamicCodeMessage)]
#endif
    public TypstCompileRequestBuilder WithJson<T>(string path, T value, JsonSerializerOptions options) =>
        WithFile(TypstVirtualFile.FromJson(TypstPath.Parse(path), value, options));

    public TypstCompileRequestBuilder WithJson<T>(string path, T value, JsonTypeInfo<T> typeInfo) =>
        WithFile(TypstVirtualFile.FromJson(TypstPath.Parse(path), value, typeInfo));

    public TypstCompileRequestBuilder WithInput(string key, string value)
    {
        _core.AddInput(key, value);
        return this;
    }

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(JsonDefaults.UnreferencedCodeMessage)]
    [RequiresDynamicCode(JsonDefaults.DynamicCodeMessage)]
#endif
    public TypstCompileRequestBuilder WithJsonInput<T>(string key, T value) => WithJsonInput(key, value, JsonDefaults.Options);

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(JsonDefaults.UnreferencedCodeMessage)]
    [RequiresDynamicCode(JsonDefaults.DynamicCodeMessage)]
#endif
    public TypstCompileRequestBuilder WithJsonInput<T>(string key, T value, JsonSerializerOptions options)
    {
        _core.AddJsonInput(key, value, options);
        return this;
    }

    public TypstCompileRequestBuilder WithJsonInput<T>(string key, T value, JsonTypeInfo<T> typeInfo)
    {
        _core.AddJsonInput(key, value, typeInfo);
        return this;
    }

    public TypstCompileRequestBuilder WithOutput(TypstOutput output)
    {
        _core.Output = output ?? throw new ArgumentNullException(nameof(output));
        return this;
    }

    public TypstCompileRequestBuilder ToPdf(params PdfStandard[] standards) =>
        WithOutput(standards is { Length: > 0 } ? TypstOutput.Pdf(standards) : TypstOutput.Pdf());

    public TypstCompileRequestBuilder ToPng(int ppi = 144) => WithOutput(TypstOutput.Png(ppi));

    public TypstCompileRequestBuilder ToSvg() => WithOutput(TypstOutput.Svg());

    public TypstCompileRequestBuilder WithOptions(TypstCompileOptions options)
    {
        _core.Options = options ?? throw new ArgumentNullException(nameof(options));
        return this;
    }

    public TypstCompileRequestBuilder ConfigureOptions(Func<TypstCompileOptions, TypstCompileOptions> configure)
    {
        _core.Configure(configure);
        return this;
    }

    public TypstCompileRequest Build() => _core.Build(_createSource(_files.ToImmutable()));
}

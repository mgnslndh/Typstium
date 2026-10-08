using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Typstium.Utilities;

namespace Typstium;

/// <summary>State shared by both request builders.</summary>
internal sealed class RequestBuilderCore
{
    private readonly ImmutableDictionary<string, string>.Builder _inputs =
        ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);

    public TypstOutput Output { get; set; } = TypstOutput.Pdf();

    public TypstCompileOptions Options { get; set; } = TypstCompileOptions.Default;

    public void AddInput(string key, string value)
    {
        InputKeys.Validate(key, nameof(key));
        ArgumentNullException.ThrowIfNull(value);
        _inputs[key] = value;
    }

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(JsonDefaults.UnreferencedCodeMessage)]
    [RequiresDynamicCode(JsonDefaults.DynamicCodeMessage)]
#endif
    public void AddJsonInput<T>(string key, T value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        AddInput(key, JsonSerializer.Serialize(value, options));
    }

    public void AddJsonInput<T>(string key, T value, JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        AddInput(key, JsonSerializer.Serialize(value, typeInfo));
    }

    public void Configure(Func<TypstCompileOptions, TypstCompileOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        Options = configure(Options) ?? throw new InvalidOperationException("ConfigureOptions returned null.");
    }

    public TypstCompileRequest Build(TypstSource source) =>
        new(source) { Inputs = _inputs.ToImmutable(), Output = Output, Options = Options };
}

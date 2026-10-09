using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Typstium.Utilities;

namespace Typstium;

/// <summary>State shared by both request builders.</summary>
internal sealed class RequestBuilderCore
{
    public TypstInputs Inputs { get; private set; } = TypstInputs.Empty;

    public TypstOutput Output { get; set; } = TypstOutput.Pdf();

    public TypstCompileOptions Options { get; set; } = TypstCompileOptions.Default;

    public void SetInput(TypstInput input) => Inputs = Inputs.Set(input);

    public void SetInput(string key, string value) => SetInput(TypstInput.FromText(TypstInputKey.Parse(key, nameof(key)), value));

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(JsonDefaults.UnreferencedCodeMessage)]
    [RequiresDynamicCode(JsonDefaults.DynamicCodeMessage)]
#endif
    public void SetJsonInput<T>(string key, T value, JsonSerializerOptions options) =>
        SetInput(TypstInput.FromJson(TypstInputKey.Parse(key, nameof(key)), value, options));

    public void SetJsonInput<T>(string key, T value, JsonTypeInfo<T> typeInfo) =>
        SetInput(TypstInput.FromJson(TypstInputKey.Parse(key, nameof(key)), value, typeInfo));

    public void Configure(Func<TypstCompileOptions, TypstCompileOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        Options = configure(Options) ?? throw new InvalidOperationException("ConfigureOptions returned null.");
    }

    public TypstCompileRequest Build(TypstSource source) =>
        new(source) { Inputs = Inputs, Output = Output, Options = Options };
}

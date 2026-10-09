using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Typstium.Utilities;

namespace Typstium;

/// <summary>
/// A key/value string the document reads as <c>sys.inputs.at("key")</c>, passed to Typst as <c>--input key=value</c>.
/// Values are redacted in logs and telemetry; <see cref="ToString"/> shows only the key.
/// </summary>
/// <remarks>Inputs travel on the command line, so keep them small; pass large data as a virtual file instead.</remarks>
public sealed class TypstInput : IEquatable<TypstInput>
{
    private TypstInput(TypstInputKey key, string value)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>The key.</summary>
    public TypstInputKey Key { get; }

    /// <summary>The value, exactly as Typst receives it.</summary>
    public string Value { get; }

    /// <summary>An input with a text value.</summary>
    public static TypstInput FromText(TypstInputKey key, string value) => new(key, value);

    /// <summary>
    /// An input holding <paramref name="value"/> serialized as JSON with Typstium's defaults (camelCase property names).
    /// The document reads it with <c>json(bytes(sys.inputs.at("key")))</c>.
    /// </summary>
    /// <remarks>Uses reflection; for trimmed or AOT apps use <see cref="FromJson{T}(TypstInputKey, T, JsonTypeInfo{T})"/>.</remarks>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(JsonDefaults.UnreferencedCodeMessage)]
    [RequiresDynamicCode(JsonDefaults.DynamicCodeMessage)]
#endif
    public static TypstInput FromJson<T>(TypstInputKey key, T value) => FromJson(key, value, JsonDefaults.Options);

    /// <summary>An input holding <paramref name="value"/> serialized as JSON with the given options.</summary>
    /// <remarks>Uses reflection; for trimmed or AOT apps use <see cref="FromJson{T}(TypstInputKey, T, JsonTypeInfo{T})"/>.</remarks>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(JsonDefaults.UnreferencedCodeMessage)]
    [RequiresDynamicCode(JsonDefaults.DynamicCodeMessage)]
#endif
    public static TypstInput FromJson<T>(TypstInputKey key, T value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new TypstInput(key, JsonSerializer.Serialize(value, options));
    }

    /// <summary>An input holding <paramref name="value"/> serialized as JSON with source-generated metadata; safe for trimmed and AOT apps.</summary>
    public static TypstInput FromJson<T>(TypstInputKey key, T value, JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        return new TypstInput(key, JsonSerializer.Serialize(value, typeInfo));
    }

    /// <summary>Two inputs are equal when their keys and values are equal (ordinal).</summary>
    public bool Equals(TypstInput? other) =>
        other is not null && Key.Equals(other.Key) && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc cref="Equals(TypstInput?)"/>
    public override bool Equals(object? obj) => Equals(obj as TypstInput);

    /// <inheritdoc/>
    public override int GetHashCode() => (Key.GetHashCode() * 31) ^ StringComparer.Ordinal.GetHashCode(Value);

    /// <summary>Returns <c>key=[REDACTED]</c>, so values never reach a log by accident.</summary>
    public override string ToString() => Key.Value + "=" + TypstArguments.Redacted;
}

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Typstium.Utilities;

namespace Typstium;

/// <summary>
/// A file that exists only for one compile, held in memory so a request can be replayed.
/// Written into the sandbox root at <see cref="Path"/>.
/// </summary>
public sealed record TypstVirtualFile
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private TypstVirtualFile(TypstPath path, ReadOnlyMemory<byte> content)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        Content = content;
    }

    /// <summary>Where the file is written, relative to the sandbox root.</summary>
    public TypstPath Path { get; }

    /// <summary>The file content.</summary>
    public ReadOnlyMemory<byte> Content { get; }

    /// <summary>A file with the given bytes. The memory is not copied, so don't change it while the file is in use.</summary>
    public static TypstVirtualFile FromBytes(TypstPath path, ReadOnlyMemory<byte> content) => new(path, content);

    /// <summary>A text file, encoded as UTF-8 without a byte order mark.</summary>
    public static TypstVirtualFile FromText(TypstPath path, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new TypstVirtualFile(path, Utf8NoBom.GetBytes(text));
    }

    /// <summary>A JSON file serialized with Typstium's defaults (camelCase property names), for Typst's <c>json()</c> function.</summary>
    /// <remarks>Uses reflection; for trimmed or AOT apps use <see cref="FromJson{T}(TypstPath, T, JsonTypeInfo{T})"/>.</remarks>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(JsonDefaults.UnreferencedCodeMessage)]
    [RequiresDynamicCode(JsonDefaults.DynamicCodeMessage)]
#endif
    public static TypstVirtualFile FromJson<T>(TypstPath path, T value) => FromJson(path, value, JsonDefaults.Options);

    /// <summary>A JSON file serialized with the given options, for Typst's <c>json()</c> function.</summary>
    /// <remarks>Uses reflection; for trimmed or AOT apps use <see cref="FromJson{T}(TypstPath, T, JsonTypeInfo{T})"/>.</remarks>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(JsonDefaults.UnreferencedCodeMessage)]
    [RequiresDynamicCode(JsonDefaults.DynamicCodeMessage)]
#endif
    public static TypstVirtualFile FromJson<T>(TypstPath path, T value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new TypstVirtualFile(path, JsonSerializer.SerializeToUtf8Bytes(value, options));
    }

    /// <summary>A JSON file serialized with source-generated metadata; safe for trimmed and AOT apps.</summary>
    public static TypstVirtualFile FromJson<T>(TypstPath path, T value, JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        return new TypstVirtualFile(path, JsonSerializer.SerializeToUtf8Bytes(value, typeInfo));
    }

    /// <summary>A file with the remaining content of a stream, copied into memory. The stream is not closed.</summary>
    public static async Task<TypstVirtualFile> FromStreamAsync(TypstPath path, Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, 81920, cancellationToken).ConfigureAwait(false);
        return new TypstVirtualFile(path, buffer.ToArray());
    }
}

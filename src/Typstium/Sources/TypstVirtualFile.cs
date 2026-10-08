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

    public TypstPath Path { get; }

    public ReadOnlyMemory<byte> Content { get; }

    public static TypstVirtualFile FromBytes(TypstPath path, ReadOnlyMemory<byte> content) => new(path, content);

    public static TypstVirtualFile FromText(TypstPath path, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new TypstVirtualFile(path, Utf8NoBom.GetBytes(text));
    }

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(JsonDefaults.UnreferencedCodeMessage)]
    [RequiresDynamicCode(JsonDefaults.DynamicCodeMessage)]
#endif
    public static TypstVirtualFile FromJson<T>(TypstPath path, T value) => FromJson(path, value, JsonDefaults.Options);

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(JsonDefaults.UnreferencedCodeMessage)]
    [RequiresDynamicCode(JsonDefaults.DynamicCodeMessage)]
#endif
    public static TypstVirtualFile FromJson<T>(TypstPath path, T value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new TypstVirtualFile(path, JsonSerializer.SerializeToUtf8Bytes(value, options));
    }

    public static TypstVirtualFile FromJson<T>(TypstPath path, T value, JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        return new TypstVirtualFile(path, JsonSerializer.SerializeToUtf8Bytes(value, typeInfo));
    }

    public static async Task<TypstVirtualFile> FromStreamAsync(TypstPath path, Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, 81920, cancellationToken).ConfigureAwait(false);
        return new TypstVirtualFile(path, buffer.ToArray());
    }
}

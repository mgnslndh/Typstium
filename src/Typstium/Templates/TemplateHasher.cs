using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Typstium.Templates;

/// <summary>
/// Computes a template bundle's version: <c>sha256:</c> plus 64 hex digits over an unambiguous framing of its files.
/// </summary>
/// <remarks>
/// Every field is fixed-size or length-prefixed, so exactly one bundle produces a given byte stream:
/// <code>
/// "typstium-template-v2" 0            format tag
/// file count                           8 bytes, little-endian
/// per file, in ordinal path order:
///     path length (UTF-8 bytes)        8 bytes, little-endian
///     path (UTF-8)
///     SHA-256 of the content           32 bytes
/// </code>
/// Content is hashed per file as it streams, because <see cref="TypstTemplateFile.OpenReadAsync"/> may return a
/// stream without a known length. (The previous framing, path NUL content NUL, let content containing NUL bytes
/// shift file boundaries, so different bundles could share a version.)
/// </remarks>
internal static class TemplateHasher
{
    private static readonly byte[] FormatTag = Encoding.ASCII.GetBytes("typstium-template-v2\0");

    public static async Task<TypstTemplateVersion> ComputeAsync(IEnumerable<TypstTemplateFile> files, CancellationToken cancellationToken)
    {
        var ordered = files.OrderBy(file => file.Path.Value, StringComparer.Ordinal).ToList();
        using var bundle = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        bundle.AppendData(FormatTag);
        AppendInt64(bundle, ordered.Count);

        var buffer = new byte[81920];
        foreach (var file in ordered)
        {
            var path = Encoding.UTF8.GetBytes(file.Path.Value);
            AppendInt64(bundle, path.Length);
            bundle.AppendData(path);
            bundle.AppendData(await HashContentAsync(file, buffer, cancellationToken).ConfigureAwait(false));
        }

        var digest = bundle.GetHashAndReset();
        var hex = new StringBuilder("sha256:", 7 + (digest.Length * 2));
        foreach (var value in digest)
        {
            hex.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        }

        return new TypstTemplateVersion(hex.ToString());
    }

    private static async Task<byte[]> HashContentAsync(TypstTemplateFile file, byte[] buffer, CancellationToken cancellationToken)
    {
        using var content = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var stream = await file.OpenReadAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            int read;
            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
            {
                content.AppendData(buffer, 0, read);
            }
        }
        finally
        {
            stream.Dispose();
        }

        return content.GetHashAndReset();
    }

    private static void AppendInt64(IncrementalHash hash, long value)
    {
        var bytes = new byte[8];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)(value >> (8 * i));
        }

        hash.AppendData(bytes);
    }
}

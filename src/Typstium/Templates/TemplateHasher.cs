using System.Security.Cryptography;
using System.Text;

namespace Typstium.Templates;

internal static class TemplateHasher
{
    private static readonly byte[] Separator = [0];

    /// <summary>SHA-256 over (path, 0, content, 0) for every file in ordinal path order.</summary>
    public static async Task<TypstTemplateVersion> ComputeAsync(IEnumerable<TypstTemplateFile> files, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        foreach (var file in files.OrderBy(file => file.Path.Value, StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(file.Path.Value));
            hash.AppendData(Separator);
            var stream = await file.OpenReadAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                int read;
                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    hash.AppendData(buffer, 0, read);
                }
            }
            finally
            {
                stream.Dispose();
            }

            hash.AppendData(Separator);
        }

        var digest = hash.GetHashAndReset();
        var hex = new StringBuilder("sha256:", 7 + (digest.Length * 2));
        foreach (var value in digest)
        {
            hex.Append(value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        }

        return new TypstTemplateVersion(hex.ToString());
    }
}

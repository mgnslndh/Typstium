using System.Collections.Immutable;
using System.Globalization;
using Typstium.Utilities;

namespace Typstium;

/// <summary>A compiled document. PDF and HTML are a single file; PNG and SVG are one file per page.</summary>
public abstract record TypstDocument
{
    private TypstDocument()
    {
    }

    public abstract TypstOutputFormat Format { get; }

    public static TypstDocument CreateSingleFile(TypstOutputFormat format, ReadOnlyMemory<byte> content)
    {
        if (format is not (TypstOutputFormat.Pdf or TypstOutputFormat.Html))
        {
            throw new ArgumentException($"{format} output is paged; use CreatePaged.", nameof(format));
        }

        return new SingleFile(format, content);
    }

    public static TypstDocument CreatePaged(TypstOutputFormat format, ImmutableArray<ReadOnlyMemory<byte>> pages)
    {
        if (format is not (TypstOutputFormat.Png or TypstOutputFormat.Svg))
        {
            throw new ArgumentException($"{format} output is a single file; use CreateSingleFile.", nameof(format));
        }

        if (pages.IsDefaultOrEmpty)
        {
            throw new ArgumentException("A paged document needs at least one page.", nameof(pages));
        }

        return new Paged(format, pages);
    }

    public sealed record SingleFile : TypstDocument
    {
        internal SingleFile(TypstOutputFormat format, ReadOnlyMemory<byte> content)
        {
            Format = format;
            Content = content;
        }

        public override TypstOutputFormat Format { get; }

        public ReadOnlyMemory<byte> Content { get; }

        public Task WriteToAsync(Stream destination, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(destination);
            var segment = ByteMemory.AsSegment(Content);
            return destination.WriteAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken);
        }

        public async Task SaveAsync(string path, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await WriteToAsync(stream, cancellationToken).ConfigureAwait(false);
        }
    }

    public sealed record Paged : TypstDocument
    {
        internal Paged(TypstOutputFormat format, ImmutableArray<ReadOnlyMemory<byte>> pages)
        {
            Format = format;
            Pages = pages;
        }

        public override TypstOutputFormat Format { get; }

        public ImmutableArray<ReadOnlyMemory<byte>> Pages { get; }

        /// <summary>
        /// Saves every page. The pattern uses Typst's placeholders: <c>{p}</c> page number,
        /// <c>{0p}</c> zero-padded page number, <c>{t}</c> page count.
        /// </summary>
        /// <returns>The written file paths, in page order.</returns>
        public async Task<IReadOnlyList<string>> SaveAsync(string pathPattern, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pathPattern);
            if (Pages.Length > 1
                && pathPattern.IndexOf("{p}", StringComparison.Ordinal) < 0
                && pathPattern.IndexOf("{0p}", StringComparison.Ordinal) < 0)
            {
                throw new ArgumentException("A multi-page document needs {p} or {0p} in the path pattern.", nameof(pathPattern));
            }

            var written = new List<string>(Pages.Length);
            for (var i = 0; i < Pages.Length; i++)
            {
                var path = FormatPath(pathPattern, i + 1, Pages.Length);
                var segment = ByteMemory.AsSegment(Pages[i]);
                using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                {
                    await stream.WriteAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken).ConfigureAwait(false);
                }

                written.Add(path);
            }

            return written;
        }

        internal static string FormatPath(string pattern, int pageNumber, int pageCount)
        {
            var digits = pageCount.ToString(CultureInfo.InvariantCulture).Length;
            return pattern
                .Replace("{0p}", pageNumber.ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0'))
                .Replace("{p}", pageNumber.ToString(CultureInfo.InvariantCulture))
                .Replace("{t}", pageCount.ToString(CultureInfo.InvariantCulture));
        }
    }
}

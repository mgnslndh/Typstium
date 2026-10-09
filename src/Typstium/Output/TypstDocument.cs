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

    /// <summary>The format the document was compiled to.</summary>
    public abstract TypstOutputFormat Format { get; }

    /// <summary>Creates a PDF or HTML document, for tests and custom <see cref="ITypstCompiler"/> implementations.</summary>
    /// <param name="format"><see cref="TypstOutputFormat.Pdf"/> or <see cref="TypstOutputFormat.Html"/>.</param>
    /// <param name="content">The file content. It is not copied.</param>
    public static TypstDocument CreateSingleFile(TypstOutputFormat format, ReadOnlyMemory<byte> content)
    {
        if (format is not (TypstOutputFormat.Pdf or TypstOutputFormat.Html))
        {
            throw new ArgumentException($"{format} output is paged; use CreatePaged.", nameof(format));
        }

        return new SingleFile(format, content);
    }

    /// <summary>Creates a PNG or SVG document, for tests and custom <see cref="ITypstCompiler"/> implementations.</summary>
    /// <param name="format"><see cref="TypstOutputFormat.Png"/> or <see cref="TypstOutputFormat.Svg"/>.</param>
    /// <param name="pages">At least one page, in page order. The page content is not copied.</param>
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

    /// <summary>A PDF or HTML document.</summary>
    public sealed record SingleFile : TypstDocument
    {
        internal SingleFile(TypstOutputFormat format, ReadOnlyMemory<byte> content)
        {
            Format = format;
            Content = content;
        }

        /// <inheritdoc/>
        public override TypstOutputFormat Format { get; }

        /// <summary>The file content.</summary>
        public ReadOnlyMemory<byte> Content { get; }

        /// <summary>Writes the content to a stream. The stream is neither flushed nor closed.</summary>
        public Task WriteToAsync(Stream destination, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(destination);
            var segment = ByteMemory.AsSegment(Content);
            return destination.WriteAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken);
        }

        /// <summary>Writes the content to a file, replacing any existing file.</summary>
        public async Task SaveAsync(string path, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await WriteToAsync(stream, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>A PNG or SVG document, one file per page.</summary>
    public sealed record Paged : TypstDocument
    {
        internal Paged(TypstOutputFormat format, ImmutableArray<ReadOnlyMemory<byte>> pages)
        {
            Format = format;
            Pages = pages;
        }

        /// <inheritdoc/>
        public override TypstOutputFormat Format { get; }

        /// <summary>The content of each exported page, in page order.</summary>
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

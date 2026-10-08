using System.Collections.Immutable;

namespace Typstium;

/// <summary>The output format and its options. Exactly one of four kinds.</summary>
public abstract record TypstOutput
{
    private TypstOutput()
    {
    }

    /// <summary>The file format this output produces.</summary>
    public abstract TypstOutputFormat Format { get; }

    /// <summary>PNG and SVG produce one file per page.</summary>
    internal bool IsPaged => Format is TypstOutputFormat.Png or TypstOutputFormat.Svg;

    internal string FileExtension => Format switch
    {
        TypstOutputFormat.Pdf => "pdf",
        TypstOutputFormat.Png => "png",
        TypstOutputFormat.Svg => "svg",
        TypstOutputFormat.Html => "html",
        _ => throw new InvalidOperationException($"Unknown output format {Format}."),
    };

    /// <summary>PDF with default options: no enforced standard, tagged, every page.</summary>
    public static TypstOutput Pdf() => new PdfOutput(TypstPdfOptions.Default);

    /// <summary>PDF that Typst checks against the given standards; the compile fails if the document violates one.</summary>
    /// <param name="standards">The standards to enforce. Empty means none.</param>
    public static TypstOutput Pdf(params PdfStandard[] standards)
    {
        ArgumentNullException.ThrowIfNull(standards);
        return new PdfOutput(TypstPdfOptions.Default with { Standards = standards.ToImmutableArray() });
    }

    /// <summary>PDF with the given options.</summary>
    public static TypstOutput Pdf(TypstPdfOptions options) => new PdfOutput(options);

    /// <summary>One PNG per page at 144 pixels per inch (Typst's default).</summary>
    public static TypstOutput Png() => new PngOutput(144, TypstPageSelection.All);

    /// <summary>One PNG per page at the given resolution.</summary>
    /// <param name="ppi">Pixels per inch; must be positive.</param>
    public static TypstOutput Png(int ppi) => new PngOutput(ppi, TypstPageSelection.All);

    /// <summary>One PNG per selected page at the given resolution.</summary>
    /// <param name="ppi">Pixels per inch; must be positive.</param>
    /// <param name="pages">The pages to render.</param>
    public static TypstOutput Png(int ppi, TypstPageSelection pages) => new PngOutput(ppi, pages);

    /// <summary>One SVG per page.</summary>
    public static TypstOutput Svg() => new SvgOutput(TypstPageSelection.All);

    /// <summary>One SVG per selected page.</summary>
    /// <param name="pages">The pages to render.</param>
    public static TypstOutput Svg(TypstPageSelection pages) => new SvgOutput(pages);

    /// <summary>Experimental HTML export; requires Typst's html feature.</summary>
    public static TypstOutput Html() => HtmlOutput.Instance;

    /// <summary>PDF output. Create it with <see cref="Pdf()"/>.</summary>
    public sealed record PdfOutput : TypstOutput
    {
        internal PdfOutput(TypstPdfOptions options) => Options = options ?? throw new ArgumentNullException(nameof(options));

        /// <summary>The PDF options.</summary>
        public TypstPdfOptions Options { get; }

        /// <inheritdoc/>
        public override TypstOutputFormat Format => TypstOutputFormat.Pdf;
    }

    /// <summary>PNG output. Create it with <see cref="Png()"/>.</summary>
    public sealed record PngOutput : TypstOutput
    {
        internal PngOutput(int ppi, TypstPageSelection pages)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ppi);
            Ppi = ppi;
            Pages = pages ?? throw new ArgumentNullException(nameof(pages));
        }

        /// <summary>Pixels per inch.</summary>
        public int Ppi { get; }

        /// <summary>The pages to render.</summary>
        public TypstPageSelection Pages { get; }

        /// <inheritdoc/>
        public override TypstOutputFormat Format => TypstOutputFormat.Png;
    }

    /// <summary>SVG output. Create it with <see cref="Svg()"/>.</summary>
    public sealed record SvgOutput : TypstOutput
    {
        internal SvgOutput(TypstPageSelection pages) => Pages = pages ?? throw new ArgumentNullException(nameof(pages));

        /// <summary>The pages to render.</summary>
        public TypstPageSelection Pages { get; }

        /// <inheritdoc/>
        public override TypstOutputFormat Format => TypstOutputFormat.Svg;
    }

    /// <summary>HTML output. Create it with <see cref="Html()"/>.</summary>
    public sealed record HtmlOutput : TypstOutput
    {
        private HtmlOutput()
        {
        }

        internal static HtmlOutput Instance { get; } = new();

        /// <inheritdoc/>
        public override TypstOutputFormat Format => TypstOutputFormat.Html;
    }
}

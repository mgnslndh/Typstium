using System.Collections.Immutable;

namespace Typstium;

/// <summary>The output format and its options. Exactly one of four kinds.</summary>
public abstract record TypstOutput
{
    private TypstOutput()
    {
    }

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

    public static TypstOutput Pdf() => new PdfOutput(TypstPdfOptions.Default);

    public static TypstOutput Pdf(params PdfStandard[] standards)
    {
        ArgumentNullException.ThrowIfNull(standards);
        return new PdfOutput(TypstPdfOptions.Default with { Standards = standards.ToImmutableArray() });
    }

    public static TypstOutput Pdf(TypstPdfOptions options) => new PdfOutput(options);

    public static TypstOutput Png() => new PngOutput(144, TypstPageSelection.All);

    public static TypstOutput Png(int ppi) => new PngOutput(ppi, TypstPageSelection.All);

    public static TypstOutput Png(int ppi, TypstPageSelection pages) => new PngOutput(ppi, pages);

    public static TypstOutput Svg() => new SvgOutput(TypstPageSelection.All);

    public static TypstOutput Svg(TypstPageSelection pages) => new SvgOutput(pages);

    /// <summary>Experimental HTML export; requires Typst's html feature.</summary>
    public static TypstOutput Html() => HtmlOutput.Instance;

    public sealed record PdfOutput : TypstOutput
    {
        internal PdfOutput(TypstPdfOptions options) => Options = options ?? throw new ArgumentNullException(nameof(options));

        public TypstPdfOptions Options { get; }

        public override TypstOutputFormat Format => TypstOutputFormat.Pdf;
    }

    public sealed record PngOutput : TypstOutput
    {
        internal PngOutput(int ppi, TypstPageSelection pages)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ppi);
            Ppi = ppi;
            Pages = pages ?? throw new ArgumentNullException(nameof(pages));
        }

        public int Ppi { get; }

        public TypstPageSelection Pages { get; }

        public override TypstOutputFormat Format => TypstOutputFormat.Png;
    }

    public sealed record SvgOutput : TypstOutput
    {
        internal SvgOutput(TypstPageSelection pages) => Pages = pages ?? throw new ArgumentNullException(nameof(pages));

        public TypstPageSelection Pages { get; }

        public override TypstOutputFormat Format => TypstOutputFormat.Svg;
    }

    public sealed record HtmlOutput : TypstOutput
    {
        private HtmlOutput()
        {
        }

        internal static HtmlOutput Instance { get; } = new();

        public override TypstOutputFormat Format => TypstOutputFormat.Html;
    }
}

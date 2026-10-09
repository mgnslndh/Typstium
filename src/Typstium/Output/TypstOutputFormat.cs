namespace Typstium;

/// <summary>The file format Typst compiles to.</summary>
public enum TypstOutputFormat
{
    /// <summary>A single PDF file.</summary>
    Pdf,

    /// <summary>One PNG image per page.</summary>
    Png,

    /// <summary>One SVG image per page.</summary>
    Svg,

    /// <summary>A single HTML file (experimental in Typst).</summary>
    Html,
}

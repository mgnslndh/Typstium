using System.Collections.Immutable;

namespace Typstium;

/// <summary>Which fonts Typst may use.</summary>
public sealed record TypstFontOptions
{
    private readonly ImmutableArray<string> _fontPaths = ImmutableArray<string>.Empty;

    /// <summary>Typst's embedded fonts only: no system fonts, no font folders.</summary>
    public static TypstFontOptions Default { get; } = new();

    /// <summary>Folders searched recursively for fonts (--font-path).</summary>
    public ImmutableArray<string> FontPaths
    {
        get => _fontPaths;
        init => _fontPaths = value.IsDefault ? ImmutableArray<string>.Empty : value;
    }

    /// <summary>Off by default: about 3× faster on Windows and identical output on every machine.</summary>
    public bool IncludeSystemFonts { get; init; }

    /// <summary>Whether Typst may use the fonts built into it (Libertinus, New Computer Modern and others). On by default.</summary>
    public bool IncludeEmbeddedFonts { get; init; } = true;
}

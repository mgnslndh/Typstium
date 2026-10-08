using System.Collections.Immutable;

namespace Typstium;

public sealed record TypstFontOptions
{
    private readonly ImmutableArray<string> _fontPaths = ImmutableArray<string>.Empty;

    public static TypstFontOptions Default { get; } = new();

    /// <summary>Folders searched recursively for fonts (--font-path).</summary>
    public ImmutableArray<string> FontPaths
    {
        get => _fontPaths;
        init => _fontPaths = value.IsDefault ? ImmutableArray<string>.Empty : value;
    }

    /// <summary>Off by default: about 3× faster on Windows and identical output on every machine.</summary>
    public bool IncludeSystemFonts { get; init; }

    public bool IncludeEmbeddedFonts { get; init; } = true;
}

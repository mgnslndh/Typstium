using System.Collections.Immutable;

namespace Typstium;

public sealed record TypstPdfOptions
{
    private readonly ImmutableArray<PdfStandard> _standards = ImmutableArray<PdfStandard>.Empty;
    private readonly TypstPageSelection _pages = TypstPageSelection.All;

    public static TypstPdfOptions Default { get; } = new();

    public ImmutableArray<PdfStandard> Standards
    {
        get => _standards;
        init
        {
            var standards = value.IsDefault ? ImmutableArray<PdfStandard>.Empty : value;
            if (standards.Any(standard => standard is null))
            {
                throw new ArgumentException("PDF standards must not be null.", nameof(value));
            }

            _standards = standards;
        }
    }

    /// <summary>Whether to write a tagged PDF (Typst's default). Set to false to pass --no-pdf-tags.</summary>
    public bool Tagged { get; init; } = true;

    public TypstPageSelection Pages
    {
        get => _pages;
        init => _pages = value ?? throw new ArgumentNullException(nameof(value));
    }
}

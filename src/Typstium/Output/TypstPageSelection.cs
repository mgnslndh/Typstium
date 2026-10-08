using System.Collections.Immutable;

namespace Typstium;

/// <summary>Which pages to export: every page, or a list of page ranges.</summary>
public sealed record TypstPageSelection
{
    private TypstPageSelection(ImmutableArray<TypstPageRange> ranges) => Ranges = ranges;

    /// <summary>Every page.</summary>
    public static TypstPageSelection All { get; } = new(ImmutableArray<TypstPageRange>.Empty);

    /// <summary>The selected ranges in the order given; empty for <see cref="All"/>.</summary>
    public ImmutableArray<TypstPageRange> Ranges { get; }

    /// <summary>Whether every page is selected.</summary>
    public bool IsAll => Ranges.IsEmpty;

    /// <summary>The pages in the given ranges.</summary>
    /// <param name="ranges">At least one range; use <see cref="All"/> for every page.</param>
    public static TypstPageSelection Of(params TypstPageRange[] ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        if (ranges.Length == 0)
        {
            throw new ArgumentException("At least one range is required; use TypstPageSelection.All for every page.", nameof(ranges));
        }

        if (ranges.Any(range => range is null))
        {
            throw new ArgumentException("Page ranges must not be null.", nameof(ranges));
        }

        return new TypstPageSelection(ranges.ToImmutableArray());
    }

    internal string ToCliValue() => string.Join(",", Ranges.Select(range => range.ToCliValue()));
}

using System.Collections.Immutable;

namespace Typstium;

public sealed record TypstPageSelection
{
    private TypstPageSelection(ImmutableArray<TypstPageRange> ranges) => Ranges = ranges;

    public static TypstPageSelection All { get; } = new(ImmutableArray<TypstPageRange>.Empty);

    public ImmutableArray<TypstPageRange> Ranges { get; }

    public bool IsAll => Ranges.IsEmpty;

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

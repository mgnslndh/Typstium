using System.Globalization;

namespace Typstium;

/// <summary>A 1-based page range: one page, a closed range, or "from page N to the end".</summary>
public sealed record TypstPageRange
{
    private TypstPageRange(int first, int? last)
    {
        First = first;
        Last = last;
    }

    public int First { get; }

    public int? Last { get; }

    public static TypstPageRange Single(int page)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
        return new TypstPageRange(page, page);
    }

    public static TypstPageRange Between(int first, int last)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(first);
        if (last < first)
        {
            throw new ArgumentOutOfRangeException(nameof(last), last, "The last page must not come before the first page.");
        }

        return new TypstPageRange(first, last);
    }

    public static TypstPageRange From(int first)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(first);
        return new TypstPageRange(first, null);
    }

    internal string ToCliValue()
    {
        var first = First.ToString(CultureInfo.InvariantCulture);
        if (Last is not { } last)
        {
            return first + "-";
        }

        return last == First ? first : first + "-" + last.ToString(CultureInfo.InvariantCulture);
    }
}

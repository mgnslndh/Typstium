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

    /// <summary>The first page, 1-based.</summary>
    public int First { get; }

    /// <summary>The last page, inclusive, or null for "to the end of the document".</summary>
    public int? Last { get; }

    /// <summary>A single page.</summary>
    /// <param name="page">The page number, 1-based.</param>
    public static TypstPageRange Single(int page)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
        return new TypstPageRange(page, page);
    }

    /// <summary>The pages from <paramref name="first"/> to <paramref name="last"/>, inclusive.</summary>
    /// <param name="first">The first page, 1-based.</param>
    /// <param name="last">The last page; must not come before <paramref name="first"/>.</param>
    public static TypstPageRange Between(int first, int last)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(first);
        if (last < first)
        {
            throw new ArgumentOutOfRangeException(nameof(last), last, "The last page must not come before the first page.");
        }

        return new TypstPageRange(first, last);
    }

    /// <summary>The pages from <paramref name="first"/> to the end of the document.</summary>
    /// <param name="first">The first page, 1-based.</param>
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

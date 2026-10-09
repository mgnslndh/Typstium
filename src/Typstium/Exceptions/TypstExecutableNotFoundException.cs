using System.Collections.Immutable;
using System.Text;

namespace Typstium;

/// <summary>No locator found Typst. The message lists every location searched and how to install Typst.</summary>
public sealed class TypstExecutableNotFoundException : TypstException
{
    /// <summary>Creates the exception from the searches of every locator, in order.</summary>
    public TypstExecutableNotFoundException(ImmutableArray<TypstLocatorSearch> searches)
        : base(BuildMessage(searches))
    {
        Searches = searches;
    }

    /// <summary>What each locator searched, in the order they ran.</summary>
    public ImmutableArray<TypstLocatorSearch> Searches { get; }

    private static string BuildMessage(ImmutableArray<TypstLocatorSearch> searches)
    {
        var builder = new StringBuilder("Typst was not found. Install it (for example 'winget install Typst.Typst'), set TYPST_PATH, or pass an explicit path.");
        builder.AppendLine().Append("Searched:");
        foreach (var search in searches)
        {
            foreach (var location in search.SearchedLocations)
            {
                builder.AppendLine().Append("  ").Append(search.LocatorName).Append(": ").Append(location);
            }
        }

        return builder.ToString();
    }
}

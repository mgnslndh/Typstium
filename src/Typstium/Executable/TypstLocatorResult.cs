using System.Collections.Immutable;

namespace Typstium;

/// <summary>What a locator found: a path, or the locations it searched.</summary>
public abstract record TypstLocatorResult
{
    private TypstLocatorResult()
    {
    }

    /// <summary>Typst is at <paramref name="path"/>, which should be fully qualified.</summary>
    public static TypstLocatorResult Found(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new FoundResult(path);
    }

    /// <summary>Typst isn't at any of <paramref name="searchedLocations"/>, shown in the error message.</summary>
    public static TypstLocatorResult NotFound(IEnumerable<string> searchedLocations)
    {
        ArgumentNullException.ThrowIfNull(searchedLocations);
        return new NotFoundResult(searchedLocations.ToImmutableArray());
    }

    /// <summary>Typst was found.</summary>
    /// <param name="Path">The executable's path.</param>
    public sealed record FoundResult(string Path) : TypstLocatorResult;

    /// <summary>Typst wasn't found.</summary>
    /// <param name="SearchedLocations">The locations searched, or a note on why none were.</param>
    public sealed record NotFoundResult(ImmutableArray<string> SearchedLocations) : TypstLocatorResult;
}

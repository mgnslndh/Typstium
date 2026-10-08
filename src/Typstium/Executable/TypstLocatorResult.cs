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

    /// <summary>Typst was found. Create it with <see cref="Found"/>.</summary>
    public sealed record FoundResult : TypstLocatorResult
    {
        internal FoundResult(string path) => Path = path;

        /// <summary>The executable's path.</summary>
        public string Path { get; }
    }

    /// <summary>Typst wasn't found. Create it with <see cref="NotFound"/>.</summary>
    public sealed record NotFoundResult : TypstLocatorResult
    {
        internal NotFoundResult(ImmutableArray<string> searchedLocations) => SearchedLocations = searchedLocations;

        /// <summary>The locations searched, or a note on why none were.</summary>
        public ImmutableArray<string> SearchedLocations { get; }
    }
}

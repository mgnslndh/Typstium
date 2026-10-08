using System.Collections.Immutable;

namespace Typstium;

public abstract record TypstLocatorResult
{
    private TypstLocatorResult()
    {
    }

    public static TypstLocatorResult Found(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new FoundResult(path);
    }

    public static TypstLocatorResult NotFound(IEnumerable<string> searchedLocations)
    {
        ArgumentNullException.ThrowIfNull(searchedLocations);
        return new NotFoundResult(searchedLocations.ToImmutableArray());
    }

    public sealed record FoundResult(string Path) : TypstLocatorResult;

    public sealed record NotFoundResult(ImmutableArray<string> SearchedLocations) : TypstLocatorResult;
}

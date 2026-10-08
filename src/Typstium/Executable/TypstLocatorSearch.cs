using System.Collections.Immutable;

namespace Typstium;

public sealed record TypstLocatorSearch(string LocatorName, ImmutableArray<string> SearchedLocations);

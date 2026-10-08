using System.Collections.Immutable;

namespace Typstium;

/// <summary>What one locator searched without finding Typst.</summary>
/// <param name="LocatorName">The locator's <see cref="ITypstExecutableLocator.Name"/>.</param>
/// <param name="SearchedLocations">The locations searched, or a note on why none were.</param>
public sealed record TypstLocatorSearch(string LocatorName, ImmutableArray<string> SearchedLocations);

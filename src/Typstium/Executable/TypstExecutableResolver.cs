using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Typstium.Utilities;

namespace Typstium;

/// <summary>Tries locators in order; the first hit is cached for the resolver's lifetime. Failures are not cached.</summary>
public sealed class TypstExecutableResolver : ITypstExecutableResolver
{
    private readonly ImmutableArray<ITypstExecutableLocator> _locators;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private TypstExecutable? _resolved;

    /// <summary>Creates a resolver over the given locators, tried in order.</summary>
    /// <exception cref="ArgumentException"><paramref name="locators"/> is empty or contains null.</exception>
    public TypstExecutableResolver(IEnumerable<ITypstExecutableLocator> locators)
    {
        ArgumentNullException.ThrowIfNull(locators);
        _locators = locators.ToImmutableArray();
        if (_locators.IsEmpty || _locators.Any(locator => locator is null))
        {
            throw new ArgumentException("At least one locator is required, and none may be null.", nameof(locators));
        }
    }

    /// <summary>TYPST_PATH → bundled runtime → WinGet → well-known locations → PATH.</summary>
    public static TypstExecutableResolver CreateDefault() => new(DefaultLocators(explicitPath: null));

    /// <summary>The explicit path first, then the default chain.</summary>
    public static TypstExecutableResolver CreateDefault(string explicitPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(explicitPath);
        return new TypstExecutableResolver(DefaultLocators(explicitPath));
    }

    /// <inheritdoc/>
    public async ValueTask<TypstExecutable> ResolveAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _resolved) is { } cached)
        {
            return cached;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_resolved is { } resolved)
            {
                return resolved;
            }

            var searches = ImmutableArray.CreateBuilder<TypstLocatorSearch>();
            foreach (var locator in _locators)
            {
                var result = await locator.LocateAsync(cancellationToken).ConfigureAwait(false);
                switch (result)
                {
                    case TypstLocatorResult.FoundResult found when !FullyQualifiedPath.IsFullyQualified(found.Path, RuntimeInformation.IsOSPlatform(OSPlatform.Windows)):
                        searches.Add(new TypstLocatorSearch(locator.Name, ImmutableArray.Create($"{found.Path} (rejected: not a fully qualified path)")));
                        break;
                    case TypstLocatorResult.FoundResult found:
                        var executable = new TypstExecutable(found.Path, locator.Name);
                        Volatile.Write(ref _resolved, executable);
                        return executable;
                    case TypstLocatorResult.NotFoundResult notFound:
                        searches.Add(new TypstLocatorSearch(locator.Name, notFound.SearchedLocations));
                        break;
                }
            }

            throw new TypstExecutableNotFoundException(searches.ToImmutable());
        }
        finally
        {
            _gate.Release();
        }
    }

    internal static IEnumerable<ITypstExecutableLocator> DefaultLocators(string? explicitPath)
    {
        if (explicitPath is not null)
        {
            yield return TypstExecutableLocators.ExplicitPath(explicitPath);
        }

        yield return TypstExecutableLocators.EnvironmentVariable();
        yield return TypstExecutableLocators.BundledRuntime();
        yield return TypstExecutableLocators.WinGet();
        yield return TypstExecutableLocators.WellKnownLocations();
        yield return TypstExecutableLocators.SearchPath();
    }
}

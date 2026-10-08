using Typstium.Executable;
using Typstium.UnitTests.Testing;
using TUnit.Assertions.Enums;

namespace Typstium.UnitTests.Executable;

public class TypstExecutableResolverTests
{
    private sealed class CountingLocator(string name, TypstLocatorResult result) : ITypstExecutableLocator
    {
        public int Calls;

        public string Name => name;

        public ValueTask<TypstLocatorResult> LocateAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            return new ValueTask<TypstLocatorResult>(result);
        }
    }

    private static string OptA => Path.Combine(FakeLocatorEnvironment.Root, "opt", "a", "typst");

    [Test]
    public async Task Custom_locators_cannot_return_relative_paths()
    {
        var relative = new CountingLocator("Custom", TypstLocatorResult.Found("C:typst.exe"));
        var resolver = new TypstExecutableResolver(new ITypstExecutableLocator[] { relative });

        var exception = await Assert.ThrowsAsync<TypstExecutableNotFoundException>(() => resolver.ResolveAsync(CancellationToken.None).AsTask());

        await Assert.That(exception!.Message).Contains("C:typst.exe");
    }

    [Test]
    public async Task First_hit_wins_and_is_cached()
    {
        var first = new CountingLocator("TYPST_PATH", TypstLocatorResult.Found(OptA));
        var second = new CountingLocator("PATH", TypstLocatorResult.Found(Path.Combine(FakeLocatorEnvironment.Root, "usr", "typst")));
        var resolver = new TypstExecutableResolver(new ITypstExecutableLocator[] { first, second });

        var resolved = await resolver.ResolveAsync(CancellationToken.None);
        await resolver.ResolveAsync(CancellationToken.None);

        await Assert.That(resolved).IsEqualTo(new TypstExecutable(OptA, "TYPST_PATH"));
        await Assert.That(first.Calls).IsEqualTo(1);
        await Assert.That(second.Calls).IsEqualTo(0);
    }

    [Test]
    public async Task Not_found_lists_every_location_and_how_to_fix_it()
    {
        var resolver = new TypstExecutableResolver(new ITypstExecutableLocator[]
        {
            new CountingLocator("TYPST_PATH", TypstLocatorResult.NotFound(new[] { "%TYPST_PATH% (not set)" })),
            new CountingLocator("PATH", TypstLocatorResult.NotFound(new[] { "/usr/bin/typst", "/bin/typst" })),
        });

        var exception = await Assert.ThrowsAsync<TypstExecutableNotFoundException>(() => resolver.ResolveAsync(CancellationToken.None).AsTask());

        await Assert.That(exception!.Searches.Length).IsEqualTo(2);
        await Assert.That(exception.Message).Contains("/bin/typst");
        await Assert.That(exception.Message).Contains("winget install Typst.Typst");
    }

    [Test]
    public async Task Failures_are_not_cached()
    {
        var environment = new FakeLocatorEnvironment();
        var resolver = new TypstExecutableResolver(new ITypstExecutableLocator[] { new EnvironmentVariableLocator("TYPST_PATH", environment) });

        await Assert.ThrowsAsync<TypstExecutableNotFoundException>(() => resolver.ResolveAsync(CancellationToken.None).AsTask());
        var typst = environment.AddFile(FakeLocatorEnvironment.Root, "late", environment.ExecutableFileName);
        environment.Variables["TYPST_PATH"] = typst;

        var resolved = await resolver.ResolveAsync(CancellationToken.None);
        await Assert.That(resolved.Path).IsEqualTo(typst);
    }

    [Test]
    public async Task Default_order_is_explicit_env_bundled_winget_wellknown_path()
    {
        var names = TypstExecutableResolver.DefaultLocators("C:\\typst.exe").Select(locator => locator.Name);
        await Assert.That(names).IsEquivalentTo(new[] { "ExplicitPath", "TYPST_PATH", "BundledRuntime", "WinGet", "WellKnownLocations", "PATH" }, CollectionOrdering.Matching);
    }
}

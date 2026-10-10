using Cake.Core.IO;
using Cake.Testing;
using Cake.Typstium.Executable;
using Cake.Typstium.UnitTests.Testing;
using Typstium;
using Typstium.Testing.Fixtures;

namespace Cake.Typstium.UnitTests.Executable;

public class TypstCakeResolverTests
{
    private static string Executable => OperatingSystem.IsWindows() ? "typst.exe" : "typst";

    [Test]
    public async Task Puts_TYPST_PATH_before_the_cake_tools_folder_and_the_tools_folder_in_the_bundled_runtimes_place()
    {
        using var work = new TemporaryDirectory("cake-resolver");
        var names = TypstCakeResolver.Locators(CakeFakes.Environment(work.Path), new FakeConfiguration(), toolPath: null).Select(l => l.Name);

        await Assert.That(string.Join(",", names)).IsEqualTo("TYPST_PATH,CakeTools,BundledRuntime,WinGet,WellKnownLocations,PATH");
    }

    [Test]
    public async Task Puts_an_explicit_tool_path_first()
    {
        using var work = new TemporaryDirectory("cake-resolver");
        var names = TypstCakeResolver.Locators(CakeFakes.Environment(work.Path), new FakeConfiguration(), new FilePath("typst")).Select(l => l.Name);

        await Assert.That(names.First()).IsEqualTo("ExplicitPath");
    }

    [Test]
    public async Task Resolves_a_relative_tool_path_against_cakes_working_directory()
    {
        using var work = new TemporaryDirectory("cake-resolver");
        var expected = work.CreateFile("bin/" + Executable, "stub");

        var locator = TypstCakeResolver.Locators(CakeFakes.Environment(work.Path), new FakeConfiguration(), new FilePath("bin/" + Executable))[0];

        await Assert.That(await locator.LocateAsync(CancellationToken.None)).IsEqualTo(TypstLocatorResult.Found(expected));
    }

    [Test]
    public async Task Looks_in_the_tools_folder_under_the_working_directory_by_default()
    {
        using var work = new TemporaryDirectory("cake-resolver");
        var rid = HostRuntimeIdentifier.Current;
        var expected = work.CreateFile($"tools/Typstium.Runtime.{rid}.0.15.1/tools/typst/{rid}/{Executable}", "stub");

        var locator = TypstCakeResolver.Locators(CakeFakes.Environment(work.Path), new FakeConfiguration(), toolPath: null)[1];

        await Assert.That(await locator.LocateAsync(CancellationToken.None)).IsEqualTo(TypstLocatorResult.Found(expected));
    }

    [Test]
    public async Task Honors_a_configured_tools_folder()
    {
        using var work = new TemporaryDirectory("cake-resolver");
        var rid = HostRuntimeIdentifier.Current;
        var expected = work.CreateFile($"build-tools/Typstium.Runtime.{rid}/tools/typst/{rid}/{Executable}", "stub");
        var configuration = new FakeConfiguration();
        configuration.SetValue("Paths_Tools", "build-tools");

        var locator = TypstCakeResolver.Locators(CakeFakes.Environment(work.Path), configuration, toolPath: null)[1];

        await Assert.That(await locator.LocateAsync(CancellationToken.None)).IsEqualTo(TypstLocatorResult.Found(expected));
    }
}

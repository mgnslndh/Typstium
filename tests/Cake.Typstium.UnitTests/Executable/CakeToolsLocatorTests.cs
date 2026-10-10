using Cake.Typstium.Executable;
using Typstium;
using Typstium.Testing.Fixtures;

namespace Cake.Typstium.UnitTests.Executable;

public class CakeToolsLocatorTests
{
    private static string Executable => OperatingSystem.IsWindows() ? "typst.exe" : "typst";

    private static string Install(TemporaryDirectory tools, string folder, string rid) =>
        tools.CreateFile($"{folder}/tools/typst/{rid}/{Executable}", "stub");

    private static async Task<TypstLocatorResult> LocateAsync(TemporaryDirectory tools, params string[] candidates) =>
        await new CakeToolsLocator(tools.Path, candidates).LocateAsync(CancellationToken.None);

    [Test]
    public async Task Is_named_CakeTools()
    {
        await Assert.That(new CakeToolsLocator("tools", ["linux-x64"]).Name).IsEqualTo("CakeTools");
    }

    [Test]
    public async Task Finds_the_binary_of_a_versioned_tool_folder()
    {
        using var tools = new TemporaryDirectory("cake-tools");
        var path = Install(tools, "Typstium.Runtime.linux-x64.0.15.1", "linux-x64");

        await Assert.That(await LocateAsync(tools, "linux-x64")).IsEqualTo(TypstLocatorResult.Found(path));
    }

    [Test]
    public async Task Finds_the_binary_of_an_unversioned_tool_folder()
    {
        using var tools = new TemporaryDirectory("cake-tools");
        var path = Install(tools, "Typstium.Runtime.osx-arm64", "osx-arm64");

        await Assert.That(await LocateAsync(tools, "osx-arm64")).IsEqualTo(TypstLocatorResult.Found(path));
    }

    [Test]
    public async Task Falls_back_to_the_next_candidate()
    {
        using var tools = new TemporaryDirectory("cake-tools");
        var path = Install(tools, "Typstium.Runtime.win-x64.0.15.1", "win-x64");

        await Assert.That(await LocateAsync(tools, "win-arm64", "win-x64")).IsEqualTo(TypstLocatorResult.Found(path));
    }

    [Test]
    public async Task Prefers_the_native_candidate_when_both_are_installed()
    {
        using var tools = new TemporaryDirectory("cake-tools");
        var native = Install(tools, "Typstium.Runtime.win-arm64.0.15.1", "win-arm64");
        Install(tools, "Typstium.Runtime.win-x64.0.15.1", "win-x64");

        await Assert.That(await LocateAsync(tools, "win-arm64", "win-x64")).IsEqualTo(TypstLocatorResult.Found(native));
    }

    [Test]
    public async Task Picks_the_highest_installed_version()
    {
        using var tools = new TemporaryDirectory("cake-tools");
        Install(tools, "Typstium.Runtime.linux-x64", "linux-x64");
        Install(tools, "Typstium.Runtime.linux-x64.0.15.1", "linux-x64");
        var highest = Install(tools, "Typstium.Runtime.linux-x64.0.15.10", "linux-x64");

        await Assert.That(await LocateAsync(tools, "linux-x64")).IsEqualTo(TypstLocatorResult.Found(highest));
    }

    [Test]
    public async Task Ignores_a_package_whose_id_only_starts_with_the_rid()
    {
        using var tools = new TemporaryDirectory("cake-tools");
        Install(tools, "Typstium.Runtime.linux-arm64.0.15.1", "linux-arm64");

        await Assert.That(await LocateAsync(tools, "linux-arm")).IsTypeOf<TypstLocatorResult.NotFoundResult>();
    }

    [Test]
    public async Task Matches_folder_names_in_any_case()
    {
        using var tools = new TemporaryDirectory("cake-tools");
        var path = Install(tools, "typstium.runtime.linux-x64.0.15.1", "linux-x64");

        await Assert.That(await LocateAsync(tools, "linux-x64")).IsEqualTo(TypstLocatorResult.Found(path));
    }

    [Test]
    public async Task Reports_the_patterns_it_searched_when_nothing_is_installed()
    {
        using var tools = new TemporaryDirectory("cake-tools");
        var missing = tools.Combine("tools");

        var result = await new CakeToolsLocator(missing, ["win-arm64", "win-x64"]).LocateAsync(CancellationToken.None);

        var notFound = (TypstLocatorResult.NotFoundResult)result;
        await Assert.That(notFound.SearchedLocations.Length).IsEqualTo(2);
        await Assert.That(notFound.SearchedLocations[0]).Contains("Typstium.Runtime.win-arm64[.<version>]");
        await Assert.That(notFound.SearchedLocations[1]).StartsWith(missing);
    }

    [Test]
    public async Task Sets_the_execute_bit_that_nuget_extraction_drops()
    {
        if (OperatingSystem.IsWindows())
        {
            Skip.Test("Unix file modes only.");
            return;
        }

        using var tools = new TemporaryDirectory("cake-tools");
        var path = Install(tools, "Typstium.Runtime.linux-x64.0.15.1", "linux-x64");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        await LocateAsync(tools, "linux-x64");

        await Assert.That(File.GetUnixFileMode(path).HasFlag(UnixFileMode.UserExecute)).IsTrue();
    }
}

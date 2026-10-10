using Cake.Typstium.Executable;
using Typstium;
using Typstium.Testing.Fixtures;

namespace Cake.Typstium.UnitTests.Executable;

public class CakeToolsLocatorTests
{
    private static string Executable => OperatingSystem.IsWindows() ? "typst.exe" : "typst";

    private static string Install(TemporaryDirectory tools, string folder, string rid) =>
        tools.CreateFile($"{folder}/tools/typst/{rid}/{Executable}", "stub");

    private static CakeToolsLocator Locator(string toolsDirectory, string[] candidates, string? registered = null, List<string>? warnings = null) =>
        new(toolsDirectory, candidates, _ => registered, message => warnings?.Add(message));

    private static async Task<TypstLocatorResult> LocateAsync(TemporaryDirectory tools, params string[] candidates) =>
        await Locator(tools.Path, candidates).LocateAsync(CancellationToken.None);

    [Test]
    public async Task Is_named_CakeTools()
    {
        await Assert.That(Locator("tools", ["linux-x64"]).Name).IsEqualTo("CakeTools");
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

        var result = await Locator(missing, ["win-arm64", "win-x64"]).LocateAsync(CancellationToken.None);

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

    [Test]
    public async Task The_version_a_tool_directive_registered_wins_over_a_higher_stale_one()
    {
        using var tools = new TemporaryDirectory("cake-tools");
        var pinned = Install(tools, "Typstium.Runtime.linux-x64.0.15.1", "linux-x64");
        Install(tools, "Typstium.Runtime.linux-x64.0.16.0", "linux-x64");

        var result = await Locator(tools.Path, ["linux-x64"], registered: pinned).LocateAsync(CancellationToken.None);

        await Assert.That(result).IsEqualTo(TypstLocatorResult.Found(pinned));
    }

    [Test]
    public async Task Only_the_version_is_taken_from_a_registered_file_of_another_platform()
    {
        using var tools = new TemporaryDirectory("cake-tools");
        var other = Install(tools, "Typstium.Runtime.linux-arm64.0.15.1", "linux-arm64");
        var host = Install(tools, "Typstium.Runtime.linux-x64.0.15.1", "linux-x64");
        Install(tools, "Typstium.Runtime.linux-x64.0.16.0", "linux-x64");

        var result = await Locator(tools.Path, ["linux-x64"], registered: other).LocateAsync(CancellationToken.None);

        await Assert.That(result).IsEqualTo(TypstLocatorResult.Found(host));
    }

    [Test]
    public async Task A_registered_file_outside_the_tools_folder_is_no_hint()
    {
        using var tools = new TemporaryDirectory("cake-tools");
        Install(tools, "Typstium.Runtime.linux-x64.0.15.1", "linux-x64");
        var highest = Install(tools, "Typstium.Runtime.linux-x64.0.16.0", "linux-x64");
        using var elsewhere = new TemporaryDirectory("cake-path");
        var onPath = elsewhere.CreateFile("Typstium.Runtime.linux-x64.0.15.1/tools/typst/linux-x64/" + Executable, "stub");

        var result = await Locator(tools.Path, ["linux-x64"], registered: onPath).LocateAsync(CancellationToken.None);

        await Assert.That(result).IsEqualTo(TypstLocatorResult.Found(highest));
    }

    [Test]
    public async Task A_hinted_version_the_host_lacks_falls_back_to_the_highest()
    {
        using var tools = new TemporaryDirectory("cake-tools");
        var other = Install(tools, "Typstium.Runtime.win-x64.0.14.0", "win-x64");
        Install(tools, "Typstium.Runtime.linux-x64.0.15.1", "linux-x64");
        var highest = Install(tools, "Typstium.Runtime.linux-x64.0.16.0", "linux-x64");

        var result = await Locator(tools.Path, ["linux-x64"], registered: other).LocateAsync(CancellationToken.None);

        await Assert.That(result).IsEqualTo(TypstLocatorResult.Found(highest));
    }

    [Test]
    public async Task Several_installed_versions_are_reported_naming_the_one_used()
    {
        using var tools = new TemporaryDirectory("cake-tools");
        Install(tools, "Typstium.Runtime.linux-x64.0.15.1", "linux-x64");
        Install(tools, "Typstium.Runtime.linux-x64.0.16.0", "linux-x64");
        var warnings = new List<string>();

        await Locator(tools.Path, ["linux-x64"], warnings: warnings).LocateAsync(CancellationToken.None);

        var warning = warnings.Single();
        await Assert.That(warning).Contains("Typstium.Runtime.linux-x64");
        await Assert.That(warning).Contains("0.15.1, 0.16.0");
        await Assert.That(warning).Contains("using 0.16.0, the highest");
    }

    [Test]
    public async Task The_report_says_when_a_tool_directive_chose_the_version()
    {
        using var tools = new TemporaryDirectory("cake-tools");
        var pinned = Install(tools, "Typstium.Runtime.linux-x64.0.15.1", "linux-x64");
        Install(tools, "Typstium.Runtime.linux-x64.0.16.0", "linux-x64");
        var warnings = new List<string>();

        await Locator(tools.Path, ["linux-x64"], registered: pinned, warnings: warnings).LocateAsync(CancellationToken.None);

        await Assert.That(warnings.Single()).Contains("using 0.15.1, registered by #tool");
    }

    [Test]
    public async Task A_single_installed_version_is_not_reported()
    {
        using var tools = new TemporaryDirectory("cake-tools");
        Install(tools, "Typstium.Runtime.linux-x64.0.15.1", "linux-x64");
        Install(tools, "Typstium.Runtime.linux-arm64.0.16.0", "linux-arm64");
        var warnings = new List<string>();

        await Locator(tools.Path, ["linux-x64"], warnings: warnings).LocateAsync(CancellationToken.None);

        await Assert.That(warnings).IsEmpty();
    }
}

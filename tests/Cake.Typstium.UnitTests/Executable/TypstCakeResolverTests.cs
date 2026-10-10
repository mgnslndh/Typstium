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
        var names = TypstCakeResolver.Locators(new TestCakeContext(CakeFakes.Environment(work.Path)), toolPath: null).Select(l => l.Name);

        await Assert.That(string.Join(",", names)).IsEqualTo("TYPST_PATH,CakeTools,BundledRuntime,WinGet,WellKnownLocations,PATH");
    }

    [Test]
    public async Task Puts_an_explicit_tool_path_first()
    {
        using var work = new TemporaryDirectory("cake-resolver");
        var names = TypstCakeResolver.Locators(new TestCakeContext(CakeFakes.Environment(work.Path)), new FilePath("typst")).Select(l => l.Name);

        await Assert.That(names.First()).IsEqualTo("ExplicitPath");
    }

    [Test]
    public async Task Resolves_a_relative_tool_path_against_cakes_working_directory()
    {
        using var work = new TemporaryDirectory("cake-resolver");
        var expected = work.CreateFile("bin/" + Executable, "stub");

        var locator = TypstCakeResolver.Locators(new TestCakeContext(CakeFakes.Environment(work.Path)), new FilePath("bin/" + Executable))[0];

        await Assert.That(await locator.LocateAsync(CancellationToken.None)).IsEqualTo(TypstLocatorResult.Found(expected));
    }

    [Test]
    public async Task Looks_in_the_tools_folder_under_the_working_directory_by_default()
    {
        using var work = new TemporaryDirectory("cake-resolver");
        var rid = HostRuntimeIdentifier.Current;
        var expected = work.CreateFile($"tools/Typstium.Runtime.{rid}.0.15.1/tools/typst/{rid}/{Executable}", "stub");

        var locator = TypstCakeResolver.Locators(new TestCakeContext(CakeFakes.Environment(work.Path)), toolPath: null)[1];

        await Assert.That(await locator.LocateAsync(CancellationToken.None)).IsEqualTo(TypstLocatorResult.Found(expected));
    }

    [Test]
    public async Task Honors_a_configured_tools_folder()
    {
        using var work = new TemporaryDirectory("cake-resolver");
        var rid = HostRuntimeIdentifier.Current;
        var expected = work.CreateFile($"build-tools/Typstium.Runtime.{rid}/tools/typst/{rid}/{Executable}", "stub");
        var context = new TestCakeContext(CakeFakes.Environment(work.Path));
        context.FakeConfiguration.SetValue("Paths_Tools", "build-tools");

        var locator = TypstCakeResolver.Locators(context, toolPath: null)[1];

        await Assert.That(await locator.LocateAsync(CancellationToken.None)).IsEqualTo(TypstLocatorResult.Found(expected));
    }

    [Test]
    public async Task The_tools_folder_follows_what_tool_directives_registered_and_reports_to_cakes_log()
    {
        using var work = new TemporaryDirectory("cake-resolver");
        var rid = HostRuntimeIdentifier.Current;
        var pinned = work.CreateFile($"tools/Typstium.Runtime.{rid}.0.15.1/tools/typst/{rid}/{Executable}", "stub");
        work.CreateFile($"tools/Typstium.Runtime.{rid}.0.16.0/tools/typst/{rid}/{Executable}", "stub");
        var context = new TestCakeContext(CakeFakes.Environment(work.Path));
        context.FakeTools.RegisterFile(new FilePath(pinned));

        var locator = TypstCakeResolver.Locators(context, toolPath: null)[1];

        await Assert.That(await locator.LocateAsync(CancellationToken.None)).IsEqualTo(TypstLocatorResult.Found(pinned));
        await Assert.That(context.FakeLog.Entries.Single(entry => entry.Level == Cake.Core.Diagnostics.LogLevel.Warning).Message)
            .Contains("registered by #tool");
    }

    [Test]
    public async Task A_tool_directive_install_is_found_after_the_script_changes_its_working_directory()
    {
        using var work = new TemporaryDirectory("cake-resolver");
        var rid = HostRuntimeIdentifier.Current;
        var pinned = work.CreateFile($"tools/Typstium.Runtime.{rid}.0.15.1/tools/typst/{rid}/{Executable}", "stub");
        var environment = CakeFakes.Environment(work.Path);
        var context = new TestCakeContext(environment);
        context.FakeTools.RegisterFile(new FilePath(pinned));
        environment.WorkingDirectory = new DirectoryPath(work.CreateDirectory("docs"));

        var locator = TypstCakeResolver.Locators(context, toolPath: null)[1];

        await Assert.That(await locator.LocateAsync(CancellationToken.None)).IsEqualTo(TypstLocatorResult.Found(pinned));
    }
}

using Typstium.Executable;
using Typstium.UnitTests.Testing;

namespace Typstium.UnitTests.Executable;

public class LocatorTests
{
    private static string Root => FakeLocatorEnvironment.Root;

    [Test]
    public async Task Explicit_path_is_found_when_it_exists()
    {
        var environment = new FakeLocatorEnvironment();
        var typst = environment.AddFile(Root, "tools", "typst.exe");

        var found = await new ExplicitPathLocator(typst, environment).LocateAsync(CancellationToken.None);
        var missing = await new ExplicitPathLocator(Path.Combine(Root, "nope.exe"), environment).LocateAsync(CancellationToken.None);

        await Assert.That(found).IsEqualTo(TypstLocatorResult.Found(typst));
        await Assert.That(((TypstLocatorResult.NotFoundResult)missing).SearchedLocations.Single()).IsEqualTo(Path.Combine(Root, "nope.exe"));
    }

    [Test]
    public async Task Environment_variable_reports_when_it_is_not_set()
    {
        var environment = new FakeLocatorEnvironment();
        var result = (TypstLocatorResult.NotFoundResult)await new EnvironmentVariableLocator("TYPST_PATH", environment).LocateAsync(CancellationToken.None);
        await Assert.That(result.SearchedLocations.Single()).Contains("not set");

        var typst = environment.AddFile(Root, "env", "typst.exe");
        environment.Variables["TYPST_PATH"] = typst;
        var found = await new EnvironmentVariableLocator("TYPST_PATH", environment).LocateAsync(CancellationToken.None);
        await Assert.That(found).IsEqualTo(TypstLocatorResult.Found(typst));
    }

    [Test]
    public async Task Bundled_runtime_supports_the_classic_asp_net_bin_folder_and_sets_the_execute_bit()
    {
        var environment = new FakeLocatorEnvironment { IsWindows = false, RuntimeIdentifier = "linux-x64" };
        var typst = environment.AddFile(environment.BaseDirectory, "bin", "typst", "linux-x64", "typst");

        var found = await new BundledRuntimeLocator(environment).LocateAsync(CancellationToken.None);

        await Assert.That(found).IsEqualTo(TypstLocatorResult.Found(typst));
        await Assert.That(environment.MadeExecutable).Contains(typst);
    }

    [Test]
    public async Task WinGet_finds_links_and_package_folders()
    {
        var local = Path.Combine(Root, "LocalAppData");
        var environment = new FakeLocatorEnvironment();
        environment.Variables["LOCALAPPDATA"] = local;
        var packaged = environment.AddFile(local, "Microsoft", "WinGet", "Packages", "Typst.Typst_Microsoft.Winget.Source_8wekyb3d8bbwe", "typst-x86_64-pc-windows-msvc", "typst.exe");

        var found = await new WinGetLocator(environment).LocateAsync(CancellationToken.None);
        await Assert.That(found).IsEqualTo(TypstLocatorResult.Found(packaged));

        var link = environment.AddFile(local, "Microsoft", "WinGet", "Links", "typst.exe");
        var preferred = await new WinGetLocator(environment).LocateAsync(CancellationToken.None);
        await Assert.That(preferred).IsEqualTo(TypstLocatorResult.Found(link));
    }

    [Test]
    public async Task WinGet_is_windows_only()
    {
        var result = await new WinGetLocator(new FakeLocatorEnvironment { IsWindows = false }).LocateAsync(CancellationToken.None);
        await Assert.That(result is TypstLocatorResult.NotFoundResult).IsTrue();
    }

    [Test]
    public async Task Search_path_never_uses_relative_entries()
    {
        var environment = new FakeLocatorEnvironment();
        var tools = Path.Combine(Root, "tools");
        environment.Variables["PATH"] = string.Join(Path.PathSeparator.ToString(), ".", "relative" + Path.DirectorySeparatorChar + "bin", "", tools);
        environment.AddFile(".", "typst.exe");
        var typst = environment.AddFile(tools, "typst.exe");

        var found = await new SearchPathLocator(environment).LocateAsync(CancellationToken.None);
        await Assert.That(found).IsEqualTo(TypstLocatorResult.Found(typst));
    }

    [Test]
    [Arguments("C:")]
    [Arguments("C:tools")]
    [Arguments("\\tools")]
    public async Task Search_path_ignores_drive_and_root_relative_entries_on_windows(string entry)
    {
        var environment = new FakeLocatorEnvironment { IsWindows = true };
        environment.Variables["PATH"] = entry;
        environment.AddFile(entry, "typst.exe");

        var result = await new SearchPathLocator(environment).LocateAsync(CancellationToken.None);

        await Assert.That(result is TypstLocatorResult.NotFoundResult).IsTrue();
    }

    [Test]
    public async Task Search_path_accepts_drive_absolute_entries_on_windows()
    {
        var environment = new FakeLocatorEnvironment { IsWindows = true };
        environment.Variables["PATH"] = "C:\\tools";
        environment.Files.Add(Path.Combine(@"C:\tools", "typst.exe"));

        var result = await new SearchPathLocator(environment).LocateAsync(CancellationToken.None);

        await Assert.That(result is TypstLocatorResult.FoundResult).IsTrue();
    }

    [Test]
    public async Task A_relative_typst_path_variable_is_not_found()
    {
        var environment = new FakeLocatorEnvironment { IsWindows = true };
        environment.Variables["TYPST_PATH"] = "C:typst.exe";

        var result = (TypstLocatorResult.NotFoundResult)await new EnvironmentVariableLocator("TYPST_PATH", environment).LocateAsync(CancellationToken.None);

        await Assert.That(result.SearchedLocations.Single()).Contains("relative paths are not allowed");
    }
}

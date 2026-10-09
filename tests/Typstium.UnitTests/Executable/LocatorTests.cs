using Typstium.Executable;
using Typstium.UnitTests.Testing;
using TUnit.Assertions.Enums;

namespace Typstium.UnitTests.Executable;

public class LocatorTests
{
    private static string Root => FakeLocatorEnvironment.Root;

    [Test]
    public async Task Explicit_path_is_found_when_it_exists()
    {
        var environment = new FakeLocatorEnvironment();
        var typst = environment.AddFile(Root, "tools", environment.ExecutableFileName);

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

        var typst = environment.AddFile(Root, "env", environment.ExecutableFileName);
        environment.Variables["TYPST_PATH"] = typst;
        var found = await new EnvironmentVariableLocator("TYPST_PATH", environment).LocateAsync(CancellationToken.None);
        await Assert.That(found).IsEqualTo(TypstLocatorResult.Found(typst));
    }

    [Test]
    public async Task Bundled_runtime_supports_the_classic_asp_net_bin_folder_and_sets_the_execute_bit()
    {
        var environment = new FakeLocatorEnvironment { IsWindows = false, RuntimeIdentifier = "linux-x64" };
        var typst = environment.AddFile(environment.BaseDirectory, "bin", "typst", "linux-x64", TypstExecutableFileNames.Unix);

        var found = await new BundledRuntimeLocator(environment).LocateAsync(CancellationToken.None);

        await Assert.That(found).IsEqualTo(TypstLocatorResult.Found(typst));
        await Assert.That(environment.MadeExecutable).Contains(typst);
    }

    [Test]
    public async Task WinGet_finds_links_and_package_folders()
    {
        var local = Path.Combine(Root, "LocalAppData");
        var environment = new FakeLocatorEnvironment { IsWindows = true };
        environment.Variables["LOCALAPPDATA"] = local;
        var packaged = environment.AddFile(local, "Microsoft", "WinGet", "Packages", "Typst.Typst_Microsoft.Winget.Source_8wekyb3d8bbwe", "typst-x86_64-pc-windows-msvc", TypstExecutableFileNames.Windows);

        var found = await new WinGetLocator(environment).LocateAsync(CancellationToken.None);
        await Assert.That(found).IsEqualTo(TypstLocatorResult.Found(packaged));

        var link = environment.AddFile(local, "Microsoft", "WinGet", "Links", TypstExecutableFileNames.Windows);
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
        var executable = environment.ExecutableFileName;
        var tools = Path.Combine(Root, "tools");
        environment.Variables["PATH"] = string.Join(Path.PathSeparator.ToString(), ".", "relative" + Path.DirectorySeparatorChar + "bin", "", tools);
        environment.AddFile(".", executable);
        var typst = environment.AddFile(tools, executable);

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
        environment.AddFile(entry, TypstExecutableFileNames.Windows);

        var result = await new SearchPathLocator(environment).LocateAsync(CancellationToken.None);

        await Assert.That(result is TypstLocatorResult.NotFoundResult).IsTrue();
    }

    [Test]
    public async Task Search_path_accepts_drive_absolute_entries_on_windows()
    {
        // PATH is split with the host's separator (';' on Windows, ':' elsewhere), so a drive-letter entry
        // only exists as such on Windows.
        if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
        {
            Skip.Test("Drive-letter PATH entries only exist on Windows.");
        }

        var environment = new FakeLocatorEnvironment { IsWindows = true };
        environment.Variables["PATH"] = "C:\\tools";
        environment.Files.Add(Path.Combine(@"C:\tools", TypstExecutableFileNames.Windows));

        var result = await new SearchPathLocator(environment).LocateAsync(CancellationToken.None);

        await Assert.That(result is TypstLocatorResult.FoundResult).IsTrue();
    }

    [Test]
    public async Task Well_known_locations_on_windows_are_cargo_then_scoop_and_nothing_unix_like()
    {
        var environment = new FakeLocatorEnvironment { IsWindows = true };
        var home = environment.HomeDirectory!;
        environment.Files.Add("/usr/local/bin/typst");
        var locator = new WellKnownLocationsLocator(environment);

        var notFound = (TypstLocatorResult.NotFoundResult)await locator.LocateAsync(CancellationToken.None);
        var scoop = environment.AddFile(home, "scoop", "shims", "typst.exe");
        var foundScoop = await locator.LocateAsync(CancellationToken.None);
        var cargo = environment.AddFile(home, ".cargo", "bin", "typst.exe");
        var foundCargo = await locator.LocateAsync(CancellationToken.None);

        await Assert.That(notFound.SearchedLocations).IsEquivalentTo(new[] { cargo, scoop }, CollectionOrdering.Matching);
        await Assert.That(foundScoop).IsEqualTo(TypstLocatorResult.Found(scoop));
        await Assert.That(foundCargo).IsEqualTo(TypstLocatorResult.Found(cargo));
    }

    [Test]
    public async Task Well_known_locations_on_unix_are_cargo_then_homebrew_then_usr_local()
    {
        var environment = new FakeLocatorEnvironment { IsWindows = false };
        var cargo = Path.Combine(environment.HomeDirectory!, ".cargo", "bin", "typst");
        var locator = new WellKnownLocationsLocator(environment);

        environment.Files.Add("/usr/local/bin/typst");
        var foundUsrLocal = await locator.LocateAsync(CancellationToken.None);
        environment.Files.Add("/opt/homebrew/bin/typst");
        var foundHomebrew = await locator.LocateAsync(CancellationToken.None);
        environment.Files.Add(cargo);
        var foundCargo = await locator.LocateAsync(CancellationToken.None);

        await Assert.That(foundUsrLocal).IsEqualTo(TypstLocatorResult.Found("/usr/local/bin/typst"));
        await Assert.That(foundHomebrew).IsEqualTo(TypstLocatorResult.Found("/opt/homebrew/bin/typst"));
        await Assert.That(foundCargo).IsEqualTo(TypstLocatorResult.Found(cargo));
    }

    [Test]
    public async Task Well_known_locations_without_a_home_directory_still_search_the_system_folders()
    {
        var environment = new FakeLocatorEnvironment { IsWindows = false, HomeDirectory = null };

        var result = (TypstLocatorResult.NotFoundResult)await new WellKnownLocationsLocator(environment).LocateAsync(CancellationToken.None);

        await Assert.That(result.SearchedLocations).IsEquivalentTo(new[] { "/opt/homebrew/bin/typst", "/usr/local/bin/typst" }, CollectionOrdering.Matching);
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

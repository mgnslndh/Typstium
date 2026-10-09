using System.Text.RegularExpressions;
using Typstium.Executable;
using Typstium.Testing.Fixtures;
using TUnit.Assertions.Enums;

namespace Typstium.UnitTests.Executable;

/// <summary>The real machine behind the locators, on a real file system.</summary>
public class SystemLocatorEnvironmentTests
{
    private static SystemLocatorEnvironment Environment => SystemLocatorEnvironment.Instance;

    // WinGet probes Packages/Typst.Typst_*/typst-*/typst.exe: a file returned as a folder would be probed as one.
    [Test]
    public async Task Enumerating_finds_matching_child_folders_only()
    {
        using var temp = new TemporaryDirectory("environment");
        var package = temp.CreateDirectory("Typst.Typst_Microsoft.Winget.Source_8wekyb3d8bbwe");
        temp.CreateDirectory("Other.Package");
        temp.CreateFile("Typst.Typst_not-a-folder.txt", "x");
        temp.CreateDirectory("Typst.Typst_x/nested");

        var found = Environment.EnumerateDirectories(temp.Path, "Typst.Typst_*").OrderBy(path => path, StringComparer.Ordinal);

        await Assert.That(found).IsEquivalentTo(new[] { package, temp.Combine("Typst.Typst_x") }, CollectionOrdering.Matching);
    }

    [Test]
    public async Task Enumerating_a_missing_folder_finds_nothing()
    {
        using var temp = new TemporaryDirectory("environment");

        await Assert.That(Environment.EnumerateDirectories(temp.Combine("missing"), "*")).IsEmpty();
    }

    // The bundled-runtime locator looks in typst/<rid>/, named after NuGet runtime identifiers.
    [Test]
    public async Task The_runtime_identifier_is_shaped_like_a_nuget_rid()
    {
        await Assert.That(Regex.IsMatch(Environment.RuntimeIdentifier, "^(win|linux|osx)-(x64|x86|arm64|arm)\\z")).IsTrue()
            .Because(Environment.RuntimeIdentifier);
    }

#if NET8_0_OR_GREATER
    private const UnixFileMode ReadWrite = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;

    private const UnixFileMode Execute = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    // NuGet drops the execute bit when it extracts a package, so a bundled typst can't start until it's restored.
    [Test]
    public async Task A_bundled_executable_gets_its_execute_bit_back_on_unix()
    {
        if (OperatingSystem.IsWindows())
        {
            Skip.Test("Unix file modes don't exist on Windows.");
            return;
        }

        using var temp = new TemporaryDirectory("environment");
        var typst = temp.CreateFile("typst", "#!/bin/sh");
        File.SetUnixFileMode(typst, ReadWrite);

        Environment.EnsureExecutable(typst);

        await Assert.That(File.GetUnixFileMode(typst)).IsEqualTo(ReadWrite | Execute);
    }

    [Test]
    public async Task An_executable_file_keeps_its_mode()
    {
        if (OperatingSystem.IsWindows())
        {
            Skip.Test("Unix file modes don't exist on Windows.");
            return;
        }

        using var temp = new TemporaryDirectory("environment");
        var typst = temp.CreateFile("typst", "#!/bin/sh");
        const UnixFileMode ownerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        File.SetUnixFileMode(typst, ownerOnly);

        Environment.EnsureExecutable(typst);

        await Assert.That(File.GetUnixFileMode(typst)).IsEqualTo(ownerOnly);
    }
#endif
}

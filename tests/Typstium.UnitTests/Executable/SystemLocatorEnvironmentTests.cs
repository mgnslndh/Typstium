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

    // Runtime packages ship typst/<rid>/ folders named by NuGet runtime identifiers; a wrong name means the bundled
    // Typst is never found, e.g. on Apple silicon (osx-arm64) or ARM Linux servers.
    [Test]
    [Arguments(true, false, System.Runtime.InteropServices.Architecture.X64, "win-x64")]
    [Arguments(true, false, System.Runtime.InteropServices.Architecture.X86, "win-x86")]
    [Arguments(true, false, System.Runtime.InteropServices.Architecture.Arm64, "win-arm64")]
    [Arguments(false, true, System.Runtime.InteropServices.Architecture.Arm64, "osx-arm64")]
    [Arguments(false, true, System.Runtime.InteropServices.Architecture.X64, "osx-x64")]
    [Arguments(false, false, System.Runtime.InteropServices.Architecture.X64, "linux-x64")]
    [Arguments(false, false, System.Runtime.InteropServices.Architecture.Arm64, "linux-arm64")]
    [Arguments(false, false, System.Runtime.InteropServices.Architecture.Arm, "linux-arm")]
    public async Task Runtime_identifiers_follow_nuget_naming(bool isWindows, bool isMacOS, System.Runtime.InteropServices.Architecture architecture, string expected)
    {
        await Assert.That(SystemLocatorEnvironment.RuntimeIdentifierFor(isWindows, isMacOS, architecture)).IsEqualTo(expected);
    }

#if NET8_0_OR_GREATER
    [Test]
    public async Task Architectures_without_a_known_name_use_their_lowercase_enum_name()
    {
        await Assert.That(SystemLocatorEnvironment.RuntimeIdentifierFor(false, false, System.Runtime.InteropServices.Architecture.S390x)).IsEqualTo("linux-s390x");
    }
#endif

    [Test]
    [Arguments(@"C:\Users\asa", null, @"C:\Users\asa")]
    [Arguments("/home/asa", "/home/other", "/home/asa")]
    [Arguments("", "/home/asa", "/home/asa")]
    [Arguments(null, "/home/asa", "/home/asa")]
    [Arguments("", null, null)]
    public async Task The_home_directory_falls_back_to_HOME_when_the_profile_is_unknown(string? userProfile, string? homeVariable, string? expected)
    {
        await Assert.That(SystemLocatorEnvironment.HomeDirectoryFrom(userProfile, homeVariable)).IsEqualTo(expected);
    }

    // The bundled-runtime locator looks in typst/<rid>/, named after NuGet runtime identifiers.
    [Test]
    public async Task The_runtime_identifier_is_shaped_like_a_nuget_rid()
    {
        await Assert.That(Regex.IsMatch(Environment.RuntimeIdentifier, "^(win|linux|osx)-(x64|x86|arm64|arm)\\z")).IsTrue()
            .Because(Environment.RuntimeIdentifier);
    }

    [Test]
    public async Task A_folder_without_a_manifest_reports_it_missing()
    {
        using var temp = new TemporaryDirectory("environment");

        var result = Environment.ReadRuntimeManifest(temp.Path);

        await Assert.That(result).IsEqualTo(new RuntimeManifestResult.Missing(Path.Combine(temp.Path, RuntimeManifest.FileName)));
    }

    [Test]
    public async Task A_manifest_file_is_read_and_parsed()
    {
        using var temp = new TemporaryDirectory("environment");
        temp.CreateFile(RuntimeManifest.FileName, """{ "version": 1, "runtimes": { "linux-x64": "tools/typst" } }""");

        var loaded = (RuntimeManifestResult.Loaded)Environment.ReadRuntimeManifest(temp.Path);

        await Assert.That(loaded.Manifest.Executables["linux-x64"]).IsEqualTo(temp.Combine("tools", "typst"));
    }

    [Test]
    public async Task An_unreadable_manifest_is_invalid()
    {
        using var temp = new TemporaryDirectory("environment");
        // A folder where the file should be: reading it throws UnauthorizedAccessException (Windows) or IOException (Unix).
        temp.CreateDirectory(RuntimeManifest.FileName);

        var invalid = (RuntimeManifestResult.Invalid)Environment.ReadRuntimeManifest(temp.Path);

        await Assert.That(invalid.Reason).StartsWith("unreadable: ");
    }

    [Test]
    public async Task A_missing_folder_reports_the_manifest_missing()
    {
        using var temp = new TemporaryDirectory("environment");

        var result = Environment.ReadRuntimeManifest(temp.Combine("nope"));

        await Assert.That(result is RuntimeManifestResult.Missing).IsTrue();
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

using System.Runtime.InteropServices;
using Cake.Typstium.Executable;

namespace Cake.Typstium.UnitTests.Executable;

public class HostRuntimeIdentifierTests
{
    [Test]
    [Arguments(true, false, Architecture.X64, "win-x64")]
    [Arguments(true, false, Architecture.Arm64, "win-arm64")]
    [Arguments(false, true, Architecture.Arm64, "osx-arm64")]
    [Arguments(false, true, Architecture.X64, "osx-x64")]
    [Arguments(false, false, Architecture.X64, "linux-x64")]
    [Arguments(false, false, Architecture.Arm, "linux-arm")]
    public async Task Names_the_runtime_package_of_an_os_and_architecture(bool isWindows, bool isMacOS, Architecture architecture, string expected)
    {
        await Assert.That(HostRuntimeIdentifier.For(isWindows, isMacOS, architecture)).IsEqualTo(expected);
    }

    [Test]
    [Arguments("win-arm64", "win-arm64,win-x64")]
    [Arguments("osx-arm64", "osx-arm64,osx-x64")]
    [Arguments("linux-arm64", "linux-arm64")]
    [Arguments("win-x64", "win-x64")]
    public async Task Tries_the_emulated_x64_package_after_the_native_one_on_arm_windows_and_macs(string rid, string expected)
    {
        await Assert.That(string.Join(",", HostRuntimeIdentifier.Candidates(rid))).IsEqualTo(expected);
    }

    [Test]
    public async Task The_current_identifier_starts_with_this_os()
    {
        var os = OperatingSystem.IsWindows() ? "win-" : OperatingSystem.IsMacOS() ? "osx-" : "linux-";
        await Assert.That(HostRuntimeIdentifier.Current).StartsWith(os);
    }
}

using System.Runtime.InteropServices;

namespace Cake.Typstium.Executable;

/// <summary>
/// The runtime identifier of the machine running Cake, and the runtime packages worth trying on it, best first. A copy
/// of the core's RuntimeIdentifierFor and RuntimeIdentifierCandidates, which stay internal there (spec cake.md D7).
/// </summary>
internal static class HostRuntimeIdentifier
{
    public static string Current { get; } =
        For(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS(), RuntimeInformation.OSArchitecture);

    public static string For(bool isWindows, bool isMacOS, Architecture architecture)
    {
        var os = isWindows ? "win" : isMacOS ? "osx" : "linux";
        var name = architecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            Architecture.X86 => "x86",
            var other => other.ToString().ToLowerInvariant(),
        };
        return os + "-" + name;
    }

    /// <summary>The native RID, then the x64 binary Windows on Arm and Rosetta run under emulation (runtime-packages D11).</summary>
    public static IReadOnlyList<string> Candidates(string runtimeIdentifier) => runtimeIdentifier switch
    {
        "win-arm64" => ["win-arm64", "win-x64"],
        "osx-arm64" => ["osx-arm64", "osx-x64"],
        _ => [runtimeIdentifier],
    };
}

namespace Typstium.Executable;

/// <summary>
/// The bundled runtimes worth trying on a machine, best first: the native RID, then the x64 binary that Windows on Arm
/// and Rosetta run under emulation (spec runtime-packages §7, D11). Linux has no general x64 emulation.
/// </summary>
internal static class RuntimeIdentifierCandidates
{
    public static IReadOnlyList<string> For(string runtimeIdentifier) => runtimeIdentifier switch
    {
        "win-arm64" => ["win-arm64", "win-x64"],
        "osx-arm64" => ["osx-arm64", "osx-x64"],
        _ => [runtimeIdentifier],
    };
}

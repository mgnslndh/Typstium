using System.Runtime.InteropServices;

// The Typst release assets packed into Typstium.Runtime.<rid> (docs/specs/runtime-packages.md §8): which RIDs, which
// Rust target each one's asset is built for, and where the assets live.
public static class TypstRuntimes
{
    public const string LockFile = "build/typst-runtimes.json";

    public const string Repository = "typst/typst";

    public static IReadOnlyDictionary<string, string> RustTargets { get; } = new SortedDictionary<string, string>(StringComparer.Ordinal)
    {
        ["linux-arm"] = "armv7-unknown-linux-musleabi",
        ["linux-arm64"] = "aarch64-unknown-linux-musl",
        ["linux-x64"] = "x86_64-unknown-linux-musl",
        ["osx-arm64"] = "aarch64-apple-darwin",
        ["osx-x64"] = "x86_64-apple-darwin",
        ["win-arm64"] = "aarch64-pc-windows-msvc",
        ["win-x64"] = "x86_64-pc-windows-msvc",
    };

    public static bool IsWindows(string rid) => rid.StartsWith("win-", StringComparison.Ordinal);

    public static string AssetName(string rid) => $"typst-{RustTargets[rid]}.{(IsWindows(rid) ? "zip" : "tar.xz")}";

    public static string ExecutableName(string rid) => IsWindows(rid) ? "typst.exe" : "typst";

    public static Uri AssetUrl(string version, string assetName) => new($"https://github.com/{Repository}/releases/download/v{version}/{assetName}");

    // The machine running the build, named like the packages (the OS architecture, as the locator uses).
    public static string HostRuntimeIdentifier =>
        (OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux") + "-" +
        RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();
}

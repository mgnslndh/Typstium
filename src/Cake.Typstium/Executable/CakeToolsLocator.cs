using Typstium;

namespace Cake.Typstium.Executable;

/// <summary>
/// Finds Typst where <c>#tool nuget:?package=Typstium.Runtime.&lt;rid&gt;</c> installs it (spec cake.md §5): in Cake's
/// tools folder, for each candidate RID in order, the runtime package's binary. Several RIDs can be installed at once,
/// so it never takes the file from Cake's tool registry, which resolves by file name only (D6). It takes only the
/// version: when the file Cake resolves for the name lies in a <c>Typstium.Runtime.&lt;any rid&gt;.&lt;version&gt;</c> folder,
/// that version (what <c>#tool</c> pinned this run) wins over higher stale ones; otherwise the highest version wins
/// (D20). Several installed versions of one package are reported, because <c>#tool</c> never removes old ones.
/// </summary>
/// <param name="toolsDirectory">Cake's tools folder.</param>
/// <param name="candidateRuntimeIdentifiers">The RIDs to try, best first.</param>
/// <param name="resolveRegistered">Cake's tool lookup by file name (<c>context.Tools.Resolve</c>); null when unknown.</param>
/// <param name="warn">Reports several installed versions.</param>
internal sealed class CakeToolsLocator(
    string toolsDirectory,
    IReadOnlyList<string> candidateRuntimeIdentifiers,
    Func<string, string?> resolveRegistered,
    Action<string> warn) : ITypstExecutableLocator
{
    private const string PackagePrefix = "Typstium.Runtime.";

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public string Name => "CakeTools";

    public ValueTask<TypstLocatorResult> LocateAsync(CancellationToken cancellationToken)
    {
        var executable = OperatingSystem.IsWindows() ? "typst.exe" : "typst";
        var pinned = RegisteredVersion(executable);
        var searched = new List<string>();
        foreach (var rid in candidateRuntimeIdentifiers)
        {
            var packageId = PackagePrefix + rid;
            var inPackage = Path.Combine("tools", "typst", rid, executable);
            searched.Add(Path.Combine(toolsDirectory, packageId + "[.<version>]", inPackage));
            var installed = PackageFolders(packageId)
                .Select(folder => (folder.Version, Executable: Path.Combine(folder.Path, inPackage)))
                .Where(folder => File.Exists(folder.Executable))
                .ToArray();
            if (installed.Length == 0)
            {
                continue;
            }

            var byPin = pinned is not null && installed.Any(folder => folder.Version.CompareTo(pinned) == 0);
            var chosen = byPin ? installed.First(folder => folder.Version.CompareTo(pinned) == 0) : installed[0];
            if (installed.Length > 1)
            {
                var versions = string.Join(", ", Enumerable.Reverse(installed).Select(folder => Describe(folder.Version)));
                warn($"{toolsDirectory} has several versions of {packageId}: {versions}; using {Describe(chosen.Version)}, " +
                     (byPin ? "registered by #tool" : "the highest") +
                     ". #tool doesn't remove old versions; delete the folders you don't need.");
            }

            EnsureExecutable(chosen.Executable);
            return new ValueTask<TypstLocatorResult>(TypstLocatorResult.Found(chosen.Executable));
        }

        return new ValueTask<TypstLocatorResult>(TypstLocatorResult.NotFound(searched));
    }

    /// <summary>
    /// The version of the runtime package folder holding the file Cake resolves for <paramref name="executable"/>, of any
    /// RID; null when Cake resolves nothing, something outside the tools folder, or a folder without a version.
    /// </summary>
    private ToolFolderVersion? RegisteredVersion(string executable)
    {
        if (resolveRegistered(executable) is not { Length: > 0 } registered)
        {
            return null;
        }

        var prefix = Path.GetFullPath(toolsDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(registered);
        if (!full.StartsWith(prefix, PathComparison))
        {
            return null;
        }

        var folder = full[prefix.Length..].Split(Path.DirectorySeparatorChar)[0];
        if (!folder.StartsWith(PackagePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // RIDs contain no dots, so the first dot after the prefix starts the version.
        var dot = folder.IndexOf('.', PackagePrefix.Length);
        return dot < 0 ? null : ToolFolderVersion.Parse(folder, folder[..dot]);
    }

    /// <summary>The package's folders, highest version first.</summary>
    private IEnumerable<(string Path, ToolFolderVersion Version)> PackageFolders(string packageId)
    {
        if (!Directory.Exists(toolsDirectory))
        {
            return [];
        }

        return Directory.EnumerateDirectories(toolsDirectory)
            .Select(path => (Path: path, Version: ToolFolderVersion.Parse(Path.GetFileName(path), packageId)))
            .Where(folder => folder.Version is not null)
            .Select(folder => (folder.Path, Version: folder.Version!))
            .OrderByDescending(folder => folder.Version)
            .ToArray();
    }

    private static string Describe(ToolFolderVersion version) => version.Text.Length > 0 ? version.Text : "(no version)";

    /// <summary>NuGet extraction drops the execute bit; a read-only tools folder leaves the start failure to report it.</summary>
    private static void EnsureExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            var mode = File.GetUnixFileMode(path);
            if ((mode & UnixFileMode.UserExecute) == 0)
            {
                File.SetUnixFileMode(path, mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}

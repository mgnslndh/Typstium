using Typstium;

namespace Cake.Typstium.Executable;

/// <summary>
/// Finds Typst where <c>#tool nuget:?package=Typstium.Runtime.&lt;rid&gt;</c> installs it (spec cake.md §5): in Cake's
/// tools folder, for each candidate RID in order, the runtime package's binary. Several RIDs can be installed at once,
/// so it never takes the file from Cake's tool registry, which resolves by file name only (D6). From the file Cake
/// resolves for the name, when it lies in a runtime package of any RID, it takes two things (D20): the tools folder it
/// was installed into, searched before the configured one because a script may have changed the working directory
/// since <c>#tool</c> ran; and the package's version, which wins over higher stale ones. Otherwise the highest version
/// wins. Several installed versions of one package are reported, because <c>#tool</c> never removes old ones.
/// </summary>
/// <param name="toolsDirectory">Cake's tools folder as configured, against the current working directory.</param>
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
        var registered = RegisteredPackage(executable);
        var pinned = registered?.Version;
        // The folder #tool really installed into comes first: the configured one follows Cake's current working
        // directory, which a script may have changed since #tool ran.
        string[] roots = registered is { } package && !SamePath(package.ToolsDirectory, toolsDirectory)
            ? [package.ToolsDirectory, toolsDirectory]
            : [toolsDirectory];
        var searched = new List<string>();
        foreach (var rid in candidateRuntimeIdentifiers)
        {
            var packageId = PackagePrefix + rid;
            var inPackage = Path.Combine("tools", "typst", rid, executable);
            foreach (var root in roots)
            {
                searched.Add(Path.Combine(root, packageId + "[.<version>]", inPackage));
                var installed = PackageFolders(root, packageId)
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
                    warn($"{root} has several versions of {packageId}: {versions}; using {Describe(chosen.Version)}, " +
                         (byPin ? "registered by #tool" : "the highest") +
                         ". #tool doesn't remove old versions; delete the folders you don't need.");
                }

                EnsureExecutable(chosen.Executable);
                return new ValueTask<TypstLocatorResult>(TypstLocatorResult.Found(chosen.Executable));
            }
        }

        return new ValueTask<TypstLocatorResult>(TypstLocatorResult.NotFound(searched));
    }

    /// <summary>
    /// Where the file Cake resolves for <paramref name="executable"/> was installed, when it sits in a runtime package of
    /// any RID (<c>&lt;tools&gt;/Typstium.Runtime.&lt;rid&gt;[.&lt;version&gt;]/tools/typst/&lt;rid&gt;/typst[.exe]</c>): that tools
    /// folder and the package's version (null for a folder without one). Null when Cake resolves nothing or a file
    /// elsewhere, such as an installed Typst on PATH.
    /// </summary>
    private (string ToolsDirectory, ToolFolderVersion? Version)? RegisteredPackage(string executable)
    {
        if (resolveRegistered(executable) is not { Length: > 0 } registered)
        {
            return null;
        }

        var ridFolder = Directory.GetParent(Path.GetFullPath(registered));
        var typstFolder = ridFolder?.Parent;
        var toolsFolder = typstFolder?.Parent;
        var packageFolder = toolsFolder?.Parent;
        if (packageFolder?.Parent is not { } root
            || !string.Equals(typstFolder!.Name, "typst", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(toolsFolder!.Name, "tools", StringComparison.OrdinalIgnoreCase)
            || !packageFolder.Name.StartsWith(PackagePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // RIDs contain no dots, so the first dot after the prefix starts the version.
        var dot = packageFolder.Name.IndexOf('.', PackagePrefix.Length);
        var version = dot < 0 ? null : ToolFolderVersion.Parse(packageFolder.Name, packageFolder.Name[..dot]);
        return (root.FullName, version);
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar), PathComparison);

    /// <summary>The package's folders in <paramref name="root"/>, highest version first.</summary>
    private static IEnumerable<(string Path, ToolFolderVersion Version)> PackageFolders(string root, string packageId)
    {
        if (!Directory.Exists(root))
        {
            return [];
        }

        return Directory.EnumerateDirectories(root)
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

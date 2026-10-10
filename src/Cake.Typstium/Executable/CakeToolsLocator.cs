using Typstium;

namespace Cake.Typstium.Executable;

/// <summary>
/// Finds Typst where <c>#tool nuget:?package=Typstium.Runtime.&lt;rid&gt;</c> installs it (spec cake.md §5): in Cake's
/// tools folder, for each candidate RID in order, the highest installed version of that runtime package. Several RIDs
/// can be installed at once, so it never relies on Cake's tool registry, which resolves by file name only (D6).
/// </summary>
internal sealed class CakeToolsLocator(string toolsDirectory, IReadOnlyList<string> candidateRuntimeIdentifiers) : ITypstExecutableLocator
{
    private const string PackagePrefix = "Typstium.Runtime.";

    public string Name => "CakeTools";

    public ValueTask<TypstLocatorResult> LocateAsync(CancellationToken cancellationToken)
    {
        var executable = OperatingSystem.IsWindows() ? "typst.exe" : "typst";
        var searched = new List<string>();
        foreach (var rid in candidateRuntimeIdentifiers)
        {
            var packageId = PackagePrefix + rid;
            var inPackage = Path.Combine("tools", "typst", rid, executable);
            searched.Add(Path.Combine(toolsDirectory, packageId + "[.<version>]", inPackage));
            foreach (var folder in PackageFolders(packageId))
            {
                var candidate = Path.Combine(folder, inPackage);
                if (File.Exists(candidate))
                {
                    EnsureExecutable(candidate);
                    return new ValueTask<TypstLocatorResult>(TypstLocatorResult.Found(candidate));
                }
            }
        }

        return new ValueTask<TypstLocatorResult>(TypstLocatorResult.NotFound(searched));
    }

    /// <summary>The package's folders, highest version first.</summary>
    private IEnumerable<string> PackageFolders(string packageId)
    {
        if (!Directory.Exists(toolsDirectory))
        {
            return [];
        }

        return Directory.EnumerateDirectories(toolsDirectory)
            .Select(path => (Path: path, Version: ToolFolderVersion.Parse(Path.GetFileName(path), packageId)))
            .Where(folder => folder.Version is not null)
            .OrderByDescending(folder => folder.Version)
            .Select(folder => folder.Path)
            .ToArray();
    }

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

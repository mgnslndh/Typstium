using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Typstium.Diagnostics;
using Typstium.Utilities;

namespace Typstium.Workspace;

/// <summary>A file source compiled where it is. Only paged output needs a temporary folder.</summary>
internal sealed class InPlaceWorkspace : IWorkspace
{
    private readonly string? _temporaryDirectory;

    private InPlaceWorkspace(string? temporaryDirectory, WorkspaceLayout layout)
    {
        _temporaryDirectory = temporaryDirectory;
        Layout = layout;
    }

    public WorkspaceLayout Layout { get; }

    public static InPlaceWorkspace Create(TypstSource.FileSource source, bool needsPagedOutput, string tempRoot)
    {
        var mainFile = Path.GetFullPath(source.Path);
        if (!File.Exists(mainFile))
        {
            throw new ArgumentException($"The Typst file '{mainFile}' does not exist.", "request");
        }

        var rootDirectory = source.Root is null ? Path.GetDirectoryName(mainFile)! : Path.GetFullPath(source.Root);
        if (!Directory.Exists(rootDirectory))
        {
            throw new ArgumentException($"The project root '{rootDirectory}' does not exist.", "request");
        }

        var root = PathCanonicalizer.CanonicalizeDirectory(rootDirectory);
        var main = Path.Combine(PathCanonicalizer.CanonicalizeDirectory(Path.GetDirectoryName(mainFile)!), Path.GetFileName(mainFile));
        var comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        // A filesystem root already ends with a separator.
        var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ? root : root + Path.DirectorySeparatorChar;
        if (!main.StartsWith(rootPrefix, comparison))
        {
            throw new ArgumentException($"The file '{mainFile}' is outside the project root '{rootDirectory}'.", "request");
        }

        string? temporaryDirectory = null;
        string? output = null;
        if (needsPagedOutput)
        {
            (temporaryDirectory, _, output) = SandboxWorkspace.CreateDirectories(tempRoot, needsPagedOutput: true);
        }

        var mapping = new SourceMappingContext(root, IsSandbox: false, ImmutableHashSet<TypstPath>.Empty);
        return new InPlaceWorkspace(temporaryDirectory, new WorkspaceLayout(root, main, ReadOnlyMemory<byte>.Empty, output, mapping));
    }

    public ValueTask DisposeAsync() =>
        _temporaryDirectory is null ? default : new ValueTask(SandboxWorkspace.DeleteDirectoryAsync(_temporaryDirectory));
}

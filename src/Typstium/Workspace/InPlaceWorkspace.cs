using System.Collections.Immutable;
using Typstium.Diagnostics;
using Typstium.Utilities;

namespace Typstium.Workspace;

/// <summary>
/// A file source compiled where it is: the caller's folder is only passed to Typst as --root and is never
/// written to. Only paged output needs a scratch folder, under the temp root, for the page files.
/// </summary>
internal sealed class InPlaceWorkspace : IWorkspace
{
    private readonly ScratchDirectory? _scratch;

    private InPlaceWorkspace(ScratchDirectory? scratch, WorkspaceLayout layout)
    {
        _scratch = scratch;
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
        var comparison = PathComparison.For(root);
        // A filesystem root already ends with a separator.
        var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ? root : root + Path.DirectorySeparatorChar;
        if (!main.StartsWith(rootPrefix, comparison))
        {
            throw new ArgumentException($"The file '{mainFile}' is outside the project root '{rootDirectory}'.", "request");
        }

        var scratch = needsPagedOutput ? ScratchDirectory.Create(tempRoot) : null;
        var output = scratch?.CreateSubdirectory("out");
        var mapping = new SourceMappingContext(root, IsSandbox: false, ImmutableHashSet<TypstPath>.Empty);
        return new InPlaceWorkspace(scratch, new WorkspaceLayout(root, main, ReadOnlyMemory<byte>.Empty, output, mapping));
    }

    public ValueTask DisposeAsync() => _scratch?.DisposeAsync() ?? default;
}

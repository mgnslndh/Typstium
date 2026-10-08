using System.Collections.Immutable;
using System.Text;
using Typstium.Diagnostics;
using Typstium.Utilities;

namespace Typstium.Workspace;

/// <summary>A fresh folder per compile holding only the template bundle and virtual files.</summary>
internal sealed class SandboxWorkspace : IWorkspace
{
    internal const string Prefix = "ws-";
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _workspaceDirectory;

    private SandboxWorkspace(string workspaceDirectory, WorkspaceLayout layout)
    {
        _workspaceDirectory = workspaceDirectory;
        Layout = layout;
    }

    public WorkspaceLayout Layout { get; }

    public static async Task<SandboxWorkspace> CreateForTemplateAsync(
        TypstTemplate template, ImmutableArray<TypstVirtualFile> files, bool needsPagedOutput, string tempRoot, CancellationToken cancellationToken)
    {
        var (directory, root, output) = CreateDirectories(tempRoot, needsPagedOutput);
        try
        {
            foreach (var file in template.Files)
            {
                var source = await file.OpenReadAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    using var target = OpenForWrite(Resolve(root, file.Path));
                    await source.CopyToAsync(target, 81920, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    source.Dispose();
                }
            }

            await WriteVirtualFilesAsync(root, files, cancellationToken).ConfigureAwait(false);
            var layout = new WorkspaceLayout(root, Resolve(root, template.MainFile), ReadOnlyMemory<byte>.Empty, output, Mapping(root, files));
            return new SandboxWorkspace(directory, layout);
        }
        catch
        {
            await DeleteDirectoryAsync(directory).ConfigureAwait(false);
            throw;
        }
    }

    public static async Task<SandboxWorkspace> CreateForMarkupAsync(
        string markup, ImmutableArray<TypstVirtualFile> files, bool needsPagedOutput, string tempRoot, CancellationToken cancellationToken)
    {
        var (directory, root, output) = CreateDirectories(tempRoot, needsPagedOutput);
        try
        {
            await WriteVirtualFilesAsync(root, files, cancellationToken).ConfigureAwait(false);
            var layout = new WorkspaceLayout(root, WorkspaceLayout.StdinInput, Utf8NoBom.GetBytes(markup), output, Mapping(root, files));
            return new SandboxWorkspace(directory, layout);
        }
        catch
        {
            await DeleteDirectoryAsync(directory).ConfigureAwait(false);
            throw;
        }
    }

    public ValueTask DisposeAsync() => new(DeleteDirectoryAsync(_workspaceDirectory));

    internal static string Resolve(string root, TypstPath path) =>
        Path.Combine(new[] { root }.Concat(path.Segments).ToArray());

    /// <summary>Deletes a folder, retrying briefly: Windows can hold handles for a moment after a process exits.</summary>
    internal static async Task DeleteDirectoryAsync(string path)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }

                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(50).ConfigureAwait(false);
            }
        }

        // Left for OrphanedWorkspaceCleaner.
    }

    internal static (string Directory, string Root, string? Output) CreateDirectories(string tempRoot, bool needsPagedOutput)
    {
        var directory = Path.Combine(tempRoot, Prefix + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(directory, "root");
        System.IO.Directory.CreateDirectory(root);
        string? output = null;
        if (needsPagedOutput)
        {
            output = Path.Combine(directory, "out");
            System.IO.Directory.CreateDirectory(output);
        }

        return (directory, root, output);
    }

    private static async Task WriteVirtualFilesAsync(string root, ImmutableArray<TypstVirtualFile> files, CancellationToken cancellationToken)
    {
        foreach (var file in files)
        {
            var segment = ByteMemory.AsSegment(file.Content);
            using var target = OpenForWrite(Resolve(root, file.Path));
            await target.WriteAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken).ConfigureAwait(false);
        }
    }

    private static FileStream OpenForWrite(string path)
    {
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
    }

    private static SourceMappingContext Mapping(string root, ImmutableArray<TypstVirtualFile> files) =>
        new(root, IsSandbox: true, files.Select(file => file.Path).ToImmutableHashSet());
}

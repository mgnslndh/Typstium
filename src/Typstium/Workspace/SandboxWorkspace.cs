using System.Collections.Immutable;
using System.Text;
using Typstium.Diagnostics;
using Typstium.Utilities;

namespace Typstium.Workspace;

/// <summary>
/// A fresh scratch folder per compile whose <c>root/</c> holds only the template bundle and virtual files,
/// so the template can't read anything else.
/// </summary>
internal sealed class SandboxWorkspace : IWorkspace
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly ScratchDirectory _scratch;

    private SandboxWorkspace(ScratchDirectory scratch, WorkspaceLayout layout)
    {
        _scratch = scratch;
        Layout = layout;
    }

    public WorkspaceLayout Layout { get; }

    public static async Task<SandboxWorkspace> CreateForTemplateAsync(
        TypstTemplate template, ImmutableArray<TypstVirtualFile> files, bool needsPagedOutput, string tempRoot, CancellationToken cancellationToken)
    {
        var (scratch, root, output) = CreateScratch(tempRoot, needsPagedOutput);
        try
        {
            foreach (var file in template.Files)
            {
                var source = await file.OpenReadAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    using var target = OpenForWrite(root, file.Path, replace: false);
                    await source.CopyToAsync(target, 81920, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    source.Dispose();
                }
            }

            var templatePaths = new HashSet<string>(template.Files.Select(file => file.Path.Value), StringComparer.OrdinalIgnoreCase);
            await WriteVirtualFilesAsync(root, files, templatePaths, cancellationToken).ConfigureAwait(false);
            var layout = new WorkspaceLayout(root, Resolve(root, template.MainFile), ReadOnlyMemory<byte>.Empty, output, Mapping(root, files));
            return new SandboxWorkspace(scratch, layout);
        }
        catch
        {
            await scratch.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public static async Task<SandboxWorkspace> CreateForMarkupAsync(
        string markup, ImmutableArray<TypstVirtualFile> files, bool needsPagedOutput, string tempRoot, CancellationToken cancellationToken)
    {
        var (scratch, root, output) = CreateScratch(tempRoot, needsPagedOutput);
        try
        {
            await WriteVirtualFilesAsync(root, files, templatePaths: null, cancellationToken).ConfigureAwait(false);
            var layout = new WorkspaceLayout(root, WorkspaceLayout.StdinInput, Utf8NoBom.GetBytes(markup), output, Mapping(root, files));
            return new SandboxWorkspace(scratch, layout);
        }
        catch
        {
            await scratch.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public ValueTask DisposeAsync() => _scratch.DisposeAsync();

    internal static string Resolve(string root, TypstPath path) =>
        Path.Combine(new[] { root }.Concat(path.Segments).ToArray());

    private static (ScratchDirectory Scratch, string Root, string? Output) CreateScratch(string tempRoot, bool needsPagedOutput)
    {
        var scratch = ScratchDirectory.Create(tempRoot);
        return (scratch, scratch.CreateSubdirectory("root"), needsPagedOutput ? scratch.CreateSubdirectory("out") : null);
    }

    /// <summary>Writes the virtual files; one may replace a template file at the same path (<c>templatePaths</c>, null for markup).</summary>
    private static async Task WriteVirtualFilesAsync(
        string root, ImmutableArray<TypstVirtualFile> files, HashSet<string>? templatePaths, CancellationToken cancellationToken)
    {
        foreach (var file in files)
        {
            var segment = ByteMemory.AsSegment(file.Content);
            var replace = templatePaths?.Contains(file.Path.Value) == true;
            using var target = OpenForWrite(root, file.Path, replace);
            await target.WriteAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Opens a new file for writing. Every file of a fresh sandbox must be new, so an existing file means two paths
    /// resolved to the same file: an alias the name rules don't list, such as an NTFS 8.3 short name
    /// (<c>DATAFI~1.JSO</c> for <c>datafile.json</c>). That fails instead of silently overwriting; only a virtual
    /// file replacing a template file at the same path (<paramref name="replace"/>) overwrites.
    /// </summary>
    private static FileStream OpenForWrite(string root, TypstPath path, bool replace)
    {
        var fullPath = Resolve(root, path);
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        try
        {
            return new FileStream(fullPath, replace ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        }
        catch (IOException exception) when (!replace && File.Exists(fullPath))
        {
            throw new ArgumentException(
                $"'{path}' refers to a file already written to the sandbox; on this file system it is another name for an earlier file.",
                "request",
                exception);
        }
    }

    private static SourceMappingContext Mapping(string root, ImmutableArray<TypstVirtualFile> files) =>
        new(root, IsSandbox: true, files.Select(file => file.Path).ToImmutableHashSet());
}

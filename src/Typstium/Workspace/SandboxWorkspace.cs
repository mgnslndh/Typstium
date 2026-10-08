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
            await WriteVirtualFilesAsync(root, files, cancellationToken).ConfigureAwait(false);
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

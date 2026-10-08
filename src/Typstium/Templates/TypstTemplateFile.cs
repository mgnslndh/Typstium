namespace Typstium;

/// <summary>One file of a template bundle. Derive from it to stream content from a database or blob store.</summary>
public abstract class TypstTemplateFile
{
    /// <summary>Initializes the file's path.</summary>
    /// <param name="path">The path relative to the bundle root.</param>
    protected TypstTemplateFile(TypstPath path) => Path = path ?? throw new ArgumentNullException(nameof(path));

    /// <summary>The path relative to the bundle root.</summary>
    public TypstPath Path { get; }

    /// <summary>Opens the content for reading. Each call returns a new stream, which the caller disposes.</summary>
    public abstract ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken);

    /// <summary>A file held in memory. The memory is not copied, so don't change it while the template is in use.</summary>
    public static TypstTemplateFile FromBytes(TypstPath path, ReadOnlyMemory<byte> content) => new MemoryTemplateFile(path, content);

    /// <summary>A file on disk, read each time it is opened.</summary>
    /// <param name="path">The path relative to the bundle root.</param>
    /// <param name="physicalPath">The file on disk; resolved against the current directory now.</param>
    public static TypstTemplateFile FromFile(TypstPath path, string physicalPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(physicalPath);
        return new PhysicalTemplateFile(path, System.IO.Path.GetFullPath(physicalPath));
    }

    private sealed class MemoryTemplateFile(TypstPath path, ReadOnlyMemory<byte> content) : TypstTemplateFile(path)
    {
        public override ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken) =>
            new(new MemoryStream(content.ToArray(), writable: false));
    }

    private sealed class PhysicalTemplateFile(TypstPath path, string physicalPath) : TypstTemplateFile(path)
    {
        public override ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken) =>
            new(new FileStream(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true));
    }
}

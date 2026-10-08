namespace Typstium;

/// <summary>One file of a template bundle. Derive from it to stream content from a database or blob store.</summary>
public abstract class TypstTemplateFile
{
    protected TypstTemplateFile(TypstPath path) => Path = path ?? throw new ArgumentNullException(nameof(path));

    public TypstPath Path { get; }

    public abstract ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken);

    public static TypstTemplateFile FromBytes(TypstPath path, ReadOnlyMemory<byte> content) => new MemoryTemplateFile(path, content);

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

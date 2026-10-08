namespace Typstium.Testing.Fixtures;

/// <summary>A unique folder under the system temp path, deleted on dispose.</summary>
public sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory(string? nameHint = null)
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "typstium-tests", (nameHint ?? "dir") + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string CreateFile(string relativePath, string content)
    {
        var full = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

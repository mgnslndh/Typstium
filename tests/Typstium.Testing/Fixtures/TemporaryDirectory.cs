namespace Typstium.Testing.Fixtures;

/// <summary>
/// A unique folder under the system temp path, deleted on dispose. Paths passed to its members are relative to
/// the folder (forward slashes work on every OS) and may not leave it, so a test can never write, or have
/// <see cref="Dispose"/> delete, anything outside.
/// </summary>
public sealed class TemporaryDirectory : IDisposable
{
    private readonly string _prefix;

    /// <summary>Creates the folder.</summary>
    /// <param name="nameHint">A readable prefix for the folder name, e.g. <c>compiler</c>.</param>
    public TemporaryDirectory(string? nameHint = null)
    {
        Path = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "typstium-tests", (nameHint ?? "dir") + "-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(Path);
        _prefix = Path + System.IO.Path.DirectorySeparatorChar;
    }

    /// <summary>The folder's full path.</summary>
    public string Path { get; }

    /// <summary>
    /// The full path of <paramref name="paths"/> inside the folder, like <see cref="System.IO.Path.Combine(string[])"/>
    /// with <see cref="Path"/> first. Creates nothing, for paths a test needs to be missing or the code under test creates.
    /// </summary>
    /// <exception cref="ArgumentException">The result is outside the folder (a rooted path or <c>..</c>).</exception>
    public string Combine(params string[] paths)
    {
        if (paths is null || paths.Length == 0)
        {
            throw new ArgumentException("At least one path segment is required.", nameof(paths));
        }

        foreach (var segment in paths)
        {
            if (segment is null || System.IO.Path.IsPathRooted(segment))
            {
                throw new ArgumentException($"'{segment}' must be a relative path inside the temporary directory.", nameof(paths));
            }
        }

        var full = System.IO.Path.GetFullPath(System.IO.Path.Combine([Path, .. paths]));
        if (!full.StartsWith(_prefix, StringComparison.Ordinal))
        {
            throw new ArgumentException($"'{string.Join("/", paths)}' leaves the temporary directory.", nameof(paths));
        }

        return full;
    }

    /// <summary>Creates a folder (and its parents) inside the folder.</summary>
    /// <returns>The folder's full path.</returns>
    /// <exception cref="ArgumentException"><paramref name="relativePath"/> leaves the folder.</exception>
    public string CreateDirectory(string relativePath)
    {
        var full = Combine(relativePath);
        Directory.CreateDirectory(full);
        return full;
    }

    /// <summary>Writes a UTF-8 text file (creating its folders) inside the folder.</summary>
    /// <returns>The file's full path.</returns>
    /// <exception cref="ArgumentException"><paramref name="relativePath"/> leaves the folder.</exception>
    public string CreateFile(string relativePath, string content)
    {
        var full = Combine(relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }

    /// <summary>Deletes the folder and everything in it; a folder still in use is left behind.</summary>
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

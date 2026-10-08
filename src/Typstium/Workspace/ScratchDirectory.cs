namespace Typstium.Workspace;

/// <summary>
/// A <c>ws-&lt;guid&gt;</c> folder under the temp root that Typstium owns for one compile: the sandbox's <c>root/</c>
/// and the paged output's <c>out/</c> live in it. Always a fresh folder of its own, never one the caller gave us,
/// so deleting it can only remove what Typstium wrote. <see cref="OrphanedWorkspaceCleaner"/> finds the ones a
/// hard kill left behind by their exact name (<see cref="IsScratchName"/>), so it never touches a folder of the
/// host's that merely starts with <see cref="Prefix"/>.
/// </summary>
internal sealed class ScratchDirectory : IAsyncDisposable
{
    internal const string Prefix = "ws-";
    private const int IdLength = 32; // Guid.ToString("N")

    private ScratchDirectory(string path) => Path = path;

    /// <summary>The folder's full path.</summary>
    public string Path { get; }

    /// <summary>Creates a fresh folder under <paramref name="tempRoot"/>.</summary>
    public static ScratchDirectory Create(string tempRoot)
    {
        var path = System.IO.Path.Combine(tempRoot, NewName());
        Directory.CreateDirectory(path);
        return new ScratchDirectory(path);
    }

    /// <summary>A new folder name: <see cref="Prefix"/> followed by 32 lowercase hex digits.</summary>
    internal static string NewName() => Prefix + Guid.NewGuid().ToString("N");

    /// <summary>Whether <paramref name="name"/> is exactly a name <see cref="NewName"/> produces.</summary>
    internal static bool IsScratchName(string name)
    {
        if (name.Length != Prefix.Length + IdLength || !name.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        for (var i = Prefix.Length; i < name.Length; i++)
        {
            if (name[i] is not ((>= '0' and <= '9') or (>= 'a' and <= 'f')))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Creates a subfolder, such as <c>root</c> or <c>out</c>, and returns its full path.</summary>
    public string CreateSubdirectory(string name)
    {
        var path = System.IO.Path.Combine(Path, name);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// Deletes the folder, retrying briefly: Windows can hold handles for a moment after a process exits.
    /// A folder that still can't be deleted is left for <see cref="OrphanedWorkspaceCleaner"/>.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }

                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(50).ConfigureAwait(false);
            }
        }
    }
}

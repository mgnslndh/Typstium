namespace Typstium.Workspace;

/// <summary>
/// A <c>ws-&lt;guid&gt;</c> folder under the temp root that Typstium owns for one compile: the sandbox's <c>root/</c>
/// and the paged output's <c>out/</c> live in it. Always a fresh folder of its own, never one the caller gave us,
/// so deleting it can only remove what Typstium wrote. <see cref="OrphanedWorkspaceCleaner"/> finds the ones a
/// hard kill left behind by their exact name (<see cref="IsScratchName"/>), so it never touches a folder of the
/// host's that merely starts with <see cref="Prefix"/>.
/// </summary>
/// <remarks>
/// <b>Lease.</b> While the folder is in use, its <c>.lease</c> file is held open with <see cref="FileShare.None"/>:
/// a sharing lock on Windows and an advisory <c>flock</c> on Unix, honored by every .NET process. The operating system
/// releases it when the owner dies, even on a hard kill, so <see cref="IsLeaseHeld"/> tells a live folder of another
/// compiler sharing the temp root from an abandoned one, whatever its age. (Setting
/// <c>DOTNET_SYSTEM_IO_DISABLEFILELOCKING=1</c> turns the Unix lock off.)
/// </remarks>
internal sealed class ScratchDirectory : IAsyncDisposable
{
    internal const string Prefix = "ws-";
    internal const string LeaseFileName = ".lease";
    private const int IdLength = 32; // Guid.ToString("N")

    private FileStream? _lease;

    private ScratchDirectory(string path, FileStream lease)
    {
        Path = path;
        _lease = lease;
    }

    /// <summary>The folder's full path.</summary>
    public string Path { get; }

    /// <summary>Creates a fresh folder under <paramref name="tempRoot"/>.</summary>
    public static ScratchDirectory Create(string tempRoot)
    {
        var path = System.IO.Path.Combine(tempRoot, NewName());
        Directory.CreateDirectory(path);
        var lease = new FileStream(System.IO.Path.Combine(path, LeaseFileName), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        return new ScratchDirectory(path, lease);
    }

    /// <summary>
    /// Whether another live owner holds the folder's lease. A folder without a lease file (its owner died before
    /// creating it) is not held. Anything that stops the lease opening counts as held: deleting a live folder is the
    /// mistake to avoid, and an unopened orphan is retried on a later start.
    /// </summary>
    internal static bool IsLeaseHeld(string scratchPath)
    {
        var leasePath = System.IO.Path.Combine(scratchPath, LeaseFileName);
        if (!File.Exists(leasePath))
        {
            return false;
        }

        try
        {
            using (new FileStream(leasePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                return false;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    /// <summary>Test seam: drops the lease as a dead owner would, leaving the folder behind.</summary>
    internal void AbandonForTesting()
    {
        _lease?.Dispose();
        _lease = null;
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
        _lease?.Dispose();
        _lease = null;
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

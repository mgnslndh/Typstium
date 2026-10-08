namespace Typstium.Workspace;

/// <summary>Removes scratch folders left behind when a host was killed hard: old enough, and with no live lease.</summary>
internal static class OrphanedWorkspaceCleaner
{
    /// <returns>The number of folders deleted.</returns>
    public static int Clean(string tempRoot, TimeSpan age, DateTime utcNow)
    {
        if (!Directory.Exists(tempRoot))
        {
            return 0;
        }

        var deleted = 0;
        foreach (var directory in new DirectoryInfo(tempRoot).EnumerateDirectories(ScratchDirectory.Prefix + "*"))
        {
            // The prefix pattern is only a pre-filter: a host folder such as "ws-reports" must never match. A held
            // lease means a live compile, possibly another compiler's sharing this temp root, however old it is.
            if (!ScratchDirectory.IsScratchName(directory.Name)
                || utcNow - directory.LastWriteTimeUtc < age
                || ScratchDirectory.IsLeaseHeld(directory.FullName))
            {
                continue;
            }

            try
            {
                directory.Delete(recursive: true);
                deleted++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // In use or locked; try again on a later start.
            }
        }

        return deleted;
    }
}

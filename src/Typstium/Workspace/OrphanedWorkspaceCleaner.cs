namespace Typstium.Workspace;

/// <summary>Removes scratch folders left behind when a host was killed hard.</summary>
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
            if (utcNow - directory.LastWriteTimeUtc < age)
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

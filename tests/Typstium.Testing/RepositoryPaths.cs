namespace Typstium.Testing;

/// <summary>Absolute paths into the repository, found by walking up to Typstium.slnx.</summary>
public static class RepositoryPaths
{
    private static readonly Lazy<string> RootPath = new(FindRoot);

    public static string Root => RootPath.Value;

    public static string Samples => Path.Combine(Root, "samples");

    public static string Fixtures => Path.Combine(Root, "tests", "fixtures");

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Typstium.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException($"Could not find Typstium.slnx above {AppContext.BaseDirectory}.");
    }
}

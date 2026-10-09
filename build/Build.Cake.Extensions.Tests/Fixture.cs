namespace Build.Cake.Extensions.Tests;

// Files under Fixtures/, copied next to the test assembly. Golden files were written by the PowerShell scripts, some
// on Windows: reading normalizes line endings, so comparisons are about content, not the machine that wrote them.
internal static class Fixture
{
    public static string Path(params string[] parts) => System.IO.Path.Combine([AppContext.BaseDirectory, "Fixtures", .. parts]);

    public static string Read(params string[] parts) => File.ReadAllText(Path(parts)).ReplaceLineEndings("\n");
}

using System.Runtime.InteropServices;
using Typstium.Testing.Fixtures;
using Typstium.Utilities;
using Typstium.Workspace;

namespace Typstium.UnitTests.Utilities;

public class PathCanonicalizerTests
{
    [Test]
    public async Task Canonicalizing_is_idempotent()
    {
        using var temp = new TemporaryDirectory("canon");

        var once = PathCanonicalizer.CanonicalizeDirectory(temp.Path);
        var twice = PathCanonicalizer.CanonicalizeDirectory(once);

        await Assert.That(twice).IsEqualTo(once);
    }

    [Test]
    public async Task Trailing_separators_are_trimmed_from_ordinary_directories()
    {
        using var temp = new TemporaryDirectory("canon");

        var canonical = PathCanonicalizer.CanonicalizeDirectory(temp.Path + Path.DirectorySeparatorChar);

        await Assert.That(canonical.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task A_drive_root_stays_a_root_ending_with_a_separator()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Skip.Test("Drive roots exist on Windows only.");
        }

        var root = Path.GetPathRoot(Path.GetTempPath())!;

        var canonical = PathCanonicalizer.CanonicalizeDirectory(root);

        await Assert.That(canonical).IsEqualTo(root);
        await Assert.That(Path.GetPathRoot(canonical)).IsEqualTo(canonical);
    }

    [Test]
    public async Task The_unix_root_stays_the_root()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Skip.Test("The '/' root exists on Unix only.");
        }

        await Assert.That(PathCanonicalizer.CanonicalizeDirectory("/")).IsEqualTo("/");
    }

    [Test]
    public async Task A_file_directly_under_a_filesystem_root_can_use_that_root_as_project_root()
    {
        using var temp = new TemporaryDirectory("rootcheck");
        var root = Path.GetPathRoot(temp.Path)!;
        var file = Path.Combine(root, "typstium-rootcheck-" + Guid.NewGuid().ToString("N") + ".typ");
        try
        {
            File.WriteAllText(file, "= Hi");
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            Skip.Test("The filesystem root is not writable here.");
        }

        try
        {
            var source = (TypstSource.FileSource)TypstSource.File(file, root);
            var workspace = InPlaceWorkspace.Create(source, needsPagedOutput: false, temp.Path);

            await Assert.That(workspace.Layout.RootDirectory).IsEqualTo(PathCanonicalizer.CanonicalizeDirectory(root));
            await workspace.DisposeAsync();
        }
        finally
        {
            File.Delete(file);
        }
    }
}

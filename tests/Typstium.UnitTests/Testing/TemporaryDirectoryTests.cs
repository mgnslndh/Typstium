using Typstium.Testing.Fixtures;

namespace Typstium.UnitTests.Testing;

public class TemporaryDirectoryTests
{
    [Test]
    public async Task Combine_builds_paths_inside_the_folder_without_creating_them()
    {
        using var temp = new TemporaryDirectory("combine");

        var path = temp.Combine("Åsa Ström", "nested/file.typ");

        await Assert.That(path).IsEqualTo(Path.GetFullPath(Path.Combine(temp.Path, "Åsa Ström", "nested", "file.typ")));
        await Assert.That(Directory.Exists(Path.GetDirectoryName(path)!)).IsFalse();
    }

    [Test]
    public async Task CreateDirectory_and_CreateFile_create_nested_paths()
    {
        using var temp = new TemporaryDirectory("create");

        var directory = temp.CreateDirectory("a/b");
        var file = temp.CreateFile("c/d/e.txt", "content");

        await Assert.That(Directory.Exists(directory)).IsTrue();
        await Assert.That(File.ReadAllText(file)).IsEqualTo("content");
    }

    [Test]
    [Arguments("..")]
    [Arguments("../sibling")]
    [Arguments("a/../../escape")]
    public async Task Paths_may_not_leave_the_folder(string relativePath)
    {
        using var temp = new TemporaryDirectory("guard");

        await Assert.That(Assert.Throws<ArgumentException>(() => temp.Combine(relativePath))).IsNotNull();
        await Assert.That(Assert.Throws<ArgumentException>(() => temp.CreateDirectory(relativePath))).IsNotNull();
        await Assert.That(Assert.Throws<ArgumentException>(() => temp.CreateFile(relativePath, "x"))).IsNotNull();
    }

    [Test]
    public async Task A_sibling_with_the_same_name_prefix_is_outside_the_folder()
    {
        using var temp = new TemporaryDirectory("prefix");
        var sibling = "../" + Path.GetFileName(temp.Path) + "-other";

        await Assert.That(Assert.Throws<ArgumentException>(() => temp.Combine(sibling))).IsNotNull();
    }

    [Test]
    public async Task Rooted_paths_are_rejected()
    {
        using var temp = new TemporaryDirectory("rooted");

        await Assert.That(Assert.Throws<ArgumentException>(() => temp.Combine(Path.GetTempPath()))).IsNotNull();
        await Assert.That(Assert.Throws<ArgumentException>(() => temp.Combine("/etc"))).IsNotNull();
    }

    [Test]
    public async Task Dispose_deletes_the_folder()
    {
        var temp = new TemporaryDirectory("dispose");
        temp.CreateFile("a/b.txt", "x");

        temp.Dispose();

        await Assert.That(Directory.Exists(temp.Path)).IsFalse();
    }
}

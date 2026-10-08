using Typstium.Testing.Fixtures;
using Typstium.Workspace;

namespace Typstium.UnitTests.Workspace;

public class ScratchDirectoryTests
{
    [Test]
    public async Task Each_scratch_directory_is_a_fresh_folder_named_for_the_cleaner()
    {
        using var temp = new TemporaryDirectory("scratch");

        await using var first = ScratchDirectory.Create(temp.Path);
        await using var second = ScratchDirectory.Create(temp.Path);

        await Assert.That(first.Path).IsNotEqualTo(second.Path);
        await Assert.That(Path.GetDirectoryName(first.Path)).IsEqualTo(temp.Path);
        await Assert.That(Path.GetFileName(first.Path)).StartsWith(ScratchDirectory.Prefix);
        await Assert.That(Directory.Exists(first.Path)).IsTrue();
    }

    [Test]
    public async Task Dispose_deletes_the_folder_and_everything_in_it()
    {
        using var temp = new TemporaryDirectory("scratch");
        var scratch = ScratchDirectory.Create(temp.Path);
        var output = scratch.CreateSubdirectory("out");
        File.WriteAllText(Path.Combine(output, "page-1.png"), "x");

        await scratch.DisposeAsync();

        await Assert.That(Directory.Exists(scratch.Path)).IsFalse();
        await Assert.That(Directory.Exists(temp.Path)).IsTrue();
    }

    [Test]
    public async Task Disposing_an_already_deleted_folder_is_harmless()
    {
        using var temp = new TemporaryDirectory("scratch");
        var scratch = ScratchDirectory.Create(temp.Path);
        Directory.Delete(scratch.Path);

        await scratch.DisposeAsync();
        await scratch.DisposeAsync();

        await Assert.That(Directory.Exists(scratch.Path)).IsFalse();
    }

    [Test]
    [Arguments("ws-0123456789abcdef0123456789abcdef", true)]
    [Arguments("ws-0123456789ABCDEF0123456789ABCDEF", false)]
    [Arguments("ws-0123456789abcdef0123456789abcde", false)]
    [Arguments("ws-0123456789abcdef0123456789abcdef0", false)]
    [Arguments("ws-0123456789abcdef0123456789abcdeg", false)]
    [Arguments("ws-reports", false)]
    [Arguments("xs-0123456789abcdef0123456789abcdef", false)]
    public async Task Only_exact_scratch_names_are_recognised(string name, bool expected)
    {
        await Assert.That(ScratchDirectory.IsScratchName(name)).IsEqualTo(expected);
        await Assert.That(ScratchDirectory.IsScratchName(ScratchDirectory.NewName())).IsTrue();
    }
}

using Typstium.Testing.Fixtures;
using Typstium.Workspace;

namespace Typstium.UnitTests.Workspace;

public class InPlaceWorkspaceTests
{
    [Test]
    public async Task Root_defaults_to_the_file_folder()
    {
        using var temp = new TemporaryDirectory("inplace");
        var file = temp.CreateFile("docs/spec.typ", "= Spec");
        var workspace = InPlaceWorkspace.Create((TypstSource.FileSource)TypstSource.File(file), needsPagedOutput: false, temp.Path);

        await Assert.That(Path.GetFileName(workspace.Layout.RootDirectory)).IsEqualTo("docs");
        await Assert.That(Path.GetFileName(workspace.Layout.MainInput)).IsEqualTo("spec.typ");
        await Assert.That(workspace.Layout.Mapping.IsSandbox).IsFalse();
        await workspace.DisposeAsync();
    }

    [Test]
    public async Task Files_outside_the_explicit_root_are_rejected()
    {
        using var temp = new TemporaryDirectory("inplace");
        var file = temp.CreateFile("a/spec.typ", "= Spec");
        Directory.CreateDirectory(Path.Combine(temp.Path, "b"));
        var source = (TypstSource.FileSource)TypstSource.File(file, Path.Combine(temp.Path, "b"));

        var exception = Assert.Throws<ArgumentException>(() => InPlaceWorkspace.Create(source, false, temp.Path));
        await Assert.That(exception!.Message).Contains("outside the project root");
    }

    [Test]
    public async Task Missing_files_are_rejected()
    {
        using var temp = new TemporaryDirectory("inplace");
        var source = (TypstSource.FileSource)TypstSource.File(Path.Combine(temp.Path, "missing.typ"));
        var exception = Assert.Throws<ArgumentException>(() => InPlaceWorkspace.Create(source, false, temp.Path));
        await Assert.That(exception!.Message).Contains("does not exist");
    }

    [Test]
    public async Task Paged_output_gets_a_temporary_folder_that_is_deleted()
    {
        using var temp = new TemporaryDirectory("inplace");
        var file = temp.CreateFile("spec.typ", "= Spec");
        var workspace = InPlaceWorkspace.Create((TypstSource.FileSource)TypstSource.File(file), needsPagedOutput: true, temp.Path);
        var output = workspace.Layout.PagedOutputDirectory!;
        await Assert.That(Directory.Exists(output)).IsTrue();
        await workspace.DisposeAsync();
        await Assert.That(Directory.Exists(output)).IsFalse();
    }
}

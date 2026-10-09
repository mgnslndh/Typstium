using Build.Cake.Extensions.Coverage.Cobertura;

namespace Build.Cake.Extensions.Tests.Coverage.Cobertura;

public class CoberturaPathNormalizerTests
{
    [Test]
    [Arguments(@"D:\a\Typstium\Typstium\src\Typstium\Compilation\Compiler.cs", "src/Typstium/Compilation/Compiler.cs")]
    [Arguments("/home/runner/work/Typstium/Typstium/src/Typstium/Compilation/Compiler.cs", "src/Typstium/Compilation/Compiler.cs")]
    [Arguments("/home/runner/work/Typstium/Typstium/tests/Typstium.Testing/Fake.cs", "tests/Typstium.Testing/Fake.cs")]
    [Arguments("/work/src/old/tests/Typstium.Testing/Fake.cs", "tests/Typstium.Testing/Fake.cs")]
    [Arguments(@"C:\elsewhere\Generated.cs", "C:/elsewhere/Generated.cs")]
    public async Task Makes_paths_repo_relative_from_the_last_src_or_tests_folder(string path, string expected)
    {
        await Assert.That(CoberturaPathNormalizer.ToRepoRelativePath(path, Fixture.Layout)).IsEqualTo(expected);
    }

    [Test]
    [Arguments("/work/repo/source/P/A.cs", "source/P/A.cs")]
    [Arguments("/work/repo/spec/P/A.Tests.cs", "spec/P/A.Tests.cs")]
    [Arguments("/work/repo/src/P/A.cs", "/work/repo/src/P/A.cs")]
    public async Task Code_folders_come_from_the_layout(string path, string expected)
    {
        var layout = new SourceLayout(["source", "spec"], "source");

        await Assert.That(CoberturaPathNormalizer.ToRepoRelativePath(path, layout)).IsEqualTo(expected);
    }

    [Test]
    public async Task Rewrites_every_filename_attribute_and_nothing_else()
    {
        const string xml = """<class name="A" filename="D:\a\r\src\P\A.cs"><source>D:\a\r\src</source></class><class name="B" filename="/home/x/src/P/B.cs" />""";

        var normalized = CoberturaPathNormalizer.ToRepoRelative(xml, Fixture.Layout);

        await Assert.That(normalized).IsEqualTo("""<class name="A" filename="src/P/A.cs"><source>D:\a\r\src</source></class><class name="B" filename="src/P/B.cs" />""");
    }

    [Test]
    public async Task Normalizes_every_raw_file_into_the_target_folder()
    {
        var root = Path.Combine(Path.GetTempPath(), "normalize-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "raw");
        var target = Path.Combine(root, "relative");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "a.cobertura.xml"), """<class filename="C:\r\src\P\A.cs" />""");
        File.WriteAllText(Path.Combine(source, "ignored.txt"), "not coverage");
        try
        {
            var count = CoberturaPathNormalizer.NormalizeFiles(source, target, Fixture.Layout);

            await Assert.That(count).IsEqualTo(1);
            await Assert.That(File.ReadAllText(Path.Combine(target, "a.cobertura.xml"))).IsEqualTo("""<class filename="src/P/A.cs" />""");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task No_raw_files_is_an_error_naming_the_folder()
    {
        var empty = Path.Combine(Path.GetTempPath(), "empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(empty);
        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() => CoberturaPathNormalizer.NormalizeFiles(empty, Path.Combine(empty, "out"), Fixture.Layout));

            await Assert.That(exception.Message).IsEqualTo($"No *.cobertura.xml files in {empty}.");
        }
        finally
        {
            Directory.Delete(empty, recursive: true);
        }
    }

    [Test]
    public async Task A_missing_raw_folder_is_the_same_error()
    {
        var missing = Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid().ToString("N"));

        var exception = Assert.Throws<InvalidOperationException>(() => CoberturaPathNormalizer.NormalizeFiles(missing, Path.Combine(missing, "out"), Fixture.Layout));

        await Assert.That(exception.Message).Contains(missing);
    }
}

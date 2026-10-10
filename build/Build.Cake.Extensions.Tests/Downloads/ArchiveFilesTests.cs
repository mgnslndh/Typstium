using System.IO.Compression;
using Build.Cake.Extensions.Downloads;

namespace Build.Cake.Extensions.Tests.Downloads;

public class ArchiveFilesTests
{
    private static string Zip(TempFolder temp, params (string Path, string Content)[] entries)
    {
        var path = temp.Combine("asset.zip");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (entryPath, content) in entries)
        {
            using var writer = new StreamWriter(archive.CreateEntry(entryPath).Open());
            writer.Write(content);
        }

        return path;
    }

    [Test]
    public async Task Extracts_named_files_from_one_folder_down_in_a_tar_xz()
    {
        using var temp = new TempFolder();
        var destinations = new Dictionary<string, string> { ["typst"] = temp.Combine("out", "typst"), ["LICENSE"] = temp.Combine("out", "LICENSE") };

        ArchiveFiles.Extract(Fixture.Path("Archives", "release.tar.xz"), destinations);

        await Assert.That(File.ReadAllText(temp.Combine("out", "typst"))).IsEqualTo("#!/bin/sh\necho typst\n");
        await Assert.That(File.ReadAllText(temp.Combine("out", "LICENSE"))).IsEqualTo("license\n");
        await Assert.That(File.Exists(temp.Combine("out", "README.md"))).IsFalse();
    }

    [Test]
    public async Task Extracts_named_files_from_the_root_of_a_zip()
    {
        using var temp = new TempFolder();
        var zip = Zip(temp, ("typst.exe", "exe"), ("LICENSE", "license"));

        ArchiveFiles.Extract(zip, new Dictionary<string, string> { ["typst.exe"] = temp.Combine("typst.exe") });

        await Assert.That(File.ReadAllText(temp.Combine("typst.exe"))).IsEqualTo("exe");
    }

    [Test]
    public async Task Entries_deeper_than_one_folder_are_not_matched()
    {
        using var temp = new TempFolder();

        var exception = Assert.Throws<InvalidDataException>(() =>
            ArchiveFiles.Extract(Fixture.Path("Archives", "deep.tar.xz"), new Dictionary<string, string> { ["typst"] = temp.Combine("typst") }));

        await Assert.That(exception.Message).Contains("no entry named typst");
    }

    [Test]
    public async Task Two_matching_entries_are_an_error()
    {
        using var temp = new TempFolder();
        var zip = Zip(temp, ("a/typst.exe", "1"), ("b/typst.exe", "2"));

        var exception = Assert.Throws<InvalidDataException>(() => ArchiveFiles.Extract(zip, new Dictionary<string, string> { ["typst.exe"] = temp.Combine("typst.exe") }));

        await Assert.That(exception.Message).Contains("more than one entry named typst.exe");
    }

    [Test]
    public void Other_formats_are_not_supported()
    {
        using var temp = new TempFolder();
        File.WriteAllText(temp.Combine("asset.7z"), "");

        Assert.Throws<NotSupportedException>(() => ArchiveFiles.Extract(temp.Combine("asset.7z"), new Dictionary<string, string>()));
    }
}

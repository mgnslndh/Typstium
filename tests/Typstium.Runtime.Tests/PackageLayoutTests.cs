using System.IO.Compression;
using Typstium.Runtime.Testing;

namespace Typstium.Runtime.Tests;

public class PackageLayoutTests
{
    private static async Task<(string[] Entries, string Nuspec, ZipArchive Archive)> OpenAsync(string id)
    {
        var feed = await PackageFeed.GetFixtureAsync();
        var archive = ZipFile.OpenRead(Path.Combine(feed.Directory, $"{id}.{feed.Version}.nupkg"));
        var nuspec = archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        using var reader = new StreamReader(nuspec.Open());
        var entries = archive.Entries.Select(entry => entry.FullName)
            .Where(name => !name.StartsWith("_rels/", StringComparison.Ordinal) && !name.StartsWith("package/", StringComparison.Ordinal) && name != "[Content_Types].xml" && !name.EndsWith(".nuspec", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal).ToArray();
        return (entries, await reader.ReadToEndAsync(), archive);
    }

    [Test]
    [Arguments("linux-x64", "typst")]
    [Arguments("win-arm64", "typst.exe")]
    public async Task A_runtime_package_holds_the_binary_its_license_and_notice_and_the_props(string rid, string executable)
    {
        var (entries, nuspec, archive) = await OpenAsync("Typstium.Runtime." + rid);
        using (archive)
        {
            await Assert.That(entries).IsEquivalentTo(new[]
            {
                $"README.md",
                $"build/Typstium.Runtime.{rid}.props",
                $"buildTransitive/Typstium.Runtime.{rid}.props",
                $"tools/typst/{rid}/LICENSE",
                $"tools/typst/{rid}/NOTICE",
                $"tools/typst/{rid}/{executable}",
            }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(nuspec).Contains("<license type=\"expression\">Apache-2.0</license>");
            await Assert.That(nuspec).DoesNotContain("<dependency ");

            using var props = new StreamReader(archive.GetEntry($"buildTransitive/Typstium.Runtime.{rid}.props")!.Open());
            var text = await props.ReadToEndAsync();
            await Assert.That(text).Contains($"<TypstiumRuntime Include=\"{rid}\"");
            await Assert.That(text).Contains($"$([MSBuild]::NormalizePath('$(MSBuildThisFileDirectory)', '..', 'tools', 'typst', '{rid}', '{executable}'))");
            await Assert.That(text).Contains("ContractVersion=\"1\"");
        }
    }

    [Test]
    public async Task The_meta_package_depends_on_every_runtime_at_exactly_its_version()
    {
        var feed = await PackageFeed.GetFixtureAsync();
        var (entries, nuspec, archive) = await OpenAsync("Typstium.Runtime.All");
        using (archive)
        {
            await Assert.That(entries).IsEquivalentTo(new[] { "README.md" });
            foreach (var rid in PackageFeed.AllRuntimeIdentifiers)
            {
                // PrivateAssets="none" makes NuGet write include="All" (no exclude="Build..."), so build assets flow on.
                await Assert.That(nuspec).Contains($"<dependency id=\"Typstium.Runtime.{rid}\" version=\"[{feed.Version}]\" include=\"All\" />");
                await Assert.That(nuspec).DoesNotContain("exclude=");
            }
        }
    }

    [Test]
    public async Task The_packed_targets_files_are_well_formed_xml()
    {
        var (_, _, archive) = await OpenAsync("Typstium");
        using (archive)
        {
            foreach (var path in new[] { "build/Typstium.targets", "buildTransitive/Typstium.targets" })
            {
                using var reader = new StreamReader(archive.GetEntry(path)!.Open());
                var document = System.Xml.Linq.XDocument.Parse(await reader.ReadToEndAsync());
                await Assert.That(document.Root!.Name.LocalName).IsEqualTo("Project");
            }
        }
    }
}

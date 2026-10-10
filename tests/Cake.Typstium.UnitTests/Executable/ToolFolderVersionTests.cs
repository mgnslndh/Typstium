using Cake.Typstium.Executable;

namespace Cake.Typstium.UnitTests.Executable;

public class ToolFolderVersionTests
{
    private const string Id = "Typstium.Runtime.linux-arm";

    [Test]
    [Arguments("Typstium.Runtime.linux-arm")]
    [Arguments("Typstium.Runtime.linux-arm.0.15.1")]
    [Arguments("typstium.runtime.linux-arm.0.15.1")]
    [Arguments("Typstium.Runtime.linux-arm.0.16.0-beta.1")]
    public async Task Recognizes_the_package_folder_with_or_without_a_version(string folder)
    {
        await Assert.That(ToolFolderVersion.Parse(folder, Id)).IsNotNull();
    }

    [Test]
    [Arguments("Typstium.Runtime.linux-arm64")]
    [Arguments("Typstium.Runtime.linux-arm64.0.15.1")]
    [Arguments("Typstium.Runtime.linux-arm.banana")]
    [Arguments("Typstium.Runtime.All.0.15.1")]
    [Arguments("Typstium.Runtime.linux-arm.")]
    public async Task Rejects_other_packages_and_unparsable_versions(string folder)
    {
        await Assert.That(ToolFolderVersion.Parse(folder, Id)).IsNull();
    }

    [Test]
    public async Task Orders_numerically_with_releases_after_prereleases_and_no_version_first()
    {
        string[] folders = [Id + ".0.15.10", Id, Id + ".0.16.0", Id + ".0.15.1", Id + ".0.16.0-beta.1"];
        var ordered = folders.OrderBy(folder => ToolFolderVersion.Parse(folder, Id)).Select(folder => folder[Id.Length..]);

        await Assert.That(string.Join(" ", ordered)).IsEqualTo(" .0.15.1 .0.15.10 .0.16.0-beta.1 .0.16.0");
    }
}

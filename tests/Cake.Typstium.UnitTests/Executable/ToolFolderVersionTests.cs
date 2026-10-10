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
    [Arguments("0.16.0-rc.2", "0.16.0-rc.10")]
    [Arguments("0.16.0-alpha.1", "0.16.0-alpha.beta")]
    [Arguments("0.16.0-alpha", "0.16.0-alpha.1")]
    [Arguments("0.15.1", "0.15.1.1")]
    public async Task Orders_prerelease_identifiers_and_revisions_as_nuget_does(string lower, string higher)
    {
        var comparison = ToolFolderVersion.Parse(Id + "." + lower, Id)!.CompareTo(ToolFolderVersion.Parse(Id + "." + higher, Id));

        await Assert.That(comparison).IsLessThan(0);
    }

    [Test]
    public async Task Ignores_case_in_prerelease_labels_and_missing_zero_parts()
    {
        await Assert.That(ToolFolderVersion.Parse(Id + ".1.0.0-RC.1", Id)!.CompareTo(ToolFolderVersion.Parse(Id + ".1.0.0-rc.1", Id))).IsEqualTo(0);
        await Assert.That(ToolFolderVersion.Parse(Id + ".1.0", Id)!.CompareTo(ToolFolderVersion.Parse(Id + ".1.0.0", Id))).IsEqualTo(0);
    }

    [Test]
    public async Task Orders_numerically_with_releases_after_prereleases_and_no_version_first()
    {
        string[] folders = [Id + ".0.15.10", Id, Id + ".0.16.0", Id + ".0.15.1", Id + ".0.16.0-beta.1"];
        var ordered = folders.OrderBy(folder => ToolFolderVersion.Parse(folder, Id)).Select(folder => folder[Id.Length..]);

        await Assert.That(string.Join(" ", ordered)).IsEqualTo(" .0.15.1 .0.15.10 .0.16.0-beta.1 .0.16.0");
    }
}

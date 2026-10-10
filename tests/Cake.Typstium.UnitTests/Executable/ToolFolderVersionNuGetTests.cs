using Cake.Typstium.Executable;
using NuGet.Versioning;

namespace Cake.Typstium.UnitTests.Executable;

/// <summary>
/// Differential tests: tool folder versions must parse and order exactly as NuGet parses and orders package versions,
/// because Cake names the folders after the installed package's version.
/// </summary>
public class ToolFolderVersionNuGetTests
{
    private const string Id = "Typstium.Runtime.linux-x64";

    private static readonly string[] EdgeCases =
    [
        "1", "1.0", "1.0.0", "1.0.0.0", "1.0.0.1", "0.15.1", "0.15.1.1", "0.15.10", "2.0.0",
        "1.0.0-alpha", "1.0.0-alpha.1", "1.0.0-alpha.beta", "1.0.0-beta", "1.0.0-beta.2", "1.0.0-beta.10",
        "1.0.0-beta.11", "1.0.0-rc.1", "1.0.0-RC.1", "1.0.0-rc1", "1.0.0-rc10", "1.0.0-rc2", "1.0.0-0", "1.0.0-0.3.7",
        "1.0.0-x.7.z.92", "1.0.0+build.5", "1.0.0-beta+exp.sha.5114f85", "0.0.0-c20261010160230367", "01.2.3",
        "1.0.0-beta.01", "1.0.0-a-b", "1.0.0-alpha.2147483648",
    ];

    private static readonly string[] Malformed =
    [
        "", "banana", "1.", "1.0.0.0.0", "1.0.0-", "1.0.0-beta..1", "1.0.0-beta_1", "1.0.0+", "-1.0.0", "1.0.0-be ta",
        "1.a.0",
    ];

    private static IEnumerable<string> Generated()
    {
        string[] labels = ["", "-alpha", "-alpha.1", "-beta.2", "-beta.10", "-rc.1", "-RC.1", "-rc1"];
        foreach (var major in new[] { 0, 1 })
        foreach (var minor in new[] { 0, 15 })
        foreach (var patch in new[] { 1, 10 })
        foreach (var revision in new[] { "", ".1" })
        foreach (var label in labels)
        {
            yield return $"{major}.{minor}.{patch}{revision}{label}";
        }
    }

    [Test]
    public async Task Accepts_exactly_the_versions_nuget_accepts()
    {
        var disagreements = EdgeCases.Concat(Malformed).Concat(Generated())
            .Where(version => (ToolFolderVersion.Parse(Id + "." + version, Id) is not null) != NuGetVersion.TryParse(version, out _))
            .ToArray();

        await Assert.That(disagreements).IsEmpty();
    }

    [Test]
    public async Task Orders_every_pair_exactly_as_nuget_does()
    {
        var versions = EdgeCases.Concat(Generated()).Distinct().Where(version => NuGetVersion.TryParse(version, out _)).ToArray();
        var disagreements = new List<string>();
        foreach (var left in versions)
        {
            foreach (var right in versions)
            {
                var ours = Math.Sign(ToolFolderVersion.Parse(Id + "." + left, Id)!.CompareTo(ToolFolderVersion.Parse(Id + "." + right, Id)));
                var nuget = Math.Sign(NuGetVersion.Parse(left).CompareTo(NuGetVersion.Parse(right)));
                if (ours != nuget)
                {
                    disagreements.Add($"{left} vs {right}: ours {ours}, NuGet {nuget}");
                }
            }
        }

        await Assert.That(disagreements).IsEmpty();
    }
}

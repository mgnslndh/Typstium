using Typstium.Utilities;

namespace Typstium.UnitTests.Utilities;

public class FullyQualifiedPathTests
{
    [Test]
    [Arguments("C:\\tools", true, true)]
    [Arguments("C:/tools", true, true)]
    [Arguments(@"\\server\share", true, true)]
    [Arguments("//server/share", true, true)]
    [Arguments(null, true, false)]
    [Arguments(null, false, false)]
    [Arguments("C:", true, false)]
    [Arguments("C:tools", true, false)]
    [Arguments("\\tools", true, false)]
    [Arguments("tools", true, false)]
    [Arguments("", true, false)]
    [Arguments("/usr/bin", false, true)]
    [Arguments("usr/bin", false, false)]
    [Arguments("C:\\tools", false, false)]
    public async Task Classifies_paths_per_platform(string? path, bool isWindows, bool expected)
    {
        await Assert.That(FullyQualifiedPath.IsFullyQualified(path, isWindows)).IsEqualTo(expected);
    }
}

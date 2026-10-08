using Typstium.Utilities;

namespace Typstium.UnitTests.Utilities;

public class PathComparisonTests
{
    [Test]
    [Arguments("C:/Users/a", StringComparison.OrdinalIgnoreCase)]
    [Arguments("c:", StringComparison.OrdinalIgnoreCase)]
    [Arguments("UNC", StringComparison.Ordinal)]
    [Arguments("/home/a", StringComparison.Ordinal)]
    [Arguments("relative/path", StringComparison.Ordinal)]
    [Arguments("", StringComparison.Ordinal)]
    public async Task Windows_shaped_paths_ignore_case_and_others_do_not(string path, StringComparison expected)
    {
        await Assert.That(PathComparison.For(path)).IsEqualTo(expected);
    }

    [Test]
    public async Task Unc_paths_ignore_case()
    {
        await Assert.That(PathComparison.For(@"\\server\share\docs")).IsEqualTo(StringComparison.OrdinalIgnoreCase);
    }
}

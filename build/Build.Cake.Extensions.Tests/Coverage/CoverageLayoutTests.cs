using Build.Cake.Extensions.Coverage;
using Cake.Core.IO;

namespace Build.Cake.Extensions.Tests.Coverage;

public class CoverageLayoutTests
{
    [Test]
    public async Task Every_coverage_file_lives_under_one_root()
    {
        var layout = new CoverageLayout(new DirectoryPath("/repo/artifacts/coverage"));

        await Assert.That(layout.Raw.FullPath).IsEqualTo("/repo/artifacts/coverage/raw");
        await Assert.That(layout.Relative.FullPath).IsEqualTo("/repo/artifacts/coverage/relative");
        await Assert.That(layout.Report.FullPath).IsEqualTo("/repo/artifacts/coverage/report");
        await Assert.That(layout.MergedReport.FullPath).IsEqualTo("/repo/artifacts/coverage/report/Cobertura.xml");
        await Assert.That(layout.Summary.FullPath).IsEqualTo("/repo/artifacts/coverage/summary.json");
    }
}

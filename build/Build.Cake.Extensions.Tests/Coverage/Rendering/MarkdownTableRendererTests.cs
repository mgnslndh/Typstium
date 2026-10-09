using System.Globalization;
using Build.Cake.Extensions.Coverage.Rendering;
using Build.Cake.Extensions.Coverage.Reporting;

namespace Build.Cake.Extensions.Tests.Coverage.Rendering;

public class MarkdownTableRendererTests
{
    private const string Heading = "### Code coverage (unit and integration tests)";

    [Test]
    [Arguments("Real")]
    [Arguments("EdgeCases")]
    public async Task Job_summary_matches_the_PowerShell_report(string fixture)
    {
        var markdown = MarkdownTableRenderer.RenderJobSummary(Fixture.BuildReport(fixture), Heading);

        // Add-Content (PowerShell) and SetStepSummary (Cake) both end the appended text with a newline.
        await Assert.That(markdown + "\n").IsEqualTo(Fixture.Read(fixture, "expected", "job-summary.md"));
    }

    [Test]
    public async Task Rows_are_decorated_by_kind_and_cells_lose_their_padding()
    {
        var table = new CoverageTable("T", ["Name", "Missed", "Gap", "Line", "File"],
        [
            new(CoverageRowKind.Group, "Api", ["1", "50.0%", "  50.0%  (1/2)", "x"]),
            new(CoverageRowKind.Item, "Alpha.Box<T>", ["1", "50.0%", "  50.0%  (1/2)", "Api/Box.cs"]),
            new(CoverageRowKind.Total, "Total", ["2", "100.0%", "  50.0%  (2/4)", "-"]),
        ]);

        var markdown = MarkdownTableRenderer.RenderTable(table);

        await Assert.That(markdown).IsEqualTo(
            "| Name | Missed | Gap | Line | File |\n" +
            "| --- | ---: | ---: | ---: | --- |\n" +
            "| &nbsp;&nbsp;Api | 1 | 50.0% | 50.0% (1/2) | x |\n" +
            "| `Alpha.Box<T>` | 1 | 50.0% | 50.0% (1/2) | Api/Box.cs |\n" +
            "| **Total** | **2** | **100.0%** | **50.0% (2/4)** | **-** |\n");
    }

    [Test]
    public async Task Uses_newlines_only_whatever_the_platform()
    {
        await Assert.That(MarkdownTableRenderer.RenderJobSummary(Fixture.BuildReport("EdgeCases"), Heading)).DoesNotContain("\r");
    }
}

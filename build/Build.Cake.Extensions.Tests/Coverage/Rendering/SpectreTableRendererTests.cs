using Build.Cake.Extensions.Coverage.Rendering;
using Spectre.Console.Testing;

namespace Build.Cake.Extensions.Tests.Coverage.Rendering;

public class SpectreTableRendererTests
{
    [Test]
    public async Task Renders_every_view_and_the_legend()
    {
        var console = new TestConsole().Width(SpectreTableRenderer.MinimumWidth);

        SpectreTableRenderer.Render(console, Fixture.BuildReport("EdgeCases"));

        await Assert.That(console.Output).Contains("Coverage by visibility");
        await Assert.That(console.Output).Contains("Top 10 classes by missed lines");
        await Assert.That(console.Output).Contains("Unknown (no PublicAPI files)");
        await Assert.That(console.Output).Contains("Sorted by missed lines.");
    }

    [Test]
    public async Task Real_report_fits_the_width_without_wrapping_a_row()
    {
        var console = new TestConsole().Width(SpectreTableRenderer.MinimumWidth);
        var report = Fixture.BuildReport("Real");

        SpectreTableRenderer.Render(console, report);

        var lines = console.Output.Split('\n');
        await Assert.That(lines.Max(line => line.TrimEnd().Length)).IsLessThanOrEqualTo(SpectreTableRenderer.MinimumWidth);
        foreach (var row in report.Tables[3].Rows)
        {
            await Assert.That(lines.Any(line => line.Contains(row.Name, StringComparison.Ordinal) && line.Contains(row.Cells[3], StringComparison.Ordinal))).IsTrue();
        }
    }

    [Test]
    public async Task Names_with_brackets_are_not_read_as_markup()
    {
        var table = new Build.Cake.Extensions.Coverage.Reporting.CoverageTable("[T]", ["Name", "Missed"],
            [new(Build.Cake.Extensions.Coverage.Reporting.CoverageRowKind.Item, "Foo[bar]", ["1"])]);
        var console = new TestConsole().Width(80);

        console.Write(SpectreTableRenderer.Build(table));

        await Assert.That(console.Output).Contains("Foo[bar]");
    }
}

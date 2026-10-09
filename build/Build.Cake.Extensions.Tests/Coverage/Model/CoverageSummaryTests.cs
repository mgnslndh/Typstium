using Build.Cake.Extensions.Coverage.Model;

namespace Build.Cake.Extensions.Tests.Coverage.Model;

public class CoverageSummaryTests
{
    [Test]
    [Arguments("Real")]
    [Arguments("EdgeCases")]
    public async Task Writes_a_summary_exactly_as_the_PowerShell_report_did(string fixture)
    {
        var expected = Fixture.Read(fixture, "expected", "summary.json");

        var written = CoverageSummary.FromJson(expected).ToJson() + "\n";

        await Assert.That(written).IsEqualTo(expected);
    }

    [Test]
    public async Task Reads_a_baseline_written_by_the_PowerShell_report()
    {
        var summary = CoverageSummary.FromJson(Fixture.Read("Real", "expected", "summary.json"));

        await Assert.That(summary.Totals.Lines).IsGreaterThan(0);
        await Assert.That(summary.Namespaces.Count).IsGreaterThan(0);
        await Assert.That(summary.Namespaces[0].Name).StartsWith("Typstium");
    }

    [Test]
    public async Task Reads_a_single_namespace_collapsed_to_an_object_and_ignores_unknown_properties()
    {
        var summary = CoverageSummary.FromJson(Fixture.Read("Summaries", "single-namespace.json"));

        await Assert.That(summary.Totals).IsEqualTo(new CoverageCounts(5, 10, 0, 0, 1, 2));
        await Assert.That(summary.Namespaces.Count).IsEqualTo(1);
        await Assert.That(summary.Namespaces[0]).IsEqualTo(new NamespaceSummary("Solo", new CoverageCounts(5, 10, 0, 0, 1, 2)));
    }

    [Test]
    public async Task Writes_no_namespaces_as_an_empty_array()
    {
        var json = new CoverageSummary(CoverageCounts.Zero, []).ToJson();

        await Assert.That(json).Contains("\"namespaces\": []");
    }

    [Test]
    public async Task Write_creates_the_folder_and_ends_with_a_newline_without_a_byte_order_mark()
    {
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "summary-" + Guid.NewGuid().ToString("N"));
        var path = System.IO.Path.Combine(folder, "nested", "summary.json");
        try
        {
            new CoverageSummary(new CoverageCounts(1, 2, 0, 0, 1, 1), []).Write(path);

            var bytes = File.ReadAllBytes(path);
            await Assert.That(bytes[0]).IsEqualTo((byte)'{');
            await Assert.That(bytes[^1]).IsEqualTo((byte)'\n');
            await Assert.That(CoverageSummary.Read(path).Totals.Lines).IsEqualTo(2);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Test]
    public async Task Counts_add_up_field_by_field()
    {
        var sum = CoverageCounts.Sum([new CoverageCounts(1, 2, 3, 4, 5, 6), new CoverageCounts(10, 20, 30, 40, 50, 60)]);

        await Assert.That(sum).IsEqualTo(new CoverageCounts(11, 22, 33, 44, 55, 66));
        await Assert.That(sum.MissedLines).IsEqualTo(11);
        await Assert.That(sum.MissedBranches).IsEqualTo(11);
    }
}

using System.Globalization;
using Build.Cake.Extensions.Coverage.Reporting;

namespace Build.Cake.Extensions.Tests.Coverage.Reporting;

public class CoverageReportBuilderTests
{
    [Test]
    [Arguments("Real")]
    [Arguments("EdgeCases")]
    public async Task Summary_matches_the_PowerShell_report(string fixture)
    {
        var report = Fixture.BuildReport(fixture);

        await Assert.That(report.Summary.ToJson() + "\n").IsEqualTo(Fixture.Read(fixture, "expected", "summary.json"));
    }

    [Test]
    public async Task Builds_the_four_views_in_order()
    {
        var titles = Fixture.BuildReport("EdgeCases").Tables.Select(t => t.Title).ToList();

        await Assert.That(titles).IsEquivalentTo(
            ["Coverage by visibility", "Coverage by namespace", "Coverage by area (top-level folder)", "Top 10 classes by missed lines"],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Visibility_follows_the_PublicAPI_files_and_says_when_there_are_none()
    {
        var rows = Fixture.BuildReport("EdgeCases").Tables[0].Rows;
        var names = rows.Select(r => (r.Kind, r.Name)).ToList();

        await Assert.That(names).Contains((CoverageRowKind.Group, "Public API"));
        await Assert.That(names).Contains((CoverageRowKind.Group, "Internal"));
        await Assert.That(names).Contains((CoverageRowKind.Group, "Unknown (no PublicAPI files)"));
        await Assert.That(names).Contains((CoverageRowKind.Assembly, "Beta (assembly)"));
        await Assert.That(rows[^1].Kind).IsEqualTo(CoverageRowKind.Total);
    }

    [Test]
    public async Task Top_classes_only_lists_classes_with_missed_lines()
    {
        var top = Fixture.BuildReport("EdgeCases").Tables[3];

        await Assert.That(top.Headers).IsEquivalentTo(["Class", "Missed", "Gap", "Line", "File"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(top.Rows.All(r => r.Kind == CoverageRowKind.Item && int.Parse(r.Cells[0], CultureInfo.InvariantCulture) > 0)).IsTrue();
        await Assert.That(top.Rows.Any(r => r.Name == "Alpha.Root")).IsFalse();
    }

    [Test]
    public async Task Zero_totals_show_dashes_instead_of_dividing_by_zero()
    {
        var empty = new Build.Cake.Extensions.Coverage.Model.ClassCoverage("Alpha", "Alpha.Empty", "Empty.cs", Build.Cake.Extensions.Coverage.Model.CoverageCounts.Zero);

        var report = CoverageReportBuilder.Build([empty], new Dictionary<string, IReadOnlySet<string>?>(), topClasses: 10);
        var total = report.Tables[0].Rows[^1];

        await Assert.That(total.Cells).IsEquivalentTo(["0", "-", "-", "-", "-"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(report.Tables[3].Rows).IsEmpty();
    }

    [Test]
    public async Task Percentages_use_a_point_on_a_Swedish_machine()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("sv-SE");
        try
        {
            var cells = Fixture.BuildReport("EdgeCases").Tables[0].Rows[^1].Cells;

            await Assert.That(string.Join(" ", cells)).DoesNotContain(",");
            await Assert.That(cells[2]).Contains(".");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}

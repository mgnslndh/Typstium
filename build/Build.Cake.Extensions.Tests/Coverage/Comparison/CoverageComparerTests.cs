using System.Globalization;
using System.Text.Json;
using Build.Cake.Extensions.Coverage.Comparison;
using Build.Cake.Extensions.Coverage.Model;

namespace Build.Cake.Extensions.Tests.Coverage.Comparison;

public class CoverageComparerTests
{
    private static readonly CoverageComparisonOptions Options = new(
        Marker: "<!-- typstium-coverage -->",
        MaxLineDecrease: 0.5,
        Footer: "<sub>Unit and integration tests, Windows and Ubuntu merged. The full tables are in the CI run's coverage job summary.</sub>");

    private static CoverageSummary Summary(string file) => CoverageSummary.FromJson(Fixture.Read("Summaries", file));

    [Test]
    [Arguments("no-baseline", "current.json", null, "`main`")]
    [Arguments("pass", "current.json", "baseline-pass.json", "`main` at `abc1234` (merge base)")]
    [Arguments("fail", "current.json", "baseline-fail.json", "`main` at `def5678` (latest; the merge base `abc1234` has no coverage)")]
    [Arguments("boundary", "current-boundary.json", "baseline-boundary.json", "`main` at `abc1234` (merge base)")]
    [Arguments("unchanged", "current.json", "current.json", "`main` at `abc1234` (merge base)")]
    public async Task Matches_the_PowerShell_comparison(string name, string current, string? baseline, string label)
    {
        var comparison = CoverageComparer.Compare(Summary(current), baseline is null ? null : Summary(baseline), label, Options);

        using var expected = JsonDocument.Parse(Fixture.Read("Summaries", "expected", name + ".result.json"));
        await Assert.That(comparison.Markdown + "\n").IsEqualTo(Fixture.Read("Summaries", "expected", name + ".md"));
        await Assert.That(comparison.Conclusion).IsEqualTo(expected.RootElement.GetProperty("conclusion").GetString());
        await Assert.That(comparison.Title).IsEqualTo(expected.RootElement.GetProperty("title").GetString());
    }

    [Test]
    [Arguments(0.05, "🟢 +0.1 pp")]
    [Arguments(0.25, "🟢 +0.3 pp")]
    [Arguments(-0.25, "🔴 −0.3 pp")]
    [Arguments(-0.04, "⚪ ±0.0 pp")]
    [Arguments(0.0, "⚪ ±0.0 pp")]
    public async Task Changes_round_half_away_from_zero_with_a_true_minus_sign(double points, string expected)
    {
        await Assert.That(CoverageFormat.Change(points)).IsEqualTo(expected);
    }

    [Test]
    [Arguments(0, "⚪ ±0")]
    [Arguments(-3, "🟢 −3")]
    [Arguments(4, "🔴 +4")]
    public async Task Fewer_missed_lines_is_green(int change, string expected)
    {
        await Assert.That(CoverageFormat.CountChange(change)).IsEqualTo(expected);
    }

    [Test]
    public async Task Starts_with_the_marker_and_ends_with_the_footer()
    {
        var comparison = CoverageComparer.Compare(Summary("current.json"), Summary("baseline-pass.json"), "`main`", Options);

        await Assert.That(comparison.Markdown).StartsWith(Options.Marker + "\n");
        await Assert.That(comparison.Markdown).EndsWith(Options.Footer + "\n");
    }

    [Test]
    public async Task Writes_points_not_commas_on_a_Swedish_machine()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("sv-SE");
        try
        {
            var comparison = CoverageComparer.Compare(Summary("current.json"), Summary("baseline-fail.json"), "`main`", Options with { MaxLineDecrease = 0.75 });

            await Assert.That(comparison.Title).IsEqualTo("83.0% lines (−2.0 pp)");
            await Assert.That(comparison.Markdown).Contains("may drop at most 0.75 pp");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}

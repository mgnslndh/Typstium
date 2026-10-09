using Build.Cake.Extensions.Coverage.Cobertura;
using Build.Cake.Extensions.Coverage.Model;

namespace Build.Cake.Extensions.Tests.Coverage.Cobertura;

public class CoberturaReaderTests
{
    private static IReadOnlyList<ClassCoverage> EdgeCases() => CoberturaReader.Read(Fixture.Read("EdgeCases", "Cobertura.xml"));

    [Test]
    public async Task Counts_lines_branches_and_called_methods()
    {
        var widget = EdgeCases().Single(c => c.Name == "Alpha.Api.Widget");

        await Assert.That(widget.Assembly).IsEqualTo("Alpha");
        await Assert.That(widget.File).IsEqualTo("Api/Widget.cs");
        await Assert.That(widget.Counts).IsEqualTo(new CoverageCounts(CoveredLines: 2, Lines: 4, CoveredBranches: 1, Branches: 4, CoveredMethods: 1, Methods: 2));
    }

    [Test]
    public async Task Keeps_one_entry_per_class_and_file()
    {
        var parser = EdgeCases().Where(c => c.Name == "Alpha.Internal.Parser").Select(c => c.File).ToList();

        await Assert.That(parser).IsEquivalentTo(["Internal/Parser.cs", "Internal/Parser.Tokens.cs"]);
    }

    [Test]
    [Arguments("Beta.Thing", "Thing.cs")]
    [Arguments("Alpha.Generated", "Generated.cs")]
    [Arguments("Alpha.Root", "Root.cs")]
    public async Task Makes_files_relative_to_the_product_folder_or_keeps_the_file_name(string name, string file)
    {
        await Assert.That(EdgeCases().Single(c => c.Name == name).File).IsEqualTo(file);
    }

    [Test]
    public async Task A_class_without_lines_has_zero_counts()
    {
        await Assert.That(EdgeCases().Single(c => c.Name == "Alpha.Empty").Counts).IsEqualTo(CoverageCounts.Zero);
    }

    [Test]
    public async Task Reads_the_real_report()
    {
        var classes = CoberturaReader.Read(Fixture.Read("Real", "Cobertura.xml"));
        var summary = Build.Cake.Extensions.Coverage.Model.CoverageSummary.FromJson(Fixture.Read("Real", "expected", "summary.json"));

        await Assert.That(CoverageCounts.Sum(classes.Select(c => c.Counts))).IsEqualTo(summary.Totals);
    }
}

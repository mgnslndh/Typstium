using System.Text.RegularExpressions;
using Typstium.Testing;

namespace Cake.Typstium.UnitTests;

public partial class ScenarioCatalogTests
{
    // §7's rows: | CKn | scenario | expected | verified by |
    [GeneratedRegex(@"^\| (CK\d+) \|", RegexOptions.Multiline)]
    private static partial Regex Row();

    [GeneratedRegex(@"\[Property\((?:Scenario\.Key|""Scenario""), ""(CK\d+)""\)\]")]
    private static partial Regex Tag();

    [Test]
    public async Task Every_cake_scenario_has_a_tagged_end_to_end_test()
    {
        var spec = File.ReadAllText(Path.Combine(RepositoryPaths.Root, "docs", "specs", "cake.md"));
        var needed = Row().Matches(spec).Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        var tests = Path.Combine(RepositoryPaths.Root, "tests", "Cake.Typstium.EndToEndTests");
        var tagged = Directory.Exists(tests)
            ? Directory.EnumerateFiles(tests, "*.cs", SearchOption.AllDirectories)
                .SelectMany(file => Tag().Matches(File.ReadAllText(file)).Select(match => match.Groups[1].Value))
                .ToHashSet(StringComparer.Ordinal)
            : [];

        await Assert.That(needed.Except(tagged).Order(StringComparer.Ordinal).ToArray()).IsEmpty();
        await Assert.That(needed.Count).IsEqualTo(7);
    }
}

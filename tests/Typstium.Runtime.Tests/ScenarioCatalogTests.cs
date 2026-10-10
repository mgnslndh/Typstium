using System.Text.RegularExpressions;
using Typstium.Testing;

namespace Typstium.Runtime.Tests;

public partial class ScenarioCatalogTests
{
    // §9's rows: | RSn | scenario | expected | tier | verified |
    [GeneratedRegex(@"^\| (RS\d+) \|.*\| ([^|]+) \|\s*$", RegexOptions.Multiline)]
    private static partial Regex Row();

    [GeneratedRegex(@"\[Property\((?:Scenario\.Key|""Scenario""), ""(RS\d+)""\)\]")]
    private static partial Regex Tag();

    [Test]
    public async Task Every_scenario_verified_by_tests_has_a_tagged_test()
    {
        var spec = File.ReadAllText(Path.Combine(RepositoryPaths.Root, "docs", "specs", "runtime-packages.md"));
        var needed = Row().Matches(spec)
            .Where(match => match.Groups[2].Value.Split(',', StringSplitOptions.TrimEntries).Any(kind => kind is "U" or "T" or "E"))
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
        var tagged = Directory.EnumerateFiles(Path.Combine(RepositoryPaths.Root, "tests"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(file => Tag().Matches(File.ReadAllText(file)).Select(match => match.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);

        await Assert.That(needed.Except(tagged).Order(StringComparer.Ordinal).ToArray()).IsEmpty();
        await Assert.That(needed.Count).IsGreaterThan(20);
    }
}

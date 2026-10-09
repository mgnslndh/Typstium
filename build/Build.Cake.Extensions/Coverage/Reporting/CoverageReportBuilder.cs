using Build.Cake.Extensions.Coverage.Model;

namespace Build.Cake.Extensions.Coverage.Reporting;

// Four views of the same numbers, all sorted by missed (uncovered) lines so the biggest contributor to the gap comes
// first:
//   1. Visibility: the public API against internal code.
//   2. Namespace: the public API (the root namespace) and each internal component.
//   3. Area: each top-level folder of a product, so a feature's public and internal types share one row.
//   4. Top classes: the classes (per file) with the most missed lines, pointing at the code to test next.
// Gap is a row's share of all missed lines.
public static class CoverageReportBuilder
{
    private static readonly string[] SummaryHeaders = ["Missed", "Gap", "Line", "Branch", "Method (called)"];

    private sealed record Row(CoverageRowKind Kind, string Name, CoverageCounts Counts);

    public static CoverageReport Build(IReadOnlyList<ClassCoverage> classes, IReadOnlyDictionary<string, IReadOnlySet<string>?> publicTypes, int topClasses)
    {
        var total = CoverageCounts.Sum(classes.Select(c => c.Counts));
        var totalMissed = total.MissedLines;

        var byVisibility = GroupedRows(classes, (type, _) => Visibility(type, publicTypes.GetValueOrDefault(type.Assembly)));
        var byNamespace = GroupedRows(classes, (type, classNames) => NamespaceResolver.Namespace(type.Name, classNames));
        var byArea = GroupedRows(classes, (type, _) => NamespaceResolver.Area(type.File));
        var worst = ByGap(classes.Where(c => c.Counts.MissedLines > 0), c => c.Counts, c => c.Name).Take(topClasses);

        CoverageTable Summary(string title, string rowHeader, IEnumerable<Row> rows) => new(
            title,
            [rowHeader, .. SummaryHeaders],
            rows.Select(row => new CoverageRow(row.Kind, row.Name, SummaryCells(row.Counts, totalMissed))).ToList());

        var tables = new List<CoverageTable>
        {
            Summary("Coverage by visibility", "Visibility", byVisibility),
            Summary("Coverage by namespace", "Namespace", byNamespace),
            Summary("Coverage by area (top-level folder)", "Area", byArea),
            new(
                Invariant.Format($"Top {topClasses} classes by missed lines"),
                ["Class", "Missed", "Gap", "Line", "File"],
                worst.Select(c => new CoverageRow(
                    CoverageRowKind.Item,
                    c.Name,
                    [Invariant.Format($"{c.Counts.MissedLines}"), Gap(c.Counts.MissedLines, totalMissed), Rate(c.Counts.CoveredLines, c.Counts.Lines), c.File])).ToList()),
        };

        var namespaces = byNamespace.Where(row => row.Kind == CoverageRowKind.Group).Select(row => new NamespaceSummary(row.Name, row.Counts)).ToList();
        return new CoverageReport(tables, new CoverageSummary(total, namespaces));
    }

    private static string Visibility(ClassCoverage type, IReadOnlySet<string>? publicTypes) =>
        publicTypes is null ? "Unknown (no PublicAPI files)"
        : publicTypes.Contains(NamespaceResolver.TypeName(type.Name)) ? "Public API"
        : "Internal";

    // Rows per assembly (biggest gap first): its groups (biggest gap first), then a subtotal; finally the total.
    private static List<Row> GroupedRows(IReadOnlyList<ClassCoverage> classes, Func<ClassCoverage, IReadOnlySet<string>, string> groupOf)
    {
        var rows = new List<Row>();
        var assemblies = classes
            .GroupBy(c => c.Assembly, NameOrder.Lookup)
            .Select(g => (Name: g.First().Assembly, Counts: CoverageCounts.Sum(g.Select(c => c.Counts)), Classes: g.ToList()));

        foreach (var assembly in ByGap(assemblies, a => a.Counts, a => a.Name))
        {
            var classNames = assembly.Classes.Select(c => NamespaceResolver.TypeName(c.Name)).ToHashSet(StringComparer.Ordinal);
            var groups = assembly.Classes
                .GroupBy(c => groupOf(c, classNames), NameOrder.Lookup)
                .Select(g => new Row(CoverageRowKind.Group, g.Key, CoverageCounts.Sum(g.Select(c => c.Counts))));

            rows.AddRange(ByGap(groups, g => g.Counts, g => g.Name));
            rows.Add(new Row(CoverageRowKind.Assembly, $"{assembly.Name} (assembly)", assembly.Counts));
        }

        rows.Add(new Row(CoverageRowKind.Total, "Total", CoverageCounts.Sum(classes.Select(c => c.Counts))));
        return rows;
    }

    // Biggest contributor to the gap first: most missed lines, then most missed branches, then name.
    private static IEnumerable<T> ByGap<T>(IEnumerable<T> items, Func<T, CoverageCounts> counts, Func<T, string> name) =>
        items.OrderByDescending(item => counts(item).MissedLines)
            .ThenByDescending(item => counts(item).MissedBranches)
            .ThenBy(name, NameOrder.Sort);

    private static IReadOnlyList<string> SummaryCells(CoverageCounts counts, int totalMissed) =>
    [
        Invariant.Format($"{counts.MissedLines}"),
        Gap(counts.MissedLines, totalMissed),
        Rate(counts.CoveredLines, counts.Lines),
        Rate(counts.CoveredBranches, counts.Branches),
        Rate(counts.CoveredMethods, counts.Methods),
    ];

    // "  83.0%  (830/1000)": the percentage padded to six characters, so console columns line up.
    private static string Rate(int covered, int total) =>
        total == 0 ? "-" : Invariant.Format($"{Invariant.Format($"{100.0 * covered / total:0.0}%"),6}  ({covered}/{total})");

    private static string Gap(int missed, int totalMissed) =>
        totalMissed == 0 ? "-" : Invariant.Format($"{100.0 * missed / totalMissed:0.0}%");
}

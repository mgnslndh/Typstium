using System.Text;
using Build.Cake.Extensions.Coverage.Model;

namespace Build.Cake.Extensions.Coverage.Comparison;

// Compares two coverage summaries and writes the pull-request comment. The rule: total line coverage may drop at most
// MaxLineDecrease percentage points from the baseline. Branch and method coverage are shown but not gated: they move
// more between runs of the same code.
public static class CoverageComparer
{
    public static CoverageComparison Compare(CoverageSummary current, CoverageSummary? baseline, string baselineLabel, CoverageComparisonOptions options)
    {
        var now = current.Totals;
        var linePercent = CoverageFormat.Percent(now.CoveredLines, now.Lines);
        var markdown = new StringBuilder();
        Line(markdown, options.Marker);

        string conclusion, title;
        if (baseline is null)
        {
            conclusion = "neutral";
            title = $"{CoverageFormat.Percentage(linePercent)} lines (no baseline)";
            Line(markdown, $"### Coverage: {CoverageFormat.Percentage(linePercent)} lines");
            Line(markdown);
            Line(markdown, $"No baseline coverage for {baselineLabel} yet, so there's nothing to compare with; the rule isn't applied.");
            Line(markdown);
            Line(markdown, "| | This PR |");
            Line(markdown, "| --- | ---: |");
            Line(markdown, $"| Line | {CoverageFormat.Rate(now.CoveredLines, now.Lines)} |");
            Line(markdown, $"| Branch | {CoverageFormat.Rate(now.CoveredBranches, now.Branches)} |");
            Line(markdown, $"| Method (called) | {CoverageFormat.Rate(now.CoveredMethods, now.Methods)} |");
            Line(markdown, Invariant.Format($"| Missed lines | {now.MissedLines} |"));
        }
        else
        {
            var before = baseline.Totals;
            var lineChange = linePercent - CoverageFormat.Percent(before.CoveredLines, before.Lines);
            var passed = lineChange >= -options.MaxLineDecrease;
            conclusion = passed ? "success" : "failure";
            var change = CoverageFormat.Change(lineChange);
            title = $"{CoverageFormat.Percentage(linePercent)} lines ({change[(change.IndexOf(' ') + 1)..]})";
            var rule = Invariant.Format($"Rule: line coverage may drop at most {options.MaxLineDecrease:0.0#} pp");
            var verdict = passed ? "✅ **passed**" : "❌ **failed**";

            Line(markdown, $"### Coverage: {CoverageFormat.Percentage(linePercent)} lines {change}");
            Line(markdown);
            Line(markdown, $"Compared with {baselineLabel}. {rule}: {verdict}");
            Line(markdown);
            Line(markdown, "| | This PR | Baseline | Change |");
            Line(markdown, "| --- | ---: | ---: | ---: |");
            MetricRow(markdown, "Line", now.CoveredLines, now.Lines, before.CoveredLines, before.Lines);
            MetricRow(markdown, "Branch", now.CoveredBranches, now.Branches, before.CoveredBranches, before.Branches);
            MetricRow(markdown, "Method (called)", now.CoveredMethods, now.Methods, before.CoveredMethods, before.Methods);
            Line(markdown, Invariant.Format($"| Missed lines | {now.MissedLines} | {before.MissedLines} | {CoverageFormat.CountChange(now.MissedLines - before.MissedLines)} |"));
            ChangedNamespaces(markdown, current, baseline);
        }

        Line(markdown);
        Line(markdown, options.Footer);
        return new CoverageComparison(conclusion, title, markdown.ToString());
    }

    private static void MetricRow(StringBuilder markdown, string name, int covered, int total, int baseCovered, int baseTotal)
    {
        var change = CoverageFormat.Percent(covered, total) - CoverageFormat.Percent(baseCovered, baseTotal);
        Line(markdown, $"| {name} | {CoverageFormat.Rate(covered, total)} | {CoverageFormat.Rate(baseCovered, baseTotal)} | {CoverageFormat.Change(change)} |");
    }

    // Namespaces whose line counts changed, biggest growth in missed lines first.
    private static void ChangedNamespaces(StringBuilder markdown, CoverageSummary current, CoverageSummary baseline)
    {
        var nowByName = ByName(current);
        var baseByName = ByName(baseline);
        var names = nowByName.Keys.Concat(baseByName.Keys).Distinct(NameOrder.Lookup).Order(NameOrder.Sort);

        var changed = new List<(string Name, string Now, string Base, string Change, int Missed)>();
        foreach (var name in names)
        {
            var now = nowByName.GetValueOrDefault(name);
            var before = baseByName.GetValueOrDefault(name);
            var nowCounts = now ?? CoverageCounts.Zero;
            var baseCounts = before ?? CoverageCounts.Zero;
            if (nowCounts.CoveredLines == baseCounts.CoveredLines && nowCounts.Lines == baseCounts.Lines)
            {
                continue;
            }

            changed.Add((
                name,
                now is null ? "(removed)" : CoverageFormat.Rate(now.CoveredLines, now.Lines),
                before is null ? "(new)" : CoverageFormat.Rate(before.CoveredLines, before.Lines),
                now is not null && before is not null ? CoverageFormat.Change(CoverageFormat.Percent(now.CoveredLines, now.Lines) - CoverageFormat.Percent(before.CoveredLines, before.Lines)) : "",
                nowCounts.MissedLines - baseCounts.MissedLines));
        }

        if (changed.Count == 0)
        {
            return;
        }

        Line(markdown);
        Line(markdown, Invariant.Format($"<details><summary>Namespaces with changed line coverage ({changed.Count})</summary>"));
        Line(markdown);
        Line(markdown, "| Namespace | This PR | Baseline | Change | Missed lines |");
        Line(markdown, "| --- | ---: | ---: | ---: | ---: |");
        foreach (var row in changed.OrderByDescending(row => row.Missed).ThenBy(row => row.Name, NameOrder.Sort))
        {
            Line(markdown, $"| {row.Name} | {row.Now} | {row.Base} | {row.Change} | {CoverageFormat.CountChange(row.Missed)} |");
        }

        Line(markdown);
        Line(markdown, "</details>");
    }

    // Later entries win, as in the PowerShell hashtable this replaces.
    private static Dictionary<string, CoverageCounts> ByName(CoverageSummary summary)
    {
        var byName = new Dictionary<string, CoverageCounts>(NameOrder.Lookup);
        foreach (var entry in summary.Namespaces)
        {
            byName[entry.Name] = entry.Counts;
        }

        return byName;
    }

    private static void Line(StringBuilder markdown, string text = "") => markdown.Append(text).Append('\n');
}

internal static class CoverageFormat
{
    public static double Percent(int covered, int total) => total == 0 ? 100.0 : 100.0 * covered / total;

    public static string Percentage(double value) => Invariant.Format($"{value:0.0}%");

    public static string Rate(int covered, int total) => Invariant.Format($"{Percentage(Percent(covered, total))} ({covered}/{total})");

    // Percentage-point change with a sign and a colour: green up, red down, white within rounding.
    public static string Change(double points)
    {
        var rounded = Math.Round(points, 1, MidpointRounding.AwayFromZero);
        if (rounded == 0)
        {
            return "⚪ ±0.0 pp";
        }

        var text = Invariant.Format($"{Math.Abs(rounded):0.0} pp");
        return rounded > 0 ? $"🟢 +{text}" : $"🔴 −{text}";
    }

    // A change in a count where fewer is better (missed lines).
    public static string CountChange(int change) =>
        change == 0 ? "⚪ ±0" : change < 0 ? Invariant.Format($"🟢 −{-change}") : Invariant.Format($"🔴 +{change}");
}

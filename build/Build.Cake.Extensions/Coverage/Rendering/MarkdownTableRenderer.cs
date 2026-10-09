using System.Text;
using System.Text.RegularExpressions;
using Build.Cake.Extensions.Coverage.Reporting;

namespace Build.Cake.Extensions.Coverage.Rendering;

// Markdown for GitHub (job summaries, comments). Lines end in \n on every platform, so output is the same wherever
// the build runs.
public static class MarkdownTableRenderer
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    // The first view open, the others folded into <details>, then the legend.
    public static string RenderJobSummary(CoverageReport report, string heading)
    {
        var markdown = new StringBuilder();
        markdown.Append(heading).Append('\n');
        markdown.Append('\n');
        markdown.Append(RenderTable(report.Tables[0])).Append('\n');
        foreach (var table in report.Tables.Skip(1))
        {
            markdown.Append("<details><summary>").Append(table.Title).Append("</summary>\n");
            markdown.Append('\n');
            markdown.Append(RenderTable(table)).Append('\n');
            markdown.Append("</details>\n");
            markdown.Append('\n');
        }

        markdown.Append(CoverageReport.Legend).Append('\n');
        return markdown.ToString();
    }

    public static string RenderTable(CoverageTable table)
    {
        var markdown = new StringBuilder();
        markdown.Append("| ").Append(string.Join(" | ", table.Headers)).Append(" |\n");
        markdown.Append("| --- |");
        foreach (var header in table.Headers.Skip(1))
        {
            markdown.Append(header == "File" ? " --- |" : " ---: |");
        }

        markdown.Append('\n');
        foreach (var row in table.Rows)
        {
            var name = row.Kind switch
            {
                CoverageRowKind.Group => $"&nbsp;&nbsp;{row.Name}",
                CoverageRowKind.Item => $"`{row.Name}`",
                _ => $"**{row.Name}**",
            };
            var bold = row.Kind is CoverageRowKind.Assembly or CoverageRowKind.Total;
            var values = row.Cells.Select(cell => Whitespace.Replace(cell, " ").Trim()).Select(value => bold ? $"**{value}**" : value);
            markdown.Append("| ").Append(name).Append(" | ").Append(string.Join(" | ", values)).Append(" |\n");
        }

        return markdown.ToString();
    }
}

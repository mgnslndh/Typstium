using Build.Cake.Extensions.Coverage.Reporting;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Build.Cake.Extensions.Coverage.Rendering;

// Coverage tables for the build log. Spectre.Console ships with Cake (through Cake.Cli), so this adds no dependency.
public static class SpectreTableRenderer
{
    // A console without a terminal (CI logs) is assumed 80 columns wide, which would wrap these tables.
    public const int MinimumWidth = 160;

    public static IAnsiConsole CreateConsole()
    {
        var console = AnsiConsole.Create(new AnsiConsoleSettings());
        if (Console.IsOutputRedirected)
        {
            console.Profile.Width = Math.Max(console.Profile.Width, MinimumWidth);
        }

        return console;
    }

    public static void Render(IAnsiConsole console, CoverageReport report)
    {
        foreach (var table in report.Tables)
        {
            console.WriteLine();
            console.Write(Build(table));
        }

        console.WriteLine();
        console.WriteLine(CoverageReport.Legend);
        console.WriteLine();
    }

    public static Table Build(CoverageTable table)
    {
        var result = new Table
        {
            Border = TableBorder.Simple,
            Title = new TableTitle(Markup.Escape(table.Title), new Style(Color.Aqua)),
        };

        for (var column = 0; column < table.Headers.Count; column++)
        {
            var header = new TableColumn(new Text(table.Headers[column])) { NoWrap = true };
            result.AddColumn(column is 1 or 2 ? header.RightAligned() : header);
        }

        foreach (var row in table.Rows)
        {
            var style = row.Kind switch
            {
                CoverageRowKind.Group or CoverageRowKind.Item => new Style(decoration: Decoration.Dim),
                _ => new Style(decoration: Decoration.Bold),
            };
            var name = row.Kind == CoverageRowKind.Group ? "  " + row.Name : row.Name;
            result.AddRow([new Text(name, style), .. row.Cells.Select(cell => (IRenderable)new Text(cell, style))]);
        }

        return result;
    }
}

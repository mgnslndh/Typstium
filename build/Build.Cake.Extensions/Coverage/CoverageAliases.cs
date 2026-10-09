using Build.Cake.Extensions.Coverage.Cobertura;
using Build.Cake.Extensions.Coverage.Model;
using Build.Cake.Extensions.Coverage.Rendering;
using Build.Cake.Extensions.Coverage.Reporting;
using Build.Cake.Extensions.GitHub;
using Cake.Core;

namespace Build.Cake.Extensions.Coverage;

public static class CoverageAliases
{
    // Merges the raw coverage files, prints the four views, writes summary.json and, on GitHub Actions, the job summary.
    public static CoverageSummary ReportCoverage(this ICakeContext context, CoverageReportSettings settings)
    {
        var cobertura = CoverageMerger.Merge(context, settings);
        var classes = CoberturaReader.Read(File.ReadAllText(cobertura.FullPath), settings.Sources.ProductsFolder);
        var publicTypes = classes.Select(c => c.Assembly).Distinct(StringComparer.Ordinal)
            .ToDictionary(assembly => assembly, assembly => PublicApiCatalog.Read(settings.SourceDirectory.Combine(assembly).FullPath));
        var report = CoverageReportBuilder.Build(classes, publicTypes, settings.TopClasses);

        SpectreTableRenderer.Render(SpectreTableRenderer.CreateConsole(), report);
        report.Summary.Write(settings.Files.Summary.FullPath);

        if (settings.WriteJobSummary)
        {
            StepSummary.TryAppend(context, MarkdownTableRenderer.RenderJobSummary(report, settings.JobSummaryHeading));
        }

        return report.Summary;
    }
}

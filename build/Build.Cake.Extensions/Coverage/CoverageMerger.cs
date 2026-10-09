using Build.Cake.Extensions.Coverage.Cobertura;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;
using Palmmedia.ReportGenerator.Core;

namespace Build.Cake.Extensions.Coverage;

// Merges every raw Cobertura file (each test project and target framework, from one machine or several) into one
// report: raw/ → relative/ (repo-relative paths) → report/Cobertura.xml, with ReportGenerator in process.
internal static class CoverageMerger
{
    public static FilePath Merge(ICakeContext context, DirectoryPath coverageDirectory, IReadOnlyList<string> products)
    {
        var relative = coverageDirectory.Combine("relative");
        var report = coverageDirectory.Combine("report");
        context.EnsureDirectoryDoesNotExist(relative, new EnsureDirectoryDoesNotExistSettings { Recursive = true, Force = true });
        context.EnsureDirectoryDoesNotExist(report, new EnsureDirectoryDoesNotExistSettings { Recursive = true, Force = true });

        var count = CoberturaPathNormalizer.NormalizeFiles(coverageDirectory.Combine("raw").FullPath, relative.FullPath);
        context.Information("Merging {0} coverage file(s) for {1}", count, string.Join(", ", products));

        var configuration = new ReportConfigurationBuilder().Create(new Dictionary<string, string>
        {
            ["reports"] = relative.CombineWithFilePath("*.cobertura.xml").FullPath,
            ["targetdir"] = report.FullPath,
            ["reporttypes"] = "Cobertura",
            // Only the products count; anything under tests/ never does.
            ["assemblyfilters"] = string.Join(';', products.Select(product => "+" + product)),
            ["verbosity"] = "Warning",
        });

        if (!new Generator().GenerateReport(configuration))
        {
            throw new CakeException($"ReportGenerator couldn't merge the coverage files in {relative.FullPath}.");
        }

        return report.CombineWithFilePath("Cobertura.xml");
    }
}

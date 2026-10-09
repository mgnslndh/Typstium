namespace Build.Cake.Extensions.Tests;

// Files under Fixtures/, copied next to the test assembly. Golden files were written by the PowerShell scripts, some
// on Windows: reading normalizes line endings, so comparisons are about content, not the machine that wrote them.
internal static class Fixture
{
    public static string Path(params string[] parts) => System.IO.Path.Combine([AppContext.BaseDirectory, "Fixtures", .. parts]);

    public static string Read(params string[] parts) => File.ReadAllText(Path(parts)).ReplaceLineEndings("\n");

    // The fixture's Cobertura.xml grouped exactly as the CoverageReport target does, with PublicAPI files from the
    // fixture's src/<assembly>/ folders.
    public static Build.Cake.Extensions.Coverage.Reporting.CoverageReport BuildReport(string fixture)
    {
        var classes = Build.Cake.Extensions.Coverage.Cobertura.CoberturaReader.Read(Read(fixture, "Cobertura.xml"));
        var publicTypes = classes.Select(c => c.Assembly).Distinct(StringComparer.Ordinal)
            .ToDictionary(assembly => assembly, assembly => Build.Cake.Extensions.Coverage.Reporting.PublicApiCatalog.Read(Path(fixture, "src", assembly)));
        return Build.Cake.Extensions.Coverage.Reporting.CoverageReportBuilder.Build(classes, publicTypes, topClasses: 10);
    }
}

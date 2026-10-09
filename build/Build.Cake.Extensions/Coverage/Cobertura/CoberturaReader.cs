using System.Text.RegularExpressions;
using System.Xml.Linq;
using Build.Cake.Extensions.Coverage.Model;

namespace Build.Cake.Extensions.Coverage.Cobertura;

// Reads a Cobertura report (ReportGenerator's merged one) into one entry per class and file.
public static class CoberturaReader
{
    private static readonly Regex Condition = new(@"\((\d+)/(\d+)\)", RegexOptions.Compiled);

    public static IReadOnlyList<ClassCoverage> Read(string coberturaXml)
    {
        var document = XDocument.Parse(coberturaXml);
        var result = new List<ClassCoverage>();
        foreach (var package in document.Root!.Elements("packages").Elements("package"))
        {
            var assembly = Attribute(package, "name");
            foreach (var type in package.Elements("classes").Elements("class"))
            {
                result.Add(new ClassCoverage(assembly, Attribute(type, "name"), RelativeToProduct(Attribute(type, "filename"), assembly), Count(type)));
            }
        }

        return result;
    }

    private static CoverageCounts Count(XElement type)
    {
        int lines = 0, coveredLines = 0, branches = 0, coveredBranches = 0;
        foreach (var line in type.Elements("lines").Elements("line"))
        {
            lines++;
            if (long.TryParse(Attribute(line, "hits"), out var hits) && hits > 0)
            {
                coveredLines++;
            }

            var condition = Condition.Match(Attribute(line, "condition-coverage"));
            if (condition.Success)
            {
                coveredBranches += int.Parse(condition.Groups[1].Value);
                branches += int.Parse(condition.Groups[2].Value);
            }
        }

        var methods = type.Elements("methods").Elements("method").ToList();
        var calledMethods = methods.Count(method => method.Elements("lines").Elements("line").Any(line => line.Attribute("hits") is { Value: not "0" }));
        return new CoverageCounts(coveredLines, lines, coveredBranches, branches, calledMethods, methods.Count);
    }

    // Paths are absolute, or repo-relative (src/<assembly>/...) when merged from several machines.
    private static string RelativeToProduct(string fileName, string assembly)
    {
        var file = fileName.Replace('\\', '/');
        var marker = $"src/{assembly}/";
        if (file.StartsWith(marker, StringComparison.Ordinal))
        {
            return file[marker.Length..];
        }

        var index = file.IndexOf('/' + marker, StringComparison.Ordinal);
        return index >= 0 ? file[(index + 1 + marker.Length)..] : file[(file.LastIndexOf('/') + 1)..];
    }

    private static string Attribute(XElement element, string name) => element.Attribute(name)?.Value ?? "";
}

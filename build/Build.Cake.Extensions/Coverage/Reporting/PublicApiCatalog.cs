namespace Build.Cake.Extensions.Coverage.Reporting;

// The public types of a product: bare type lines (no "->") of its PublicAPI.*.txt files, which the PublicAPI analyzer
// keeps complete. Without such files there is no catalog, and the product's classes show as "Unknown" rather than as a
// guess.
public static class PublicApiCatalog
{
    public static IReadOnlySet<string>? Read(string productDirectory)
    {
        var files = Directory.Exists(productDirectory) ? Directory.GetFiles(productDirectory, "PublicAPI.*.txt") : [];
        return files.Length == 0 ? null : Parse(files.SelectMany(File.ReadLines));
    }

    public static IReadOnlySet<string> Parse(IEnumerable<string> lines)
    {
        var types = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            var entry = line.Trim();
            if (entry.Length > 0 && !entry.StartsWith('#') && !entry.Contains("->", StringComparison.Ordinal))
            {
                types.Add(NamespaceResolver.TypeName(entry));
            }
        }

        return types;
    }
}

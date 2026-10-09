using System.Text.RegularExpressions;

namespace Build.Cake.Extensions.Coverage.Cobertura;

// Each machine records absolute paths (D:\... on Windows, /home/runner/... on Ubuntu). Made repo-relative, the same
// file from several machines is one file to ReportGenerator, so a line counts as covered when any run hit it,
// instead of being counted once per machine.
public static class CoberturaPathNormalizer
{
    private static readonly Regex FileName = new("filename=\"([^\"]*)\"", RegexOptions.Compiled);

    // Cut before the last of the layout's code folders (/src/, /tests/); paths outside them only get forward slashes.
    public static string ToRepoRelativePath(string path, SourceLayout layout)
    {
        var normalized = path.Replace('\\', '/');
        var index = layout.CodeFolders.Max(folder => normalized.LastIndexOf($"/{folder}/", StringComparison.Ordinal));
        return index >= 0 ? normalized[(index + 1)..] : normalized;
    }

    public static string ToRepoRelative(string coberturaXml, SourceLayout layout) =>
        FileName.Replace(coberturaXml, match => $"filename=\"{ToRepoRelativePath(match.Groups[1].Value, layout)}\"");

    // Copies every *.cobertura.xml of the source folder into the target folder, normalized; returns how many.
    public static int NormalizeFiles(string sourceDirectory, string targetDirectory, SourceLayout layout)
    {
        var files = Directory.Exists(sourceDirectory) ? Directory.GetFiles(sourceDirectory, "*.cobertura.xml") : [];
        if (files.Length == 0)
        {
            throw new InvalidOperationException($"No *.cobertura.xml files in {sourceDirectory}.");
        }

        Directory.CreateDirectory(targetDirectory);
        foreach (var file in files)
        {
            File.WriteAllText(Path.Combine(targetDirectory, Path.GetFileName(file)), ToRepoRelative(File.ReadAllText(file), layout));
        }

        return files.Length;
    }
}

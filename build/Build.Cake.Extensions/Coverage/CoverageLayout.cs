using Cake.Core.IO;

namespace Build.Cake.Extensions.Coverage;

// Where a coverage run keeps its files, all under one root (e.g. artifacts/coverage): the test runs write raw/, the
// merge writes relative/ and report/, and the report writes summary.json.
public sealed record CoverageLayout(DirectoryPath Root)
{
    public DirectoryPath Raw => Root.Combine("raw");

    public DirectoryPath Relative => Root.Combine("relative");

    public DirectoryPath Report => Root.Combine("report");

    public FilePath MergedReport => Report.CombineWithFilePath("Cobertura.xml");

    public FilePath Summary => Root.CombineWithFilePath("summary.json");
}

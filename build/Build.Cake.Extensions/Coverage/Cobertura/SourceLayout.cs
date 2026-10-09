namespace Build.Cake.Extensions.Coverage.Cobertura;

// Where a repository keeps its code. CodeFolders are the top-level folders holding source files (src, tests): a
// recorded path is made repo-relative from the last of them. ProductsFolder holds one folder per product assembly
// (src/<assembly>/), which file names in the report are made relative to.
public sealed record SourceLayout(IReadOnlyList<string> CodeFolders, string ProductsFolder);

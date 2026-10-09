namespace Build.Cake.Extensions.Coverage.Model;

// One class in one file (partial classes appear once per file). File is relative to the product folder
// (src/<Assembly>/), or just the file name when the class lives elsewhere.
public sealed record ClassCoverage(string Assembly, string Name, string File, CoverageCounts Counts);

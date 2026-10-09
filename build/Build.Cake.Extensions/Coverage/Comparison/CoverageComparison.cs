namespace Build.Cake.Extensions.Coverage.Comparison;

// Conclusion is a GitHub check-run conclusion: "neutral" (no baseline), "success" or "failure".
public sealed record CoverageComparison(string Conclusion, string Title, string Markdown);

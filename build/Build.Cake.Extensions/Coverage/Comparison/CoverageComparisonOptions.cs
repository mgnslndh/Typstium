namespace Build.Cake.Extensions.Coverage.Comparison;

// Marker: the comment's first line, by which the sticky comment is found again. Footer: its last line.
// MaxLineDecrease: how many percentage points (pp) line coverage may drop from the baseline.
public sealed record CoverageComparisonOptions(string Marker, double MaxLineDecrease, string Footer);

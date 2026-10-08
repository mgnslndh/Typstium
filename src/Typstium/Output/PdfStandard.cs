using System.Text.RegularExpressions;

namespace Typstium;

/// <summary>A PDF standard Typst can enforce. The CLI value (e.g. <c>a-2b</c>) is its identity.</summary>
public sealed class PdfStandard : IEquatable<PdfStandard>
{
    private static readonly Regex CliValueFormat = new("^[a-z0-9.-]+\\z", RegexOptions.CultureInvariant);

    private PdfStandard(string cliValue) => CliValue = cliValue;

    public static PdfStandard Pdf14 { get; } = new("1.4");
    public static PdfStandard Pdf15 { get; } = new("1.5");
    public static PdfStandard Pdf16 { get; } = new("1.6");
    public static PdfStandard Pdf17 { get; } = new("1.7");
    public static PdfStandard Pdf20 { get; } = new("2.0");
    public static PdfStandard A1b { get; } = new("a-1b");
    public static PdfStandard A1a { get; } = new("a-1a");
    public static PdfStandard A2b { get; } = new("a-2b");
    public static PdfStandard A2u { get; } = new("a-2u");
    public static PdfStandard A2a { get; } = new("a-2a");
    public static PdfStandard A3b { get; } = new("a-3b");
    public static PdfStandard A3u { get; } = new("a-3u");
    public static PdfStandard A3a { get; } = new("a-3a");
    public static PdfStandard A4 { get; } = new("a-4");
    public static PdfStandard A4f { get; } = new("a-4f");
    public static PdfStandard A4e { get; } = new("a-4e");
    public static PdfStandard UA1 { get; } = new("ua-1");

    // Declared after the instances: static initializers run in textual order.
    internal static IReadOnlyList<PdfStandard> KnownInTypst015 { get; } =
        [Pdf14, Pdf15, Pdf16, Pdf17, Pdf20, A1b, A1a, A2b, A2u, A2a, A3b, A3u, A3a, A4, A4f, A4e, UA1];

    public string CliValue { get; }

    /// <summary>Escape hatch for standards added by newer Typst versions.</summary>
    public static PdfStandard FromCliValue(string value)
    {
        if (value is null || !CliValueFormat.IsMatch(value))
        {
            throw new ArgumentException($"'{value}' is not a single PDF standard value such as 'a-2b'.", nameof(value));
        }

        return new PdfStandard(value);
    }

    public bool Equals(PdfStandard? other) => other is not null && string.Equals(CliValue, other.CliValue, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as PdfStandard);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(CliValue);

    public override string ToString() => CliValue;
}

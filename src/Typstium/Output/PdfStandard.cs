using System.Text.RegularExpressions;

namespace Typstium;

/// <summary>A PDF standard Typst can enforce. The CLI value (e.g. <c>a-2b</c>) is its identity.</summary>
public sealed class PdfStandard : IEquatable<PdfStandard>
{
    private static readonly Regex CliValueFormat = new("^[a-z0-9.-]+\\z", RegexOptions.CultureInvariant);

    private PdfStandard(string cliValue) => CliValue = cliValue;

    /// <summary>PDF 1.4.</summary>
    public static PdfStandard Pdf14 { get; } = new("1.4");
    /// <summary>PDF 1.5.</summary>
    public static PdfStandard Pdf15 { get; } = new("1.5");
    /// <summary>PDF 1.6.</summary>
    public static PdfStandard Pdf16 { get; } = new("1.6");
    /// <summary>PDF 1.7.</summary>
    public static PdfStandard Pdf17 { get; } = new("1.7");
    /// <summary>PDF 2.0.</summary>
    public static PdfStandard Pdf20 { get; } = new("2.0");
    /// <summary>PDF/A-1b (basic conformance).</summary>
    public static PdfStandard A1b { get; } = new("a-1b");
    /// <summary>PDF/A-1a (accessible conformance).</summary>
    public static PdfStandard A1a { get; } = new("a-1a");
    /// <summary>PDF/A-2b (basic conformance).</summary>
    public static PdfStandard A2b { get; } = new("a-2b");
    /// <summary>PDF/A-2u (Unicode conformance).</summary>
    public static PdfStandard A2u { get; } = new("a-2u");
    /// <summary>PDF/A-2a (accessible conformance).</summary>
    public static PdfStandard A2a { get; } = new("a-2a");
    /// <summary>PDF/A-3b (basic conformance).</summary>
    public static PdfStandard A3b { get; } = new("a-3b");
    /// <summary>PDF/A-3u (Unicode conformance).</summary>
    public static PdfStandard A3u { get; } = new("a-3u");
    /// <summary>PDF/A-3a (accessible conformance).</summary>
    public static PdfStandard A3a { get; } = new("a-3a");
    /// <summary>PDF/A-4.</summary>
    public static PdfStandard A4 { get; } = new("a-4");
    /// <summary>PDF/A-4f (allows embedded files of any kind).</summary>
    public static PdfStandard A4f { get; } = new("a-4f");
    /// <summary>PDF/A-4e (engineering documents).</summary>
    public static PdfStandard A4e { get; } = new("a-4e");
    /// <summary>PDF/UA-1 (universal accessibility).</summary>
    public static PdfStandard UA1 { get; } = new("ua-1");

    // Declared after the instances: static initializers run in textual order.
    internal static IReadOnlyList<PdfStandard> KnownInTypst015 { get; } =
        [Pdf14, Pdf15, Pdf16, Pdf17, Pdf20, A1b, A1a, A2b, A2u, A2a, A3b, A3u, A3a, A4, A4f, A4e, UA1];

    /// <summary>The value passed to <c>--pdf-standard</c>, such as <c>a-2b</c>.</summary>
    public string CliValue { get; }

    /// <summary>Escape hatch for standards added by newer Typst versions.</summary>
    /// <param name="value">The CLI value, such as <c>a-2b</c>: lowercase letters, digits, dots and dashes.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null or not a single CLI value.</exception>
    public static PdfStandard FromCliValue(string value)
    {
        if (value is null || !CliValueFormat.IsMatch(value))
        {
            throw new ArgumentException($"'{value}' is not a single PDF standard value such as 'a-2b'.", nameof(value));
        }

        return new PdfStandard(value);
    }

    /// <summary>Two standards are equal when their CLI values are equal (ordinal).</summary>
    public bool Equals(PdfStandard? other) => other is not null && string.Equals(CliValue, other.CliValue, StringComparison.Ordinal);

    /// <inheritdoc cref="Equals(PdfStandard?)"/>
    public override bool Equals(object? obj) => Equals(obj as PdfStandard);

    /// <inheritdoc/>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(CliValue);

    /// <summary>Returns <see cref="CliValue"/>.</summary>
    public override string ToString() => CliValue;
}

namespace Build.Cake.Extensions;

// How the PowerShell scripts compared names, kept in one place: Sort-Object sorts culture-aware and ignores case;
// hashtables look keys up ignoring case.
internal static class NameOrder
{
    public static StringComparer Sort { get; } = StringComparer.InvariantCultureIgnoreCase;

    public static StringComparer Lookup { get; } = StringComparer.OrdinalIgnoreCase;
}

using System.Globalization;

namespace Typstium;

/// <summary>A Typst version, compared numerically. Pre-release suffixes are ignored.</summary>
public readonly record struct TypstVersion(int Major, int Minor, int Patch) : IComparable<TypstVersion>
{
    public static TypstVersion Parse(string value) =>
        TryParse(value, out var version)
            ? version
            : throw new FormatException($"'{value}' is not a Typst version (expected major.minor.patch).");

    public static bool TryParse(string? value, out TypstVersion version)
    {
        version = default;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var core = value!;
        var dash = core.IndexOf('-');
        if (dash >= 0)
        {
            core = core.Substring(0, dash);
        }

        var parts = core.Split('.');
        if (parts.Length != 3
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor)
            || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var patch))
        {
            return false;
        }

        version = new TypstVersion(major, minor, patch);
        return true;
    }

    public int CompareTo(TypstVersion other)
    {
        var major = Major.CompareTo(other.Major);
        if (major != 0)
        {
            return major;
        }

        var minor = Minor.CompareTo(other.Minor);
        return minor != 0 ? minor : Patch.CompareTo(other.Patch);
    }

    public static bool operator <(TypstVersion left, TypstVersion right) => left.CompareTo(right) < 0;

    public static bool operator >(TypstVersion left, TypstVersion right) => left.CompareTo(right) > 0;

    public static bool operator <=(TypstVersion left, TypstVersion right) => left.CompareTo(right) <= 0;

    public static bool operator >=(TypstVersion left, TypstVersion right) => left.CompareTo(right) >= 0;

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}

using System.Globalization;

namespace Typstium;

/// <summary>A Typst version, compared numerically. Pre-release suffixes are ignored.</summary>
public readonly record struct TypstVersion(int Major, int Minor, int Patch) : IComparable<TypstVersion>
{
    /// <summary>Parses <c>major.minor.patch</c>, ignoring a <c>-</c> suffix such as <c>-rc1</c>.</summary>
    /// <exception cref="FormatException"><paramref name="value"/> isn't a version.</exception>
    public static TypstVersion Parse(string value) =>
        TryParse(value, out var version)
            ? version
            : throw new FormatException($"'{value}' is not a Typst version (expected major.minor.patch).");

    /// <summary>Like <see cref="Parse"/>, but returns <c>false</c> instead of throwing.</summary>
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

    /// <summary>Compares major, then minor, then patch.</summary>
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

    /// <summary>Whether <paramref name="left"/> is older than <paramref name="right"/>.</summary>
    public static bool operator <(TypstVersion left, TypstVersion right) => left.CompareTo(right) < 0;

    /// <summary>Whether <paramref name="left"/> is newer than <paramref name="right"/>.</summary>
    public static bool operator >(TypstVersion left, TypstVersion right) => left.CompareTo(right) > 0;

    /// <summary>Whether <paramref name="left"/> is older than or the same as <paramref name="right"/>.</summary>
    public static bool operator <=(TypstVersion left, TypstVersion right) => left.CompareTo(right) <= 0;

    /// <summary>Whether <paramref name="left"/> is newer than or the same as <paramref name="right"/>.</summary>
    public static bool operator >=(TypstVersion left, TypstVersion right) => left.CompareTo(right) >= 0;

    /// <summary>Returns <c>major.minor.patch</c>.</summary>
    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}

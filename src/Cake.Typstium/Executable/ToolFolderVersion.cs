namespace Cake.Typstium.Executable;

/// <summary>
/// The version in a folder <c>#tool</c> installed a package into: <c>&lt;id&gt;</c> or <c>&lt;id&gt;.&lt;version&gt;</c>,
/// depending on Cake's UseVersionInToolsFolder. A folder without a version sorts first, a prerelease before its release.
/// </summary>
internal sealed class ToolFolderVersion : IComparable<ToolFolderVersion>
{
    private ToolFolderVersion(Version? number, string prerelease)
    {
        Number = number;
        Prerelease = prerelease;
    }

    /// <summary>Null for a folder without a version.</summary>
    private Version? Number { get; }

    /// <summary>Empty for a release.</summary>
    private string Prerelease { get; }

    /// <summary>The folder's version, or null when the folder belongs to another package or its version can't be read.</summary>
    public static ToolFolderVersion? Parse(string folderName, string packageId)
    {
        if (string.Equals(folderName, packageId, StringComparison.OrdinalIgnoreCase))
        {
            return new ToolFolderVersion(null, "");
        }

        if (!folderName.StartsWith(packageId + ".", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var text = folderName[(packageId.Length + 1)..];
        var dash = text.IndexOf('-');
        var number = dash < 0 ? text : text[..dash];
        var prerelease = dash < 0 ? "" : text[(dash + 1)..];
        return Version.TryParse(number, out var parsed) ? new ToolFolderVersion(parsed, prerelease) : null;
    }

    public int CompareTo(ToolFolderVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        if (Number is null || other.Number is null)
        {
            return (Number is null ? 0 : 1) - (other.Number is null ? 0 : 1);
        }

        var byNumber = Number.CompareTo(other.Number);
        if (byNumber != 0)
        {
            return byNumber;
        }

        if (Prerelease.Length == 0 || other.Prerelease.Length == 0)
        {
            return (Prerelease.Length == 0 ? 1 : 0) - (other.Prerelease.Length == 0 ? 1 : 0);
        }

        return string.CompareOrdinal(Prerelease, other.Prerelease);
    }
}

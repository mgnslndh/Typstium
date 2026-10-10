using System.Globalization;

namespace Cake.Typstium.Executable;

/// <summary>
/// The version in a folder <c>#tool</c> installed a package into: <c>&lt;id&gt;</c> or <c>&lt;id&gt;.&lt;version&gt;</c>,
/// depending on Cake's UseVersionInToolsFolder. Versions parse and order as NuGet's <c>NuGetVersion</c> and
/// <c>VersionComparer.Default</c> do (pinned by differential tests): up to four numeric parts, missing ones 0; a
/// prerelease before its release; prerelease identifiers compared one by one, numbers as numbers and before text, text
/// ignoring case, more identifiers higher; build metadata ignored. A folder without a version sorts first.
/// </summary>
internal sealed class ToolFolderVersion : IComparable<ToolFolderVersion>
{
    private static readonly string[] NoLabels = [];

    private ToolFolderVersion(int[]? numbers, string[] labels)
    {
        Numbers = numbers;
        Labels = labels;
    }

    /// <summary>Major, minor, patch and revision; null for a folder without a version.</summary>
    private int[]? Numbers { get; }

    /// <summary>The prerelease identifiers; empty for a release.</summary>
    private string[] Labels { get; }

    /// <summary>The folder's version, or null when the folder belongs to another package or its version isn't valid.</summary>
    public static ToolFolderVersion? Parse(string folderName, string packageId)
    {
        if (string.Equals(folderName, packageId, StringComparison.OrdinalIgnoreCase))
        {
            return new ToolFolderVersion(null, NoLabels);
        }

        return folderName.StartsWith(packageId + ".", StringComparison.OrdinalIgnoreCase)
            ? ParseVersion(folderName[(packageId.Length + 1)..])
            : null;
    }

    private static ToolFolderVersion? ParseVersion(string text)
    {
        var plus = text.IndexOf('+');
        if (plus >= 0 && !AreValidIdentifiers(text[(plus + 1)..], numericLeadingZeros: true))
        {
            return null;
        }

        var head = plus >= 0 ? text[..plus] : text;
        var dash = head.IndexOf('-');
        var labels = NoLabels;
        if (dash >= 0)
        {
            var release = head[(dash + 1)..];
            if (!AreValidIdentifiers(release, numericLeadingZeros: false))
            {
                return null;
            }

            labels = release.Split('.');
            head = head[..dash];
        }

        var parts = head.Split('.');
        if (parts.Length > 4)
        {
            return null;
        }

        var numbers = new int[4];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!IsDigits(parts[i]) || !int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
            {
                return null;
            }
        }

        return new ToolFolderVersion(numbers, labels);
    }

    /// <summary>Dot-separated, non-empty identifiers of ASCII letters, digits and '-'.</summary>
    private static bool AreValidIdentifiers(string text, bool numericLeadingZeros)
    {
        foreach (var identifier in text.Split('.'))
        {
            if (identifier.Length == 0 || !identifier.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '-'))
            {
                return false;
            }

            if (!numericLeadingZeros && identifier.Length > 1 && identifier[0] == '0' && IsDigits(identifier))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsDigits(string text) => text.Length > 0 && text.All(c => c is >= '0' and <= '9');

    public int CompareTo(ToolFolderVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        if (Numbers is null || other.Numbers is null)
        {
            return (Numbers is null ? 0 : 1) - (other.Numbers is null ? 0 : 1);
        }

        for (var i = 0; i < 4; i++)
        {
            var byNumber = Numbers[i].CompareTo(other.Numbers[i]);
            if (byNumber != 0)
            {
                return byNumber;
            }
        }

        if (Labels.Length == 0 || other.Labels.Length == 0)
        {
            return (Labels.Length == 0 ? 1 : 0) - (other.Labels.Length == 0 ? 1 : 0);
        }

        for (var i = 0; i < Math.Min(Labels.Length, other.Labels.Length); i++)
        {
            var byLabel = CompareLabel(Labels[i], other.Labels[i]);
            if (byLabel != 0)
            {
                return byLabel;
            }
        }

        return Labels.Length.CompareTo(other.Labels.Length);
    }

    private static int CompareLabel(string left, string right)
    {
        var leftIsNumber = int.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out var leftNumber);
        var rightIsNumber = int.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out var rightNumber);
        return (leftIsNumber, rightIsNumber) switch
        {
            (true, true) => leftNumber.CompareTo(rightNumber),
            (true, false) => -1,
            (false, true) => 1,
            _ => StringComparer.OrdinalIgnoreCase.Compare(left, right),
        };
    }
}

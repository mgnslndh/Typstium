using System.Globalization;

namespace Build.Cake.Extensions;

// Numbers in reports, comments and JSON never follow the machine's culture: a Swedish laptop must write 83.0%, not 83,0%.
internal static class Invariant
{
    public static string Format(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}

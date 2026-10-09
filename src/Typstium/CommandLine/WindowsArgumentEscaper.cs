using System.Text;

namespace Typstium.CommandLine;

/// <summary>
/// Quotes arguments using the MSVCRT/CommandLineToArgvW rules, which .NET also uses to split
/// ProcessStartInfo.Arguments on every platform. Used on netstandard2.0 (no ArgumentList) and for display.
/// </summary>
internal static class WindowsArgumentEscaper
{
    private static readonly char[] NeedsQuoting = [' ', '\t', '\n', '\v', '"'];

    public static string Join(IEnumerable<string> arguments) => string.Join(" ", arguments.Select(Escape));

    public static string Escape(string argument)
    {
        if (argument.Length > 0 && argument.IndexOfAny(NeedsQuoting) < 0)
        {
            return argument;
        }

        var builder = new StringBuilder(argument.Length + 2);
        builder.Append('"');
        for (var i = 0; i < argument.Length; i++)
        {
            var backslashes = 0;
            while (i < argument.Length && argument[i] == '\\')
            {
                backslashes++;
                i++;
            }

            if (i == argument.Length)
            {
                // Backslashes before the closing quote must be doubled.
                builder.Append('\\', backslashes * 2);
                break;
            }

            if (argument[i] == '"')
            {
                builder.Append('\\', (backslashes * 2) + 1);
                builder.Append('"');
            }
            else
            {
                builder.Append('\\', backslashes);
                builder.Append(argument[i]);
            }
        }

        builder.Append('"');
        return builder.ToString();
    }
}

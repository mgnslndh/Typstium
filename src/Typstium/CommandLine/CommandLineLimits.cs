namespace Typstium.CommandLine;

/// <summary>
/// Windows limits a command line to 32,767 characters. The same limit applies on every platform so a
/// request that works on a Linux build agent also works on a Windows server.
/// </summary>
internal static class CommandLineLimits
{
    public const int MaxLength = 32000;

    public static void EnsureFits(string executable, TypstArguments arguments)
    {
        var length = WindowsArgumentEscaper.Escape(executable).Length + 1 + arguments.Render().Length;
        if (length > MaxLength)
        {
            throw new ArgumentException(
                $"The Typst command line would be {length} characters, over the {MaxLength} limit. Large data such as JSON inputs should be passed as a virtual file (a template or markup source with WithJson) instead of WithJsonInput.",
                "request");
        }
    }
}
